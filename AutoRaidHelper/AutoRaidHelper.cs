using System.Numerics;
using System.Runtime.Loader;
using AEAssist.AEPlugin;
using ECommons.DalamudServices;
using ImGuiNET;

namespace AutoRaidHelper
{
    public static class GeometryUtilsXZ
    {
        // 在 XZ 平面计算两点的 2D 距离，忽略 Y
        public static float DistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        // 在 XZ 平面计算：以 basePos 为圆心，向量(base->v1)与(base->v2) 的夹角(0~180)
        // 忽略 Y
        public static float AngleXZ(Vector3 v1, Vector3 v2, Vector3 basePos)
        {
            Vector3 a = new Vector3(v1.X - basePos.X, 0, v1.Z - basePos.Z);
            Vector3 b = new Vector3(v2.X - basePos.X, 0, v2.Z - basePos.Z);

            float dot = Vector3.Dot(a, b);
            float magA = a.Length();
            float magB = b.Length();

            if (magA < 1e-6f || magB < 1e-6f)
                return 0f;

            float cosTheta = dot / (magA * magB);
            cosTheta = Math.Clamp(cosTheta, -1f, 1f);

            float rad = MathF.Acos(cosTheta);
            return rad * (180f / MathF.PI);
        }

        // 弦长、角度(°)、半径 互算
        public static (float? value, string desc) ChordAngleRadius(float? chord, float? angleDeg, float? radius)
        {
            // 1) angle+radius => chord
            if (chord == null && angleDeg != null && radius != null)
            {
                float angleRad = angleDeg.Value * MathF.PI / 180f;
                float c = 2f * radius.Value * MathF.Sin(angleRad / 2f);
                return (c, "弦长");
            }
            // 2) chord+radius => angle
            if (angleDeg == null && chord != null && radius != null)
            {
                float x = chord.Value / (2f * radius.Value);
                x = Math.Clamp(x, -1f, 1f);
                float aRad = 2f * MathF.Asin(x);
                float aDeg = aRad * 180f / MathF.PI;
                return (aDeg, "角度(°)");
            }
            // 3) chord+angle => radius
            if (radius == null && chord != null && angleDeg != null)
            {
                float angleRad = angleDeg.Value * MathF.PI / 180f;
                float denominator = 2f * MathF.Sin(angleRad / 2f);
                if (Math.Abs(denominator) < 1e-6f)
                    return (null, "角度过小,无法计算半径");
                float r = chord.Value / denominator;
                return (r, "半径");
            }

            return (null, "请只留一个值为空，其余两个有值");
        }
    }

    
    public class AutoRaidHelper : IAEPlugin
    {
        // 场地中心 / 朝向点
        private readonly string[] _centerLabels = { "旧(0,0,0)", "新(100,0,100)" };
        private readonly Vector3[] _centerPositions =
        {
            new Vector3(0,0,0),
            new Vector3(100,0,100),
        };
        private readonly string[] _directionLabels = { "东(101,0,100)", "西(99,0,100)", "南(100,0,101)", "北(100,0,99)" };
        private readonly Vector3[] _directionPositions =
        {
            new Vector3(101,0,100),
            new Vector3(99,0,100),
            new Vector3(100,0,101),
            new Vector3(100,0,99),
        };
        
        private int _selectedCenterIndex = 1;
        private int _selectedDirectionIndex = 3;

        // Points: Ctrl=Point1, Shift=Point2, Alt=Point3
        private Vector3? _point1World = null;
        private Vector3? _point2World = null;
        private Vector3? _point3World = null;

        private float _twoPointDistanceXZ = 0f; // Point1-Point2距离

        // 选择夹角顶点：0 => 场地中心, 1 => 第三点
        private int _apexMode = 0;

        // 弦长 / 角度 / 半径
        private float _chordInput = 0f;
        private float _angleInput = 0f;
        private float _radiusInput = 0f;
        private string _chordResultLabel = "";
        
        public PluginSetting BuildPlugin()
        {
            return new PluginSetting
            {
                Name = "全自动小助手"
            };
        }

        public void OnLoad(AssemblyLoadContext loadContext)
        {
        }

        public void Update()
        {
            // 每帧检测按键(Ctrl/Shift/Alt)
            CheckPointRecording();
        }

        public void OnPluginUI()
        {
            // 提示
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f),
                "提示: 按下Ctrl=点1, Shift=点2, Alt=点3；" +
                "\n计算夹角可选“场地中心”或“点3”作为顶点计算点1与点2夹角");

            // 鼠标坐标
            Vector2 mousePos = ImGui.GetMousePos();
            if (Svc.GameGui.ScreenToWorld(mousePos, out var wPos3D))
            {
                ImGui.Text($"鼠标屏幕坐标: X={mousePos.X:F2}, Y={mousePos.Y:F2}");
                ImGui.Text($"鼠标世界(3D): X={wPos3D.X:F2}, Y={wPos3D.Y:F2}, Z={wPos3D.Z:F2}");
            }
            else
            {
                ImGui.Text("鼠标不在游戏窗口内");
            }

            // 显示三点
            ImGui.Text($"点1: {FormatPointXZ(_point1World)}");
            ImGui.Text($"点2: {FormatPointXZ(_point2World)}");
            ImGui.Text($"点3(顶点): {FormatPointXZ(_point3World)}");

            // 如果点1/点2都存在，显示距离
            if (_point1World.HasValue && _point2World.HasValue)
            {
                ImGui.Text($"距离: {_twoPointDistanceXZ:F2}");

                // 选择：0=场地中心, 1=点3 作为夹角顶点
                ImGui.Text("夹角顶点:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(120f);
                if (ImGui.BeginCombo("##ApexMode", _apexMode == 0 ? "场地中心" : "第三点(Alt)"))
                {
                    if (ImGui.Selectable("场地中心", _apexMode == 0))
                    {
                        _apexMode = 0;
                    }
                    if (ImGui.Selectable("第三点(Alt)", _apexMode == 1))
                    {
                        _apexMode = 1;
                    }
                    ImGui.EndCombo();
                }

                // 根据 apexMode 计算并显示夹角
                float angleAtApex = 0f;
                if (_apexMode == 0)
                {
                    // 用场地中心
                    Vector3 apexCenter = _centerPositions[_selectedCenterIndex];
                    angleAtApex = GeometryUtilsXZ.AngleXZ(_point1World.Value, _point2World.Value, apexCenter);
                    ImGui.Text($"夹角: {angleAtApex:F2}°");
                }
                else
                {
                    // 用点3
                    if (_point3World.HasValue)
                    {
                        angleAtApex = GeometryUtilsXZ.AngleXZ(_point1World.Value, _point2World.Value, _point3World.Value);
                        ImGui.Text($"夹角: {angleAtApex:F2}°");
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1,0.2f,0.2f,1), "点3尚未记录，无法计算夹角");
                    }
                }
            }

            ImGui.Separator();

            // 场地中心 & 朝向点
            ImGui.Text("场地中心:"); 
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f); 
            if (ImGui.BeginCombo("##CenterCombo", _centerLabels[_selectedCenterIndex]))
            {
                for (int i = 0; i < _centerLabels.Length; i++)
                {
                    bool isSelected = (i == _selectedCenterIndex);
                    if (ImGui.Selectable(_centerLabels[i], isSelected))
                    {
                        _selectedCenterIndex = i;
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.Text("朝向点:"); 
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f); 
            if (ImGui.BeginCombo("##DirectionCombo", _directionLabels[_selectedDirectionIndex]))
            {
                for (int i = 0; i < _directionLabels.Length; i++)
                {
                    bool isSelected = (i == _selectedDirectionIndex);
                    if (ImGui.Selectable(_directionLabels[i], isSelected))
                    {
                        _selectedDirectionIndex = i;
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            // 鼠标->中心 距离 & 夹角
            if (Svc.GameGui.ScreenToWorld(mousePos, out var wPos3D2))
            {
                Vector3 mouseXZ = new Vector3(wPos3D2.X, 0, wPos3D2.Z);
                Vector3 centerXZ = new Vector3(_centerPositions[_selectedCenterIndex].X, 0,
                                               _centerPositions[_selectedCenterIndex].Z);
                float distMouseCenter = GeometryUtilsXZ.DistanceXZ(mouseXZ, centerXZ);
                ImGui.Text($"鼠标->中心 距离: {distMouseCenter:F2}");

                Vector3 directionXZ = _directionPositions[_selectedDirectionIndex];
                float angleDeg = GeometryUtilsXZ.AngleXZ(mouseXZ, directionXZ, centerXZ);
                ImGui.Text($"夹角: {angleDeg:F2}°");
            }

            ImGui.Separator();

            // 弦长 / 角度 / 半径 互算
            ImGui.Text("弦长 / 角度(°) / 半径 (输入两个):");
            ImGui.SetNextItemWidth(100f);
            ImGui.InputFloat("弦长##chord", ref _chordInput);
            ImGui.SetNextItemWidth(100f);
            ImGui.InputFloat("角度##angle", ref _angleInput);
            ImGui.SetNextItemWidth(100f);
            ImGui.InputFloat("半径##radius", ref _radiusInput);
            
            if (ImGui.Button("Compute##chordAngleRadius"))
            {
                float? chordVal  = (MathF.Abs(_chordInput)  < 1e-6f) ? (float?)null : _chordInput;
                float? angleVal  = (MathF.Abs(_angleInput)  < 1e-6f) ? (float?)null : _angleInput;
                float? radiusVal = (MathF.Abs(_radiusInput) < 1e-6f) ? (float?)null : _radiusInput;

                var (res, desc) = GeometryUtilsXZ.ChordAngleRadius(chordVal, angleVal, radiusVal);
                if (res.HasValue)
                    _chordResultLabel = $"{desc}: {res.Value:F2}";
                else
                    _chordResultLabel = $"错误: {desc}";
            }
            ImGui.SameLine();
            ImGui.Text(_chordResultLabel);
        }

        public void Dispose() { }

        /// <summary>
        /// 按下Ctrl=点1, 按下Shift=点2, 按下Alt=点3
        /// 并在XZ平面计算点1-点2距离
        /// </summary>
        private void CheckPointRecording()
        {
            bool ctrl = ImGui.IsKeyPressed(ImGuiKey.LeftCtrl)  || ImGui.IsKeyPressed(ImGuiKey.RightCtrl);
            bool shft = ImGui.IsKeyPressed(ImGuiKey.LeftShift) || ImGui.IsKeyPressed(ImGuiKey.RightShift);
            bool alt  = ImGui.IsKeyPressed(ImGuiKey.LeftAlt)   || ImGui.IsKeyPressed(ImGuiKey.RightAlt);

            Vector2 mousePos = ImGui.GetMousePos();
            if (Svc.GameGui.ScreenToWorld(mousePos, out var wPos3D))
            {
                Vector3 pointXZ = new Vector3(wPos3D.X, 0, wPos3D.Z);

                if (ctrl)
                {
                    _point1World = pointXZ;
                }
                else if (shft)
                {
                    _point2World = pointXZ;
                }
                else if (alt)
                {
                    _point3World = pointXZ;
                }
            }

            // 每当 point1/point2 都存在时，计算距离
            if (_point1World.HasValue && _point2World.HasValue)
            {
                _twoPointDistanceXZ = GeometryUtilsXZ.DistanceXZ(_point1World.Value, _point2World.Value);
            }
        }

        /// <summary>
        /// 辅助函数：格式化 pointXZ => (x.xx, z.zz)
        /// 若为空则返回 "未记录"
        /// </summary>
        private string FormatPointXZ(Vector3? p)
        {
            if (!p.HasValue) return "未记录";
            return $"<{p.Value.X:F2}, 0, {p.Value.Z:F2}>";
        }
    }
}
