using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// UI辅助类，提供通用的UI组件和布局功能
    /// 使用现代化UI系统
    /// </summary>
    public static class UIHelpers
    {
        /// <summary>
        /// 开始绘制一个可折叠的卡片容器（使用现代化UI）
        /// </summary>
        public static bool BeginCard(string title, FontAwesomeIcon? icon = null, bool defaultOpen = true)
        {
            return ModernUI.BeginModernCard(title, icon, defaultOpen);
        }

        /// <summary>
        /// 结束卡片容器
        /// </summary>
        public static void EndCard(bool wasOpen)
        {
            ModernUI.EndModernCard(wasOpen);
        }

        /// <summary>
        /// 绘制一个分组标题
        /// </summary>
        public static void DrawSectionHeader(string text, FontAwesomeIcon? icon = null)
        {
            ModernUI.DrawSectionHeader(text, icon);
        }

        /// <summary>
        /// 绘制一个带颜色的按钮
        /// </summary>
        public static bool ColoredButton(string label, Vector4 color, Vector2? size = null)
        {
            return ModernUI.ModernButton(label, color, size);
        }

        /// <summary>
        /// 创建一个信息提示框
        /// </summary>
        public static void InfoBox(string text, Vector4? color = null)
        {
            ModernUI.InfoBox(text, color);
        }
    }
}

