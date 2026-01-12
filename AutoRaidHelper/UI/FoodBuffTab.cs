using System.Numerics;
using AEAssist.Helper;
using AutoRaidHelper.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;

namespace AutoRaidHelper.UI;

public class FoodBuffTab
{
    private const uint FoodBuffId = 48;

    public void Draw()
    {
        ImGui.TextColored(new Vector4(0.2f, 1f, 0.6f, 1f), "全队食物Buff检查");
        ImGui.Separator();
        ImGui.Spacing();

        try
        {
            // 获取队伍成员列表
            var battleCharaMembers = Svc.Party
                .Select(p => p.GameObject as IBattleChara)
                .Where(bc => bc != null);

            // 获取包含状态信息的队伍成员
            var partyInfo = battleCharaMembers.ToPartyMemberInfo().ToList();

            if (!partyInfo.Any())
            {
                ImGui.TextDisabled("当前不在队伍中或无法获取队伍信息");
                return;
            }

            ImGui.Text($"队伍人数: {partyInfo.Count}");
            ImGui.Spacing();

            // 统计有食物buff和没有食物buff的人数
            int withFoodCount = 0;
            int withoutFoodCount = 0;

            foreach (var member in partyInfo)
            {
                if (member.StatusIds.Contains(FoodBuffId))
                    withFoodCount++;
                else
                    withoutFoodCount++;
            }

            // 显示统计信息
            ImGui.Text($"已吃食物: ");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), $"{withFoodCount}");

            ImGui.SameLine();
            ImGui.Text(" | 未吃食物: ");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), $"{withoutFoodCount}");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // 显示详细列表
            if (ImGui.BeginTable("##FoodBuffTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("职能", ImGuiTableColumnFlags.WidthFixed, 60f);
                ImGui.TableSetupColumn("角色名", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("食物状态", ImGuiTableColumnFlags.WidthFixed, 100f);
                ImGui.TableHeadersRow();

                foreach (var member in partyInfo.OrderBy(m => m.Role))
                {
                    ImGui.TableNextRow();

                    // 职能列
                    ImGui.TableNextColumn();
                    ImGui.Text(member.Role);

                    // 角色名列
                    ImGui.TableNextColumn();
                    ImGui.Text(member.Name);

                    // 食物状态列
                    ImGui.TableNextColumn();
                    bool hasFood = member.StatusIds.Contains(FoodBuffId);

                    if (hasFood)
                    {
                        ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), "✓ 已吃");
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "✗ 未吃");
                    }
                }

                ImGui.EndTable();
            }
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), $"错误: {ex.Message}");
            LogHelper.PrintError($"FoodBuffTab错误: {ex.Message}");
        }
    }
}