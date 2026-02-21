using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using AutoRaidHelper.Settings;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// FAControlTab 合并了 FA全局设置 和 FA手动操作 两个Tab的功能
    /// </summary>
    public class FAControlTab
    {
        private readonly FaGeneralSettingTab _generalSettingTab;
        private readonly FaManualTab _manualTab;

        public FAControlTab()
        {
            _generalSettingTab = new FaGeneralSettingTab();
            _manualTab = new FaManualTab();
        }

        public void Update()
        {
            _manualTab.Update();
        }

        public void Draw()
        {
            // 全局设置卡片
            bool settingsOpen = UIHelpers.BeginCard("全局设置", FontAwesomeIcon.Cog, true);
            if (settingsOpen)
            {
                _generalSettingTab.Draw();
            }
            UIHelpers.EndCard(settingsOpen);

            // 手动操作卡片
            bool manualOpen = UIHelpers.BeginCard("手动操作", FontAwesomeIcon.Gamepad, true);
            if (manualOpen)
            {
                _manualTab.Draw();
            }
            UIHelpers.EndCard(manualOpen);
        }
    }
}
