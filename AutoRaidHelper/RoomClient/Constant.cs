namespace AutoRaidHelper.RoomClient;

/// <summary>
/// 房间规模
/// </summary>
public enum RoomSize
{
    Size4 = 4,
    Size8 = 8,
    Size24 = 24,
    Size32 = 32,
    Size48 = 48
}

/// <summary>
/// 玩家职能
/// </summary>
public static class JobRole
{
    public const string Unassigned = "";
    public const string MT = "MT";
    public const string ST = "ST";
    public const string H1 = "H1";
    public const string H2 = "H2";
    public const string D1 = "D1";
    public const string D2 = "D2";
    public const string D3 = "D3";
    public const string D4 = "D4";
}

/// <summary>
/// 队伍标识
/// </summary>
public static class TeamID
{
    public const string Unassigned = "";
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
    public const string E = "E";
    public const string F = "F";
}

/// <summary>
/// 玩家身份
/// </summary>
public enum PlayerRole
{
    User = 0,
    Admin = 1
}

/// <summary>
/// WebSocket 消息类型
/// </summary>
public static class MessageType
{
    // 系统消息
    public const string Heartbeat = "heartbeat";
    public const string HeartbeatAck = "heartbeat_ack";
    public const string Ack = "ack";
    public const string Error = "error";

    // 认证消息
    public const string Auth = "auth";
    public const string AuthResult = "auth_result";

    // 房间消息
    public const string RoomList = "room_list";
    public const string RoomCreate = "room_create";
    public const string RoomJoin = "room_join";
    public const string RoomLeave = "room_leave";
    public const string RoomInfo = "room_info";
    public const string RoomUpdate = "room_update";
    public const string RoomPlayerList = "room_player_list";
    public const string RoomKick = "room_kick";
    public const string RoomDisband = "room_disband";
    public const string RoomAssignRole = "room_assign_role";
    public const string RoomAssignTeam = "room_assign_team";
    public const string RoomPlayerJoined = "room_player_joined";
    public const string RoomPlayerLeft = "room_player_left";
    public const string RoomPlayerUpdated = "room_player_updated";

    // 玩家消息
    public const string PlayerUpdate = "player_update";

    // 管理员消息
    public const string AdminGetUsers = "admin_get_users";
    public const string AdminKickUser = "admin_kick_user";

    // 邀请消息
    public const string RoomCreateInvite = "room_create_invite";
    public const string RoomJoinByInvite = "room_join_by_invite";
}

/// <summary>
/// 聊天邀请消息前缀
/// </summary>
public static class InviteMessagePrefix
{
    /// <summary>
    /// 邀请消息前缀
    /// </summary>
    public const string Prefix = "[AE房间]";
}

/// <summary>
/// 连接状态
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Authenticating,
    Authenticated,
    Error
}
