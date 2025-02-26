using System.Numerics;
using System.Runtime.Loader;
using AEAssist;
using AEAssist.AEPlugin;
using AEAssist.Extension;
using AEAssist.Helper;
using AEAssist.MemoryApi;
using AEAssist.Verify;
using ECommons.DalamudServices;
using ImGuiNET;

namespace AutoRaidHelper
{
    public static class GeometryUtilsXZ
    {
        /// <summary>
        /// 在 XZ 平面计算两点的 2D 距离，忽略 Y
        /// </summary>
        public static float DistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// 在 XZ 平面计算：以 basePos 为圆心，向量(base->v1)与(base->v2) 的夹角(0~180)
        /// 忽略 Y
        /// </summary>
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

        /// <summary>
        /// 弦长、角度(°)、半径 互算
        /// chord=null => angle+radius => chord
        /// angle=null => chord+radius => angle
        /// radius=null => chord+angle => radius
        /// </summary>
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
                float aDeg = aRad * (180f / MathF.PI);
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

    /// <summary>
    /// 插件示例：将所有界面内容放入“几何计算”标签页，新开“自动化”标签页（此页示例仅显示提示）
    /// </summary>
    public class AutoRaidHelper : IAEPlugin
    {
        // 场地中心 / 朝向点
        private readonly string[] _centerLabels = { "旧(0,0,0)", "新(100,0,100)" };

        private readonly Vector3[] _centerPositions =
        {
            new Vector3(0, 0, 0),
            new Vector3(100, 0, 100),
        };

        private readonly string[] _directionLabels = { "东(101,0,100)", "西(99,0,100)", "南(100,0,101)", "北(100,0,99)" };

        private readonly Vector3[] _directionPositions =
        {
            new Vector3(101, 0, 100),
            new Vector3(99, 0, 100),
            new Vector3(100, 0, 101),
            new Vector3(100, 0, 99),
        };

        // 默认选择“新(100,0,100)” & “北(100,0,99)”
        private int _selectedCenterIndex = 1;
        private int _selectedDirectionIndex = 3;

        // 点记录：Ctrl=点1, Shift=点2, Alt=点3
        private Vector3? _point1World = null;
        private Vector3? _point2World = null;
        private Vector3? _point3World = null;
        private float _twoPointDistanceXZ = 0f; // 点1-点2距离
        private bool _addDebugPoints = false;
        
        // 选择夹角顶点：0 => 场地中心, 1 => 第三点(Alt)
        private int _apexMode = 0;

        // 弦长 / 角度 / 半径
        private float _chordInput = 0f;
        private float _angleInput = 0f;
        private float _radiusInput = 0f;
        private string _chordResultLabel = "";

        // 定义自动倒计时相关字段
        private bool _enableAutoCountdown = false;
        private bool _countdownTriggered = false;
        private uint _autoFuncZoneId = 1122;

        // 定义自动退本相关字段
        private bool _enableAutoLeaveDuty = false;
        private bool _dutyCompleted = false;
        
        // 定义自动排本相关字段
        private bool _enableAutoQueue = false;
        private string _selectedDutyName = "欧米茄绝境验证战";
        private string _customDutyName = "";
        private DateTime _lastAutoQueueTime = DateTime.MinValue;
        private bool _enableUnreset = false; // 表示是否解限
        
        // 定义自动排本相关字段
        private int _omegaCompletedCount = 0; // 记录低保数
        
        #region IAEPlugin Implementation

        public PluginSetting BuildPlugin()
        {
            return new PluginSetting
            {
                Name = "全自动小助手",
                LimitLevel = VIPLevel.Normal
            };
        }

        public void OnLoad(AssemblyLoadContext loadContext)
        {
            Svc.DutyState.DutyCompleted += OnDutyCompleted;
            LogHelper.Print("已订阅副本完成事件");
        }

        public void Dispose()
        {
            Svc.DutyState.DutyCompleted -= OnDutyCompleted;
            LogHelper.Print("已取消订阅副本完成事件");
        }

        public void Update()
        {
            CheckPointRecording();
            CheckAutoCountdown();
            CheckAutoLeaveDuty();
            ResetDutyCompletedIfNotInDuty();
            CheckAutoQueue();
        }

        public void OnPluginUI()
        {
            // 使用 TabBar 分页：一个“几何计算”，一个“自动化”
            if (ImGui.BeginTabBar("MainTabBar"))
            {
                if (ImGui.BeginTabItem("几何计算"))
                {
                    DrawGeometryTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("自动化"))
                {
                    DrawAutomationTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        #endregion

        /// <summary>
        /// 绘制“几何计算”标签页内容
        /// </summary>
        private void DrawGeometryTab()
        {
            // 提示信息
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f),
                "提示: Ctrl 记录点1, Shift 记录点2, Alt 记录点3 (顶点)" +
                "\n夹角顶点可选“场地中心”或“点3”");
            ImGui.Separator();

            // 鼠标坐标显示
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

            ImGui.Separator();

            // 显示是否添加 Debug 点的复选框
            ImGui.Checkbox("添加Debug点", ref _addDebugPoints);
            
            // 添加一个按钮用于清理 Debug 点
            ImGui.SameLine();
            if (ImGui.Button("清理Debug点")) 
                Share.TrustDebugPoint.Clear();
            
            // 显示记录的点 (保留2位小数)
            ImGui.Text($"点1: {FormatPointXZ(_point1World)}");
            ImGui.Text($"点2: {FormatPointXZ(_point2World)}");
            ImGui.Text($"点3(顶点): {FormatPointXZ(_point3World)}");
            
            // 如果点1和点2存在，显示距离及夹角（夹角顶点可选）
            if (_point1World.HasValue && _point2World.HasValue)
            {
                ImGui.Text($"点1-点2 距离: {_twoPointDistanceXZ:F2}");
                ImGui.Text("夹角顶点:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(120f);
                if (ImGui.BeginCombo("##ApexMode", _apexMode == 0 ? "场地中心" : "点3(Alt)"))
                {
                    if (ImGui.Selectable("场地中心", _apexMode == 0))
                        _apexMode = 0;
                    if (ImGui.Selectable("点3(Alt)", _apexMode == 1))
                        _apexMode = 1;
                    ImGui.EndCombo();
                }

                float angleAtApex = 0f;
                if (_apexMode == 0)
                {
                    Vector3 apexCenter = _centerPositions[_selectedCenterIndex];
                    angleAtApex = GeometryUtilsXZ.AngleXZ(_point1World.Value, _point2World.Value, apexCenter);
                    ImGui.Text($"夹角(场地中心): {angleAtApex:F2}°");
                }
                else
                {
                    if (_point3World.HasValue)
                    {
                        angleAtApex =
                            GeometryUtilsXZ.AngleXZ(_point1World.Value, _point2World.Value, _point3World.Value);
                        ImGui.Text($"夹角(点3): {angleAtApex:F2}°");
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "点3未记录，无法计算夹角");
                    }
                }
            }

            ImGui.Separator();

            // 场地中心 & 朝向点选择
            ImGui.Text("场地中心:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f);
            if (ImGui.BeginCombo("##CenterCombo", _centerLabels[_selectedCenterIndex]))
            {
                for (int i = 0; i < _centerLabels.Length; i++)
                {
                    bool isSelected = (i == _selectedCenterIndex);
                    if (ImGui.Selectable(_centerLabels[i], isSelected))
                        _selectedCenterIndex = i;
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
                        _selectedDirectionIndex = i;
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            // 计算鼠标->中心距离 & 夹角
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

            // 弦长 / 角度 / 半径互算
            ImGui.Text("弦长 / 角度(°) / 半径 (输入两个):");
            ImGui.SetNextItemWidth(100f);
            ImGui.InputFloat("弦长##chord", ref _chordInput);
            ImGui.SetNextItemWidth(100f);
            ImGui.InputFloat("角度##angle", ref _angleInput);
            ImGui.SetNextItemWidth(100f);
            ImGui.InputFloat("半径##radius", ref _radiusInput);

            if (ImGui.Button("Compute##chordAngleRadius"))
            {
                float? chordVal = (MathF.Abs(_chordInput) < 1e-6f) ? (float?)null : _chordInput;
                float? angleVal = (MathF.Abs(_angleInput) < 1e-6f) ? (float?)null : _angleInput;
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


        private void DrawAutomationTab()
        {
            //【地图记录与倒计时设置】
            if (ImGui.Button("记录当前地图ID"))
            {
                _autoFuncZoneId = Core.Resolve<MemApiZoneInfo>().GetCurrTerrId();
            }

            ImGui.SameLine();
            ImGui.Text($"当前指定地图ID: {_autoFuncZoneId}");

            ImGui.Checkbox("进本自动倒计时(15s)", ref _enableAutoCountdown);
            ImGui.Checkbox("指定地图ID的副本结束后自动退本(需启用DR <即刻退本> 模块)", ref _enableAutoLeaveDuty);

            // 显示自动倒计时状态
            string autoCountdownStatus = _enableAutoCountdown ? (_countdownTriggered ? "已触发" : "待触发") : "未启用";
            ImGui.Text($"自动倒计时状态: {autoCountdownStatus}");

            // 显示副本状态信息
            bool inMission = Core.Resolve<MemApiDuty>().InMission;
            bool isBoundByDuty = Core.Resolve<MemApiDuty>().IsBoundByDuty();
            bool isOver = _dutyCompleted;
            ImGui.Text($"副本是否正式开始: {inMission}");
            ImGui.SameLine();
            ImGui.Text($"是否在副本里: {isBoundByDuty}");
            ImGui.Text($"副本是否结束: {isOver}");

            ImGui.Separator();

            //【自动排本设置】
            ImGui.Checkbox("自动排本(需启用DR <任务搜索器指令> 模块)", ref _enableAutoQueue);
            // 检查是否解限
            ImGui.Checkbox("是否解限", ref _enableUnreset);
            
            ImGui.Text("选择副本:");

            ImGui.SetNextItemWidth(150f);
            if (ImGui.BeginCombo("##DutyName", _selectedDutyName))
            {
                if (ImGui.Selectable("欧米茄绝境验证战", _selectedDutyName == "欧米茄绝境验证战"))
                    _selectedDutyName = "欧米茄绝境验证战";
                if (ImGui.Selectable("幻想龙诗绝境战", _selectedDutyName == "幻想龙诗绝境战"))
                    _selectedDutyName = "幻想龙诗绝境战";
                if (ImGui.Selectable("自定义", _selectedDutyName == "自定义"))
                    _selectedDutyName = "自定义";
                ImGui.EndCombo();
            }

            if (_selectedDutyName == "自定义")
            {
                ImGui.SetNextItemWidth(150f);
                ImGui.InputText("自定义副本名称", ref _customDutyName, 50);
            }
        }


        /// <summary>
        /// 按下Ctrl=点1, Shift=点2, Alt=点3，记录后计算点1-点2距离（仅在XZ平面）
        /// </summary>
        private void CheckPointRecording()
        {
            bool ctrl = ImGui.IsKeyPressed(ImGuiKey.LeftCtrl) || ImGui.IsKeyPressed(ImGuiKey.RightCtrl);
            bool shft = ImGui.IsKeyPressed(ImGuiKey.LeftShift) || ImGui.IsKeyPressed(ImGuiKey.RightShift);
            bool alt = ImGui.IsKeyPressed(ImGuiKey.LeftAlt) || ImGui.IsKeyPressed(ImGuiKey.RightAlt);

            Vector2 mousePos = ImGui.GetMousePos();
            if (Svc.GameGui.ScreenToWorld(mousePos, out var wPos3D))
            {
                Vector3 pointXZ = new Vector3(wPos3D.X, 0, wPos3D.Z);

                if (ctrl)
                    _point1World = pointXZ;
                else if (shft)
                    _point2World = pointXZ;
                else if (alt)
                    _point3World = pointXZ;
                
                if (_addDebugPoints && (ctrl || shft || alt))
                    Share.TrustDebugPoint.Add(pointXZ);
            }

            if (_point1World.HasValue && _point2World.HasValue)
            {
                _twoPointDistanceXZ = GeometryUtilsXZ.DistanceXZ(_point1World.Value, _point2World.Value);
            }
        }

        /// <summary>
        /// 辅助函数：格式化点坐标，只显示 X 和 Z，保留两位小数
        /// </summary>
        private string FormatPointXZ(Vector3? p)
        {
            if (!p.HasValue)
                return "未记录";
            return $"<{p.Value.X:F2}, 0, {p.Value.Z:F2}>";
        }

        private void CheckAutoCountdown()
        {
            if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != _autoFuncZoneId)
                return;

            if (!_enableAutoCountdown)
                return;

            bool notInCombat = !Core.Me.InCombat();
            bool inMission = Core.Resolve<MemApiDuty>().InMission;
            bool isBoundByDuty = Core.Resolve<MemApiDuty>().IsBoundByDuty();
            bool partyIs8 = Core.Resolve<MemApiDuty>().DutyMembersNumber() == 8;

            if (notInCombat && inMission && partyIs8 && !_countdownTriggered)
            {
                ChatHelper.SendMessage("/countdown 15");
                _countdownTriggered = true;
                Task.Run(async () =>
                {
                    await Task.Delay(30000);
                    _countdownTriggered = false;
                });
            }

            // 每次检测到战斗重置时重置倒计时标识
            if (!inMission && isBoundByDuty)
            {
                _countdownTriggered = false;
            }

        }

        private async void CheckAutoLeaveDuty()
        {
            if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != _autoFuncZoneId)
                return;

            if (!_enableAutoLeaveDuty)
                return;

            bool isBoundByDuty = Core.Resolve<MemApiDuty>().IsBoundByDuty();
            bool isOver = _dutyCompleted;
            
            // 如果副本已结束并且还在副本内，则发送退本命令
            if (isOver && isBoundByDuty)
            {
                await Coroutine.Instance.WaitAsync(1000);
                ChatHelper.SendMessage("/pdr leaveduty");
            }
        }
        private void CheckAutoQueue()
        {
            if (!_enableAutoQueue)
                return;
            
            if ((DateTime.Now - _lastAutoQueueTime) < TimeSpan.FromSeconds(3))
                return;
            
            if (Core.Resolve<MemApiDuty>().IsBoundByDuty())
                return;
            
            var mismatchedMembers = Svc.Party.Where(p => p.Territory.Value.RowId == _autoFuncZoneId).ToList();
            if (mismatchedMembers.Any())
            {
                foreach (var p in mismatchedMembers)
                {
                    LogHelper.Print($"[{p.Name}] 还在指定副本内，取消排本");
                }
                return;
            }
            
            // 组装副本名称
            // 若下拉框选择“自定义”且自定义名称非空，则用自定义名称
            // 否则用下拉框的预设名称
            string dutyName = (_selectedDutyName == "自定义" && !string.IsNullOrEmpty(_customDutyName))
                ? _customDutyName
                : _selectedDutyName;
            
            // 如果启用了解限模式，在命令后附加 " unrest"
            if (_enableUnreset)
            {
                dutyName += " unrest";
            }
            
            // 最后发送排本命令
            ChatHelper.SendMessage($"/pdrduty n {dutyName}");
            _lastAutoQueueTime = DateTime.Now;
            LogHelper.Print($"自动排本命令已发送：/pdrduty n {dutyName}");
        }
        
        // 当副本完成时，触发事件，将 _dutyCompleted 置为 true
        private void OnDutyCompleted(object sender, ushort e)
        {
            LogHelper.Print($"副本任务完成（DutyCompleted 事件，ID: {e}）");
            _dutyCompleted = true;
            if (e == 1122)
            {
                _omegaCompletedCount++;
                LogHelper.Print($"绝欧低保 + 1, 本次已加低保数: {_omegaCompletedCount}");
            }
        }
        private void ResetDutyCompletedIfNotInDuty()
        {
            // 如果玩家不在副本中，则重置 _dutyCompleted
            if (!Core.Resolve<MemApiDuty>().IsBoundByDuty())
            {
                if (_dutyCompleted)
                    LogHelper.Print("检测到玩家不在副本内，自动重置_dutyCompleted");
                _dutyCompleted = false;
            }
        }
    }
}
