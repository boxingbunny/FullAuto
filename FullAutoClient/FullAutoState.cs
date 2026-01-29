using System.Collections.Generic;

namespace FullAuto;

/// <summary>
/// FullAuto 状态管理
/// </summary>
public class FullAutoState
{
    private static FullAutoState? _instance;
    public static FullAutoState Instance => _instance ??= new FullAutoState();

    /// <summary>
    /// 当前房间ID
    /// </summary>
    public string? CurrentRoomId { get; set; }

    /// <summary>
    /// 当前房间信息
    /// </summary>
    public RoomInfo? CurrentRoom { get; set; }

    /// <summary>
    /// 房间内玩家列表
    /// </summary>
    public List<RoomPlayer> RoomPlayers { get; set; } = new();

    /// <summary>
    /// 是否是房主
    /// </summary>
    public bool IsRoomOwner { get; set; }

    /// <summary>
    /// 房间列表
    /// </summary>
    public List<RoomInfo> RoomList { get; set; } = new();

    /// <summary>
    /// 房间列表总数
    /// </summary>
    public int RoomListTotal { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// 状态消息
    /// </summary>
    public string StatusMessage { get; set; } = "";

    /// <summary>
    /// 是否在房间中
    /// </summary>
    public bool IsInRoom => !string.IsNullOrEmpty(CurrentRoomId);

    /// <summary>
    /// 所有连接用户列表（管理员功能）
    /// </summary>
    public List<AdminUserInfo> AllConnectedUsers { get; set; } = new();

    /// <summary>
    /// 是否是管理员
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// 获取指定队伍内已被占用的职能（排除指定玩家）
    /// </summary>
    public HashSet<string> GetOccupiedRolesInTeam(string teamId, string excludePlayerId)
    {
        var occupiedRoles = new HashSet<string>();
        foreach (var player in RoomPlayers)
        {
            // 排除指定玩家
            if (player.Id == excludePlayerId)
                continue;
            // 检查同一队伍内的玩家
            if (player.TeamId == teamId && !string.IsNullOrEmpty(player.JobRole))
            {
                occupiedRoles.Add(player.JobRole);
            }
        }
        return occupiedRoles;
    }

    /// <summary>
    /// 检查职能是否在指定队伍内被占用
    /// </summary>
    public bool IsRoleOccupiedInTeam(string teamId, string role, string excludePlayerId)
    {
        if (string.IsNullOrEmpty(role)) return false;
        return GetOccupiedRolesInTeam(teamId, excludePlayerId).Contains(role);
    }

    /// <summary>
    /// 清空房间状态
    /// </summary>
    public void ClearRoomState()
    {
        CurrentRoomId = null;
        CurrentRoom = null;
        RoomPlayers.Clear();
        IsRoomOwner = false;
    }

    /// <summary>
    /// 重置所有状态
    /// </summary>
    public void Reset()
    {
        ClearRoomState();
        RoomList.Clear();
        RoomListTotal = 0;
        CurrentPage = 1;
        StatusMessage = "";
        AllConnectedUsers.Clear();
        IsAdmin = false;
    }
}
