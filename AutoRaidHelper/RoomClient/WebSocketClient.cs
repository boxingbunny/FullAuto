using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoRaidHelper.RoomClient;

/// <summary>
/// WebSocket 客户端
/// </summary>
public class WebSocketClient : IDisposable
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _heartbeatTask;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<WSAck>> _pendingAcks = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRawResponses = new();
    private readonly ConcurrentQueue<WSMessage> _messageQueue = new();

    // 使用 volatile 确保线程安全的状态读写
    private volatile ConnectionState _state = ConnectionState.Disconnected;

    /// <summary>
    /// 连接状态（线程安全）
    /// </summary>
    public ConnectionState State => _state;

    public string? PlayerId { get; private set; }
    public PlayerRole PlayerRole { get; private set; }
    public string? ErrorMessage { get; private set; }

    public event Action<ConnectionState>? OnStateChanged;
    public event Action<WSMessage>? OnMessage;
    public event Action<string>? OnError;

    private bool _disposed;
    private bool _isManualDisconnect; // 是否是主动断开

    /// <summary>
    /// 连接到服务器
    /// </summary>
    public async Task<bool> ConnectAsync(string url)
    {
        // 如果已经连接或认证成功，直接返回
        if (State == ConnectionState.Connected || State == ConnectionState.Authenticated)
        {
            return true;
        }

        // 如果正在连接或认证中，也跳过
        if (State == ConnectionState.Connecting || State == ConnectionState.Authenticating)
        {
            return false;
        }

        _isManualDisconnect = false;

        // 如果不是断开状态，先清理
        if (State != ConnectionState.Disconnected)
        {
            await CleanupAsync();
        }

        try
        {
            SetState(ConnectionState.Connecting);
            ErrorMessage = null;

            _webSocket = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            // 设置连接超时
            using var connectCts = new CancellationTokenSource(10000);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, connectCts.Token);

            await _webSocket.ConnectAsync(new Uri(url), linkedCts.Token);

            SetState(ConnectionState.Connected);

            // 启动接收任务
            _receiveTask = ReceiveLoopAsync();

            // 启动心跳任务
            _heartbeatTask = HeartbeatLoopAsync();

            return true;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "连接超时";
            SetState(ConnectionState.Error);
            return false;
        }
        catch (WebSocketException)
        {
            ErrorMessage = "无法连接到服务器";
            SetState(ConnectionState.Error);
            return false;
        }
        catch (Exception)
        {
            ErrorMessage = "连接失败";
            SetState(ConnectionState.Error);
            return false;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_webSocket == null || State == ConnectionState.Disconnected)
        {
            return;
        }

        _isManualDisconnect = true;

        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                using var closeCts = new CancellationTokenSource(3000);
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", closeCts.Token);
            }
        }
        catch
        {
            // 忽略断开时的错误
        }

        await CleanupAsync();
    }

    /// <summary>
    /// 是否是主动断开（用于判断是否需要自动重连）
    /// </summary>
    public bool IsManualDisconnect => _isManualDisconnect;

    /// <summary>
    /// 认证
    /// </summary>
    public async Task<bool> AuthenticateAsync(string aeCode, PlayerInfo playerInfo)
    {
        if (State != ConnectionState.Connected)
        {
            return false;
        }

        SetState(ConnectionState.Authenticating);

        var request = new AuthRequest
        {
            AECode = aeCode,
            PlayerInfo = playerInfo
        };

        var ack = await SendWithAckAsync(MessageType.Auth, request);

        if (ack == null || !ack.Success)
        {
            ErrorMessage = ack?.Error ?? "认证超时";
            SetState(ConnectionState.Error);
            return false;
        }

        SetState(ConnectionState.Authenticated);
        return true;
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    public async Task SendAsync(string type, object? payload = null)
    {
        var message = new WSMessage
        {
            MsgId = GenerateMsgId(),
            Type = type,
            Payload = payload
        };

        await SendMessageAsync(message);
    }

    /// <summary>
    /// 发送消息并等待 ACK
    /// </summary>
    public async Task<WSAck?> SendWithAckAsync(string type, object? payload = null, int timeoutMs = 10000)
    {
        var message = new WSMessage
        {
            MsgId = GenerateMsgId(),
            Type = type,
            Payload = payload
        };

        var tcs = new TaskCompletionSource<WSAck>();
        _pendingAcks[message.MsgId] = tcs;

        try
        {
            await SendMessageAsync(message);

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _pendingAcks.TryRemove(message.MsgId, out _);
        }
    }

    /// <summary>
    /// 处理队列中的消息（在主线程调用）
    /// </summary>
    public void ProcessMessages()
    {
        while (_messageQueue.TryDequeue(out var message))
        {
            try
            {
                OnMessage?.Invoke(message);
            }
            catch
            {
                // 忽略消息处理错误
            }
        }
    }

    #region 房间操作

    /// <summary>
    /// 获取房间列表
    /// </summary>
    public async Task<RoomListResponse?> GetRoomListAsync(int page = 1, int pageSize = 20)
    {
        await SendWithAckAsync(MessageType.RoomList, new RoomListRequest
        {
            Page = page,
            PageSize = pageSize
        });

        return null; // 结果通过 OnMessage 回调返回
    }

    /// <summary>
    /// 创建房间
    /// </summary>
    public async Task<WSAck?> CreateRoomAsync(string name, RoomSize size, string password = "")
    {
        return await SendWithAckAsync(MessageType.RoomCreate, new RoomCreateRequest
        {
            Name = name,
            Size = (int)size,
            Password = password
        });
    }

    /// <summary>
    /// 加入房间
    /// </summary>
    public async Task<WSAck?> JoinRoomAsync(string roomId, string password = "")
    {
        return await SendWithAckAsync(MessageType.RoomJoin, new RoomJoinRequest
        {
            RoomId = roomId,
            Password = password
        });
    }

    /// <summary>
    /// 离开房间
    /// </summary>
    public async Task<WSAck?> LeaveRoomAsync()
    {
        return await SendWithAckAsync(MessageType.RoomLeave, null);
    }

    /// <summary>
    /// 获取房间信息
    /// </summary>
    public async Task GetRoomInfoAsync(string roomId)
    {
        await SendAsync(MessageType.RoomInfo, new RoomInfoRequest { RoomId = roomId });
    }

    /// <summary>
    /// 踢出玩家
    /// </summary>
    public async Task<WSAck?> KickPlayerAsync(string roomId, string playerId)
    {
        return await SendWithAckAsync(MessageType.RoomKick, new RoomKickRequest
        {
            RoomId = roomId,
            PlayerId = playerId
        });
    }

    /// <summary>
    /// 解散房间
    /// </summary>
    public async Task<WSAck?> DisbandRoomAsync(string roomId)
    {
        return await SendWithAckAsync(MessageType.RoomDisband, new RoomDisbandRequest
        {
            RoomId = roomId
        });
    }

    /// <summary>
    /// 分配职能
    /// </summary>
    public async Task<WSAck?> AssignRoleAsync(string roomId, string playerId, string role)
    {
        return await SendWithAckAsync(MessageType.RoomAssignRole, new RoomAssignRoleRequest
        {
            RoomId = roomId,
            PlayerId = playerId,
            Role = role
        });
    }

    /// <summary>
    /// 分配队伍
    /// </summary>
    public async Task<WSAck?> AssignTeamAsync(string roomId, string playerId, string teamId)
    {
        return await SendWithAckAsync(MessageType.RoomAssignTeam, new RoomAssignTeamRequest
        {
            RoomId = roomId,
            PlayerId = playerId,
            TeamId = teamId
        });
    }

    /// <summary>
    /// 更新玩家信息（职业、ACR、时间轴）
    /// </summary>
    public async Task<WSAck?> UpdatePlayerInfoAsync(string job, string acrName, string triggerLineName)
    {
        return await SendWithAckAsync(MessageType.PlayerUpdate, new PlayerUpdateRequest
        {
            Job = job,
            AcrName = acrName,
            TriggerLineName = triggerLineName
        });
    }

    /// <summary>
    /// 创建邀请码
    /// </summary>
    public async Task<RoomCreateInviteResponse?> CreateInviteAsync(string roomId)
    {
        var message = new WSMessage
        {
            MsgId = GenerateMsgId(),
            Type = MessageType.RoomCreateInvite,
            Payload = new RoomCreateInviteRequest { RoomId = roomId }
        };

        var tcs = new TaskCompletionSource<string>();
        _pendingAcks[message.MsgId] = new TaskCompletionSource<WSAck>();

        try
        {
            // 注册原始响应处理
            _pendingRawResponses[message.MsgId] = tcs;
            await SendMessageAsync(message);

            using var cts = new CancellationTokenSource(10000);
            cts.Token.Register(() => tcs.TrySetCanceled());

            var rawJson = await tcs.Task;
            var response = JsonSerializer.Deserialize<WSAckWithData<RoomCreateInviteResponse>>(rawJson);
            if (response?.Success == true && response.Data != null)
            {
                return response.Data;
            }
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _pendingAcks.TryRemove(message.MsgId, out _);
            _pendingRawResponses.TryRemove(message.MsgId, out _);
        }
    }

    /// <summary>
    /// 通过邀请码加入房间
    /// </summary>
    public async Task<WSAck?> JoinRoomByInviteAsync(string inviteCode)
    {
        return await SendWithAckAsync(MessageType.RoomJoinByInvite, new RoomJoinByInviteRequest
        {
            InviteCode = inviteCode
        });
    }

    #endregion

    #region 管理员操作

    /// <summary>
    /// 获取所有在线用户（管理员）
    /// </summary>
    public async Task<WSAck?> GetAllUsersAsync()
    {
        return await SendWithAckAsync(MessageType.AdminGetUsers, null);
    }

    /// <summary>
    /// 踢出用户（管理员，断开 WebSocket）
    /// </summary>
    public async Task<WSAck?> KickUserAsync(string userId)
    {
        return await SendWithAckAsync(MessageType.AdminKickUser, new AdminKickUserRequest
        {
            UserId = userId
        });
    }

    #endregion

    #region 私有方法

    private async Task SendMessageAsync(WSMessage message)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("未连接到服务器");
        }

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[8192];

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !(_cts?.Token.IsCancellationRequested ?? true))
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts!.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    var closeStatus = result.CloseStatus;
                    var closeDescription = result.CloseStatusDescription ?? "";

                    // 检查是否是被踢出
                    // 4001: 账号在其他位置登录
                    // 4002: 被管理员踢出
                    if (closeStatus == (WebSocketCloseStatus)4001 || closeStatus == (WebSocketCloseStatus)4002)
                    {
                        _isManualDisconnect = true;
                        RoomClientState.Instance.StatusMessage = closeDescription;
                        OnError?.Invoke(closeDescription);
                    }

                    // 发送关闭确认
                    if (_webSocket.State == WebSocketState.CloseReceived)
                    {
                        try
                        {
                            await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        }
                        catch
                        {
                            // 忽略
                        }
                    }

                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleMessage(json);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (WebSocketException)
        {
            // 检查是否是被踢出
            if (_webSocket?.CloseStatus != null)
            {
                var closeStatus = _webSocket.CloseStatus.Value;
                var closeDescription = _webSocket.CloseStatusDescription ?? "";

                if ((int)closeStatus == 4001 || (int)closeStatus == 4002)
                {
                    _isManualDisconnect = true;
                    RoomClientState.Instance.StatusMessage = closeDescription;
                    OnError?.Invoke(closeDescription);
                }
            }

            ErrorMessage = "连接已断开";
        }
        catch
        {
            ErrorMessage = "连接异常";
        }
        finally
        {
            // 无论什么原因导致接收循环结束，都应该设置为断开状态以触发重连逻辑
            if (State != ConnectionState.Disconnected)
            {
                SetState(ConnectionState.Disconnected);
            }
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize<WSMessage>(json);
            if (message == null) return;

            // 处理 ACK
            if (message.Type == MessageType.Ack)
            {
                var payloadStr = message.Payload?.ToString() ?? "{}";
                var ack = JsonSerializer.Deserialize<WSAck>(payloadStr);
                if (ack != null)
                {
                    // 检查是否有等待原始响应的请求
                    if (_pendingRawResponses.TryRemove(ack.MsgId, out var rawTcs))
                    {
                        rawTcs.TrySetResult(payloadStr);
                    }

                    // 检查是否有等待 ACK 的请求
                    if (_pendingAcks.TryRemove(ack.MsgId, out var tcs))
                    {
                        tcs.TrySetResult(ack);
                    }
                }
                return;
            }

            // 处理心跳响应
            if (message.Type == MessageType.HeartbeatAck)
            {
                return;
            }

            // 处理认证结果
            if (message.Type == MessageType.AuthResult)
            {
                var result = JsonSerializer.Deserialize<AuthResult>(message.Payload?.ToString() ?? "{}");
                if (result != null)
                {
                    PlayerId = result.PlayerId;
                    PlayerRole = result.Role;
                    RoomClientState.Instance.IsAdmin = result.Role == PlayerRole.Admin;
                }
            }

            // 将消息加入队列，在主线程处理
            _messageQueue.Enqueue(message);
        }
        catch
        {
            // 忽略解析错误
        }
    }

    private async Task HeartbeatLoopAsync()
    {
        try
        {
            while (_webSocket?.State == WebSocketState.Open && !(_cts?.Token.IsCancellationRequested ?? true))
            {
                await Task.Delay(30000, _cts!.Token);

                if (_webSocket?.State == WebSocketState.Open)
                {
                    await SendAsync(MessageType.Heartbeat);
                }
            }
        }
        catch
        {
            // 忽略心跳错误
        }
    }

    /// <summary>
    /// 线程安全地设置连接状态
    /// </summary>
    private void SetState(ConnectionState newState)
    {
        var oldState = _state;
        if (oldState != newState)
        {
            _state = newState;
            OnStateChanged?.Invoke(newState);
        }
    }

    private async Task CleanupAsync()
    {
        _cts?.Cancel();

        // 等待接收任务完成
        if (_receiveTask != null)
        {
            try
            {
                await Task.WhenAny(_receiveTask, Task.Delay(2000));
            }
            catch
            {
                // 忽略
            }
        }

        // 等待心跳任务完成
        if (_heartbeatTask != null)
        {
            try
            {
                await Task.WhenAny(_heartbeatTask, Task.Delay(1000));
            }
            catch
            {
                // 忽略
            }
        }

        // 清理资源
        _webSocket?.Dispose();
        _webSocket = null;
        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;
        _heartbeatTask = null;

        foreach (var tcs in _pendingAcks.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingAcks.Clear();

        PlayerId = null;
        SetState(ConnectionState.Disconnected);
    }

    private static string GenerateMsgId()
    {
        return $"M-{Guid.NewGuid():N}"[..18];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _cts?.Cancel();
            _webSocket?.Abort();
            _webSocket?.Dispose();
            _webSocket = null;
            _cts?.Dispose();
            _cts = null;

            foreach (var tcs in _pendingAcks.Values)
            {
                tcs.TrySetCanceled();
            }
            _pendingAcks.Clear();
        }
        catch
        {
            // 忽略销毁时的错误
        }

        SetState(ConnectionState.Disconnected);
    }

    #endregion
}
