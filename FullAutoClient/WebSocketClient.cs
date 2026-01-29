using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AEAssist.Helper;

namespace FullAuto;

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
        LogHelper.Info($"[FullAuto] ConnectAsync 开始, 当前状态: {State}");

        // 如果已经连接或认证成功，直接返回（避免后台自动重连任务干扰）
        if (State == ConnectionState.Connected || State == ConnectionState.Authenticated)
        {
            LogHelper.Info($"[FullAuto] 已经连接，跳过此次连接请求");
            return true;
        }

        // 如果正在连接或认证中，也跳过
        if (State == ConnectionState.Connecting || State == ConnectionState.Authenticating)
        {
            LogHelper.Info($"[FullAuto] 正在连接中，跳过此次连接请求");
            return false;
        }

        _isManualDisconnect = false; // 重置标志

        // 如果不是断开状态，先清理并等待
        if (State != ConnectionState.Disconnected)
        {
            LogHelper.Info($"[FullAuto] 状态不是 Disconnected，先执行清理");
            await CleanupAsync();
        }

        try
        {
            SetState(ConnectionState.Connecting);
            LogHelper.Info($"[FullAuto] 状态已设置为 Connecting");

            _webSocket = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            LogHelper.Info($"[FullAuto] 正在连接到 {url}");
            await _webSocket.ConnectAsync(new Uri(url), _cts.Token);
            LogHelper.Info($"[FullAuto] WebSocket 连接成功");

            SetState(ConnectionState.Connected);

            // 启动接收任务
            _receiveTask = ReceiveLoopAsync();

            // 启动心跳任务
            _heartbeatTask = HeartbeatLoopAsync();

            LogHelper.Info($"[FullAuto] 接收和心跳任务已启动");
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            SetState(ConnectionState.Error);
            LogHelper.Error($"[FullAuto] WebSocket连接失败: {ex}");
            return false;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        LogHelper.Info($"[FullAuto] DisconnectAsync 开始, 当前状态: {State}");

        if (_webSocket == null || State == ConnectionState.Disconnected)
        {
            LogHelper.Info($"[FullAuto] 已经断开，无需操作");
            return;
        }

        _isManualDisconnect = true; // 标记为主动断开

        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                LogHelper.Info($"[FullAuto] 发送关闭消息");
                using var closeCts = new CancellationTokenSource(3000);
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", closeCts.Token);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 断开连接时出错: {ex.Message}");
        }

        await CleanupAsync();
        LogHelper.Info($"[FullAuto] DisconnectAsync 完成");
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
            catch (Exception ex)
            {
                LogHelper.Error($"[FullAuto] 处理消息时出错: {ex.Message}");
            }
        }
    }

    #region 房间操作

    /// <summary>
    /// 获取房间列表
    /// </summary>
    public async Task<RoomListResponse?> GetRoomListAsync(int page = 1, int pageSize = 20)
    {
        var ack = await SendWithAckAsync(MessageType.RoomList, new RoomListRequest
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
        LogHelper.Info($"[FullAuto] WebSocket发送创建房间请求: name={name}, size={size}");
        var result = await SendWithAckAsync(MessageType.RoomCreate, new RoomCreateRequest
        {
            Name = name,
            Size = (int)size,
            Password = password
        });
        LogHelper.Info($"[FullAuto] 创建房间结果: success={result?.Success}, error={result?.Error}");
        return result;
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
            LogHelper.Error($"[FullAuto] WebSocket 未连接，无法发送消息: {message.Type}");
            throw new InvalidOperationException("WebSocket 未连接");
        }

        var json = JsonSerializer.Serialize(message);
        LogHelper.Info($"[FullAuto] 发送WebSocket消息: {message.Type}, msgId={message.MsgId}");
        var bytes = Encoding.UTF8.GetBytes(json);

        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[8192];

        LogHelper.Info($"[FullAuto] ReceiveLoopAsync 开始");

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !(_cts?.Token.IsCancellationRequested ?? true))
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts!.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // 处理服务器发来的关闭帧
                    var closeStatus = result.CloseStatus;
                    var closeDescription = result.CloseStatusDescription ?? "";

                    LogHelper.Info($"[FullAuto] 收到关闭消息: code={closeStatus}, desc={closeDescription}");

                    // 检查是否是被踢出
                    // 4001: 账号在其他位置登录
                    // 4002: 被管理员踢出
                    if (closeStatus == (WebSocketCloseStatus)4001 || closeStatus == (WebSocketCloseStatus)4002)
                    {
                        _isManualDisconnect = true; // 标记为"已知断开"，避免自动重连
                        FullAutoState.Instance.StatusMessage = closeDescription;
                        LogHelper.Info($"[FullAuto] 连接被关闭: {closeDescription}");
                        OnError?.Invoke(closeDescription);
                    }

                    // 发送关闭确认（完成握手）
                    if (_webSocket.State == WebSocketState.CloseReceived)
                    {
                        try
                        {
                            await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        }
                        catch
                        {
                            // 忽略关闭时的错误
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
            LogHelper.Info($"[FullAuto] 接收循环被取消");
        }
        catch (WebSocketException ex)
        {
            // 检查是否是正常关闭（服务器发送了关闭帧后我们收到了异常）
            if (_webSocket?.CloseStatus != null)
            {
                var closeStatus = _webSocket.CloseStatus.Value;
                var closeDescription = _webSocket.CloseStatusDescription ?? "";

                LogHelper.Info($"[FullAuto] WebSocket已关闭: code={closeStatus}, desc={closeDescription}");

                // 检查是否是被踢出（4001: 其他位置登录，4002: 管理员踢出）
                if ((int)closeStatus == 4001 || (int)closeStatus == 4002)
                {
                    _isManualDisconnect = true;
                    FullAutoState.Instance.StatusMessage = closeDescription;
                    OnError?.Invoke(closeDescription);
                    return;
                }
            }

            LogHelper.Error($"[FullAuto] WebSocket异常: {ex.Message}");
            ErrorMessage = ex.Message;
            // 只有在非手动断开时才设置错误状态
            if (State != ConnectionState.Disconnected)
            {
                SetState(ConnectionState.Error);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 接收消息时出错: {ex.Message}");
            ErrorMessage = ex.Message;
            if (State != ConnectionState.Disconnected)
            {
                SetState(ConnectionState.Error);
            }
        }
        finally
        {
            LogHelper.Info($"[FullAuto] ReceiveLoopAsync 结束, 当前状态: {State}");
            // 不在这里调用 Cleanup，让调用方处理
            // 但如果是异常断开，需要通知
            if (State == ConnectionState.Connected || State == ConnectionState.Authenticated || State == ConnectionState.Authenticating)
            {
                LogHelper.Info($"[FullAuto] 连接意外断开");
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
                var ack = JsonSerializer.Deserialize<WSAck>(message.Payload?.ToString() ?? "{}");
                if (ack != null && _pendingAcks.TryRemove(ack.MsgId, out var tcs))
                {
                    tcs.TrySetResult(ack);
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
                    // 设置管理员标志
                    FullAutoState.Instance.IsAdmin = result.Role == PlayerRole.Admin;
                }
            }

            // 将消息加入队列，在主线程处理
            _messageQueue.Enqueue(message);
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 解析消息失败: {ex.Message}");
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
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 心跳发送失败: {ex.Message}");
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

    private void Cleanup()
    {
        _cts?.Cancel();
        _webSocket?.Dispose();
        _webSocket = null;
        _cts?.Dispose();
        _cts = null;

        foreach (var tcs in _pendingAcks.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingAcks.Clear();

        PlayerId = null;
    }

    private async Task CleanupAsync()
    {
        LogHelper.Info($"[FullAuto] CleanupAsync 开始");

        // 先取消所有操作
        _cts?.Cancel();

        // 等待接收任务完成
        if (_receiveTask != null)
        {
            try
            {
                await Task.WhenAny(_receiveTask, Task.Delay(2000));
                LogHelper.Info($"[FullAuto] 接收任务已停止");
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
                LogHelper.Info($"[FullAuto] 心跳任务已停止");
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

        LogHelper.Info($"[FullAuto] CleanupAsync 完成, isManualDisconnect={_isManualDisconnect}");
    }

    private static string GenerateMsgId()
    {
        return $"M-{Guid.NewGuid():N}"[..18];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        LogHelper.Info("[FullAuto] WebSocketClient 正在销毁...");

        // 同步清理，不等待异步操作完成
        try
        {
            _cts?.Cancel();
            _webSocket?.Abort(); // 强制关闭连接
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
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] WebSocketClient 销毁时出错: {ex.Message}");
        }

        SetState(ConnectionState.Disconnected);
        LogHelper.Info("[FullAuto] WebSocketClient 已销毁");
    }

    #endregion
}
