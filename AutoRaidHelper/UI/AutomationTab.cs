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
using ImGuiNET;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using AEAssist.GUI;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCommonList.CharacterData.OnlineStatus;
using DutyType = AutoRaidHelper.Settings.AutomationSettings.DutyType;
using DutyCategory = AutoRaidHelper.Settings.AutomationSettings.DutyCategory;
using KillTargetType = AutoRaidHelper.Settings.AutomationSettings.KillTargetType;
using PartyRole = AutoRaidHelper.Settings.AutomationSettings.PartyRole;

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
        private int _runtimes;

        private readonly Dictionary<string, bool> _roleSelection = new()
        {
            { "MT", false },
            { "ST", false },
            { "H1", false },
            { "H2", false },
            { "D1", false },
            { "D2", false },
            { "D3", false },
            { "D4", false }
        };

        private string _selectedRoles = "";
        private bool _customRoleEnabled;
        private string _customRoleInput = "";
        private string _customCmd = "";

        // 添加击杀目标选择相关的状态变量
        private string _selectedKillTarget = "请选择目标";
        private string _selectedKillRole = "";
        private string _selectedKillName = "";
        private KillTargetType _killTargetType = KillTargetType.None;
        
        public AutomationTab()
        {
            _dutyUpdateActions = new Dictionary<DutyType, Action>
            {
                { DutyType.UCOB, () => UpdateDuty(DutyType.UCOB, ref _ucobCompletedCount, 1, "巴哈") },
                { DutyType.UWU, () => UpdateDuty(DutyType.UWU, ref _uwuCompletedCount, 1, "神兵") },
                { DutyType.TEA, () => UpdateDuty(DutyType.TEA, ref _teaCompletedCount, 1, "绝亚") },
                { DutyType.DSR, () => UpdateDuty(DutyType.DSR, ref _dsrCompletedCount, 1, "龙诗") },
                { DutyType.TOP, () => UpdateDuty(DutyType.TOP, ref _topCompletedCount, 1, "绝欧") },
                { DutyType.FRU, () => UpdateDuty(DutyType.FRU, ref _fruCompletedCount, 1, "伊甸") },
                { DutyType.Aloalo, () => UpdateDuty(DutyType.Aloalo, ref _aloaloCompletedCount, 1, "阿罗阿罗") },
                { DutyType.Worqor, () => UpdateDuty(DutyType.Worqor, ref _worqorCompletedCount, 1, "蛇鸟") },
                { DutyType.Everkeep, () => UpdateDuty(DutyType.Everkeep, ref _everkeepCompletedCount, 1, "佐拉加") },
                { DutyType.Sphene, () => UpdateDuty(DutyType.Sphene, ref _spheneCompletedCount, 2, "女王") },
                { DutyType.Recollection, () => UpdateDuty(DutyType.Recollection, ref _recollectionCompletedCount, 1, "泽莲尼娅") },
            };
            Settings.AutoEnterOccult = false;
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
                DutyType.UCOB => Settings.UCOBCompletedCount,
                DutyType.UWU => Settings.UWUCompletedCount,
                DutyType.TEA => Settings.TEACompletedCount,
                DutyType.DSR => Settings.DSRCompletedCount,
                DutyType.TOP => Settings.TOPCompletedCount,
                DutyType.FRU => Settings.FRUCompletedCount,
                DutyType.Aloalo => Settings.AloaloCompletedCount,
                DutyType.Worqor => Settings.WorqorCompletedCount,
                DutyType.Everkeep => Settings.EverkeepCompletedCount,
                DutyType.Sphene => Settings.SpheneCompletedCount,
                DutyType.Recollection => Settings.RecollectionCompletedCount,
                _ => 0
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
        private int _dsrCompletedCount;
        // 记录欧米茄低保数
        private int _topCompletedCount;
        // 记录女王低保数
        private int _spheneCompletedCount;
        // 记录蛇鸟低保数
        private int _worqorCompletedCount;
        // 记录泽莲尼娅低保数
        private int _recollectionCompletedCount;
        // 记录伊甸低保数
        private int _fruCompletedCount;
        // 记录神兵低保数
        private int _uwuCompletedCount;
        // 记录巴哈低保数
        private int _ucobCompletedCount;
        // 记录绝亚低保数
        private int _teaCompletedCount;
        // 记录佐拉加低保数
        private int _everkeepCompletedCount;
        // 记录零式阿罗阿罗岛低保数
        private int _aloaloCompletedCount;

        private bool _isCountdownRunning;
        private bool _isLeaveRunning;
        private bool _isQueueRunning;
        private bool _isEnterOccultRunning;
        private bool _isSwitchNotMaxSupJobRunning;
        
        private bool _isCountdownCompleted;
        private bool _isLeaveCompleted;
        private bool _isQueueCompleted;
        private bool _isEnterOccultCompleted;
        private bool _isSwitchNotMaxSupJobCompleted;

        private readonly object _countdownLock = new();
        private readonly object _leaveLock = new();
        private readonly object _queueLock = new();
        private readonly object _enterOccultLock = new();
        private readonly object _switchNotMaxSupJobLock = new();

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
                await UpdateAutoLeave();
                await UpdateAutoQueue();
                await UpdateAutoEnterOccult();
                await UpdateAutoSwitchNotMaxSupJob();
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
            _runtimes++;

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
        public unsafe void Draw()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(infoAttr))
            {
                var parts = infoAttr.Split('+', '.');
                ImGui.Text($"Version: {parts[0]}.{parts[1]}.{parts[2]}+{parts[4]}");
            }
            ImGui.Separator();
            //【地图记录与倒计时设置】
            ImGui.Text("本内自动化设置:");
            // 按钮用于记录当前地图ID，并更新相应设置
            if (ImGui.Button("记录当前地图ID"))
            {
                Settings.UpdateAutoFuncZoneId(Core.Resolve<MemApiZoneInfo>().GetCurrTerrId());
            }
            ImGuiHelper.SetHoverTooltip("设置本部分内容先记录地图。");
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
            if (ImGui.Checkbox("副本结束后自动退本", ref leaveEnabled))
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

            //【遥控按钮】

            ImGui.Separator();
            ImGui.Text("遥控按钮:");
            
            // 全队TP至指定位置，操作为"撞电网"
            if (ImGui.Button("全队TP撞电网"))
            {
                if (Core.Resolve<MemApiDuty>().InMission)
                    RemoteControlHelper.SetPos("", new Vector3(100, 0, 125));
            }
            ImGui.SameLine();
            // 全队即刻退本按钮（需在副本内才可执行命令）
            if (ImGui.Button("全队即刻退本"))
            {
                RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                RemoteControlHelper.Cmd("", "/pdr leaveduty");
            }

            // 修改为下拉菜单选择目标
            if (ImGui.BeginCombo("##KillAllCombo", _selectedKillTarget))
            {
                // 获取当前玩家角色和队伍信息
                var roleMe = AI.Instance.PartyRole;
                // 使用 Svc.Party 获取队伍列表，并转换为 IBattleChara
                var battleCharaMembers = Svc.Party
                    .Select(p => p.GameObject as IBattleChara)
                    .Where(bc => bc != null);
                // 获取包含 Role 的队伍信息
                var partyInfo = battleCharaMembers.ToPartyMemberInfo();

                // 添加全队选项
                if (ImGui.Selectable("向7个队友发送Kill指令", _killTargetType == KillTargetType.AllParty))
                {
                    _selectedKillTarget = "向7个队友发送Kill指令";
                    _killTargetType = KillTargetType.AllParty;
                    _selectedKillRole = "";
                    _selectedKillName = "";
                }

                ImGui.Separator();

                // 列出队员选项
                foreach (var info in partyInfo)
                {
                    // 跳过自己
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

            // 添加执行按钮
            if (ImGui.Button("关闭所选目标游戏"))
            {
                ExecuteSelectedKillAction();
            }
            
            ImGui.Text("选择队员职能：");

            foreach (var role in _roleSelection.Keys.ToList())
            {
                ImGui.SameLine();

                bool value = _roleSelection[role];
                if (ImGui.Checkbox(role, ref value))
                {
                    _roleSelection[role] = value;

                    var selected = _roleSelection
                        .Where(pair => pair.Value)
                        .Select(pair => pair.Key);
                    _selectedRoles = string.Join("|", selected);
                }
            }

            ImGui.SameLine();
            ImGui.Checkbox("自定义", ref _customRoleEnabled);

            if (_customRoleEnabled)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f * scale); // 可选：设定宽度
                ImGui.InputText("##_customRoleInput", ref _customRoleInput, 20);

                // 更新字符串拼接逻辑
                var selected = _roleSelection
                    .Where(pair => pair.Value)
                    .Select(pair => pair.Key)
                    .ToList();

                if (!string.IsNullOrWhiteSpace(_customRoleInput))
                {
                    selected.Add(_customRoleInput.Trim());
                }

                _selectedRoles = string.Join("|", selected);
            }

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
            
            // ────────────────────── 顶蟹 ──────────────────────
            if (ImGui.Button("顶蟹"))
            {
                const ulong targetCid = 19014409511470591UL; // 小猪蟹 Cid
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
            }
            
            //【自动排本设置】
            ImGui.Separator();
            ImGui.Text("自动排本设置:");
            // 设置自动排本是否启用
            bool autoQueue = Settings.AutoQueueEnabled;
            if (ImGui.Checkbox("自动排本", ref autoQueue))
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

            if (ImGui.InputInt("##QueueDelay", ref queueDelay))
            {
                // 强制最小为 0
                queueDelay = Math.Max(0, queueDelay);
                Settings.UpdateAutoQueueDelay(queueDelay);
            }

            ImGui.SameLine();
            ImGui.Text("秒");
            ImGui.SameLine();
            // 设置解限（若启用则在排本命令中加入 "unrest"）
            bool unrest = Settings.UnrestEnabled;
            if (ImGui.Checkbox("解限", ref unrest))
            {
                Settings.UpdateUnrestEnabled(unrest);
            }

            //通过副本指定次数后停止自动排本 & 关游戏/关机
            {
                // 读取指定次数
                bool runtimeEnabled = Settings.RunTimeEnabled;

                // 勾选总开关
                if (ImGui.Checkbox($"通过副本指定次后停止自动排本(目前已通过{_runtimes}次)", ref runtimeEnabled))
                {
                    Settings.UpdateRunTimeEnabled(runtimeEnabled);
                    if (!runtimeEnabled) // 关掉时清零已通过次数
                        _runtimes = 0;
                }

                ImGui.SameLine();

                // 输入指定次数
                ImGui.SetNextItemWidth(80f * scale);
                int runtime = Settings.RunTimeLimit;
                if (ImGui.InputInt("##RunTimeLimit", ref runtime))
                    Settings.UpdateRunTimeLimit(runtime);

                ImGui.SameLine();
                ImGui.Text("次");

                // 勾选各职能需不需要关游戏 / 关机
                if (runtimeEnabled)
                {
                    ImGui.Separator();
                    ImGui.Text("完成指定次数后要操作的职能：");

                    if (ImGui.BeginTable("##KillShutdownTable", 3,
                                         ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        // 列设置 + 彩色表头
                        ImGui.TableSetupColumn("职能",   ImGuiTableColumnFlags.None, 70f);
                        ImGui.TableSetupColumn("关游戏", ImGuiTableColumnFlags.None, 60f);
                        ImGui.TableSetupColumn("关机",   ImGuiTableColumnFlags.None, 60f);

                        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                        ImGui.TableSetColumnIndex(0);  ImGui.Text("职能");
                        ImGui.TableSetColumnIndex(1);  ImGui.TextColored(new(1,0.70f,0,1), "关游戏");
                        ImGui.TableSetColumnIndex(2);  ImGui.TextColored(new(1,0.25f,0.25f,1), "关机");

                        var roles = new[]
                        {
                            PartyRole.MT, PartyRole.ST, PartyRole.H1, PartyRole.H2,
                            PartyRole.D1, PartyRole.D2, PartyRole.D3, PartyRole.D4
                        };

                        foreach (var role in roles)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0); ImGui.Text(role.ToString());

                            // 关游戏
                            ImGui.TableSetColumnIndex(1);
                            bool kill = Settings.KillRoleFlags[role];
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.70f, 0f, 1f)); // 橙色
                            if (ImGui.Checkbox($"##Kill{role}", ref kill))
                                Settings.UpdateKillRoleFlag(role, kill);
                            ImGui.PopStyleColor();

                            // 关机
                            ImGui.TableSetColumnIndex(2);
                            bool shut = Settings.ShutdownRoleFlags[role];
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.25f, 0.25f, 1f)); // 红色
                            if (ImGui.Checkbox($"##Shut{role}", ref shut))
                                Settings.UpdateShutdownRoleFlag(role, shut);
                            ImGui.PopStyleColor();
                        }
                        ImGui.EndTable();
                    }
                }
            }

            ImGui.Text("选择副本:");

            // 下拉框选择副本名称，包括预设名称和自定义选项
            var settings = FullAutoSettings.Instance.AutomationSettings;

            ImGui.SetNextItemWidth(200f * scale);
            if (ImGui.BeginCombo("##DutyName", settings.SelectedDutyName))
            {
                bool firstGroup = true;
                foreach (DutyCategory category in Enum.GetValues<DutyCategory>())
                {
                    // 按分组筛选副本
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
            
            // 如果选择自定义，则允许用户输入副本名称
            if (Settings.SelectedDutyName == "自定义")
            {
                ImGui.SetNextItemWidth(200f * scale);
                string custom = Settings.CustomDutyName;
                if (ImGui.InputText("自定义副本名称", ref custom, 50))
                {
                    Settings.UpdateCustomDutyName(custom);
                }
            }
            ImGui.SameLine();
            // 为队长发送排本命令按钮，通过获取队长名称后发送命令
            if (ImGui.Button("为队长发送排本命令"))
            {
                var leaderName = GetPartyLeaderName();
                if (!string.IsNullOrEmpty(leaderName))
                {
                    var leaderRole = RemoteControlHelper.GetRoleByPlayerName(leaderName);
                    RemoteControlHelper.Cmd(leaderRole, "/pdr load ContentFinderCommand");
                    RemoteControlHelper.Cmd(leaderRole, $"/pdrduty n {Settings.FinalSendDutyName}");
                    LogHelper.Print($"为队长 {leaderName} 发送排本命令: /pdrduty n {Settings.FinalSendDutyName}");
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
            
            ImGui.Text("新月岛设置:");
            // 设置自动排本是否启用
            bool enterOccult = Settings.AutoEnterOccult;
            if (ImGui.Checkbox("自动进岛/换岛", ref enterOccult))
            {
                // 不用Update，免得下次上线自动传送到新月岛
                Settings.AutoEnterOccult = enterOccult;
            }
            bool switchNotMaxSupJob = Settings.AutoSwitchNotMaxSupJob;
            if (ImGui.Checkbox("自动切换未满级辅助职业", ref switchNotMaxSupJob))
            {
                Settings.UpdateAutoSwitchNotMaxSupJob(switchNotMaxSupJob);
            }
            
            ImGui.Separator();
            //【调试区域】
            if (ImGui.CollapsingHeader("自动化Debug"))
            {
                // 打印敌对单位信息（调试用按钮）
                ImGui.Text("Debug用按钮:");
                if (ImGui.Button("打印可选中敌对单位信息"))
                {
                    var enemies = Svc.Objects.OfType<IBattleNpc>().Where(x => x.IsTargetable && x.IsEnemy());
                    foreach (var enemy in enemies)
                    {
                        LogHelper.Print(
                            $"敌对单位: {enemy.Name} (EntityId: {enemy.EntityId}, DataId: {enemy.DataId}, ObjId: {enemy.GameObjectId}), 位置: {enemy.Position}");
                    }
                }
                
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
                // 如果在新月岛内
                var instancePtr = PublicContentOccultCrescent.GetInstance();
                var statePtr = PublicContentOccultCrescent.GetState();
                if (instancePtr != null && statePtr != null)
                {
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
                        ImGui.SameLine();
                        if (ImGui.Button($"切换##{i}") && statePtr->CurrentSupportJob != i)
                        {
                            PublicContentOccultCrescent.ChangeSupportJob(i);
                        }
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
                    await Task.Delay(8000);
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
        private bool _hasLootAppeared; // 是否出现过roll点界面

        private async Task UpdateAutoLeave()
        {
            if (_isLeaveRunning || _isLeaveCompleted)
                return;

            lock (_leaveLock)
                _isLeaveRunning = true;

            try
            {
                if (Settings is { AutoLeaveEnabled: false, AutoLeaveAfterLootEnabled: false })
                    return;

                if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != Settings.AutoFuncZoneId)
                    return;

                if (Core.Resolve<MemApiDuty>().IsBoundByDuty() && _dutyCompleted)
                {
                    // 获取当前副本信息
                    var info = AutomationSettings.DutyPresets.FirstOrDefault(d => d.Name == Settings.SelectedDutyName || d.Name == Settings.CustomDutyName);
                    // 判断是否极神
                    bool hasChest = info is { Category: DutyCategory.Extreme };
                    
                    if (Settings.AutoLeaveAfterLootEnabled && hasChest)
                    {
                        unsafe
                        {
                            var lootPtr = Loot.Instance();
                            bool hasValidLoot = false;
                            bool allAwarded = true;

                            if (lootPtr != null)
                            {
                                var items = lootPtr->Items;
                                for (int i = 0; i < items.Length; i++)
                                {
                                    var loot = items[i];
                                    if (loot.ItemId != 0)
                                    {
                                        hasValidLoot = true;
                                        if (loot.RollResult != RollResult.Awarded)
                                        {
                                            allAwarded = false;
                                            break; // 还有未分配，不能退本
                                        }
                                    }
                                }

                                if (hasValidLoot && !_hasLootAppeared)
                                {
                                    LogHelper.Print("检测到roll点界面出现，开始等待分配");
                                    _hasLootAppeared = true;
                                }
                            }

                            // 没见过有掉落物的roll点界面
                            if (!_hasLootAppeared)
                                return;

                            // 见过roll点界面，但是还有未分配物品
                            if (!allAwarded)
                                return;

                            // 要么全部分配完成，要么面板已消失
                            LogHelper.Print("所有物品已真正分配完成，准备退本");
                            RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                            RemoteControlHelper.Cmd("", "/pdr leaveduty");

                            _isLeaveCompleted = true;
                            _isLeaveRunning = false;
                            return;
                        }
                    }
                    
                    // 否则直接延迟指定时间再退本
                    await Task.Delay(Settings.AutoLeaveDelay * 1000);
                    RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                    RemoteControlHelper.Cmd("", "/pdr leaveduty");
                    _isLeaveCompleted = true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.PrintError($"UpdateAutoLeave 异常: {ex}");
            }
            finally
            {
                _isLeaveRunning = false;
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

                // 如果到达指定次数则停止排本
                if (Settings.RunTimeEnabled && _runtimes >= Settings.RunTimeLimit)
                {
                    Settings.UpdateAutoQueueEnabled(false);
                    _runtimes = 0;

                    // 关游戏
                    string killRegex = Settings.BuildRegex(forKill: true);
                    if (!string.IsNullOrEmpty(killRegex))
                    {
                        RemoteControlHelper.Cmd(killRegex, "/xlkill");
                    }

                    // 关机
                    string shutRegex = Settings.BuildRegex(forKill: false);
                    if (!string.IsNullOrEmpty(shutRegex))
                    {
                        RemoteControlHelper.Shutdown(shutRegex);
                    }
                }

                // 未启用自动排本或上次命令不足3秒则返回
                if (!Settings.AutoQueueEnabled)
                    return;
                if (DateTime.Now - _lastAutoQueueTime < TimeSpan.FromSeconds(3))
                    return;
                // 已经在排本队列中则返回
                if (Svc.Condition[ConditionFlag.InDutyQueue])
                    return;
                if (Core.Resolve<MemApiDuty>().IsBoundByDuty())
                    return;
                // 解限时不考虑人数
                if (InfoProxyCrossRealm.GetPartyMemberCount() < 8 && !Settings.UnrestEnabled)
                    return;

                // 检查跨服队伍中是否所有成员均在线且未在副本中，否则退出
                var partyStatus = GetCrossRealmPartyStatus();
                var invalidNames = partyStatus.Where(s => !s.IsOnline || s.IsInDuty)
                    .Select(s => s.Name)
                    .ToList();
                if (invalidNames.Any())
                {
                    LogHelper.Print("玩家不在线或在副本中：" + string.Join(", ", invalidNames));
                    await Task.Delay(1000);
                    return;
                }

                await Task.Delay(Settings.AutoQueueDelay * 1000);

                _lastAutoQueueTime = DateTime.Now;
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
        /// 根据配置和当前队伍状态自动发送进岛命令。
        /// 条件包括：启用自动进岛、足够的时间间隔、队伍状态满足要求（队伍成员均在线、不在副本中）。
        /// 若任一条件不满足则不发送进岛命令。
        /// </summary>
        private async Task UpdateAutoEnterOccult()
        {
            if (_isEnterOccultRunning) return;
            if (_isEnterOccultCompleted) return;
            lock (_enterOccultLock)
            {
                if (_isEnterOccultRunning) return;
                _isEnterOccultRunning = true;
            }

            try
            {
                // 未启用自动进岛或上次命令不足5秒则返回
                if (!Settings.AutoEnterOccult)
                    return;
                if (DateTime.Now - _lastAutoQueueTime < TimeSpan.FromSeconds(5))
                    return;
                
                // 剩余时间不足90分钟且在大水晶边上直接退岛
                if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() == 1252 && Vector3.Distance(Core.Me.Position, new Vector3(828, 73, -696)) < 25 && Svc.ClientState.LocalPlayer != null)
                {
                    unsafe
                    {
                        var instancePtr = PublicContentOccultCrescent.GetInstance();
                        if (instancePtr != null && instancePtr->ContentTimeLeft / 60 is < 90 and > 0)
                        {
                            RemoteControlHelper.Cmd("", "/pdr load InstantLeaveDuty");
                            RemoteControlHelper.Cmd("", "/pdr leaveduty");
                            _lastAutoQueueTime = DateTime.Now;
                            return;
                        }
                    }
                }
                
                // 已经在排本队列中则返回
                if (Svc.Condition[ConditionFlag.InDutyQueue])
                    return;
                if (Core.Resolve<MemApiDuty>().IsBoundByDuty())
                    return;

                // 检查跨服队伍中是否所有成员均在线且未在副本中，否则退出
                var partyStatus = GetCrossRealmPartyStatus();
                var invalidNames = partyStatus.Where(s => !s.IsOnline || s.IsInDuty)
                    .Select(s => s.Name)
                    .ToList();
                if (invalidNames.Any())
                {
                    LogHelper.Print("玩家不在线或在副本中：" + string.Join(", ", invalidNames));
                    await Task.Delay(1000);
                    return;
                }
                
                var leaderName = GetPartyLeaderName();
                if (!string.IsNullOrEmpty(leaderName))
                {

                    if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != 1252)
                    {
                        var leaderRole = RemoteControlHelper.GetRoleByPlayerName(leaderName);
                        RemoteControlHelper.Cmd(leaderRole, "/pdr load FieldEntryCommand");
                        RemoteControlHelper.Cmd(leaderRole, "/pdrfe occultcrescent");
                        RemoteControlHelper.Cmd("", "/bocchiillegal on");
                        
                        RemoteControlHelper.Cmd("", "/pdr unload FasterTerritoryTransport");
                        RemoteControlHelper.Cmd("", "/pdr unload NoUIFade");
                        RemoteControlHelper.Cmd("", "/pdr unload OptimizedInteraction");
                        RemoteControlHelper.Cmd("", "/pdrspeed 1");
                        RemoteControlHelper.Cmd("", "/aeTargetSelector off");
                    }
                }
            
                _lastAutoQueueTime = DateTime.Now;
            }
            catch (Exception e)
            {
                LogHelper.Print(e.Message);
            }
            finally
            {
                _isEnterOccultRunning = false;
            }
        }

        /// <summary>
        /// 新月岛自动切换到未满级的辅助职业。
        /// </summary>
        private async Task UpdateAutoSwitchNotMaxSupJob()
        {
            if (_isSwitchNotMaxSupJobRunning) return;
            if (_isSwitchNotMaxSupJobCompleted) return;
            lock (_switchNotMaxSupJobLock)
            {
                if (_isSwitchNotMaxSupJobRunning) return;
                _isSwitchNotMaxSupJobRunning = true;
            }

            try
            {
                if (!Settings.AutoSwitchNotMaxSupJob)
                    return;
                // 如果不在新月岛内或距离大水晶太远则不切换
                if (Core.Resolve<MemApiZoneInfo>().GetCurrTerrId() != 1252 || Vector3.Distance(Core.Me.Position, new Vector3(828, 73, -696)) > 10)
                    return;
                
                await Task.Delay(5000);
                unsafe
                {
                    var statePtr = PublicContentOccultCrescent.GetState();
                    if (statePtr == null)
                        return;
                    var levels = statePtr->SupportJobLevels;
                    byte currentJob = statePtr->CurrentSupportJob;
                    // 当前职业未满级则不切换
                     if (levels[currentJob] < AutomationSettings.SupportJobData[currentJob].MaxLevel)
                         return;
                    for (byte jobId = 0; jobId < AutomationSettings.SupportJobData.Count; jobId++)
                    {
                        var (name, maxLevel) = AutomationSettings.SupportJobData[jobId];
                        byte level = levels[jobId];
                        // 已解锁且未满级
                        if (level <= 0 || level >= maxLevel || jobId == currentJob)
                            continue;
                        PublicContentOccultCrescent.ChangeSupportJob(jobId);
                        LogHelper.Print($"自动切换到 {name} (Lv.{level} / Max {maxLevel})");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                LogHelper.Print(e.Message);
            }
            finally
            {
                _isSwitchNotMaxSupJobRunning = false;
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
                _isCountdownCompleted = false;
                _isLeaveCompleted = false;
                _isQueueCompleted = false;
                _isEnterOccultCompleted = false;
                _isSwitchNotMaxSupJobCompleted = false;
                _hasLootAppeared = false;
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

        /// <summary>
        /// 执行选择的击杀操作
        /// </summary>
        private void ExecuteSelectedKillAction()
        {
            try
            {
                switch (_killTargetType)
                {
                    case KillTargetType.AllParty:
                        // 执行全队击杀
                        var roleMe = AI.Instance.PartyRole;
                        var battleCharaMembers = Svc.Party
                            .Select(p => p.GameObject as IBattleChara)
                            .Where(bc => bc != null);
                        var partyInfo = battleCharaMembers.ToPartyMemberInfo();
                        var partyExpectMe = partyInfo.Where(info => info.Role != roleMe).Select(info => info.Role);

                        foreach (var role in partyExpectMe)
                        {
                            if (!string.IsNullOrEmpty(role))
                            {
                                RemoteControlHelper.Cmd(role, "/xlkill");
                            }
                        }

                        LogHelper.Print("已向全队发送击杀命令");
                        break;

                    case KillTargetType.SinglePlayer:
                        // 执行单个玩家击杀
                        if (!string.IsNullOrEmpty(_selectedKillRole))
                        {
                            RemoteControlHelper.Cmd(_selectedKillRole, "/xlkill");
                            LogHelper.Print($"已向 {_selectedKillName} (职能: {_selectedKillRole}) 发送击杀命令");
                        }

                        break;

                    case KillTargetType.None:
                    default:
                        LogHelper.Print("请先选择要击杀的目标");
                        Core.Resolve<MemApiChatMessage>().Toast2("请先选择要击杀的目标", 1, 2000);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.PrintError($"执行击杀操作时发生异常: {ex}");
            }
        }
    }
}