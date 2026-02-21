using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// 现代化UI系统 - 与磨砂背景完美融合的玻璃态设计
    /// </summary>
    public static class ModernUI
    {
        // 配色方案 - 玻璃态设计，极低不透明度，融入背景
        private static readonly Vector4 AccentColor = new(0.5f, 0.7f, 1.0f, 0.6f);        // 主题色（半透明）
        private static readonly Vector4 AccentColorHover = new(0.6f, 0.8f, 1.0f, 0.8f);   // 悬停色
        private static readonly Vector4 GlassBg = new(0.15f, 0.15f, 0.2f, 0.15f);         // 玻璃背景（极低透明度）
        private static readonly Vector4 GlassBgHover = new(0.18f, 0.18f, 0.25f, 0.25f);   // 玻璃悬停
        private static readonly Vector4 GlassBorder = new(1.0f, 1.0f, 1.0f, 0.12f);       // 玻璃边框（白色半透明）
        private static readonly Vector4 GlassBorderHover = new(1.0f, 1.0f, 1.0f, 0.25f);  // 边框悬停
        private static readonly Vector4 TextPrimary = new(0.95f, 0.95f, 0.98f, 0.95f);    // 主文本
        private static readonly Vector4 TextSecondary = new(0.75f, 0.8f, 0.9f, 0.75f);    // 次要文本
        private static readonly Vector4 DividerColor = new(1.0f, 1.0f, 1.0f, 0.08f);      // 分隔线（白色极淡）

        // 动画状态存储
        private static readonly Dictionary<string, float> AnimationStates = new();
        private static readonly Dictionary<string, bool> HoverStates = new();
        private static readonly Dictionary<string, bool> CardOpenStates = new();

        /// <summary>
        /// 获取平滑的动画值
        /// </summary>
        private static float GetSmoothValue(string id, float target, float speed = 8f)
        {
            if (!AnimationStates.ContainsKey(id))
                AnimationStates[id] = target;

            float current = AnimationStates[id];
            float delta = ImGui.GetIO().DeltaTime;
            float newValue = current + (target - current) * Math.Min(speed * delta, 1f);
            AnimationStates[id] = newValue;

            return newValue;
        }

        /// <summary>
        /// 绘制玻璃态卡片 - 与磨砂背景融合
        /// </summary>
        public static bool BeginModernCard(string title, FontAwesomeIcon? icon = null, bool defaultOpen = true)
        {
            var id = $"##ModernCard_{title}";
            var drawList = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();
            var windowWidth = ImGui.GetContentRegionAvail().X;

            // 初始化卡片状态
            if (!CardOpenStates.ContainsKey(id))
                CardOpenStates[id] = defaultOpen;

            // 标题栏配置
            const float headerHeight = 38f;
            const float headerRounding = 6f;
            const float padding = 12f;

            var headerMin = cursorPos;
            var headerMax = new Vector2(cursorPos.X + windowWidth, cursorPos.Y + headerHeight);

            // 检测悬停状态
            var mousePos = ImGui.GetMousePos();
            bool isHovered = mousePos.X >= headerMin.X && mousePos.X <= headerMax.X &&
                           mousePos.Y >= headerMin.Y && mousePos.Y <= headerMax.Y;

            // 平滑动画
            float hoverAnim = GetSmoothValue($"{id}_hover", isHovered ? 1f : 0f, 10f);

            // 绘制完整圆角矩形背景
            var bgColor = Vector4.Lerp(GlassBg, GlassBgHover, hoverAnim);
            drawList.AddRectFilled(headerMin, headerMax, ImGui.ColorConvertFloat4ToU32(bgColor), headerRounding);

            // 绘制边框
            var borderColor = Vector4.Lerp(GlassBorder, GlassBorderHover, hoverAnim);
            drawList.AddRect(headerMin, headerMax, ImGui.ColorConvertFloat4ToU32(borderColor), headerRounding, ImDrawFlags.None, 1.0f);

            // 绘制展开/折叠箭头
            float arrowX = headerMin.X + padding;
            float arrowY = headerMin.Y + headerHeight * 0.5f;
            bool isOpen = CardOpenStates[id];
            float arrowRotation = GetSmoothValue($"{id}_arrow", isOpen ? 90f : 0f, 12f);

            DrawRotatedArrow(drawList, new Vector2(arrowX, arrowY), arrowRotation, TextPrimary, 6f);

            // 绘制图标（如果有）
            float textStartX = arrowX + 20f;
            if (icon.HasValue)
            {
                ImGui.PushFont(UiBuilder.IconFont);
                var iconText = icon.Value.ToIconString();
                var iconTextSize = ImGui.CalcTextSize(iconText);
                ImGui.PopFont();

                var iconColor = Vector4.Lerp(AccentColor, AccentColorHover, hoverAnim);
                var iconPos = new Vector2(textStartX, headerMin.Y + (headerHeight - iconTextSize.Y) * 0.5f);

                ImGui.PushFont(UiBuilder.IconFont);
                drawList.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), iconPos, ImGui.ColorConvertFloat4ToU32(iconColor), iconText);
                ImGui.PopFont();

                textStartX += iconTextSize.X + 8f;
            }

            // 绘制标题文字
            var textSize = ImGui.CalcTextSize(title);
            var textPos = new Vector2(textStartX, headerMin.Y + (headerHeight - textSize.Y) * 0.5f);
            drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(TextPrimary), title);

            // 处理点击
            if (isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                CardOpenStates[id] = !CardOpenStates[id];
            }

            // 移动光标到标题栏下方
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + headerHeight);

            if (CardOpenStates[id])
            {
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Indent(12f);
            }

            return CardOpenStates[id];
        }

        /// <summary>
        /// 绘制旋转的箭头
        /// </summary>
        private static void DrawRotatedArrow(ImDrawListPtr drawList, Vector2 center, float angleDegrees, Vector4 color, float size)
        {
            float angleRad = angleDegrees * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);

            // 箭头的三个点（指向右的箭头）
            Vector2[] points = new[]
            {
                new Vector2(-size * 0.3f, -size * 0.4f),
                new Vector2(size * 0.4f, 0f),
                new Vector2(-size * 0.3f, size * 0.4f)
            };

            // 旋转并平移
            for (int i = 0; i < points.Length; i++)
            {
                float x = points[i].X * cos - points[i].Y * sin;
                float y = points[i].X * sin + points[i].Y * cos;
                points[i] = new Vector2(center.X + x, center.Y + y);
            }

            // 绘制箭头
            drawList.AddTriangleFilled(points[0], points[1], points[2], ImGui.ColorConvertFloat4ToU32(color));
        }

        /// <summary>
        /// 结束玻璃态卡片
        /// </summary>
        public static void EndModernCard(bool wasOpen)
        {
            if (wasOpen)
            {
                ImGui.Unindent(12f);
                ImGui.Spacing();
                ImGui.Spacing();
            }
            else
            {
                ImGui.Spacing();
            }
        }

        /// <summary>
        /// 绘制玻璃态分组标题
        /// </summary>
        public static void DrawSectionHeader(string text, FontAwesomeIcon? icon = null)
        {
            ImGui.Spacing();

            var drawList = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();
            var windowWidth = ImGui.GetContentRegionAvail().X;

            // 绘制极淡的装饰线（主题色）
            drawList.AddLine(
                new Vector2(cursorPos.X, cursorPos.Y + 10f),
                new Vector2(cursorPos.X + 2f, cursorPos.Y + 10f),
                ImGui.ColorConvertFloat4ToU32(AccentColor),
                2f
            );

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);

            if (icon.HasValue)
            {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(AccentColor, icon.Value.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine(0, 6f);
            }

            ImGui.TextColored(TextPrimary, text);

            // 绘制极淡的分隔线
            var textEnd = ImGui.GetCursorScreenPos();
            drawList.AddLine(
                new Vector2(textEnd.X + 8f, textEnd.Y - 8f),
                new Vector2(cursorPos.X + windowWidth, textEnd.Y - 8f),
                ImGui.ColorConvertFloat4ToU32(DividerColor),
                1f
            );

            ImGui.Spacing();
        }

        /// <summary>
        /// 绘制玻璃态按钮
        /// </summary>
        public static bool ModernButton(string label, Vector4? color = null, Vector2? size = null)
        {
            var id = $"##ModernBtn_{label}";
            var btnColor = color ?? AccentColor;
            var drawList = ImGui.GetWindowDrawList();

            var buttonSize = size ?? new Vector2(ImGui.CalcTextSize(label).X + 20f, 28f);
            var cursorPos = ImGui.GetCursorScreenPos();

            // 检测悬停和点击
            ImGui.InvisibleButton(id, buttonSize);
            bool isHovered = ImGui.IsItemHovered();
            bool isClicked = ImGui.IsItemClicked();
            bool isActive = ImGui.IsItemActive();

            // 平滑动画
            float hoverAnim = GetSmoothValue($"{id}_hover", isHovered ? 1f : 0f, 10f);
            float activeAnim = GetSmoothValue($"{id}_active", isActive ? 1f : 0f, 15f);

            // 绘制玻璃态背景（极淡）
            var bgAlpha = 0.15f + hoverAnim * 0.15f - activeAnim * 0.05f;
            var bgColor = new Vector4(btnColor.X, btnColor.Y, btnColor.Z, bgAlpha);
            drawList.AddRectFilled(
                cursorPos,
                new Vector2(cursorPos.X + buttonSize.X, cursorPos.Y + buttonSize.Y),
                ImGui.ColorConvertFloat4ToU32(bgColor),
                4f
            );

            // 绘制边框（白色半透明）
            var borderAlpha = 0.2f + hoverAnim * 0.2f;
            var borderColor = new Vector4(1f, 1f, 1f, borderAlpha);
            drawList.AddRect(
                cursorPos,
                new Vector2(cursorPos.X + buttonSize.X, cursorPos.Y + buttonSize.Y),
                ImGui.ColorConvertFloat4ToU32(borderColor),
                4f,
                ImDrawFlags.None,
                1.0f
            );

            // 绘制文字
            var textSize = ImGui.CalcTextSize(label);
            var textPos = new Vector2(
                cursorPos.X + (buttonSize.X - textSize.X) * 0.5f,
                cursorPos.Y + (buttonSize.Y - textSize.Y) * 0.5f - activeAnim * 0.5f
            );
            var textColor = Vector4.Lerp(TextPrimary, new Vector4(1f, 1f, 1f, 1f), hoverAnim);
            drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(textColor), label);

            return isClicked;
        }

        /// <summary>
        /// 绘制玻璃态信息提示框
        /// </summary>
        public static void InfoBox(string text, Vector4? color = null, FontAwesomeIcon? icon = null)
        {
            var boxColor = color ?? new Vector4(0.3f, 0.5f, 0.8f, 0.15f);
            var drawList = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();
            var windowWidth = ImGui.GetContentRegionAvail().X;

            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + windowWidth - 24f);
            var textSize = ImGui.CalcTextSize(text);
            ImGui.PopTextWrapPos();

            var boxHeight = textSize.Y + 20f;

            // 绘制玻璃态背景
            drawList.AddRectFilled(
                cursorPos,
                new Vector2(cursorPos.X + windowWidth, cursorPos.Y + boxHeight),
                ImGui.ColorConvertFloat4ToU32(boxColor),
                4f
            );

            // 绘制边框
            drawList.AddRect(
                cursorPos,
                new Vector2(cursorPos.X + windowWidth, cursorPos.Y + boxHeight),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.15f)),
                4f,
                ImDrawFlags.None,
                1f
            );

            // 绘制左侧装饰线
            drawList.AddLine(
                new Vector2(cursorPos.X + 2f, cursorPos.Y + 4f),
                new Vector2(cursorPos.X + 2f, cursorPos.Y + boxHeight - 4f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(boxColor.X * 3f, boxColor.Y * 3f, boxColor.Z * 3f, 0.6f)),
                2f
            );

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 10f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 12f);

            if (icon.HasValue)
            {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(TextPrimary, icon.Value.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine(0, 6f);
            }

            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + windowWidth - 24f);
            ImGui.TextColored(TextSecondary, text);
            ImGui.PopTextWrapPos();

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 10f);
            ImGui.Spacing();
        }
    }
}
