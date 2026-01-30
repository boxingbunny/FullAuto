using System;
using System.Text.RegularExpressions;
using AEAssist;
using AEAssist.Helper;
using AEAssist.MemoryApi;
using AutoRaidHelper.Settings;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.Automation;
using ECommons.DalamudServices;

namespace AutoRaidHelper.RoomClient;

/// <summary>
/// 聊天邀请处理器 - 监控聊天消息并处理房间邀请
/// </summary>
public class ChatInviteHandler : IDisposable
{
    private static ChatInviteHandler? _instance;
    public static ChatInviteHandler Instance => _instance ??= new ChatInviteHandler();

    // 邀请消息正则表达式: [AE房间] {邀请码}
    private static readonly Regex InviteRegex = new(@"\[AE房间\]\s*(\S+)", RegexOptions.Compiled);

    // 自定义链接 Payload（用于可点击的邀请消息）
    private static DalamudLinkPayload? InviteLinkPayload;

    private bool _initialized;

    private ChatInviteHandler()
    {
    }

    /// <summary>
    /// 初始化聊天监控
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        // 初始化链接处理器
        InviteLinkPayload = Svc.Chat.AddChatLinkHandler(1001, OnChatLinkClick);

        Svc.Chat.ChatMessage += OnChatMessage;
        _initialized = true;

        LogHelper.Debug("[ChatInvite] 聊天邀请监控已启动");
    }

    /// <summary>
    /// 发送邀请消息到聊天频道
    /// </summary>
    /// <param name="inviteCode">邀请码</param>
    /// <param name="roomSize">房间规模（用于决定发送频道）</param>
    public void SendInviteMessage(string inviteCode, int roomSize)
    {
        // 构建邀请消息
        var message = $"{InviteMessagePrefix.Prefix} {inviteCode}";

        // 4/8人房间发小队频道，其他规模发团队频道
        if (roomSize <= 8)
        {
            Chat.SendMessage($"/p {message}");
            LogHelper.Info($"[ChatInvite] 已发送小队邀请: {inviteCode}");
        }
        else
        {
            Chat.SendMessage($"/a {message}");
            LogHelper.Info($"[ChatInvite] 已发送团队邀请: {inviteCode}");
        }
    }

    /// <summary>
    /// 处理聊天消息
    /// </summary>
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // 只处理小队和团队频道
        if (type != XivChatType.Party && type != XivChatType.Alliance)
            return;

        // 如果未连接服务器，不处理
        if (RoomClientManager.Instance.Client.State != ConnectionState.Authenticated)
            return;

        // 获取纯文本消息
        var msgText = message.TextValue;
        if (string.IsNullOrEmpty(msgText))
            return;

        // 匹配邀请消息
        var match = InviteRegex.Match(msgText);
        if (!match.Success)
            return;

        var inviteCode = match.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(inviteCode))
            return;

        LogHelper.Debug($"[ChatInvite] 检测到邀请消息: {inviteCode}");

        // 检查是否已在房间中
        if (RoomClientState.Instance.IsInRoom)
        {
            // 如果已在房间中，将消息改为可点击提示
            if (InviteLinkPayload != null)
            {
                message = new SeString(
                    InviteLinkPayload,
                    new UIForegroundPayload(710),
                    new TextPayload($"[点击加入房间] {inviteCode}"),
                    UIForegroundPayload.UIForegroundOff,
                    RawPayload.LinkTerminator
                );
            }
            return;
        }

        // 自动加入房间
        _ = JoinRoomByInviteAsync(inviteCode);

        // 修改消息显示
        message = new SeString(
            new UIForegroundPayload(43),
            new TextPayload($"[AE房间邀请] 正在加入..."),
            UIForegroundPayload.UIForegroundOff
        );
    }

    /// <summary>
    /// 处理聊天链接点击
    /// </summary>
    private static void OnChatLinkClick(uint commandId, SeString message)
    {
        var text = message.TextValue;
        if (!text.StartsWith("[点击加入房间]"))
            return;

        // 提取邀请码
        var parts = text.Split(' ', 2);
        if (parts.Length < 2)
            return;

        var inviteCode = parts[1].Trim();
        if (string.IsNullOrEmpty(inviteCode))
            return;

        // 如果还在房间中，提示先离开
        if (RoomClientState.Instance.IsInRoom)
        {
            Core.Resolve<MemApiNotification>().ShowError("请先离开当前房间");
            return;
        }

        _ = JoinRoomByInviteAsync(inviteCode);
    }

    /// <summary>
    /// 通过邀请码加入房间
    /// </summary>
    private static async System.Threading.Tasks.Task JoinRoomByInviteAsync(string inviteCode)
    {
        try
        {
            var ack = await RoomClientManager.Instance.Client.JoinRoomByInviteAsync(inviteCode);
            if (ack?.Success == true)
            {
                RoomClientState.Instance.StatusMessage = "通过邀请加入房间成功";
                Core.Resolve<MemApiNotification>().ShowSuccess("已加入房间");

                // 发送聊天消息确认
                Chat.SendMessage("/e [AE] 已通过邀请加入房间");

                // 获取房间信息
                await RoomClientManager.Instance.Client.GetRoomListAsync();
            }
            else
            {
                var errorMsg = ack?.Error ?? "加入失败";
                RoomClientState.Instance.StatusMessage = errorMsg;
                Core.Resolve<MemApiNotification>().ShowError(errorMsg);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[ChatInvite] 通过邀请加入房间失败: {ex.Message}");
            RoomClientState.Instance.StatusMessage = "加入房间失败";
        }
    }

    public void Dispose()
    {
        if (_initialized)
        {
            Svc.Chat.ChatMessage -= OnChatMessage;

            // 移除链接处理器
            if (InviteLinkPayload != null)
            {
                Svc.Chat.RemoveChatLinkHandler(1001);
                InviteLinkPayload = null;
            }

            _initialized = false;
        }

        _instance = null;
    }
}
