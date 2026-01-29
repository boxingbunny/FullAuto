using System.Reflection;
using Dalamud.Bindings.ImGui;

namespace AutoRaidHelper.UI;

public class aboutTab
{
    public void Draw()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(infoAttr))
        {
            var parts = infoAttr.Split('+', '.');
            ImGui.Text($"Version: {parts[0]}.{parts[1]}.{parts[2]}+{parts[4]}");
        }
        ImGui.Separator();
        ImGui.Text("Authors:JiaXX, BoxingBunny, FrostBlade, Sinclair, Cindy-Master, MisaUo, 小猪蟹, Uncle Ken, Fragile");
    }
}