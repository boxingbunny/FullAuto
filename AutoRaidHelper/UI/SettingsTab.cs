using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using AutoRaidHelper.Settings;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// SettingsTab 合并了 UI设置 和 关于 两个Tab的功能
    /// </summary>
    public class SettingsTab
    {
        private readonly UISettingsTab _uiSettingsTab;
        private readonly AboutTab _aboutTab;

        public SettingsTab()
        {
            _uiSettingsTab = new UISettingsTab();
            _aboutTab = new AboutTab();
        }

        public void Draw()
        {
            // UI设置卡片
            bool uiOpen = UIHelpers.BeginCard("UI设置", FontAwesomeIcon.PaintBrush, true);
            if (uiOpen)
            {
                _uiSettingsTab.Draw();
            }
            UIHelpers.EndCard(uiOpen);

            // 关于卡片
            bool aboutOpen = UIHelpers.BeginCard("关于", FontAwesomeIcon.InfoCircle, false);
            if (aboutOpen)
            {
                _aboutTab.Draw();
            }
            UIHelpers.EndCard(aboutOpen);
        }
    }
}
