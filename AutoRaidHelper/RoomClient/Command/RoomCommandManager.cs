using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AEAssist.Helper;
using AEAssist.Verify;
using AutoRaidHelper.RoomClient.Command.Handlers;

namespace AutoRaidHelper.RoomClient.Command;

/// <summary>
/// 房间指令管理器
/// 负责发送指令和分发收到的指令给对应的处理器
/// </summary>
public class RoomCommandManager
{
    private static RoomCommandManager? _instance;
    public static RoomCommandManager Instance => _instance ??= new RoomCommandManager();

    private readonly Dictionary<CommandType, IRoomCommandHandler> _handlers = new();
    private readonly SendMessageHandler _sendMessageHandler;

    private RoomCommandManager()
    {
        // 注册内置处理器
        _sendMessageHandler = new SendMessageHandler();
        RegisterHandler(_sendMessageHandler);
    }

    /// <summary>
    /// 注册指令处理器
    /// </summary>
    public void RegisterHandler(IRoomCommandHandler handler)
    {
        _handlers[handler.CommandType] = handler;
    }

    /// <summary>
    /// 每帧更新（在主线程调用）
    /// </summary>
    public void Update()
    {
        // 处理需要主线程执行的处理器
        _sendMessageHandler.Update();
    }

    /// <summary>
    /// 处理收到的指令消息
    /// </summary>
    public void HandleCommand(RoomCommandMessage message)
    {
        var commandType = (CommandType)message.CommandType;
        if (_handlers.TryGetValue(commandType, out var handler))
        {
            try
            {
                handler.Handle(message);
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[RoomCommand] 处理指令失败: {ex.Message}");
            }
        }
    }

    #region 发送指令 API

    /// <summary>
    /// 发送房间指令
    /// </summary>
    /// <param name="targets">接收人（逗号分隔，如 "MT,ST" 或 "A+MT"，空表示所有人）</param>
    /// <param name="commandType">指令类型</param>
    /// <param name="command">具体指令内容</param>
    /// <param name="roomId">房间ID（空则使用当前所在房间）</param>
    public async Task<bool> SendCommandAsync(string targets, CommandType commandType, string command, string roomId = "")
    {
        var client = RoomClientManager.Instance.Client;
        if (client.State != ConnectionState.Authenticated)
        {
            LogHelper.Debug("[RoomCommand] 未连接服务器，无法发送指令");
            return false;
        }

        var ack = await client.SendRoomCommandAsync(roomId, targets, (int)commandType, command);
        return ack?.Success == true;
    }

    /// <summary>
    /// 发送 SendMessage 指令给指定目标
    /// </summary>
    /// <param name="targets">接收人（逗号分隔，如 "MT,ST" 或 "A+MT"，空表示所有人）</param>
    /// <param name="message">要发送的聊天消息</param>
    /// <param name="roomId">房间ID（空则使用当前所在房间）</param>
    public Task<bool> SendChatMessageAsync(string targets, string message, string roomId = "")
    {
        return SendCommandAsync(targets, CommandType.SendMessage, message, roomId);
    }

    /// <summary>
    /// 发送 SendMessage 指令给房间所有人
    /// </summary>
    /// <param name="message">要发送的聊天消息</param>
    /// <param name="roomId">房间ID（空则使用当前所在房间）</param>
    public Task<bool> SendChatMessageToAllAsync(string message, string roomId = "")
    {
        return SendChatMessageAsync("", message, roomId);
    }

    /// <summary>
    /// 发送 SendMessage 指令给指定队伍
    /// </summary>
    /// <param name="teamId">队伍ID（如 "A", "B"）</param>
    /// <param name="message">要发送的聊天消息</param>
    /// <param name="roomId">房间ID（空则使用当前所在房间）</param>
    public Task<bool> SendChatMessageToTeamAsync(string teamId, string message, string roomId = "")
    {
        return SendChatMessageAsync(teamId, message, roomId);
    }

    /// <summary>
    /// 发送 SendMessage 指令给指定职能
    /// </summary>
    /// <param name="jobRole">职能（如 "MT", "ST", "H1"）</param>
    /// <param name="message">要发送的聊天消息</param>
    /// <param name="roomId">房间ID（空则使用当前所在房间）</param>
    public Task<bool> SendChatMessageToRoleAsync(string jobRole, string message, string roomId = "")
    {
        return SendChatMessageAsync(jobRole, message, roomId);
    }

    #endregion

    /// <summary>
    /// 重置实例
    /// </summary>
    public static void Reset()
    {
        _instance = null;
    }
}
