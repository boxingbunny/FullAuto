using System.Numerics;
using ImGuiNET;
using AutoRaidHelper.Utils;
using AutoRaidHelper.Settings;
using AEAssist;
using AEAssist.Helper;
using ECommons.DalamudServices;
using AEAssist.CombatRoutine.Module;
using System.Runtime.Loader;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// GeometryTab 用于处理几何计算及相关UI交互，记录鼠标点击点、计算距离与角度，并提供调试点添加及清理功能。
    /// </summary>
    public class GeometryTab
    {
        /// <summary>
        /// 获取全局的 GeometrySettings 配置单例，存储场地中心、朝向点及计算参数等配置。
        /// </summary>
        public GeometrySettings Settings => FullAutoSettings.Instance.GeometrySettings;

        /// <summary>
        /// 记录运行时的点1（一般通过按Ctrl记录）。
        /// </summary>
        public Vector3? Point1World { get; private set; }
        /// <summary>
        /// 记录运行时的点2（一般通过按Shift记录）。
        /// </summary>
        public Vector3? Point2World { get; private set; }
        /// <summary>
        /// 记录运行时的点3（一般通过按Alt记录，被用于计算夹角时选用点3作为顶点）。
        /// </summary>
        public Vector3? Point3World { get; private set; }
        /// <summary>
        /// 点1与点2在XZ平面的距离，实时计算，不保存到配置文件中。
        /// </summary>
        public float TwoPointDistanceXZ { get; private set; }
        /// <summary>
        /// 用于显示弦长、角度和半径计算后的结果描述与数值。
        /// </summary>
        public string ChordResultLabel { get; private set; } = "";

        // 固定数据：场地中心标签与对应的实际坐标值
        private readonly string[] _centerLabels = new string[] { "旧(0,0,0)", "新(100,0,100)" };
        private readonly Vector3[] _centerPositions = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(100, 0, 100)
        };

        // 固定数据：朝向点标签与对应的实际坐标值
        private readonly string[] _directionLabels = new string[] { "东(101,0,100)", "西(99,0,100)", "南(100,0,101)", "北(100,0,99)" };
        private readonly Vector3[] _directionPositions = new Vector3[]
        {
            new Vector3(101, 0, 100),
            new Vector3(99, 0, 100),
            new Vector3(100, 0, 101),
            new Vector3(100, 0, 99)
        };

        /// <summary>
        /// 在每一帧调用，主要用于更新鼠标点击记录（点1、点2、点3）。
        /// </summary>
        public async void Update()
        {
            // 每帧检查是否按下Ctrl/Shift/Alt键，记录对应的点信息
            CheckPointRecording();
        }

        /// <summary>
        /// 绘制与更新 GeometryTab 的各项UI组件，展示实时鼠标位置、Debug点操作、距离、角度计算等信息。
        /// </summary>
        public void Draw()
        {
            // 绘制提示信息，说明如何使用键盘记录点以及如何选择夹角顶点模式
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f),
                "提示: Ctrl 记录点1, Shift 记录点2, Alt 记录点3 (顶点)\n夹角顶点可选“场地中心”或“点3”");
            ImGui.Separator();
            ImGui.Spacing();

            // 显示鼠标当前在屏幕及转换后的世界坐标
            var mousePos = ImGui.GetMousePos();
            if (ScreenToWorld(mousePos, out var wPos3D))
            {
                ImGui.Text($"鼠标屏幕: <{mousePos.X:F2}, {mousePos.Y:F2}>\n鼠标世界: <{wPos3D.X:F2}, {wPos3D.Z:F2}>");
                // 计算鼠标与当前选定场地中心的距离，以及参考方向与鼠标之间的角度
                float distMouseCenter = GeometryUtilsXZ.DistanceXZ(wPos3D, _centerPositions[Settings.SelectedCenterIndex]);
                float angleMouseCenter = GeometryUtilsXZ.AngleXZ(_directionPositions[Settings.SelectedDirectionIndex], wPos3D, _centerPositions[Settings.SelectedCenterIndex]);
                ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f),
                    $"鼠标 -> 场地中心: 距离 {distMouseCenter:F2}, 角度 {angleMouseCenter:F2}°");
            }
            else
            {
                ImGui.Text("鼠标不在游戏窗口内");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Debug点操作：提供添加或清理调试点的功能
            // 读取当前是否启用Debug点
            bool addDebug = Settings.AddDebugPoints;
            if (ImGui.Checkbox("添加Debug点", ref addDebug))
            {
                Settings.UpdateAddDebugPoints(addDebug);
            }
            ImGui.SameLine();
            if (ImGui.Button("清理Debug点"))
            {
                ClearDebugPoints();
            }

            ImGui.Spacing();
            // 显示记录的三个点坐标
            ImGui.Text($"点1: {FormatPointXZ(Point1World)}");
            ImGui.Text($"点2: {FormatPointXZ(Point2World)}");
            ImGui.Text($"点3: {FormatPointXZ(Point3World)}");

            // 当记录了点1和点2后，计算并显示两点间的XZ平面距离，同时允许选择夹角顶点模式进行角度计算
            if (Point1World.HasValue && Point2World.HasValue)
            {
                ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f),
                    $"点1 -> 点2: 距离 {TwoPointDistanceXZ:F2}");
                ImGui.Text("夹角顶点:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(120f);

                // 根据配置判断当前使用的夹角顶点模式（场地中心或者点3）
                string apexLabel = Settings.ApexMode == 0 ? "场地中心" : "点3(Alt)";
                if (ImGui.BeginCombo("##ApexMode", apexLabel))
                {
                    if (ImGui.Selectable("场地中心", Settings.ApexMode == 0))
                    {
                        Settings.UpdateApexMode(0);
                    }
                    if (ImGui.Selectable("点3(Alt)", Settings.ApexMode == 1))
                    {
                        Settings.UpdateApexMode(1);
                    }
                    ImGui.EndCombo();
                }

                float angleAtApex = 0f;
                // 根据选择的模式计算夹角
                if (Settings.ApexMode == 0)
                {
                    var apexCenter = _centerPositions[Settings.SelectedCenterIndex];
                    // 使用场地中心作为夹角顶点
                    angleAtApex = GeometryUtilsXZ.AngleXZ(Point1World.Value, Point2World.Value, apexCenter);
                    ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f),
                        $"夹角(场地中心): {angleAtApex:F2}°");
                }
                else if (Point3World.HasValue)
                {
                    // 使用记录的点3作为夹角顶点
                    angleAtApex = GeometryUtilsXZ.AngleXZ(Point1World.Value, Point2World.Value, Point3World.Value);
                    ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f),
                        $"夹角(点3): {angleAtApex:F2}°");

                    // 计算点1、点2相对于线（场地中心到点3）的偏移量
                    var (offsetX1, offsetZ1) = GeometryUtilsXZ.CalculateOffsetFromReference(Point1World.Value, Point3World.Value, _centerPositions[Settings.SelectedCenterIndex]);
                    var (offsetX2, offsetZ2) = GeometryUtilsXZ.CalculateOffsetFromReference(Point2World.Value, Point3World.Value, _centerPositions[Settings.SelectedCenterIndex]);

                    ImGui.Text("在场地中心到点3线上的偏移:");
                    ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f),
                        $"点1: X={offsetX1:F2}, Z={offsetZ1:F2}   点2: X={offsetX2:F2}, Z={offsetZ2:F2}");
                }
                else
                {
                    // 未记录点3时，无法计算点3为顶点的夹角，给出错误提示
                    ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f),
                        "点3未记录，无法计算夹角");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // 绘制下拉框，供用户选择场地中心和朝向点
            ImGui.Text("场地中心:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f);
            if (ImGui.BeginCombo("##CenterCombo", _centerLabels[Settings.SelectedCenterIndex]))
            {
                for (int i = 0; i < _centerLabels.Length; i++)
                {
                    if (ImGui.Selectable(_centerLabels[i], i == Settings.SelectedCenterIndex))
                    {
                        // 更新当前选中的场地中心，并持久化设置
                        Settings.UpdateSelectedCenterIndex(i);
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Text("朝向点:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f);
            if (ImGui.BeginCombo("##DirectionCombo", _directionLabels[Settings.SelectedDirectionIndex]))
            {
                for (int i = 0; i < _directionLabels.Length; i++)
                {
                    if (ImGui.Selectable(_directionLabels[i], i == Settings.SelectedDirectionIndex))
                    {
                        // 更新当前选中的朝向点，并持久化设置
                        Settings.UpdateSelectedDirectionIndex(i);
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // 弦长、角度、半径互算的输入区域，要求用户输入其中两个，由程序计算第三个数值
            ImGui.Text("弦长 / 角度(°) / 半径 (输入其中两个):");
            {
                float chordInput = Settings.ChordInput;
                if (ImGui.InputFloat("弦长##chord", ref chordInput))
                {
                    Settings.UpdateChordInput(chordInput);
                }
            }
            {
                float angleInput = Settings.AngleInput;
                if (ImGui.InputFloat("角度##angle", ref angleInput))
                {
                    Settings.UpdateAngleInput(angleInput);
                }
            }
            {
                float radiusInput = Settings.RadiusInput;
                if (ImGui.InputFloat("半径##radius", ref radiusInput))
                {
                    Settings.UpdateRadiusInput(radiusInput);
                }
            }
            if (ImGui.Button("计算##chordAngleRadius"))
            {
                // 使用一个很小的阈值判断输入是否有效，否则认为为null
                float? chordVal = MathF.Abs(Settings.ChordInput) < 1e-6f ? null : Settings.ChordInput;
                float? angleVal = MathF.Abs(Settings.AngleInput) < 1e-6f ? null : Settings.AngleInput;
                float? radiusVal = MathF.Abs(Settings.RadiusInput) < 1e-6f ? null : Settings.RadiusInput;
                var (res, desc) = GeometryUtilsXZ.ChordAngleRadius(chordVal, angleVal, radiusVal);
                // 如果计算结果有效，则显示计算的数值，否则显示错误描述
                ChordResultLabel = res.HasValue ? $"{desc}: {res.Value:F2}" : $"错误: {desc}";
            }
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), ChordResultLabel);
        }

        /// <summary>
        /// 通过监听键盘按键（Ctrl、Shift、Alt）记录鼠标在世界坐标中的位置，
        /// 同时在记录 Debug 点时更新点1/点2/点3的值，并计算点1与点2之间的距离。
        /// </summary>
        public void CheckPointRecording()
        {
            // 检查按键状态：Ctrl、Shift、Alt分别对应记录点1、点2、点3
            bool ctrl = ImGui.IsKeyPressed(ImGuiKey.LeftCtrl) || ImGui.IsKeyPressed(ImGuiKey.RightCtrl);
            bool shift = ImGui.IsKeyPressed(ImGuiKey.LeftShift) || ImGui.IsKeyPressed(ImGuiKey.RightShift);
            bool alt = ImGui.IsKeyPressed(ImGuiKey.LeftAlt) || ImGui.IsKeyPressed(ImGuiKey.RightAlt);

            // 获取当前鼠标屏幕坐标，并尝试转换到3D世界坐标
            var mousePos = ImGui.GetMousePos();
            if (ScreenToWorld(mousePos, out var wPos3D))
            {
                // 仅保留XZ分量，Y置0，适应2D平面计算
                var pointXZ = new Vector3(wPos3D.X, 0, wPos3D.Z);
                if (ctrl)
                    Point1World = pointXZ;
                else if (shift)
                    Point2World = pointXZ;
                else if (alt)
                    Point3World = pointXZ;

                // 如果启用了Debug点模式，则将记录的点添加到Debug点集合中
                if (Settings.AddDebugPoints && (ctrl || shift || alt))
                    AddDebugPoint(pointXZ);
            }

            // 当记录了点1和点2后，计算并更新这两点在XZ平面的距离
            if (Point1World.HasValue && Point2World.HasValue)
            {
                TwoPointDistanceXZ = GeometryUtilsXZ.DistanceXZ(Point1World.Value, Point2World.Value);
            }
        }

        /// <summary>
        /// 格式化输出点的XZ坐标（如果点存在），否则返回"未记录"提示。
        /// </summary>
        /// <param name="p">需要格式化的点坐标</param>
        /// <returns>格式化后的字符串</returns>
        private string FormatPointXZ(Vector3? p) =>
            p.HasValue ? $"<{p.Value.X:F2}, 0, {p.Value.Z:F2}>" : "未记录";

        /// <summary>
        /// 将屏幕坐标转换为3D世界坐标。
        /// </summary>
        /// <param name="screenPos">当前鼠标在屏幕上的位置</param>
        /// <param name="worldPos">转换后的3D世界坐标</param>
        /// <returns>转换是否成功（当前实现始终返回true）</returns>
        private bool ScreenToWorld(Vector2 screenPos, out Vector3 worldPos)
        {
            Svc.GameGui.ScreenToWorld(screenPos, out worldPos);
            return true;
        }

        /// <summary>
        /// 添加一个调试点，用于在调试模式下显示具体点击位置的信息。
        /// </summary>
        /// <param name="point">在世界坐标中的调试点</param>
        private void AddDebugPoint(Vector3 point)
        {
            LogHelper.Print($"添加Debug点: {point}");
            Share.TrustDebugPoint.Add(point);
        }

        /// <summary>
        /// 清理所有已经记录的调试点。
        /// </summary>
        private void ClearDebugPoints()
        {
            LogHelper.Print("清理Debug点");
            Share.TrustDebugPoint.Clear();
        }
    }
}
