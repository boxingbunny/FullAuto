using AEAssist.AEPlugin;
using AEAssist.CombatRoutine.Trigger;
using AEAssist.Verify;
using AutoRaidHelper.Triggers.TriggerAction;
using AutoRaidHelper.Triggers.TriggerCondition;
using AutoRaidHelper.UI;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using System.Runtime.Loader;
using AutoRaidHelper.Utils;

namespace AutoRaidHelper.Plugin
{
    public class AutoRaidHelper : IAEPlugin
    {
        private const string CommandName = "/arh";
        private readonly GeometryTab _geometryTab = new();
        private readonly AutomationTab _automationTab = new();
        private readonly FaGeneralSettingTab _faGeneralSettingTab = new();
        private readonly FaManualTab _faManualTab = new();
        private readonly DebugPrintTab _debugPrintTab = new();
        private readonly BlackListTab _blackListTab = new();
        private readonly FoodBuffTab _foodBuffTab = new();

        #region IAEPlugin Implementation

        public PluginSetting BuildPlugin()
        {
            TriggerMgr.Instance.Add("全自动小助手", new 指定职能tp指定位置().GetType());
            TriggerMgr.Instance.Add("全自动小助手", new 检测目标位置().GetType());
            return new PluginSetting
            {
                Name = "全自动小助手",
                LimitLevel = VIPLevel.Normal,
            };
        }

        public void OnLoad(AssemblyLoadContext loadContext)
        {
            _automationTab.OnLoad(loadContext);
            _debugPrintTab.OnLoad(loadContext);

            // 注册命令
            Svc.Commands.AddHandler(CommandName, new Dalamud.Game.Command.CommandInfo(OnCommand)
            {
                HelpMessage = "全自动小助手命令\n"
                           + "/arh transferleader <玩家名> - 转移队长给指定玩家"
            });
        }

        public void Dispose()
        {
            // 注销命令
            Svc.Commands.RemoveHandler(CommandName);

            _automationTab.Dispose();
            _debugPrintTab.Dispose();
            DebugPoint.Clear();
        }

        public void Update()
        {
            _geometryTab.Update();
            _automationTab.Update();
            _faManualTab.Update();
            _blackListTab.Update();
        }

        public void OnPluginUI()
        {
            if (ImGui.BeginTabBar("MainTabBar"))
            {
                if (ImGui.BeginTabItem("几何计算"))
                {
                    _geometryTab.Draw();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("自动化"))
                {
                    _automationTab.Draw();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("FA全局设置"))
                {
                    _faGeneralSettingTab.Draw();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("FA手动操作"))
                {
                    _faManualTab.Draw();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("日志监听"))
                {
                    _debugPrintTab.Draw();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("黑名单管理"))
                {
                    _blackListTab.Draw();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("食物警察"))
                {
                    _foodBuffTab.Draw();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            DebugPoint.Render();
        }

        private static void OnCommand(string command, string args)
        {
            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                return;

            var subCommand = parts[0].ToLower();

            switch (subCommand)
            {
                case "transferleader":
                    if (parts.Length < 2)
                    {
                        Svc.Chat.Print($"[ARH] 用法: {CommandName} transferleader <玩家名>");
                        return;
                    }
                    var targetPlayer = parts[1];
                    PartyLeaderHelper.TransferPartyLeader(targetPlayer);
                    break;

                default:
                    Svc.Chat.Print($"[ARH] 未知子命令: {subCommand}");
                    Svc.Chat.Print($"可用命令:");
                    Svc.Chat.Print($"  {CommandName} transferleader <玩家名> - 转移队长");
                    break;
            }
        }

        #endregion
    }
}
