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
using AutoRaidHelper.Settings;
using AutoRaidHelper.Utils;
using AutoRaidHelper.UI;

namespace AutoRaidHelper.Plugin
{
    public class AutoRaidHelper : IAEPlugin
    {
        private readonly GeometryTab _geometryTab = new GeometryTab();
        private readonly AutomationTab _automationTab = new AutomationTab();
        private readonly FaGeneralSettingTab _faGeneralSettingTab = new FaGeneralSettingTab();
        private readonly DebugPrintTab _debugPrintTab = new DebugPrintTab();
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
            _automationTab.OnLoad(loadContext);
            _debugPrintTab.OnLoad(loadContext);
        }

        public void Dispose()
        {
            _automationTab.Dispose();
            _debugPrintTab.Dispose();
        }

        public void Update()
        {
            _geometryTab.Update();
            _automationTab.Update();
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

                if (ImGui.BeginTabItem("日志监听"))
                {
                    _debugPrintTab.Draw();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        #endregion
               

    }
}