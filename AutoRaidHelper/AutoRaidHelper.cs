using AEAssist;
using AEAssist.AEPlugin;
using AEAssist.Helper;
using AEAssist.MemoryApi;
using AEAssist.Verify;
using AEAssist.Extension;
using AEAssist.CombatRoutine.Module;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ImGuiNET;
using System.Numerics;
using System.Runtime.Loader;
using AEAssist.CombatRoutine.Trigger;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCommonList.CharacterData.OnlineStatus;


namespace AutoRaidHelper
{
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
        private uint _autoFuncZoneId = 1238;

        // 定义自动退本相关字段
        private bool _enableAutoLeaveDuty;
        private bool _dutyCompleted;
        private bool _leaveDutyTriggered;

        // 定义自动排本相关字段
        private bool _enableAutoQueue;
        private string _selectedDutyName = "光暗未来绝境战";
        private string _customDutyName = "";
        private DateTime _lastAutoQueueTime = DateTime.MinValue;
        private bool _enableUnrest; // 表示是否解限
        private string? _finalSendDutyName;

        // 定义自动排本相关字段
        private int _omegaCompletedCount; // 记录低保数

        //详细打印相关开关
        private bool _enemyCastSpellCondParams;
        private bool _onMapEffectCreateEvent;
        private bool _tetherCondParams;

        private bool _targetIconEffectCondParams;
        private bool _unitCreateCondParams;
        private bool _unitDeleteCondParams;
        private bool _addStatusCondParams;
        private bool _removeStatusCondParams;

        private bool _receiveAbilityEffectCondParams;
        private bool _gameLogCondParams;
        private bool _weatherChangedCondParams;
        private bool _actorControlCondParams;

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
            TriggerlineData.OnCondParamsCreate += OnCondParamsCreateEvent;
        }

        public void Dispose()
        {
            Svc.DutyState.DutyCompleted -= OnDutyCompleted;
            Svc.DutyState.DutyWiped -= OnDutyWiped;
            TriggerlineData.OnCondParamsCreate -= OnCondParamsCreateEvent;
        }

        public void Update()
        {
            CheckPointRecording();
            CheckAutoCountdown();
            CheckAutoLeaveDuty();
            ResetFlagIfNotInDuty();
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

                if (ImGui.BeginTabItem("日志监听"))
                {
                    DebugPrint();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        #endregion

        private void OnCondParamsCreateEvent(ITriggerCondParams condParams)
        {
            if (condParams is EnemyCastSpellCondParams spell && _enemyCastSpellCondParams)
                LogHelper.Print($"{spell}");

            if (condParams is OnMapEffectCreateEvent mapEffect && _onMapEffectCreateEvent)
                LogHelper.Print($"{mapEffect}");

            if (condParams is TetherCondParams tether && _tetherCondParams)
                LogHelper.Print($"{tether}");

            if (condParams is TargetIconEffectCondParams iconEffect && _targetIconEffectCondParams)
                LogHelper.Print($"{iconEffect}");

            if (condParams is UnitCreateCondParams unitCreate && _unitCreateCondParams)
                LogHelper.Print($"{unitCreate}");

            if (condParams is UnitDeleteCondParams unitDelete && _unitDeleteCondParams)
                LogHelper.Print($"{unitDelete}");

            if (condParams is AddStatusCondParams addStatus && _addStatusCondParams)
                LogHelper.Print($"{addStatus}");

            if (condParams is RemoveStatusCondParams removeStatus && _removeStatusCondParams)
                LogHelper.Print($"{removeStatus}");

            if (condParams is ReceviceAbilityEffectCondParams abilityEffect && _receiveAbilityEffectCondParams)
                LogHelper.Print($"{abilityEffect}");

            if (condParams is GameLogCondParams gameLog && _gameLogCondParams)
                LogHelper.Print($"{gameLog}");

            if (condParams is WeatherChangedCondParams weatherChanged && _weatherChangedCondParams)
                LogHelper.Print($"{weatherChanged}");

            if (condParams is ActorControlCondParams actorControl && _actorControlCondParams)
                LogHelper.Print($"{actorControl}");
        }


        private void DrawGeometryTab()
        {
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f),
                "提示: Ctrl 记录点1, Shift 记录点2, Alt 记录点3 (顶点)\n夹角顶点可选“场地中心”或“点3”");
            ImGui.Separator();
            ImGui.Spacing();

            // ===== 鼠标实时坐标信息 =====
            var mousePos = ImGui.GetMousePos();
            if (Svc.GameGui.ScreenToWorld(mousePos, out var wPos3D))
            {
                ImGui.Text($"鼠标屏幕: <{mousePos.X:F2}, {mousePos.Y:F2}>\n鼠标世界: <{wPos3D.X:F2}, {wPos3D.Z:F2}>");

                // 计算鼠标 → 场地中心的距离和角度
                float distMouseCenter = GeometryUtilsXZ.DistanceXZ(wPos3D, _centerPositions[_selectedCenterIndex]);
                float angleMouseCenter = GeometryUtilsXZ.AngleXZ(_directionPositions[_selectedDirectionIndex], wPos3D, _centerPositions[_selectedCenterIndex]);

                ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), $"鼠标 -> 场地中心: 距离 {distMouseCenter:F2}, 角度 {angleMouseCenter:F2}°");
            }
            else
            {
                ImGui.Text("鼠标不在游戏窗口内");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // ===== Debug点记录 & 清理 =====
            ImGui.Checkbox("添加Debug点", ref _addDebugPoints);
            ImGui.SameLine();
            if (ImGui.Button("清理Debug点")) Share.TrustDebugPoint.Clear();

            ImGui.Spacing();

            // ===== 记录的点 & 距离信息 =====
            ImGui.Text($"点1: {FormatPointXZ(_point1World)}   \n点2: {FormatPointXZ(_point2World)}   \n点3: {FormatPointXZ(_point3World)}");

            if (_point1World.HasValue && _point2World.HasValue)
            {
                ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), $"点1 -> 点2: 距离 {_twoPointDistanceXZ:F2}");

                ImGui.Text("夹角顶点:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(120f);
                if (ImGui.BeginCombo("##ApexMode", _apexMode == 0 ? "场地中心" : "点3(Alt)"))
                {
                    if (ImGui.Selectable("场地中心", _apexMode == 0)) _apexMode = 0;
                    if (ImGui.Selectable("点3(Alt)", _apexMode == 1)) _apexMode = 1;
                    ImGui.EndCombo();
                }

                float angleAtApex;
                if (_apexMode == 0)
                {
                    var apexCenter = _centerPositions[_selectedCenterIndex];
                    angleAtApex = GeometryUtilsXZ.AngleXZ(_point1World.Value, _point2World.Value, apexCenter);
                    ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), $"夹角(场地中心): {angleAtApex:F2}°");
                }
                else if (_point3World.HasValue)
                {
                    angleAtApex = GeometryUtilsXZ.AngleXZ(_point1World.Value, _point2World.Value, _point3World.Value);
                    ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), $"夹角(点3): {angleAtApex:F2}°");

                    var (offsetX1, offsetZ1) = GeometryUtilsXZ.CalculateOffsetFromReference(
                        _point1World.Value, _point3World.Value, _centerPositions[_selectedCenterIndex]);
                    var (offsetX2, offsetZ2) = GeometryUtilsXZ.CalculateOffsetFromReference(
                        _point2World.Value, _point3World.Value, _centerPositions[_selectedCenterIndex]);

                    ImGui.Text("在场地中心到点3线上的偏移:");
                    ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), $"点1: X={offsetX1:F2}, Z={offsetZ1:F2}   点2: X={offsetX2:F2}, Z={offsetZ2:F2}");
                }
                else
                {
                    ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "点3未记录，无法计算夹角");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // ===== 场地中心 & 朝向点选择 =====
            ImGui.Text("场地中心:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f);
            if (ImGui.BeginCombo("##CenterCombo", _centerLabels[_selectedCenterIndex]))
            {
                for (var i = 0; i < _centerLabels.Length; i++)
                {
                    if (ImGui.Selectable(_centerLabels[i], i == _selectedCenterIndex)) _selectedCenterIndex = i;
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
                    if (ImGui.Selectable(_directionLabels[i], i == _selectedDirectionIndex)) _selectedDirectionIndex = i;
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // ===== 弦长 / 角度(°) / 半径互算 =====
            ImGui.Text("弦长 / 角度(°) / 半径 (输入其中两个):");
            ImGui.SetNextItemWidth(100f);
            ImGui.InputFloat("弦长##chord", ref _chordInput);
            ImGui.SetNextItemWidth(100f);
            ImGui.InputFloat("角度##angle", ref _angleInput);
            ImGui.SetNextItemWidth(100f);
            ImGui.InputFloat("半径##radius", ref _radiusInput);

            if (ImGui.Button("计算##chordAngleRadius"))
            {
                float? chordVal = MathF.Abs(_chordInput) < 1e-6f ? null : _chordInput;
                float? angleVal = MathF.Abs(_angleInput) < 1e-6f ? null : _angleInput;
                float? radiusVal = MathF.Abs(_radiusInput) < 1e-6f ? null : _radiusInput;

                var (res, desc) = GeometryUtilsXZ.ChordAngleRadius(chordVal, angleVal, radiusVal);
                _chordResultLabel = res.HasValue ? $"{desc}: {res.Value:F2}" : $"错误: {desc}";
            }
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), _chordResultLabel);
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
            
            //【按钮类】
            ImGui.Separator();
            ImGui.Text("遥控按钮:");
            
            if (ImGui.Button("全队即刻退本"))
            {
                if (Core.Resolve<MemApiDuty>().InMission)
                {
                    RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                    RemoteControlHelper.Cmd("", "/pdr leaveduty");
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("全队TP撞电网"))
            {
                if (Core.Resolve<MemApiDuty>().InMission) 
                    RemoteControlHelper.SetPos("", new Vector3(100, 0, 125));
            }
            ImGui.SameLine();
            if (ImGui.Button("为小队队长发送排本命令"))
            {
                var leaderName = GetPartyLeaderName();
                if (!string.IsNullOrEmpty(leaderName))
                {
                    var leaderRole = RemoteControlHelper.GetRoleByPlayerName(leaderName);

                    RemoteControlHelper.Cmd(leaderRole, $"/pdrduty n {_finalSendDutyName}");
                    LogHelper.Print($"为队长 {leaderName} 发送排本命令: /pdrduty n {_finalSendDutyName}");
                }
            }
            
            ImGui.Text("Debug用按钮:");
            if (ImGui.Button("打印可选中敌对单位信息"))
            {
                var enemies = Svc.Objects.OfType<IBattleNpc>().Where(x => x.IsTargetable && x.IsEnemy());
                foreach (var enemy in enemies)
                {
                    LogHelper.Print($"敌对单位: {enemy.Name} (EntityIdID: {enemy.EntityId}, DataId: {enemy.DataId}), 位置: {enemy.Position}");
                }
            }

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
                if (ImGui.Selectable("光暗未来绝境战", _selectedDutyName == "光暗未来绝境战"))
                    _selectedDutyName = "光暗未来绝境战";
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
                var isCrossRealmParty = InfoProxyCrossRealm.IsCrossRealmParty();
                
                ImGui.Text($"自动倒计时状态: {autoCountdownStatus}");
                ImGui.Text($"处于战斗中: {inCombat}");
                ImGui.Text($"处于黑屏中: {inCutScene}");
                ImGui.Text($"副本正式开始: {inMission}");
                ImGui.Text($"在副本中: {isBoundByDuty}");
                ImGui.Text($"副本结束: {isOver}");
                ImGui.Text($"跨服小队状态: {isCrossRealmParty}");
                
                ImGui.Separator();
                
                if (!isCrossRealmParty) return;
                
                // 打印跨服小队玩家名字和状态
                ImGui.Text("跨服小队成员及状态:");
                var partyStatus = GetCrossRealmPartyStatus();
                for (int i = 0; i < partyStatus.Count; i++)
                {
                    var status = partyStatus[i];
                    var onlineText = status.IsOnline ? "在线" : "离线";
                    var dutyText = status.IsInDuty ? "副本中" : "副本外";
                    ImGui.Text($"[{i}] {status.Name} 状态: {onlineText}, {dutyText}");
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

        private void DebugPrint()
        {
            ImGui.Checkbox("咏唱事件", ref _enemyCastSpellCondParams);
            ImGui.SameLine();
            ImGui.Checkbox("地图事件", ref _onMapEffectCreateEvent);
            ImGui.SameLine();
            ImGui.Checkbox("连线事件", ref _tetherCondParams);
            ImGui.SameLine();
            ImGui.Checkbox("点名头标", ref _targetIconEffectCondParams);

            ImGui.Checkbox("创建单位", ref _unitCreateCondParams);
            ImGui.SameLine();
            ImGui.Checkbox("删除单位", ref _unitDeleteCondParams);
            ImGui.SameLine();
            ImGui.Checkbox("添加Buff", ref _addStatusCondParams);
            ImGui.SameLine();
            ImGui.Checkbox("删除Buff", ref _removeStatusCondParams);

            ImGui.Checkbox("效果事件", ref _receiveAbilityEffectCondParams);
            ImGui.SameLine();
            ImGui.Checkbox("游戏日志", ref _gameLogCondParams);
            ImGui.SameLine();
            ImGui.Checkbox("天气变化", ref _weatherChangedCondParams);
            ImGui.SameLine();
            ImGui.Checkbox("ActorControl", ref _actorControlCondParams);
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
                if (Svc.Party.Any(member => member.GameObject is not { IsTargetable: true }))
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
                if (isOver && isBoundByDuty && !_leaveDutyTriggered)
                {
                    _leaveDutyTriggered = true;
                    await Coroutine.Instance.WaitAsync(1000);
                    RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                    RemoteControlHelper.Cmd("", "/pdr leaveduty");
                }
                
            }
            catch (Exception e)
            {
                LogHelper.Print(e.Message);
            }
        }

        private void CheckAutoQueue()
        {
            // 组装副本名称
            var dutyName = _selectedDutyName == "自定义" && !string.IsNullOrEmpty(_customDutyName)
                ? _customDutyName
                : _selectedDutyName;

            if (_enableUnrest)
            {
                dutyName += " unrest";
            }
            _finalSendDutyName = dutyName;
            
            if (!_enableAutoQueue)
                return;
            
            if (DateTime.Now - _lastAutoQueueTime < TimeSpan.FromSeconds(3))
                return;
            
            if (Svc.Condition[ConditionFlag.InDutyQueue])
                return;
            
            if (Core.Resolve<MemApiDuty>().IsBoundByDuty())
                return;
            
            // 检查跨服小队人数
            if (InfoProxyCrossRealm.GetPartyMemberCount() < 8)
                return;
            
            // 获取跨服小队所有玩家状态
            var partyStatus = GetCrossRealmPartyStatus();
            var invalidNames = partyStatus
                .Where(s => !s.IsOnline || s.IsInDuty)
                .Select(s => s.Name)
                .ToList();

            if (invalidNames.Count != 0)
            {
                LogHelper.Print("玩家不在线或在副本中：" + string.Join(", ", invalidNames));
                return;
            }
            
            ChatHelper.SendMessage($"/pdrduty n {_finalSendDutyName}");
            _lastAutoQueueTime = DateTime.Now;
            LogHelper.Print($"自动排本命令已发送：/pdrduty n {_finalSendDutyName}");
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

        private void ResetFlagIfNotInDuty()
        {
            // 如果玩家不在副本中，则重置标志
            if (Core.Resolve<MemApiDuty>().IsBoundByDuty())
                return;

            if (!_dutyCompleted)
                return;

            LogHelper.Print("检测到玩家不在副本内，自动重置_dutyCompleted");

            _countdownTriggered = false;
            _leaveDutyTriggered = false;
            _dutyCompleted = false;
        }
        
        // 获取跨服小队玩家名字及其状态
        private static unsafe List<(string Name, bool IsOnline, bool IsInDuty)> GetCrossRealmPartyStatus()
        {
            var result = new List<(string, bool, bool)>();

            var crossRealmProxy = InfoProxyCrossRealm.Instance();
            if (crossRealmProxy == null)
                return result;

            var infoModulePtr = InfoModule.Instance();
            if (infoModulePtr == null)
                return result;

            var commonListPtr = (InfoProxyCommonList*)infoModulePtr->GetInfoProxyById(InfoProxyId.PartyMember);
            if (commonListPtr == null)
                return result;

            var groups = crossRealmProxy->CrossRealmGroups;
            foreach (var group in groups)
            {
                int count = group.GroupMemberCount;
                if (commonListPtr->CharDataSpan.Length < count)
                    continue;

                for (int i = 0; i < count; i++)
                {
                    var member = group.GroupMembers[i];
                    var data = commonListPtr->CharDataSpan[i];

                    bool isOnline = data.State.HasFlag(Online);
                    bool isInDuty = data.State.HasFlag(InDuty);
                    
                    result.Add((member.NameString, isOnline, isInDuty));
                }
            }

            return result;
        }
        private static unsafe string? GetPartyLeaderName()
        {
            var infoModulePtr = InfoModule.Instance();
            if (infoModulePtr == null)
                return null;
            
            var commonListPtr = (InfoProxyCommonList*)infoModulePtr->GetInfoProxyById(InfoProxyId.PartyMember);
            if (commonListPtr == null)
                return null;
            
            foreach (var data in commonListPtr->CharDataSpan)
            {
                if (data.State.HasFlag(PartyLeader) || data.State.HasFlag(PartyLeaderCrossWorld))
                    return data.NameString;
            }
            return null;
        }

    }
}