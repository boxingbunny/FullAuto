using AEAssist;
using AEAssist.CombatRoutine.Module;
using AEAssist.Extension;
using AEAssist.Helper;
using AEAssist.MemoryApi;
using AutoRaidHelper.Settings;
using AutoRaidHelper.Utils;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using AEAssist.GUI;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Dalamud.Interface;
using DutyCategory = AutoRaidHelper.Settings.AutomationSettings.DutyCategory;
using KillTargetType = AutoRaidHelper.Settings.AutomationSettings.KillTargetType;
using PartyRole = AutoRaidHelper.Settings.AutomationSettings.PartyRole;

namespace AutoRaidHelper.UI
{
    public partial class AutomationTab
    {
        /// <summary>
        /// 重构后的Draw方法，使用卡片式布局
        /// </summary>
        public unsafe void DrawRefactored()
        {
            // 副本内自动化设置卡片
            bool dutyOpen = UIHelpers.BeginCard("副本内自动化", FontAwesomeIcon.Cog, true);
            if (dutyOpen)
            {
                DrawDutyAutomationCard();
            }
            UIHelpers.EndCard(dutyOpen);

            // 遥控功能卡片
            bool remoteOpen = UIHelpers.BeginCard("遥控功能", FontAwesomeIcon.Gamepad, false);
            if (remoteOpen)
            {
                DrawRemoteControlCard();
            }
            UIHelpers.EndCard(remoteOpen);

            // 自动排本设置卡片
            bool queueOpen = UIHelpers.BeginCard("自动排本", FontAwesomeIcon.ListAlt, false);
            if (queueOpen)
            {
                DrawAutoQueueCard();
            }
            UIHelpers.EndCard(queueOpen);

            // 新月岛设置卡片
            bool occultOpen = UIHelpers.BeginCard("新月岛设置", FontAwesomeIcon.Moon, false);
            if (occultOpen)
            {
                DrawOccultSettings();
            }
            UIHelpers.EndCard(occultOpen);

            // Debug信息卡片（折叠）

            bool debugOpen = UIHelpers.BeginCard("自动化Debug", FontAwesomeIcon.Bug, false);
            if (debugOpen)
            {
                DrawDebugInfo();
            }

            UIHelpers.EndCard(debugOpen);
            
        }

        /// <summary>
        /// 绘制副本内自动化设置卡片
        /// </summary>
        private void DrawDutyAutomationCard()
        {
            // 地图ID设置
            if (ImGui.Button("记录当前地图ID"))
            {
                Settings.UpdateAutoFuncZoneId(Core.Resolve<MemApiZoneInfo>().GetCurrTerrId());
            }
            ImGuiHelper.SetHoverTooltip("设置本部分内容先记录地图");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1f), $"当前指定地图ID: {Settings.AutoFuncZoneId}");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // 自动倒计时
            bool countdownEnabled = Settings.AutoCountdownEnabled;
            if (ImGui.Checkbox("进本自动倒计时", ref countdownEnabled))
            {
                Settings.UpdateAutoCountdownEnabled(countdownEnabled);
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f * scale);
            int countdownDelay = Settings.AutoCountdownDelay;
            if (ImGui.InputInt("##CountdownDelay", ref countdownDelay))
            {
                Settings.UpdateAutoCountdownDelay(countdownDelay);
            }
            ImGui.SameLine();
            ImGui.Text("秒");

            ImGui.Spacing();

            // 自动退本
            bool leaveEnabled = Settings.AutoLeaveEnabled;
            if (ImGui.Checkbox("副本结束后自动退本", ref leaveEnabled))
            {
                Settings.UpdateAutoLeaveEnabled(leaveEnabled);
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f * scale);
            int leaveDelay = Settings.AutoLeaveDelay;
            if (ImGui.InputInt("##LeaveDutyDelay", ref leaveDelay))
            {
                Settings.UpdateAutoLeaveDelay(leaveDelay);
            }
            ImGui.SameLine();
            ImGui.Text("秒");

            ImGui.Spacing();

            // 等待R点完成和Roll点追踪
            bool waitRCompleted = Settings.AutoLeaveAfterLootEnabled;
            if (ImGui.Checkbox("等待R点完成后再退本", ref waitRCompleted))
            {
                Settings.UpdateAutoLeaveAfterLootEnabled(waitRCompleted);
            }

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            // Roll点追踪
            bool lootTrackEnabled = Settings.LootTrackEnabled;
            if (ImGui.Checkbox("开启Roll点追踪", ref lootTrackEnabled))
            {
                Settings.UpdateLootTrackEnabled(lootTrackEnabled);
                if (lootTrackEnabled)
                    LootTracker.Initialize();
                else
                    LootTracker.Dispose();
            }

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            if (!Settings.LootTrackEnabled)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button("打印Roll点记录"))
            {
                LootTracker.PrintAllRecords();
            }
            if (!Settings.LootTrackEnabled)
            {
                ImGui.EndDisabled();
            }
        }

        /// <summary>
        /// 绘制遥控功能卡片
        /// </summary>
        private void DrawRemoteControlCard()
        {
            // 快捷操作按钮
            if (ImGui.Button("全队TP撞电网"))
            {
                if (Core.Resolve<MemApiDuty>().InMission)
                    RemoteControlHelper.SetPos("", new Vector3(0, 0, 0));
            }
            ImGui.SameLine();
            if (ImGui.Button("全队即刻退本"))
            {
                RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                RemoteControlHelper.Cmd("", "/pdr leaveduty");
            }
            ImGui.SameLine();
            if (ImGui.Button("清理Debug点"))
            {
                DebugPoint.Clear();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // 关闭游戏功能
            UIHelpers.DrawSectionHeader("关闭游戏", FontAwesomeIcon.PowerOff);

            if (ImGui.BeginCombo("##KillAllCombo", _selectedKillTarget))
            {
                var roleMe = AI.Instance.PartyRole;
                var battleCharaMembers = Svc.Party
                    .Select(p => p.GameObject as IBattleChara)
                    .Where(bc => bc != null);
                var partyInfo = battleCharaMembers.ToPartyMemberInfo();

                if (ImGui.Selectable("向7个队友发送Kill指令", _killTargetType == KillTargetType.AllParty))
                {
                    _selectedKillTarget = "向7个队友发送Kill指令";
                    _killTargetType = KillTargetType.AllParty;
                    _selectedKillRole = "";
                    _selectedKillName = "";
                }

                ImGui.Separator();

                foreach (var info in partyInfo)
                {
                    if (info.Role == roleMe) continue;

                    var displayText = $"{info.Name} (ID: {info.Member.EntityId})";
                    bool isSelected = _killTargetType == KillTargetType.SinglePlayer &&
                                      _selectedKillRole == info.Role;

                    if (ImGui.Selectable(displayText, isSelected))
                    {
                        _selectedKillTarget = displayText;
                        _killTargetType = KillTargetType.SinglePlayer;
                        _selectedKillRole = info.Role;
                        _selectedKillName = info.Name;
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();

            if (ImGui.Button("关闭所选目标游戏"))
            {
                ExecuteSelectedKillAction();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // 角色选择和自定义指令
            UIHelpers.DrawSectionHeader("自定义指令", FontAwesomeIcon.Terminal);

            DrawRoleSelectionTable();

            ImGui.Spacing();

            ImGui.InputTextWithHint("##_customCmd", "请输入需要发送的指令", ref _customCmd, 256);
            ImGui.SameLine();

            if (ImGui.Button("发送指令"))
            {
                if (!string.IsNullOrEmpty(_selectedRoles))
                {
                    RemoteControlHelper.Cmd(_selectedRoles, _customCmd);
                    LogHelper.Print($"为 {_selectedRoles} 发送了文本指令:{_customCmd}");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // 顶蟹按钮
            if (ImGui.Button("顶蟹"))
            {
                ExecuteTopCrab();
            }
        }

        /// <summary>
        /// 绘制角色选择表格
        /// </summary>
        private void DrawRoleSelectionTable()
        {
            if (ImGui.BeginTable("##RoleSelectTable_Auto", _roleOrder.Length + 1, ImGuiTableFlags.SizingFixedFit))
            {
                var roleNameMap = BuildRoleNameMap();
                float colWidth = 38f;
                for (int i = 0; i < _roleOrder.Length; i++)
                {
                    ImGui.TableSetupColumn($"##RoleColAuto{i}", ImGuiTableColumnFlags.WidthFixed, colWidth);
                }
                ImGui.TableSetupColumn("##RoleColAutoAll", ImGuiTableColumnFlags.WidthFixed, 52f);

                ImGui.TableNextRow();
                for (int i = 0; i < _roleOrder.Length; i++)
                {
                    ImGui.TableSetColumnIndex(i);
                    var text = _roleOrder[i];
                    float textWidth = ImGui.CalcTextSize(text).X;
                    float cellX = ImGui.GetCursorPosX();
                    float centerX = cellX + colWidth * 0.5f;
                    ImGui.SetCursorPosX(centerX - textWidth * 0.5f);
                    ImGui.TextColored(GetRoleColor(text), text);
                    if (ImGui.IsItemHovered() && roleNameMap.TryGetValue(text, out var name) && !string.IsNullOrEmpty(name))
                        ImGui.SetTooltip(name);
                }
                ImGui.TableSetColumnIndex(_roleOrder.Length);
                ImGui.Dummy(new Vector2(1f, ImGui.GetTextLineHeight()));

                ImGui.TableNextRow();
                for (int i = 0; i < _roleOrder.Length; i++)
                {
                    ImGui.TableSetColumnIndex(i);
                    float cellX = ImGui.GetCursorPosX();
                    float centerX = cellX + colWidth * 0.5f;
                    ImGui.SetCursorPosX(centerX - 32f * 0.5f);

                    var role = _roleOrder[i];
                    bool value = _roleSelection[role];
                    if (DrawRoleDot(role, ref value))
                        _roleSelection[role] = value;
                }

                ImGui.TableSetColumnIndex(_roleOrder.Length);
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 6f);
                if (ImGui.Button("全选"))
                    ToggleAllRoles();

                ImGui.EndTable();
            }

            UpdateSelectedRoles();
        }

        /// <summary>
        /// 绘制自动排本设置卡片
        /// </summary>
        private void DrawAutoQueueCard()
        {
            // 自动排本开关
            bool autoQueue = Settings.AutoQueueEnabled;
            if (ImGui.Checkbox("自动排本", ref autoQueue))
            {
                Settings.UpdateAutoQueueEnabled(autoQueue);
            }

            ImGui.SameLine();
            ImGui.Text("延迟");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f * scale);
            int queueDelay = Settings.AutoQueueDelay;
            if (ImGui.InputInt("##QueueDelay", ref queueDelay))
            {
                queueDelay = Math.Max(0, queueDelay);
                Settings.UpdateAutoQueueDelay(queueDelay);
            }
            ImGui.SameLine();
            ImGui.Text("秒");
            ImGui.SameLine();

            bool unrest = Settings.UnrestEnabled;
            if (ImGui.Checkbox("解限", ref unrest))
            {
                Settings.UpdateUnrestEnabled(unrest);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // 通过次数限制
            bool runtimeEnabled = Settings.RunTimeEnabled;
            if (ImGui.Checkbox($"通过副本指定次后停止自动排本(目前已通过{_runtimes}次)", ref runtimeEnabled))
            {
                Settings.UpdateRunTimeEnabled(runtimeEnabled);
                if (!runtimeEnabled)
                    _runtimes = 0;
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f * scale);
            int runtime = Settings.RunTimeLimit;
            if (ImGui.InputInt("##RunTimeLimit", ref runtime))
                Settings.UpdateRunTimeLimit(runtime);
            ImGui.SameLine();
            ImGui.Text("次");

            if (runtimeEnabled)
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "完成指定次数后要操作的职能：");

                if (ImGui.BeginTable("##KillShutdownTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("职能", ImGuiTableColumnFlags.None, 70f);
                    ImGui.TableSetupColumn("关游戏", ImGuiTableColumnFlags.None, 60f);
                    ImGui.TableSetupColumn("关机", ImGuiTableColumnFlags.None, 60f);

                    ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                    ImGui.TableSetColumnIndex(0); ImGui.Text("职能");
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.2f, 1.0f), "关游戏");
                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), "关机");

                    var roles = new[]
                    {
                        PartyRole.MT, PartyRole.ST, PartyRole.H1, PartyRole.H2,
                        PartyRole.D1, PartyRole.D2, PartyRole.D3, PartyRole.D4
                    };

                    foreach (var role in roles)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0); ImGui.Text(role.ToString());

                        ImGui.TableSetColumnIndex(1);
                        bool kill = Settings.KillRoleFlags[role];
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.70f, 0f, 1f));
                        if (ImGui.Checkbox($"##Kill{role}", ref kill))
                            Settings.UpdateKillRoleFlag(role, kill);
                        ImGui.PopStyleColor();

                        ImGui.TableSetColumnIndex(2);
                        bool shut = Settings.ShutdownRoleFlags[role];
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.25f, 0.25f, 1f));
                        if (ImGui.Checkbox($"##Shut{role}", ref shut))
                            Settings.UpdateShutdownRoleFlag(role, shut);
                        ImGui.PopStyleColor();
                    }
                    ImGui.EndTable();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // 副本选择
            UIHelpers.DrawSectionHeader("选择副本", FontAwesomeIcon.Dungeon);

            var settings = FullAutoSettings.Instance.AutomationSettings;
            ImGui.SetNextItemWidth(200f * scale);
            if (ImGui.BeginCombo("##DutyName", settings.SelectedDutyName))
            {
                bool firstGroup = true;
                foreach (DutyCategory category in Enum.GetValues<DutyCategory>())
                {
                    var duties = AutomationSettings.DutyPresets.Where(d => d.Category == category).ToList();
                    if (duties.Count == 0) continue;

                    if (!firstGroup) ImGui.Separator();
                    firstGroup = false;

                    string tag = category switch
                    {
                        DutyCategory.Ultimate => "绝本",
                        DutyCategory.Extreme => "极神",
                        DutyCategory.Savage => "零式",
                        DutyCategory.Variant => "异闻",
                        DutyCategory.Criterion => "零式异闻",
                        DutyCategory.Custom => "自定义",
                        _ => "其它"
                    };
                    ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.2f, 1.0f), tag);

                    foreach (var duty in duties.Where(duty => ImGui.Selectable(duty.Name, settings.SelectedDutyName == duty.Name)))
                    {
                        settings.UpdateSelectedDutyName(duty.Name);
                    }
                }
                ImGui.EndCombo();
            }

            if (Settings.SelectedDutyName == "自定义")
            {
                ImGui.Spacing();
                ImGui.SetNextItemWidth(200f * scale);
                string custom = Settings.CustomDutyName;
                if (ImGui.InputText("自定义副本名称", ref custom, 50))
                {
                    Settings.UpdateCustomDutyName(custom);
                }
            }
            else
            {
                ImGui.Spacing();
            }

            if (ImGui.Button("为队长发送排本命令"))
            {
                var leaderName = PartyLeaderHelper.GetPartyLeaderName();
                if (!string.IsNullOrEmpty(leaderName))
                {
                    var leaderRole = RemoteControlHelper.GetRoleByPlayerName(leaderName);
                    RemoteControlHelper.Cmd(leaderRole, "/pdr load ContentFinderCommand");
                    RemoteControlHelper.Cmd(leaderRole, $"/pdrduty n {Settings.FinalSendDutyName}");
                    LogHelper.Print($"为队长 {leaderName} 发送排本命令: /pdrduty n {Settings.FinalSendDutyName}");
                }
            }

            string finalDuty = Settings.SelectedDutyName == "自定义" && !string.IsNullOrEmpty(Settings.CustomDutyName)
                ? Settings.CustomDutyName
                : Settings.SelectedDutyName;
            if (Settings.UnrestEnabled)
                finalDuty += " unrest";
            Settings.UpdateFinalSendDutyName(finalDuty);

            ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1f), $"将发送的排本命令: /pdrduty n {finalDuty}");
        }

        /// <summary>
        /// 绘制新月岛设置卡片
        /// </summary>
        private void DrawOccultSettings()
        {
            bool enterOccult = Settings.AutoEnterOccult;
            if (ImGui.Checkbox("自动进岛/换岛 (满足以下任一条件)", ref enterOccult))
            {
                Settings.AutoEnterOccult = enterOccult;
            }

            ImGui.Spacing();

            // 剩余时间设置
            ImGui.Text("剩余时间:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f * scale);
            int reEnterTimeThreshold = Settings.OccultReEnterThreshold;
            if (ImGui.InputInt("##OccultReEnterThreshold", ref reEnterTimeThreshold))
            {
                reEnterTimeThreshold = Math.Clamp(reEnterTimeThreshold, 0, 180);
                Settings.UpdateOccultReEnterThreshold(reEnterTimeThreshold);
            }
            ImGui.SameLine();
            ImGui.Text("分钟");

            // 锁岛人数设置
            ImGui.Text("总人数:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f * scale);
            int lockThreshold = Settings.OccultLockThreshold;
            if (ImGui.InputInt("##OccultLockThreshold", ref lockThreshold))
            {
                lockThreshold = Math.Clamp(lockThreshold, 1, 72);
                Settings.UpdateOccultLockThreshold(lockThreshold);
            }
            ImGui.SameLine();
            ImGui.Text("人 (连续5次采样低于此值)");

            // 黑名单人数设置
            ImGui.Text("命中黑名单玩家人数:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f * scale);
            int blackListThreshold = Settings.OccultBlackListThreshold;
            if (ImGui.InputInt("##OccultBlackListThreshold", ref blackListThreshold))
            {
                blackListThreshold = Math.Clamp(blackListThreshold, 0, 72);
                Settings.UpdateOccultBlackListThreshold(blackListThreshold);
            }
            ImGui.SameLine();
            ImGui.Text("人");

            ImGui.Spacing();

            bool switchNotMaxSupJob = Settings.AutoSwitchNotMaxSupJob;
            if (ImGui.Checkbox("自动切换未满级辅助职业", ref switchNotMaxSupJob))
            {
                Settings.UpdateAutoSwitchNotMaxSupJob(switchNotMaxSupJob);
            }
        }

        /// <summary>
        /// 绘制调试信息
        /// </summary>
        private unsafe void DrawDebugInfo()
        {
            if (ImGui.Button("打印可选中敌对单位信息"))
            {
                var enemies = Svc.Objects.OfType<IBattleNpc>().Where(x => x.IsTargetable && x.IsEnemy());
                foreach (var enemy in enemies)
                {
                    LogHelper.Print(
                        $"敌对单位: {enemy.Name} (EntityId: {enemy.EntityId}, BaseId: {enemy.BaseId}, ObjId: {enemy.GameObjectId}), 位置: {enemy.Position}");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

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

            if (isCrossRealmParty)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.Text("跨服小队成员及状态:");
                var partyStatus = PartyLeaderHelper.GetCrossRealmPartyStatus();
                for (int i = 0; i < partyStatus.Count; i++)
                {
                    var status = partyStatus[i];
                    var onlineText = status.IsOnline ? "在线" : "离线";
                    var dutyText = status.IsInDuty ? "副本中" : "副本外";
                    ImGui.Text($"[{i}] {status.Name} 状态: {onlineText}, {dutyText}");
                }
            }

            var instancePtr = PublicContentOccultCrescent.GetInstance();
            var statePtr = PublicContentOccultCrescent.GetState();
            if (instancePtr != null && statePtr != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.Text("新月岛内状态");
                float remainingTime = instancePtr->ContentTimeLeft;
                ImGui.Text($"剩余时间: {(int)(remainingTime / 60)}分{(int)(remainingTime % 60)}秒");

                ImGui.Text("职业等级:");
                var supportLevels = statePtr->SupportJobLevels;
                for (byte i = 0; i < supportLevels.Length; i++)
                {
                    var job = AutomationSettings.SupportJobData[i].Name;
                    byte level = supportLevels[i];
                    ImGui.Text($"{job}: Level {level}");
                    if (level >= AutomationSettings.SupportJobData[i].MaxLevel)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Max");
                    }
                    if (level <= 0)
                        continue;
                    ImGui.SameLine();
                    if (ImGui.Button($"切换##{i}") && statePtr->CurrentSupportJob != i)
                    {
                        PublicContentOccultCrescent.ChangeSupportJob(i);
                    }
                }
                var proxy = (InfoProxy24*)InfoModule.Instance()->GetInfoProxyById((InfoProxyId)24);
                ImGui.Text($"现在岛内人数: {proxy->EntryCount}");
                ImGui.Text($"当前岛内黑名单玩家数量: {BlackListTab.LastHitCount}");
                ImGui.Text($"当前是否处于CE范围内: {IsInsideCriticalEncounter(Core.Me.Position)}");
            }
        }

        /// <summary>
        /// 执行顶蟹操作
        /// </summary>
        private unsafe void ExecuteTopCrab()
        {
            const ulong targetCid = 19014409511470591UL;
            string? targetRole = null;

            var infoModule = InfoModule.Instance();
            var commonList = (InfoProxyCommonList*)infoModule->GetInfoProxyById(InfoProxyId.PartyMember);
            if (commonList != null)
            {
                foreach (var data in commonList->CharDataSpan)
                {
                    if (data.ContentId == targetCid)
                    {
                        var targetName = data.NameString;
                        targetRole = RemoteControlHelper.GetRoleByPlayerName(targetName);
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(targetRole))
            {
                RemoteControlHelper.Cmd(targetRole, "/gaction 跳跃");
                Core.Resolve<MemApiChatMessage>().Toast2("顶蟹成功!", 1, 2000);
            }
            else
            {
                string msg = "队伍中未找到小猪蟹";
                LogHelper.Print(msg);
            }

            var random = new Random().Next(10);
            var message = "允许你顶蟹";
            if (random > 5)
            {
                message = "不许顶我！";
            }

            Utilities.FakeMessage("歌无谢", "拉诺西亚", message, XivChatType.TellIncoming);
        }
    }
}
