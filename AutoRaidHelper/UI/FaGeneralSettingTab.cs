using ImGuiNET;
using AutoRaidHelper.Settings;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// FaGeneralSettingTab 用于展示和管理 FaGeneralSetting 配置的 UI 部分，
    /// 主要控制是否在界面上绘制坐标点并打印调试信息。
    /// </summary>
    public class FaGeneralSettingTab
    {
        /// <summary>
        /// 获取全局单例 FullAutoSettings 中保存的 FaGeneralSetting 配置实例，
        /// 该配置包含了是否打印调试信息的状态。
        /// </summary>
        public FaGeneralSetting Settings => FullAutoSettings.Instance.FaGeneralSetting;

        /// <summary>
        /// 绘制 FaGeneralSetting 的 UI 界面。
        /// 此方法会显示一个复选框，允许用户启用或禁用“绘制坐标点并打印 Debug 信息”的功能，
        /// 当状态改变时调用 UpdatePrintDebugInfo 保存新的配置值。
        /// </summary>
        public void Draw()
        {
            // 从配置中获取当前是否启用调试信息的状态
            var printDebug = Settings.PrintDebugInfo;

            // 在 UI 中创建复选框，显示提示文本“绘制坐标点并打印Debug信息”
            // 并允许用户通过复选框来切换此功能的开启与关闭
            if (ImGui.Checkbox("绘制坐标点并打印Debug信息", ref printDebug))
            {
                // 当用户更改复选框状态后，调用配置更新方法保存新的状态
                Settings.UpdatePrintDebugInfo(printDebug);
            }
        }
    }
}
