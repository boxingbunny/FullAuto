using System.Numerics;
using AEAssist.Helper;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;

namespace AutoRaidHelper.Utils;

public static class DebugPoint
{
    private static readonly List<Vector3> Points = new();
    public static void Add(Vector3 pos) => Points.Add(pos);
    public static void Clear() => Points.Clear();

    public static void Render()
    {
        if (Points.Count == 0)
            return;
        try
        {
            var drawList = ImGui.GetForegroundDrawList();
            uint red = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1));
            uint yellow = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 1));
            const float radius = 4f;
            const float lineThickness = 2f;

            // 先画线
            for (int i = 0; i < Points.Count - 1; i++)
            {
                Svc.GameGui.WorldToScreen(Points[i], out var p1);
                Svc.GameGui.WorldToScreen(Points[i + 1], out var p2);
                drawList.AddLine(p1, p2, red, lineThickness);
            }

            // 再画点和序号
            for (int i = 0; i < Points.Count; i++)
            {
                if (Svc.GameGui.WorldToScreen(Points[i], out var screenPos))
                {
                    drawList.AddCircleFilled(screenPos, radius, red);
                    Vector2 textPos = new(screenPos.X + radius + 4, screenPos.Y - radius / 2);
                    drawList.AddText(textPos, yellow, $"[{i + 1}]");
                }
            }
        }
        catch(Exception ex)
        {
            LogHelper.PrintError(ex.Message);
        }
    }
}