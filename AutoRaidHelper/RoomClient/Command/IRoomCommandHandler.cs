namespace AutoRaidHelper.RoomClient.Command;

/// <summary>
/// 房间指令处理器接口
/// </summary>
public interface IRoomCommandHandler
{
    /// <summary>
    /// 处理的指令类型
    /// </summary>
    CommandType CommandType { get; }

    /// <summary>
    /// 处理指令
    /// </summary>
    /// <param name="message">指令消息</param>
    void Handle(RoomCommandMessage message);
}
