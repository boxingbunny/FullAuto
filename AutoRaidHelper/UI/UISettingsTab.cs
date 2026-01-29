using System.Numerics;
using AEAssist.GUI;
using AutoRaidHelper.Settings;
using Dalamud.Bindings.ImGui;

namespace AutoRaidHelper.UI;

public class UISettingsTab
{
    public void Draw()
    {
        var settings = FullAutoSettings.Instance;

        ImGui.Text("窗口外观设置:");
        ImGui.Separator();
        ImGui.Spacing();

        // 磨砂玻璃背景开关
        bool useFrostedGlass = settings.UseFrostedGlass;
        if (ImGui.Checkbox("启用磨砂玻璃背景", ref useFrostedGlass))
        {
            settings.UpdateUseFrostedGlass(useFrostedGlass);
        }
        ImGuiHelper.SetHoverTooltip("启用后窗口将显示磨砂玻璃背景效果\n需要DirectX 11支持");

        ImGui.Spacing();

        // 窗口不透明度滑块
        float opacity = settings.WindowOpacity;
        ImGui.SetNextItemWidth(300f);
        if (ImGui.SliderFloat("窗口不透明度", ref opacity, 0.1f, 1.0f, "%.2f"))
        {
            settings.UpdateWindowOpacity(opacity);
        }
        ImGuiHelper.SetHoverTooltip("调整窗口背景的不透明度\n数值越大越不透明");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 显示当前状态
        ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "当前状态:");
        ImGui.Text($"磨砂玻璃: {(settings.UseFrostedGlass ? "已启用" : "已禁用")}");
        ImGui.Text($"不透明度: {settings.WindowOpacity:P0}");
    }
}
