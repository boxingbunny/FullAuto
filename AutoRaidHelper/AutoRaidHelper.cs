using System.Numerics;
using System.Runtime.Loader;
using AEAssist;
using AEAssist.AEPlugin;
using AEAssist.Extension;
using AEAssist.Helper;
using AEAssist.MemoryApi;
using AEAssist.Verify;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ImGuiNET;

namespace AutoRaidHelper;

public class AutoRaidHelper : IAEPlugin
{
    // 场地中心 / 朝向点
    private readonly string[] _centerLabels = ["旧(0,0,0)", "新(100,0,100)"];

    private readonly Vector3[] _centerPositions =
    [
        new(0, 0, 0),
        new(100, 0, 100)
    ];

    private readonly string[] _directionLabels = ["东(101,0,100)", "西(99,0,100)", "南(100,0,101)", "北(100,0,99)"];

    private readonly Vector3[] _directionPositions =
    [
        new(101, 0, 100),
        new(99, 0, 100),
        new(100, 0, 101),
        new(100, 0, 99)
    ];

    // 默认选择“新(100,0,100)” & “北(100,0,99)”
    private int _selectedCenterIndex = 1;
    private int _selectedDirectionIndex = 3;

    // 点记录：Ctrl=点1, Shift=点2, Alt=点3
    private Vector3? _point1World;
    private Vector3? _point2World;
    private Vector3? _point3World;
    private float _twoPointDistanceXZ; // 点1-点2的距离
    private bool _addDebugPoints;

    // 选择夹角顶点：0 => 场地中心, 1 => 第三点(Alt)
    private int _apexMode;

    // 弦长 / 角度 / 半径
    private float _chordInput;
    private float _angleInput;
    private float _radiusInput;
    private string _chordResultLabel = "";

    // 定义自动倒计时相关字段
    private bool _enableAutoCountdown;
    private bool _countdownTriggered;
    private uint _autoFuncZoneId = 1122;

    // 定义自动退本相关字段
    private bool _enableAutoLeaveDuty;
    private bool _dutyCompleted;

    // 定义自动排本相关字段
    private bool _enableAutoQueue;
    private string _selectedDutyName = "欧米茄绝境验证战";
    private string _customDutyName = "";
    private DateTime _lastAutoQueueTime = DateTime.MinValue;
    private bool _enableUnrest; // 表示是否解限

    // 定义自动排本相关字段
    private int _omegaCompletedCount; // 记录低保数

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
        Svc.DutyState.DutyWiped += OnDutyWiped;
    }

    public void Dispose()
    {
        Svc.DutyState.DutyCompleted -= OnDutyCompleted;
        Svc.DutyState.DutyWiped -= OnDutyWiped;
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

            if (ImGui.BeginTabItem("FA全局设置"))
            {
                DrawFaGeneralSettingTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    #endregion


    private void DrawGeometryTab()
    {
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f),
            "提示: Ctrl 记录点1, Shift 记录点2, Alt 记录点3 (顶点)" +
            "\n夹角顶点可选“场地中心”或“点3”");
        ImGui.Separator();

        // 鼠标坐标显示
        var mousePos = ImGui.GetMousePos();
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

            float angleAtApex;
            if (_apexMode == 0)
            {
                var apexCenter = _centerPositions[_selectedCenterIndex];
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
            for (var i = 0; i < _centerLabels.Length; i++)
            {
                var isSelected = i == _selectedCenterIndex;
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
            for (var i = 0; i < _directionLabels.Length; i++)
            {
                var isSelected = i == _selectedDirectionIndex;
                if (ImGui.Selectable(_directionLabels[i], isSelected))
                    _selectedDirectionIndex = i;
                if (isSelected) ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        // 计算鼠标->中心距离 & 夹角
        if (Svc.GameGui.ScreenToWorld(mousePos, out var wPos3D2))
        {
            var mouseXZ = wPos3D2 with { Y = 0 };
            var centerXZ = new Vector3(_centerPositions[_selectedCenterIndex].X, 0,
                _centerPositions[_selectedCenterIndex].Z);
            var distMouseCenter = GeometryUtilsXZ.DistanceXZ(mouseXZ, centerXZ);
            ImGui.Text($"鼠标->中心 距离: {distMouseCenter:F2}");

            var directionXZ = _directionPositions[_selectedDirectionIndex];
            var angleDeg = GeometryUtilsXZ.AngleXZ(mouseXZ, directionXZ, centerXZ);
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
            float? chordVal = MathF.Abs(_chordInput) < 1e-6f ? null : _chordInput;
            float? angleVal = MathF.Abs(_angleInput) < 1e-6f ? null : _angleInput;
            float? radiusVal = MathF.Abs(_radiusInput) < 1e-6f ? null : _radiusInput;

            var (res, desc) = GeometryUtilsXZ.ChordAngleRadius(chordVal, angleVal, radiusVal);
            _chordResultLabel = res.HasValue ? $"{desc}: {res.Value:F2}" : $"错误: {desc}";
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

        ImGui.Separator();

        //【自动排本设置】
        ImGui.Checkbox("自动排本(需启用DR <任务搜索器指令> 模块)", ref _enableAutoQueue);
        // 检查是否解限
        ImGui.Checkbox("解限", ref _enableUnrest);

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

        ImGui.Separator();

        if (ImGui.CollapsingHeader("自动化Debug"))
        {
            var autoCountdownStatus = _enableAutoCountdown ? _countdownTriggered ? "已触发" : "待触发" : "未启用";
            var inCombat = Core.Me.InCombat();
            var inCutScene = Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent];
            var inMission = Core.Resolve<MemApiDuty>().InMission;
            var isBoundByDuty = Core.Resolve<MemApiDuty>().IsBoundByDuty();
            var isOver = _dutyCompleted;
            var partyList = Svc.Party;

            ImGui.Text($"自动倒计时状态: {autoCountdownStatus}");
            ImGui.Text($"处于战斗中: {inCombat}");
            ImGui.Text($"处于黑屏中: {inCutScene}");
            ImGui.Text($"副本正式开始: {inMission}");
            ImGui.Text($"在副本里: {isBoundByDuty}");
            ImGui.Text($"副本结束: {isOver}");
            ImGui.Text($"小队人数: {partyList.Count}");

            ImGui.Separator();
            ImGui.Text("小队成员状态:");
            foreach (var member in partyList)
            {
                var isValid = member.GameObject != null && member.GameObject.IsValid();
                ImGui.Text($"[{member.Name}] 是否为有效单位: {isValid}");
            }
        }
    }

    private void DrawFaGeneralSettingTab()
    {
        var printDebug = FullAutoSettings.PrintDebugInfo;
        if (ImGui.Checkbox("绘制坐标点并打印Debug信息", ref printDebug))
        {
            FullAutoSettings.PrintDebugInfo = printDebug;
        }
    }

    /// <summary>
    /// 按下Ctrl=点1, Shift=点2, Alt=点3，记录后计算点1-点2距离（仅在XZ平面）
    /// </summary>
    private void CheckPointRecording()
    {
        var ctrl = ImGui.IsKeyPressed(ImGuiKey.LeftCtrl) || ImGui.IsKeyPressed(ImGuiKey.RightCtrl);
        var shift = ImGui.IsKeyPressed(ImGuiKey.LeftShift) || ImGui.IsKeyPressed(ImGuiKey.RightShift);
        var alt = ImGui.IsKeyPressed(ImGuiKey.LeftAlt) || ImGui.IsKeyPressed(ImGuiKey.RightAlt);

        var mousePos = ImGui.GetMousePos();
        if (Svc.GameGui.ScreenToWorld(mousePos, out var wPos3D))
        {
            var pointXZ = wPos3D with { Y = 0 };

            if (ctrl)
                _point1World = pointXZ;
            else if (shift)
                _point2World = pointXZ;
            else if (alt)
                _point3World = pointXZ;

            if (_addDebugPoints && (ctrl || shift || alt))
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

    private async void CheckAutoCountdown()
    {
        try
        {
            // 检查当前地图是否符合要求
            if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != _autoFuncZoneId)
                return;

            // 检查是否启用自动倒计时
            if (!_enableAutoCountdown)
                return;

            // 检查是否都可选中
            if (Svc.Party.Any(member => member.GameObject == null || !member.GameObject.IsTargetable))
                return;

            var notInCombat = !Core.Me.InCombat();
            var inMission = Core.Resolve<MemApiDuty>().InMission;
            var partyIs8 = Core.Resolve<MemApiDuty>().DutyMembersNumber() == 8;


            if (notInCombat && inMission && partyIs8 && !_countdownTriggered)
            {
                _countdownTriggered = true;
                await Coroutine.Instance.WaitAsync(8000);
                ChatHelper.SendMessage("/countdown 15");
            }
        }
        catch (Exception e)
        {
            LogHelper.Print(e.Message);
        }
    }


    private async void CheckAutoLeaveDuty()
    {
        try
        {
            if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != _autoFuncZoneId)
                return;

            if (!_enableAutoLeaveDuty)
                return;

            var isBoundByDuty = Core.Resolve<MemApiDuty>().IsBoundByDuty();
            var isOver = _dutyCompleted;

            // 如果副本已结束并且还在副本内，则发送退本命令
            if (isOver && isBoundByDuty)
            {
                await Coroutine.Instance.WaitAsync(1000);
                ChatHelper.SendMessage("/pdr leaveduty");
            }
        }
        catch (Exception e)
        {
            LogHelper.Print(e.Message);
        }
    }

    private void CheckAutoQueue()
    {
        if (!_enableAutoQueue)
            return;

        if (DateTime.Now - _lastAutoQueueTime < TimeSpan.FromSeconds(3))
            return;

        if (Svc.Condition[ConditionFlag.InDutyQueue])
            return;

        if (Core.Resolve<MemApiDuty>().IsBoundByDuty())
            return;

        // 如果队伍中有任意一个成员的 GameObject 为 null 或 IsValid() 为 false，则直接 return
        if (Svc.Party.Any(member => member.GameObject == null || !member.GameObject.IsValid()))
            return;


        // 组装副本名称
        // 若下拉框选择“自定义”且自定义名称非空，则用自定义名称
        // 否则用下拉框的预设名称
        var dutyName = _selectedDutyName == "自定义" && !string.IsNullOrEmpty(_customDutyName)
            ? _customDutyName
            : _selectedDutyName;

        // 如果启用了解限模式，在命令后附加 " unrest"
        if (_enableUnrest)
        {
            dutyName += " unrest";
        }

        // 最后发送排本命令
        ChatHelper.SendMessage($"/pdrduty n {dutyName}");
        _lastAutoQueueTime = DateTime.Now;
        LogHelper.Print($"自动排本命令已发送：/pdrduty n {dutyName}");
    }

    // 当副本完成时，触发事件，将 _dutyCompleted 置为 true
    private void OnDutyCompleted(object? sender, ushort e)
    {
        LogHelper.Print($"副本任务完成（DutyCompleted 事件，ID: {e}）");
        _dutyCompleted = true;
        if (e == 1122)
        {
            _omegaCompletedCount++;
            LogHelper.Print($"绝欧低保 + 1, 本次已加低保数: {_omegaCompletedCount}");
        }
    }

    private void OnDutyWiped(object? sender, ushort e)
    {
        LogHelper.Print($"副本团灭重置（DutyWiped 事件，ID: {e}）");
        _countdownTriggered = false;
    }

    private void ResetDutyCompletedIfNotInDuty()
    {
        // 如果玩家不在副本中，则重置 _dutyCompleted
        if (Core.Resolve<MemApiDuty>().IsBoundByDuty())
            return;

        if (!_dutyCompleted)
            return;

        LogHelper.Print("检测到玩家不在副本内，自动重置_dutyCompleted");
        _dutyCompleted = false;
    }
}