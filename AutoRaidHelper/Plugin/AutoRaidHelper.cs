using AEAssist.AEPlugin;
using AEAssist.Verify;
using ImGuiNET;
using System.Runtime.Loader;
using AEAssist.CombatRoutine.Trigger;
using AutoRaidHelper.UI;
using AutoRaidHelper.Triggers.TriggerAction;
using AutoRaidHelper.Triggers.TriggerCondition;

namespace AutoRaidHelper.Plugin
{
    public class AutoRaidHelper : IAEPlugin
    {
        private readonly GeometryTab _geometryTab = new();
        private readonly AutomationTab _automationTab = new();
        private readonly FaGeneralSettingTab _faGeneralSettingTab = new();
        private readonly DebugPrintTab _debugPrintTab = new();
        #region IAEPlugin Implementation

        public PluginSetting BuildPlugin()
        {
            TriggerMgr.Instance.Add("全自动小助手", new 指定职能tp指定位置().GetType());
            TriggerMgr.Instance.Add("全自动小助手", new 检测目标位置().GetType());
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