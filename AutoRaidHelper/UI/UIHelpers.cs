using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// UI辅助类，提供通用的UI组件和布局功能
    /// </summary>
    public static class UIHelpers
    {
        private static readonly Vector4 CardBgColor = new(0.12f, 0.12f, 0.15f, 0.4f); // 降低不透明度
        private static readonly Vector4 CardBorderColor = new(0.3f, 0.3f, 0.35f, 0.6f); // 降低不透明度
        private static readonly Vector4 HeaderColor = new(0.2f, 0.25f, 0.35f, 0.5f); // 降低不透明度
        private static readonly Vector4 TitleTextColor = new(0.85f, 0.9f, 1f, 1f);

        /// <summary>
        /// 开始绘制一个可折叠的卡片容器
        /// </summary>
        /// <param name="title">卡片标题</param>
        /// <param name="icon">可选的图标</param>
        /// <param name="defaultOpen">默认是否展开</param>
        /// <returns>是否展开（用户可以点击标题栏折叠/展开）</returns>
        public static bool BeginCard(string title, FontAwesomeIcon? icon = null, bool defaultOpen = true)
        {
            // 使用CollapsingHeader实现折叠功能
            ImGui.PushStyleColor(ImGuiCol.Header, HeaderColor);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.25f, 0.3f, 0.4f, 0.6f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.3f, 0.35f, 0.45f, 0.7f));

            // 绘制图标（如果有）
            bool isOpen;
            if (icon.HasValue)
            {
                // 先绘制图标
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(icon.Value.ToIconString());
                ImGui.PopFont();

                // 在同一行绘制CollapsingHeader
                ImGui.SameLine(0, 8f);
                var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
                isOpen = ImGui.CollapsingHeader(title, flags);
            }
            else
            {
                var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
                isOpen = ImGui.CollapsingHeader(title, flags);
            }

            ImGui.PopStyleColor(3);

            if (isOpen)
            {
                // 添加左右padding
                ImGui.Indent(10f);
                ImGui.Spacing();
            }

            return isOpen;
        }

        /// <summary>
        /// 结束卡片容器
        /// </summary>
        /// <param name="wasOpen">卡片是否处于展开状态（从BeginCard返回值传入）</param>
        public static void EndCard(bool wasOpen)
        {
            if (wasOpen)
            {
                ImGui.Unindent(10f);
                ImGui.Spacing();
            }
            ImGui.Spacing();
        }

        /// <summary>
        /// 绘制一个分组标题
        /// </summary>
        public static void DrawSectionHeader(string text, FontAwesomeIcon? icon = null)
        {
            ImGui.Spacing();
            if (icon.HasValue)
            {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(new Vector4(0.7f, 0.8f, 1f, 1f), icon.Value.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine(0, 6f);
            }
            ImGui.TextColored(new Vector4(0.7f, 0.8f, 1f, 1f), text);
            ImGui.Separator();
            ImGui.Spacing();
        }

        /// <summary>
        /// 绘制一个带颜色的按钮
        /// </summary>
        public static bool ColoredButton(string label, Vector4 color, Vector2? size = null)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, color);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color * 1.2f);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, color * 0.8f);

            bool result = size.HasValue ? ImGui.Button(label, size.Value) : ImGui.Button(label);

            ImGui.PopStyleColor(3);
            return result;
        }

        /// <summary>
        /// 创建一个信息提示框
        /// </summary>
        public static void InfoBox(string text, Vector4? color = null)
        {
            var bgColor = color ?? new Vector4(0.2f, 0.3f, 0.5f, 0.3f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, bgColor);
            ImGui.BeginChild($"##InfoBox_{text.GetHashCode()}", new Vector2(0, 0), true);
            ImGui.TextWrapped(text);
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }
    }
}
