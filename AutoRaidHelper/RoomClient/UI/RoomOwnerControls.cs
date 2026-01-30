using System;
using System.Threading.Tasks;
using AEAssist.Helper;
using Dalamud.Bindings.ImGui;

namespace AutoRaidHelper.RoomClient.UI;

/// <summary>
/// 房主控制组件 - 踢人、分配职能、分配队伍
/// </summary>
public class RoomOwnerControls
{
    // 队伍选项
    private static readonly string[] TeamLabels = { "-", "A", "B", "C", "D", "E", "F" };
    private static readonly string[] TeamValues = { "", "A", "B", "C", "D", "E", "F" };

    // 职能选项
    private static readonly string[] RoleLabels = { "-", "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
    private static readonly string[] RoleValues = { "", "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };

    /// <summary>
    /// 绘制玩家行（在表格中调用）
    /// </summary>
    public void DrawPlayerRow(RoomPlayer player, RoomInfo room, bool isOwner)
    {
        // 列 1: 名称
        ImGui.TableNextColumn();
        var nameLabel = player.Name;
        if (player.Id == room.OwnerId)
        {
            nameLabel += " [房主]";
        }
        ImGui.Text(nameLabel);

        // 列 2: 职业
        ImGui.TableNextColumn();
        ImGui.Text(player.Job);

        // 列 3: 队伍（所有成员都可以设置）
        ImGui.TableNextColumn();
        DrawTeamCombo(player, room);

        // 列 4: 职能（所有成员都可以设置）
        ImGui.TableNextColumn();
        DrawRoleCombo(player, room);

        // 列 5: ACR
        ImGui.TableNextColumn();
        ImGui.Text(player.AcrName ?? "-");

        // 列 6: 时间轴
        ImGui.TableNextColumn();
        ImGui.Text(player.TriggerLineName ?? "-");

        // 房主额外列
        if (isOwner)
        {
            // 列 7: 操作
            ImGui.TableNextColumn();
            DrawKickButton(player, room);
        }
    }

    #region 下拉框组件

    private void DrawTeamCombo(RoomPlayer player, RoomInfo room)
    {
        ImGui.PushID($"team_{player.Id}");

        // 根据房间规模确定可用队伍数量
        int maxTeams = GetMaxTeams(room.Size);

        // 4人/8人房间只有一个队伍，禁用选择
        if (maxTeams <= 1)
        {
            ImGui.TextDisabled("A");
        }
        else
        {
            var availableLabels = GetAvailableTeamLabels(maxTeams);
            var availableValues = GetAvailableTeamValues(maxTeams);
            var teamIndex = GetTeamIndexInArray(player.TeamId, availableValues);

            ImGui.SetNextItemWidth(70);
            if (ImGui.Combo("##team", ref teamIndex, availableLabels, availableLabels.Length))
            {
                var newTeam = availableValues[teamIndex];
                _ = AssignTeamAsync(room.Id, player.Id, newTeam);
            }
        }

        ImGui.PopID();
    }

    private void DrawRoleCombo(RoomPlayer player, RoomInfo room)
    {
        ImGui.PushID($"role_{player.Id}");

        // 获取该队伍内已被占用的职能
        var occupiedRoles = RoomClientState.Instance.GetOccupiedRolesInTeam(player.TeamId, player.Id);

        // 生成带占用标记的标签
        var displayLabels = new string[RoleLabels.Length];
        for (int i = 0; i < RoleLabels.Length; i++)
        {
            if (i > 0 && occupiedRoles.Contains(RoleValues[i]))
            {
                displayLabels[i] = $"{RoleLabels[i]} [已占用]";
            }
            else
            {
                displayLabels[i] = RoleLabels[i];
            }
        }

        var roleIndex = GetRoleIndex(player.JobRole);

        ImGui.SetNextItemWidth(70);
        if (ImGui.Combo("##role", ref roleIndex, displayLabels, displayLabels.Length))
        {
            var newRole = RoleValues[roleIndex];

            // 客户端也进行检查，提前阻止无效操作
            if (!string.IsNullOrEmpty(newRole) && occupiedRoles.Contains(newRole))
            {
                RoomClientState.Instance.StatusMessage = $"职能 {newRole} 已被占用";
            }
            else
            {
                _ = AssignRoleSafeAsync(room.Id, player.Id, newRole);
            }
        }

        ImGui.PopID();
    }

    private void DrawKickButton(RoomPlayer player, RoomInfo room)
    {
        // 不能踢自己
        if (player.Id == RoomClientManager.Instance.Client.PlayerId)
        {
            ImGui.TextDisabled("-");
            return;
        }

        if (ImGui.SmallButton($"踢出##RCT_KickPlayer_{player.Id}"))
        {
            _ = KickPlayerAsync(room.Id, player.Id);
        }
    }

    #endregion

    #region 辅助方法

    private static int GetMaxTeams(int roomSize)
    {
        return roomSize switch
        {
            4 or 8 => 1,    // 只有 A 队
            24 => 3,        // A/B/C
            32 => 4,        // A/B/C/D
            48 => 6,        // A/B/C/D/E/F
            _ => 1
        };
    }

    /// <summary>
    /// 根据最大队伍数获取可用的队伍标签
    /// </summary>
    private static string[] GetAvailableTeamLabels(int maxTeams)
    {
        // maxTeams + 1 是因为要包含 "-" 选项
        var count = Math.Min(maxTeams + 1, TeamLabels.Length);
        var result = new string[count];
        Array.Copy(TeamLabels, result, count);
        return result;
    }

    /// <summary>
    /// 根据最大队伍数获取可用的队伍值
    /// </summary>
    private static string[] GetAvailableTeamValues(int maxTeams)
    {
        var count = Math.Min(maxTeams + 1, TeamValues.Length);
        var result = new string[count];
        Array.Copy(TeamValues, result, count);
        return result;
    }

    /// <summary>
    /// 在指定数组中查找队伍索引
    /// </summary>
    private static int GetTeamIndexInArray(string teamId, string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == teamId) return i;
        }
        return 0;
    }

    private static int GetTeamIndex(string teamId)
    {
        for (int i = 0; i < TeamValues.Length; i++)
        {
            if (TeamValues[i] == teamId) return i;
        }
        return 0;
    }

    private static int GetRoleIndex(string role)
    {
        for (int i = 0; i < RoleValues.Length; i++)
        {
            if (RoleValues[i] == role) return i;
        }
        return 0;
    }

    #endregion

    #region 操作方法

    private async Task AssignTeamAsync(string roomId, string playerId, string teamId)
    {
        var ack = await RoomClientManager.Instance.Client.AssignTeamAsync(roomId, playerId, teamId);
        if (ack?.Success == true)
        {
            // 刷新房间信息以更新显示
            await RoomClientManager.Instance.Client.GetRoomInfoAsync(roomId);
        }
        else
        {
            RoomClientState.Instance.StatusMessage = $"分配队伍失败: {ack?.Error ?? "未知错误"}";
        }
    }

    /// <summary>
    /// 安全地分配职能（捕获异常）
    /// </summary>
    private async Task AssignRoleSafeAsync(string roomId, string playerId, string role)
    {
        try
        {
            await AssignRoleAsync(roomId, playerId, role);
        }
        catch (OperationCanceledException)
        {
            // 操作被取消
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 分配职能异常: {ex}");
            RoomClientState.Instance.StatusMessage = $"分配职能异常: {ex.Message}";
        }
    }

    private async Task AssignRoleAsync(string roomId, string playerId, string role)
    {
        var ack = await RoomClientManager.Instance.Client.AssignRoleAsync(roomId, playerId, role);
        if (ack?.Success == true)
        {
            // 刷新房间信息以更新显示
            await RoomClientManager.Instance.Client.GetRoomInfoAsync(roomId);
        }
        else
        {
            RoomClientState.Instance.StatusMessage = $"分配职能失败: {ack?.Error ?? "未知错误"}";
        }
    }

    private async Task KickPlayerAsync(string roomId, string playerId)
    {
        var ack = await RoomClientManager.Instance.Client.KickPlayerAsync(roomId, playerId);
        if (ack?.Success == true)
        {
            RoomClientState.Instance.StatusMessage = "玩家已被踢出";
            await RoomClientManager.Instance.Client.GetRoomInfoAsync(roomId);
        }
        else
        {
            RoomClientState.Instance.StatusMessage = $"踢出失败: {ack?.Error ?? "未知错误"}";
        }
    }

    #endregion
}
