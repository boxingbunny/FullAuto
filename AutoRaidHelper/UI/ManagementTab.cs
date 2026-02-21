using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// ManagementTab 合并了 黑名单管理 和 Roll点设置 两个Tab的功能
    /// </summary>
    public class ManagementTab
    {
        private readonly BlackListTab _blackListTab;
        private readonly LootRollingTab _lootRollingTab;

        public ManagementTab()
        {
            _blackListTab = new BlackListTab();
            _lootRollingTab = new LootRollingTab();
        }

        public void Update()
        {
            _blackListTab.Update();
        }

        public void Draw()
        {
            // 黑名单管理卡片
            bool blacklistOpen = UIHelpers.BeginCard("黑名单管理", FontAwesomeIcon.Ban, true);
            if (blacklistOpen)
            {
                _blackListTab.Draw();
            }
            UIHelpers.EndCard(blacklistOpen);

            // Roll点设置卡片
            bool lootOpen = UIHelpers.BeginCard("Roll点设置", FontAwesomeIcon.Dice, false);
            if (lootOpen)
            {
                _lootRollingTab.Draw();
            }
            UIHelpers.EndCard(lootOpen);
        }
    }
}
