using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AutoRaidHelper.RoomClient;

#region 基础消息

/// <summary>
/// WebSocket 消息
/// </summary>
public class WSMessage
{
    [JsonPropertyName("msgId")]
    public string MsgId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
}

/// <summary>
/// ACK 消息
/// </summary>
public class WSAck
{
    [JsonPropertyName("msgId")]
    public string MsgId { get; set; } = "";

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";
}

/// <summary>
/// 错误消息
/// </summary>
public class WSError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

#endregion

#region 玩家模型

/// <summary>
/// 玩家信息
/// </summary>
public class PlayerInfo
{
    [JsonPropertyName("cid")]
    public string CID { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("worldId")]
    public int WorldId { get; set; }

    [JsonPropertyName("job")]
    public string Job { get; set; } = "";

    [JsonPropertyName("acrName")]
    public string AcrName { get; set; } = "";

    [JsonPropertyName("triggerLineName")]
    public string TriggerLineName { get; set; } = "";
}

/// <summary>
/// 玩家
/// </summary>
public class Player
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("cid")]
    public string CID { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("worldId")]
    public int WorldId { get; set; }

    [JsonPropertyName("job")]
    public string Job { get; set; } = "";

    [JsonPropertyName("acrName")]
    public string AcrName { get; set; } = "";

    [JsonPropertyName("triggerLineName")]
    public string TriggerLineName { get; set; } = "";

    [JsonPropertyName("role")]
    public PlayerRole Role { get; set; }

    [JsonPropertyName("joinTime")]
    public DateTime JoinTime { get; set; }
}

/// <summary>
/// 房间内玩家
/// </summary>
public class RoomPlayer : Player
{
    [JsonPropertyName("teamId")]
    public string TeamId { get; set; } = "";

    [JsonPropertyName("jobRole")]
    public string JobRole { get; set; } = "";
}

#endregion

#region 房间模型

/// <summary>
/// 房间信息
/// </summary>
public class RoomInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; } = "";

    [JsonPropertyName("ownerName")]
    public string OwnerName { get; set; } = "";

    [JsonPropertyName("hasPassword")]
    public bool HasPassword { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("playerCount")]
    public int PlayerCount { get; set; }

    [JsonPropertyName("createTime")]
    public DateTime CreateTime { get; set; }
}

#endregion

#region 认证消息

/// <summary>
/// 认证请求
/// </summary>
public class AuthRequest
{
    [JsonPropertyName("aeCode")]
    public string AECode { get; set; } = "";

    [JsonPropertyName("playerInfo")]
    public PlayerInfo PlayerInfo { get; set; } = new();
}

/// <summary>
/// 认证结果
/// </summary>
public class AuthResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("role")]
    public PlayerRole Role { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

#endregion

#region 房间消息

/// <summary>
/// 房间列表请求
/// </summary>
public class RoomListRequest
{
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 房间列表响应
/// </summary>
public class RoomListResponse
{
    [JsonPropertyName("rooms")]
    public List<RoomInfo> Rooms { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}

/// <summary>
/// 创建房间请求
/// </summary>
public class RoomCreateRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("size")]
    public int Size { get; set; }
}

/// <summary>
/// 创建房间响应
/// </summary>
public class RoomCreateResponse
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";
}

/// <summary>
/// 加入房间请求
/// </summary>
public class RoomJoinRequest
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}

/// <summary>
/// 房间信息请求
/// </summary>
public class RoomInfoRequest
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";
}

/// <summary>
/// 房间信息响应
/// </summary>
public class RoomInfoResponse
{
    [JsonPropertyName("room")]
    public RoomInfo? Room { get; set; }

    [JsonPropertyName("players")]
    public List<RoomPlayer> Players { get; set; } = new();
}

/// <summary>
/// 更新房间请求
/// </summary>
public class RoomUpdateRequest
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}

/// <summary>
/// 踢人请求
/// </summary>
public class RoomKickRequest
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = "";
}

/// <summary>
/// 解散房间请求
/// </summary>
public class RoomDisbandRequest
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";
}

/// <summary>
/// 分配职能请求
/// </summary>
public class RoomAssignRoleRequest
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
}

/// <summary>
/// 分配队伍请求
/// </summary>
public class RoomAssignTeamRequest
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("teamId")]
    public string TeamId { get; set; } = "";
}

/// <summary>
/// 玩家事件
/// </summary>
public class RoomPlayerEvent
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";

    [JsonPropertyName("player")]
    public RoomPlayer? Player { get; set; }
}

#endregion

#region 玩家消息

/// <summary>
/// 玩家信息更新请求
/// </summary>
public class PlayerUpdateRequest
{
    [JsonPropertyName("job")]
    public string Job { get; set; } = "";

    [JsonPropertyName("acrName")]
    public string AcrName { get; set; } = "";

    [JsonPropertyName("triggerLineName")]
    public string TriggerLineName { get; set; } = "";
}

#endregion

#region 管理员消息

/// <summary>
/// 管理员用户信息
/// </summary>
public class AdminUserInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("worldId")]
    public int WorldId { get; set; }

    [JsonPropertyName("job")]
    public string? Job { get; set; }

    [JsonPropertyName("acrName")]
    public string? AcrName { get; set; }

    [JsonPropertyName("triggerLineName")]
    public string? TriggerLineName { get; set; }

    [JsonPropertyName("roomId")]
    public string? RoomId { get; set; }

    [JsonPropertyName("roomName")]
    public string? RoomName { get; set; }

    [JsonPropertyName("connectTime")]
    public long ConnectTimeMs { get; set; }

    /// <summary>
    /// 连接时间（从 Unix 时间戳转换）
    /// </summary>
    [JsonIgnore]
    public DateTime ConnectTime => DateTimeOffset.FromUnixTimeMilliseconds(ConnectTimeMs).LocalDateTime;
}

/// <summary>
/// 管理员获取用户列表响应
/// </summary>
public class AdminGetUsersResponse
{
    [JsonPropertyName("users")]
    public List<AdminUserInfo> Users { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// 管理员踢出用户请求
/// </summary>
public class AdminKickUserRequest
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";
}

#endregion
