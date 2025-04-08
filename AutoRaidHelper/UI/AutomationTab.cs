using System.Numerics;
using ImGuiNET;
using AEAssist;
using AEAssist.Helper;
using AEAssist.MemoryApi;
using AutoRaidHelper.Settings;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCommonList.CharacterData.OnlineStatus;
using Dalamud.Game.ClientState.Objects.Types;
using AEAssist.Extension;
using Dalamud.Game.ClientState.Conditions;
using System.Runtime.Loader;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using DutyType = AutoRaidHelper.Settings.AutomationSettings.DutyType;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// AutomationTab 用于处理自动化模块的 UI 展示与业务逻辑，
    /// 包括自动倒计时、自动退本、自动排本以及遥控功能等。
    /// </summary>
    public class AutomationTab
    {
        // 声明一个字典，用于将副本 ID (ushort) 映射到对应的更新操作
        private readonly Dictionary<DutyType, Action> _dutyUpdateActions;
        public AutomationTab()
        {
            _dutyUpdateActions = new Dictionary<DutyType, Action>
            {
                { DutyType.Dragon, () => UpdateDuty(DutyType.Dragon, ref _dragonCompletedCount, 1, "龙诗") },
                { DutyType.Omega, () => UpdateDuty(DutyType.Omega, ref _omegaCompletedCount, 1, "绝欧") },
                { DutyType.Alal, () => UpdateDuty(DutyType.Alal, ref _alalCompletedCount, 1, "阿罗阿罗") },
                { DutyType.Eden, () => UpdateDuty(DutyType.Eden, ref _edenCompletedCount, 1, "伊甸") },
                { DutyType.Sphene, () => UpdateDuty(DutyType.Sphene, ref _spheneCompletedCount, 2, "女王") },
            };
        }
        
        private void UpdateDuty(DutyType duty, ref int localCount, int increment, string dutyName)
        {
            // 取出当前全局累计值
            int globalBefore = GetGlobalCount(duty);
            localCount += increment;
            int globalNew = globalBefore + increment;
            // 计算全局累计值更新配置
            Settings.UpdateDutyCount(duty, globalNew);
            LogHelper.Print($"{dutyName}低保 + {increment}, 本次已加低保数: {localCount}, 共计加低保数 {GetGlobalCount(duty)}");
        }

        private int GetGlobalCount(DutyType duty) =>
            duty switch
            {
                DutyType.Dragon => Settings.DragonCompletedCount,
                DutyType.Omega  => Settings.OmegaCompletedCount,
                DutyType.Alal   => Settings.AlalCompletedCount,
                DutyType.Eden   => Settings.EdenCompletedCount,
                DutyType.Sphene => Settings.SpheneCompletedCount,
                _               => 0
            };
        
        /// <summary>
        /// 通过全局配置单例获取 AutomationSettings 配置，
        /// 该配置保存了地图ID、倒计时、退本、排本等设置。
        /// </summary>
        public AutomationSettings Settings => FullAutoSettings.Instance.AutomationSettings;

        public static float scale => ImGui.GetFontSize() / 13.0f;

        // 记录上次发送自动排本命令的时间，避免频繁发送
        private DateTime _lastAutoQueueTime = DateTime.MinValue;
        // 标记副本是否已经完成，通常在 DutyCompleted 事件中设置
        private bool _dutyCompleted;
        // 记录龙诗低保数
        private int _dragonCompletedCount;
        // 记录欧米茄低保数（通过副本完成事件累加）
        private int _omegaCompletedCount;
        // 记录女王低保数（通过副本完成事件累加）
        private int _spheneCompletedCount;
        // 记录伊甸低保数
        private int _edenCompletedCount;
        // 记录零式阿罗阿罗岛低保数
        private int _alalCompletedCount;
        private bool _isCountdownRunning;
        private bool _isOpenChestRunning;
        private bool _isLeaveRunning;
        private bool _isQueueRunning;

        private bool _isCountdownCompleted;
        private bool _isLeaveCompleted;
        private bool _isQueueCompleted;
        private bool _isOpenChestCompleted;

        private bool _isLootCompleted;
        private IGameObject? _treasure;

        private readonly object _countdownLock = new();
        private readonly object _openChestLock = new();
        private readonly object _leaveLock = new();
        private readonly object _queueLock = new();

     
        /// <summary>
        /// 在加载时，订阅副本状态相关事件（如副本完成和团灭）
        /// 以便更新自动化状态或低保统计数据。
        /// </summary>
        /// <param name="loadContext">当前加载上下文</param>
        public void OnLoad(AssemblyLoadContext loadContext)
        {
            Svc.DutyState.DutyCompleted += OnDutyCompleted;
            Svc.DutyState.DutyWiped += OnDutyWiped;
        }

        /// <summary>
        /// 在插件卸载时取消对副本状态事件的订阅，
        /// 防止因事件残留引起内存泄漏或异常提交。
        /// </summary>
        public void Dispose()
        {
            Svc.DutyState.DutyCompleted -= OnDutyCompleted;
            Svc.DutyState.DutyWiped -= OnDutyWiped;
        }

        /// <summary>
        /// 每帧调用 Update 方法，依次执行倒计时、退本与排本更新逻辑，
        /// 同时重置副本完成状态标志。
        /// </summary>
        public async void Update()
        {
            try
            {
                await UpdateAutoCountdown();
                await UpdateAutoOpenChest();
                await UpdateAutoLeave();
                await UpdateAutoQueue();
                ResetDutyFlag();
            }
            catch (Exception e)
            {
                LogHelper.Print(e.Message + e.StackTrace);
            }
        }

        /// <summary>
        /// 当副本完成时触发 DutyCompleted 事件，对应更新副本完成状态，
        /// 并根据传入的副本ID更新不同的低保数统计。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">副本任务ID</param>
        private void OnDutyCompleted(object? sender, ushort e)
        {
            // 打印副本完成事件日志
            LogHelper.Print($"副本任务完成（DutyCompleted 事件，ID: {e}）");
            _dutyCompleted = true; // 标记副本已完成

            // 查找字典中是否存在与当前副本 ID 对应的更新操作
            if (_dutyUpdateActions.TryGetValue((DutyType)e, out var updateAction))
            {
                updateAction();
            }
        }

        /// <summary>
        /// 当副本团灭时触发 DutyWiped 事件，可用于重置某些状态（目前仅打印日志）。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">副本任务ID</param>
        private void OnDutyWiped(object? sender, ushort e)
        {
            LogHelper.Print($"副本团灭重置（DutyWiped 事件，ID: {e}）");
            // 如有需要，在此处重置其他状态
            _isCountdownCompleted = false;
        }

        /// <summary>
        /// 绘制 AutomationTab 的所有 UI 控件，
        /// 包括地图记录、自动倒计时、自动退本、遥控按钮以及自动排本的设置和调试信息。
        /// </summary>
        public void Draw()
        {
            //【地图记录与倒计时设置】

            // 按钮用于记录当前地图ID，并更新相应设置
            if (ImGui.Button("记录当前地图ID"))
            {
                Settings.UpdateAutoFuncZoneId(Core.Resolve<MemApiZoneInfo>().GetCurrTerrId());
            }
            ImGui.SameLine();
            ImGui.Text($"当前指定地图ID: {Settings.AutoFuncZoneId}");

            // 设置自动倒计时是否启用
            bool countdownEnabled = Settings.AutoCountdownEnabled;
            if (ImGui.Checkbox("进本自动倒计时", ref countdownEnabled))
            {
                Settings.UpdateAutoCountdownEnabled(countdownEnabled);
            }
            ImGui.SameLine();

            // 输入倒计时延迟时间（秒）
            ImGui.SetNextItemWidth(80f * scale);
            int countdownDelay = Settings.AutoCountdownDelay;
            if (ImGui.InputInt("##CountdownDelay", ref countdownDelay))
            {
                Settings.UpdateAutoCountdownDelay(countdownDelay);
            }
            ImGui.SameLine();
            ImGui.Text("秒");

            // 设置自动退本是否启用
            bool leaveEnabled = Settings.AutoLeaveEnabled;
            if (ImGui.Checkbox("副本结束后自动退本(需启用DR <即刻退本> 模块)", ref leaveEnabled))
            {
                Settings.UpdateAutoLeaveEnabled(leaveEnabled);
            }
            ImGui.SameLine();

            // 输入退本延迟时间（秒）
            ImGui.SetNextItemWidth(80f * scale);
            int leaveDelay = Settings.AutoLeaveDelay;
            if (ImGui.InputInt("##LeaveDutyDelay", ref leaveDelay))
            {
                Settings.UpdateAutoLeaveDelay(leaveDelay);
            }
            ImGui.SameLine();
            ImGui.Text("秒");

            //设置是否等待R点完成后再退本
            bool waitRCompleted = Settings.AutoLeaveAfterLootEnabled;
            if (ImGui.Checkbox("等待R点完成后再退本", ref waitRCompleted))
            {
                Settings.UpdateAutoLeaveAfterLootEnabled(waitRCompleted);
            }

            //设置自动开宝箱是否启用
            bool openChestEnabled = Settings.AutoOpenChestEnabled;
            if (ImGui.Checkbox("副本结束后自动开宝箱", ref openChestEnabled))
            {
                Settings.UpdateAutoOpenChestEnabled(openChestEnabled);
            }

            //【遥控按钮】

            ImGui.Separator();
            ImGui.Text("遥控按钮:");

            // 全队即刻退本按钮（需在副本内才可执行命令）
            if (ImGui.Button("全队即刻退本"))
            {
                if (Core.Resolve<MemApiDuty>().InMission)
                {
                    RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                    RemoteControlHelper.Cmd("", "/pdr leaveduty");
                }
            }
            ImGui.SameLine();

            // 全队TP至指定位置，操作为“撞电网”
            if (ImGui.Button("全队TP撞电网"))
            {
                if (Core.Resolve<MemApiDuty>().InMission)
                    RemoteControlHelper.SetPos("", new Vector3(100, 0, 125));
            }
            ImGui.SameLine();

            // 为队长发送排本命令按钮，通过获取队长名称后发送命令
            if (ImGui.Button("为队长发送排本命令"))
            {
                var leaderName = GetPartyLeaderName();
                if (!string.IsNullOrEmpty(leaderName))
                {
                    var leaderRole = RemoteControlHelper.GetRoleByPlayerName(leaderName);
                    RemoteControlHelper.Cmd(leaderRole,"/pdr load ContentFinderCommand");
                    RemoteControlHelper.Cmd(leaderRole, $"/pdrduty n {Settings.FinalSendDutyName}");
                    LogHelper.Print($"为队长 {leaderName} 发送排本命令: /pdrduty n {Settings.FinalSendDutyName}");
                }
            }

            // 打印敌对单位信息（调试用按钮）
            ImGui.Text("Debug用按钮:");
            if (ImGui.Button("打印可选中敌对单位信息"))
            {
                var enemies = Svc.Objects.OfType<IBattleNpc>().Where(x => x.IsTargetable && x.IsEnemy());
                foreach (var enemy in enemies)
                {
                    LogHelper.Print($"敌对单位: {enemy.Name} (EntityIdID: {enemy.EntityId}, DataId: {enemy.DataId}), 位置: {enemy.Position}");
                }
            }

            //【自动排本设置】

            ImGui.Separator();

            // 设置自动排本是否启用
            bool autoQueue = Settings.AutoQueueEnabled;
            if (ImGui.Checkbox("自动排本(需启用DR <任务搜索器指令> 模块)", ref autoQueue))
            {
                Settings.UpdateAutoQueueEnabled(autoQueue);
            }

            //输入排本延迟时间（秒）
            ImGui.SameLine();
            ImGui.Text("延迟");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f * scale);
            ImGui.SameLine();
            int queueDelay = Settings.AutoQueueDelay;
            if (queueDelay > 0)
            {
                if (ImGui.InputInt("##QueueDelay", ref queueDelay))
                {
                    Settings.UpdateAutoQueueDelay(queueDelay);
                }
                ImGui.SameLine();
                ImGui.Text("秒");
            }


            // 设置解限（若启用则在排本命令中加入 "unrest"）
            bool unrest = Settings.UnrestEnabled;
            if (ImGui.Checkbox("解限", ref unrest))
            {
                Settings.UpdateUnrestEnabled(unrest);
            }
            ImGui.Text("选择副本:");

            // 下拉框选择副本名称，包括预设名称和自定义选项
            ImGui.SetNextItemWidth(150f * scale);
            if (ImGui.BeginCombo("##DutyName", Settings.SelectedDutyName))
            {
                if (ImGui.Selectable("欧米茄绝境验证战", Settings.SelectedDutyName == "欧米茄绝境验证战"))
                    Settings.UpdateSelectedDutyName("欧米茄绝境验证战");
                if (ImGui.Selectable("幻想龙诗绝境战", Settings.SelectedDutyName == "幻想龙诗绝境战"))
                    Settings.UpdateSelectedDutyName("幻想龙诗绝境战");
                if (ImGui.Selectable("光暗未来绝境战", Settings.SelectedDutyName == "光暗未来绝境战"))
                    Settings.UpdateSelectedDutyName("光暗未来绝境战");
                if (ImGui.Selectable("永恒女王忆想歼灭战", Settings.SelectedDutyName == "永恒女王忆想歼灭战"))
                    Settings.UpdateSelectedDutyName("永恒女王忆想歼灭战");
                if (ImGui.Selectable("异闻阿罗阿罗岛", Settings.SelectedDutyName == "异闻阿罗阿罗岛"))
                    Settings.UpdateSelectedDutyName("异闻阿罗阿罗岛");
                if (ImGui.Selectable("零式异闻阿罗阿罗岛", Settings.SelectedDutyName == "零式异闻阿罗阿罗岛"))
                    Settings.UpdateSelectedDutyName("零式异闻阿罗阿罗岛");
                if (ImGui.Selectable("自定义", Settings.SelectedDutyName == "自定义"))
                    Settings.UpdateSelectedDutyName("自定义");
                ImGui.EndCombo();
            }

            // 如果选择自定义，则允许用户输入副本名称
            if (Settings.SelectedDutyName == "自定义")
            {
                ImGui.SetNextItemWidth(150f * scale);
                string custom = Settings.CustomDutyName;
                if (ImGui.InputText("自定义副本名称", ref custom, 50))
                {
                    Settings.UpdateCustomDutyName(custom);
                }
            }

            // 根据当前选择的副本和解限选项构造最终排本命令
            string finalDuty = Settings.SelectedDutyName == "自定义" && !string.IsNullOrEmpty(Settings.CustomDutyName)
                ? Settings.CustomDutyName
                : Settings.SelectedDutyName;
            if (Settings.UnrestEnabled)
                finalDuty += " unrest";
            Settings.UpdateFinalSendDutyName(finalDuty);
            ImGui.Text($"将发送的排本命令: /pdrduty n {finalDuty}");

            ImGui.Separator();

            //【调试区域】
            if (ImGui.CollapsingHeader("自动化Debug"))
            {
                // 显示自动倒计时、战斗状态、副本状态和跨服小队状态等辅助调试信息
                var autoCountdownStatus = Settings.AutoCountdownEnabled ? _isCountdownCompleted ? "已触发" : "待触发" : "未启用";
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
                ImGui.Text($"是否完成宝箱拾取: {_isLootCompleted}");
                ImGui.Separator();

                // 如果为跨服小队，显示每个队员的在线与副本状态
                if (isCrossRealmParty)
                {
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
        }

        /// <summary>
        /// 根据当前设置和游戏状态，自动发送倒计时命令。
        /// 在满足条件（地图匹配、启用倒计时、队伍所有成员有效、非战斗中、副本已开始且队伍人数为8）时：
        /// 等待8秒后，通过聊天框发送倒计时命令，命令格式为 "/countdown {delay}"。
        /// </summary>
        private async Task UpdateAutoCountdown()
        {
            if (_isCountdownRunning) return;
            if (_isCountdownCompleted) return;
            lock (_countdownLock)
            {
                if (_isCountdownRunning) return;
                _isCountdownRunning = true;
            }

            try
            {
                // 如果当前地图ID与设置不匹配，直接返回
                if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != Settings.AutoFuncZoneId)
                    return;
                if (!Settings.AutoCountdownEnabled)
                    return;
                // 检查队伍中是否所有成员均可选中（在线且有效）；否则返回
                if (Svc.Party.Any(member => member.GameObject is not { IsTargetable: true }))
                    return;

                var notInCombat = !Core.Me.InCombat();
                var inMission = Core.Resolve<MemApiDuty>().InMission;
                var partyIs8 = Core.Resolve<MemApiDuty>().DutyMembersNumber() == 8;

                // 若条件满足，则等待8秒后发送倒计时命令
                if (notInCombat && inMission && partyIs8)
                {
                    await Coroutine.Instance.WaitAsync(8000);
                    ChatHelper.SendMessage($"/countdown {Settings.AutoCountdownDelay}");
                    _isCountdownCompleted = true;
                }
            }
            catch (Exception e)
            {
                LogHelper.Print(e.Message);
            }
            finally
            {
                _isCountdownRunning = false;
            }
        }

        /// <summary>
        /// 当副本结束后，自动在等待设定的延迟时间后通过遥控命令退本。
        /// 前提条件：当前地图匹配、启用退本、在副本内且副本已完成。
        /// </summary>
        private async Task UpdateAutoLeave()
        {
            if (_isLeaveRunning) return;
            if (_isLeaveCompleted) return;

            lock (_leaveLock)
            {
                if (_isLeaveRunning) return;
                _isLeaveRunning = true;
            }

            try
            {
                if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != Settings.AutoFuncZoneId)
                    return;
                if (!Settings.AutoLeaveEnabled && !Settings.AutoLeaveAfterLootEnabled)
                    return;

                var isBoundByDuty = Core.Resolve<MemApiDuty>().IsBoundByDuty();
                if (isBoundByDuty && _dutyCompleted)
                {
                    if (Settings.AutoLeaveAfterLootEnabled)
                    {
                        if (_isLootCompleted)
                        {
                            RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                            RemoteControlHelper.Cmd("", "/pdr leaveduty");
                            _isLeaveCompleted = true;
                        }
                    }
                    else
                    {
                        await Coroutine.Instance.WaitAsync(Settings.AutoLeaveDelay * 1000);
                        RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                        RemoteControlHelper.Cmd("", "/pdr leaveduty");
                        _isLeaveCompleted = true;
                    }
                }
            }
            catch (Exception e)
            {
                LogHelper.Print(e.Message);
            }
            finally
            {
                _isLeaveRunning = false;
            }
        }

        /// <summary>
        /// 当副本结束后，自动开启副本的战利品宝箱。
        /// 前提条件：当前地图匹配、在副本内且副本已完成。
        ///  </summary>
        private async Task UpdateAutoOpenChest()
        {
            if (_isOpenChestRunning) return;
            if (_isOpenChestCompleted) return;
            if (_isLootCompleted) return;

            lock (_openChestLock)
            {
                if (_isOpenChestRunning) return;
                _isOpenChestRunning = true;
            }
            try
            {
                if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != Settings.AutoFuncZoneId)
                    return;
                if (!Settings.AutoOpenChestEnabled)
                    return;

                var isBoundByDuty = Core.Resolve<MemApiDuty>().IsBoundByDuty();
                if (isBoundByDuty && _dutyCompleted)
                {
                    if (_treasure == null)
                    {
                        //LogHelper.Print("尝试寻找宝箱");
                        var player = Core.Me;
                        if (player == null) return;
                        unsafe
                        {
                            var treasure = Svc.Objects.FirstOrDefault(o =>
                                {
                                    var obj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(void*)o.Address;
                                    if (!obj->GetIsTargetable()) return false;
                                    if ((ObjectKind)obj->ObjectKind != ObjectKind.Treasure) return false;

                                    // Check if the chest is already opened
                                    foreach (var item in Loot.Instance()->Items)
                                        if (item.ChestObjectId == o.GameObjectId) return false;

                                    return true;
                                });
                            if (treasure == null)
                            {
                                //LogHelper.Print("未找到可开启的宝箱");
                                return;
                            }
                            _treasure = treasure;
                        }

                    }
                    try
                    {
                        //LogHelper.Print($"尝试打开宝箱: {_treasure.Name} (EntityId: {_treasure.EntityId})");
                        var player = Core.Me;
                        if (player == null) return;
                        var distance = Vector3.Distance(player.Position, _treasure.Position) - player.HitboxRadius - _treasure.HitboxRadius;
                        if (distance > 0.5f)
                        {
                            RemoteControlHelper.Cmd("", $"/aetp {_treasure.Position.X},{_treasure.Position.Y},{_treasure.Position.Z}");
                            await Coroutine.Instance.WaitAsync(3000);

                        }
                        if (_treasure.IsTargetable)
                        {
                            LogHelper.Print("尝试打开宝箱");
                            Svc.Targets.Target = _treasure;
                            unsafe
                            {
                                TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(void*)_treasure.Address);
                            }
                            await Coroutine.Instance.WaitAsync(3000);
                        }
                        else
                        {
                            _isOpenChestCompleted = true;
                            _isLootCompleted = true;
                            LogHelper.Print($"已完成战利品分配");
                            _treasure = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Print($"Failed to open the chest: {ex.Message}");
                    }
                    finally
                    {
                        _isOpenChestRunning = false;
                    }
                }
            }
            catch (Exception e)
            {
                LogHelper.Print(e.Message);
            }
            finally
            {
                _isOpenChestRunning = false;
            }
        }

        /// <summary>
        /// 根据配置和当前队伍状态自动发送排本命令。
        /// 条件包括：启用自动排本、足够的时间间隔、队伍状态满足要求（队伍成员均在线、不在副本中、队伍人数为8）。
        /// 若任一条件不满足则不发送排本命令。
        /// </summary>
        private async Task UpdateAutoQueue()
        {
            if (_isQueueRunning) return;
            if (_isQueueCompleted) return;

            lock (_queueLock)
            {
                if (_isQueueRunning) return;
                _isQueueRunning = true;
            }
            try
            {
                // 根据选择的副本名称构造实际发送命令
                string dutyName = Settings.SelectedDutyName == "自定义" && !string.IsNullOrEmpty(Settings.CustomDutyName)
                    ? Settings.CustomDutyName
                    : Settings.SelectedDutyName;
                if (Settings.UnrestEnabled)
                    dutyName += " unrest";
                Settings.UpdateFinalSendDutyName(dutyName);

                // 未启用自动排本或上次命令不足3秒则返回
                if (!Settings.AutoQueueEnabled)
                    return;
                if (DateTime.Now - _lastAutoQueueTime < TimeSpan.FromSeconds(3))
                    return;
                if (Svc.Condition[ConditionFlag.InDutyQueue])
                    return;
                if (Core.Resolve<MemApiDuty>().IsBoundByDuty())
                    return;
                if (InfoProxyCrossRealm.GetPartyMemberCount() < 8)
                    return;

                // 检查跨服队伍中是否所有成员均在线且未在副本中，否则退出
                var partyStatus = GetCrossRealmPartyStatus();
                var invalidNames = partyStatus.Where(s => !s.IsOnline || s.IsInDuty)
                                              .Select(s => s.Name)
                                              .ToList();
                if (invalidNames.Any())
                {
                    LogHelper.Print("玩家不在线或在副本中：" + string.Join(", ", invalidNames));
                    return;
                }
                await Coroutine.Instance.WaitAsync(Settings.AutoQueueDelay * 1000);
                // 发送排本命令，通过聊天输入框
                ChatHelper.SendMessage("/pdr load ContentFinderCommand");
                ChatHelper.SendMessage($"/pdrduty n {Settings.FinalSendDutyName}");
                _lastAutoQueueTime = DateTime.Now;
                LogHelper.Print($"自动排本命令已发送：/pdrduty n {Settings.FinalSendDutyName}");
            }
            catch (Exception e)
            {
                LogHelper.Print(e.Message);
            }
            finally
            {
                _isQueueRunning = false;
            }
        }

        /// <summary>
        /// 重置副本完成标志 _dutyCompleted，当检测到玩家已经不在副本中时调用，
        /// 防止在下一次副本前仍保留上次完成状态。
        /// </summary>
        private void ResetDutyFlag()
        {
            try
            {
                if (Core.Resolve<MemApiDuty>().IsBoundByDuty())
                {
                    _isQueueCompleted = true;
                    return;
                }
                if (!_dutyCompleted)
                    return;
                LogHelper.Print("检测到玩家不在副本内，自动重置_dutyCompleted");
                _dutyCompleted = false;
                _isOpenChestCompleted = false;
                _isCountdownCompleted = false;
                _isLeaveCompleted = false;
                _isQueueCompleted = false;
                _isLootCompleted = false;
            }
            catch (Exception e)
            {
                LogHelper.Print(e.Message);
            }
        }

        /// <summary>
        /// 获取跨服小队中每个成员的状态信息，
        /// 返回每个成员的姓名、是否在线以及是否处于副本中的状态。
        /// </summary>
        /// <returns>包含队员状态的列表</returns>
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

        /// <summary>
        /// 遍历队伍成员信息，获取队长的名称。队长由 PartyLeader 或 PartyLeaderCrossWorld 标记确定。
        /// </summary>
        /// <returns>队长名称或 null（若未找到）</returns>
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
