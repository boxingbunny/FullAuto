using System.Collections.Concurrent;
using AEAssist.Helper;

namespace AutoRaidHelper.RoomClient.Command.Handlers;

/// <summary>
/// SendMessage 指令处理器
/// 收到指令后调用 ChatHelper.SendMessage 发送聊天消息
/// </summary>
public class SendMessageHandler : IRoomCommandHandler
{
    /// <summary>
    /// 待发送的聊天消息队列（用于主线程执行）
    /// </summary>
    private readonly ConcurrentQueue<string> _pendingMessages = new();

    public CommandType CommandType => CommandType.SendMessage;

    /// <summary>
    /// 处理指令（将消息加入队列）
    /// </summary>
    public void Handle(RoomCommandMessage message)
    {
        if (string.IsNullOrEmpty(message.Command))
            return;

        _pendingMessages.Enqueue(message.Command);
    }

    /// <summary>
    /// 在主线程调用，处理待发送的消息
    /// </summary>
    public void Update()
    {
        while (_pendingMessages.TryDequeue(out var message))
        {
            try
            {
                ChatHelper.SendMessage(message);
            }
            catch
            {
                // 忽略发送错误
            }
        }
    }
}
