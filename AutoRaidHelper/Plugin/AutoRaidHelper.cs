using AEAssist.AEPlugin;
using AEAssist.CombatRoutine.Trigger;
using AEAssist.Verify;
using AutoRaidHelper.Triggers.TriggerAction;
using AutoRaidHelper.Triggers.TriggerCondition;
using AutoRaidHelper.Windows;
using AutoRaidHelper.Utils;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using System.Runtime.Loader;

namespace AutoRaidHelper.Plugin
{
    public class AutoRaidHelper : IAEPlugin
    {
        private const string CommandName = "/arh";
        private WindowSystem? _windowSystem;
        private MainWindow? _mainWindow;

        // 无参构造函数，供AEAssist的PluginLoader使用
        public AutoRaidHelper()
        {
        }

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
            // 初始化窗口系统
            _windowSystem = new WindowSystem("AutoRaidHelper");
            _mainWindow = new MainWindow();
            _windowSystem.AddWindow(_mainWindow);

            // 注册窗口绘制
            Svc.PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += () =>
            {
                if (_mainWindow != null)
                    _mainWindow.IsOpen = true;
            };

            // 初始化MainWindow
            _mainWindow.OnLoad(loadContext);

            // 注册命令
            Svc.Commands.AddHandler(CommandName, new Dalamud.Game.Command.CommandInfo(OnCommand)
            {
                HelpMessage = "全自动小助手命令\n"
                           + "/arh - 打开/关闭主窗口\n"
                           + "/arh transferleader <玩家名> - 转移队长给指定玩家"
            });
        }

        public void Dispose()
        {
            // 注销命令
            Svc.Commands.RemoveHandler(CommandName);

            // 清理窗口
            if (_windowSystem != null && _mainWindow != null)
            {
                Svc.PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
                _mainWindow.Dispose();
                _windowSystem.RemoveAllWindows();
            }

            DebugPoint.Clear();
        }

        public void Update()
        {
            _mainWindow?.OnUpdate();
        }

        public void OnPluginUI()
        {
            // 在AEAssist UI中显示一个按钮来打开独立窗口
            if (_mainWindow != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.5f, 0.8f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.6f, 0.9f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.1f, 0.4f, 0.7f, 1.0f));

                if (ImGui.Button("打开全自动小助手独立窗口", new System.Numerics.Vector2(300, 40)))
                {
                    _mainWindow.IsOpen = true;
                }

                ImGui.PopStyleColor(3);

                ImGui.SameLine();
                ImGui.TextDisabled("(或使用命令: /arh)");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // 显示窗口状态
                var statusText = _mainWindow.IsOpen ? "窗口状态: 已打开" : "窗口状态: 已关闭";
                var statusColor = _mainWindow.IsOpen
                    ? new System.Numerics.Vector4(0.4f, 0.8f, 0.4f, 1.0f)
                    : new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1.0f);

                ImGui.TextColored(statusColor, statusText);
            }
        }

        #endregion

        private void OnCommand(string command, string args)
        {
            if (_mainWindow == null)
                return;

            if (string.IsNullOrWhiteSpace(args))
            {
                // 切换主窗口显示状态
                _mainWindow.IsOpen = !_mainWindow.IsOpen;
                return;
            }

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
                    Svc.Chat.Print($"  {CommandName} - 打开/关闭主窗口");
                    Svc.Chat.Print($"  {CommandName} transferleader <玩家名> - 转移队长");
                    break;
            }
        }
    }
}
