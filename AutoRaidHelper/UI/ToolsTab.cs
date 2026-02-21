using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Runtime.Loader;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// ToolsTab 合并了 几何计算 和 日志监听 两个Tab的功能
    /// </summary>
    public class ToolsTab
    {
        private readonly GeometryTab _geometryTab;
        private readonly DebugPrintTab _debugPrintTab;

        public ToolsTab()
        {
            _geometryTab = new GeometryTab();
            _debugPrintTab = new DebugPrintTab();
        }

        public void Update()
        {
            _geometryTab.Update();
        }

        public void Draw()
        {
            // 几何计算卡片
            bool geometryOpen = UIHelpers.BeginCard("几何计算", FontAwesomeIcon.Calculator, true);
            if (geometryOpen)
            {
                _geometryTab.Draw();
            }
            UIHelpers.EndCard(geometryOpen);

            // 日志监听卡片
            bool debugOpen = UIHelpers.BeginCard("日志监听", FontAwesomeIcon.FileAlt, false);
            if (debugOpen)
            {
                _debugPrintTab.Draw();
            }
            UIHelpers.EndCard(debugOpen);
        }

        public void OnLoad(AssemblyLoadContext loadContext)
        {
            _debugPrintTab.OnLoad(loadContext);
        }

        public void Dispose()
        {
            _debugPrintTab.Dispose();
        }
    }
}
