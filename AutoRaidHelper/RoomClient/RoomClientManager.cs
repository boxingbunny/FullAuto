using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AEAssist;
using AEAssist.CombatRoutine;
using AEAssist.CombatRoutine.Module;
using AEAssist.Extension;
using AEAssist.Helper;
using AEAssist.JobApi;
using AEAssist.Verify;
using AutoRaidHelper.Settings;

namespace AutoRaidHelper.RoomClient;

/// <summary>
/// 房间客户端管理器 - 管理 WebSocket 连接和消息处理
/// </summary>
public class RoomClientManager : IDisposable
{
    private static RoomClientManager? _instance;
    public static RoomClientManager Instance => _instance ??= new RoomClientManager();

    public WebSocketClient Client { get; } = new();

    private bool _initialized;
    private CancellationTokenSource? _pluginCts;

    // 玩家状态追踪（用于检测变化）
    private string _lastJob = "";
    private string _lastAcrName = "";
    private string _lastTriggerLineName = "";
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private const int UpdateCheckIntervalMs = 1000; // 每秒检查一次

    // 自动连接状态追踪
    private DateTime _lastAutoConnectAttempt = DateTime.MinValue;
    private bool _isAutoConnecting = false;  // 标记是否正在进行自动连接尝试

    private RoomClientManager()
    {
    }

    /// <summary>
    /// 初始化客户端管理器
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        try
        {
            _pluginCts = new CancellationTokenSource();

            // 订阅消息事件
            Client.OnMessage += OnWebSocketMessage;
            Client.OnStateChanged += OnConnectionStateChanged;
            Client.OnError += OnWebSocketError;

            _initialized = true;
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 客户端管理器初始化失败: {ex}");
        }
    }

    /// <summary>
    /// 每帧更新（在主线程调用）
    /// </summary>
    public void Update()
    {
        if (!_initialized) return;

        // 处理消息队列
        Client.ProcessMessages();

        // 检测玩家状态变化并上报
        CheckAndReportPlayerInfoChanges();

        // 检查自动连接
        CheckAutoConnect();
    }

    /// <summary>
    /// 检测玩家信息变化并上报服务端
    /// </summary>
    private void CheckAndReportPlayerInfoChanges()
    {
        // 只在已认证状态下检测
        if (Client.State != ConnectionState.Authenticated)
            return;

        // 限制检查频率
        var now = DateTime.Now;
        if ((now - _lastUpdateCheck).TotalMilliseconds < UpdateCheckIntervalMs)
            return;
        _lastUpdateCheck = now;

        try
        {
            var currentJob = GetCurrentJobName();
            var currentAcrName = GetCurrentAcrName();
            var currentTriggerLineName = GetCurrentTriggerLineName();

            // 检测是否有变化
            bool hasChange = currentJob != _lastJob ||
                             currentAcrName != _lastAcrName ||
                             currentTriggerLineName != _lastTriggerLineName;

            if (hasChange)
            {
                // 更新本地缓存
                _lastJob = currentJob;
                _lastAcrName = currentAcrName;
                _lastTriggerLineName = currentTriggerLineName;

                // 上报服务端
                _ = ReportPlayerInfoChangeAsync(currentJob, currentAcrName, currentTriggerLineName);
            }
        }
        catch
        {
            // 忽略检测错误
        }
    }

    /// <summary>
    /// 上报玩家信息变化到服务端
    /// </summary>
    private async Task ReportPlayerInfoChangeAsync(string job, string acrName, string triggerLineName)
    {
        try
        {
            var ack = await Client.UpdatePlayerInfoAsync(job, acrName, triggerLineName);
            if (ack?.Success == true)
            {
                // 如果在房间中，刷新房间信息以更新自己的显示
                if (RoomClientState.Instance.IsInRoom)
                {
                    await Client.GetRoomInfoAsync(RoomClientState.Instance.CurrentRoomId!);
                }
            }
        }
        catch
        {
            // 忽略上报错误
        }
    }

    #region 连接管理

    /// <summary>
    /// 检查是否需要自动连接
    /// 条件：启用自动连接、AE已认证（有AECode）、当前未连接
    /// </summary>
    private void CheckAutoConnect()
    {
        // 如果正在进行自动连接，跳过
        if (_isAutoConnecting)
            return;

        var setting = FullAutoSettings.Instance.RoomClientSetting;

        // 如果未启用自动连接，跳过
        if (!setting.AutoConnect)
            return;

        // 如果已经连接或正在连接/认证中，跳过
        var state = Client.State;
        if (state == ConnectionState.Connected ||
            state == ConnectionState.Authenticated ||
            state == ConnectionState.Connecting ||
            state == ConnectionState.Authenticating)
            return;

        // 检查 AE 是否已认证（有 AECode）
        var aeCode = GetAECode();
        if (string.IsNullOrEmpty(aeCode))
            return;

        // 检查重连间隔
        var now = DateTime.Now;
        var intervalSeconds = setting.ReconnectInterval;
        if ((now - _lastAutoConnectAttempt).TotalSeconds < intervalSeconds)
            return;

        // 更新上次尝试时间并开始连接
        _lastAutoConnectAttempt = now;
        _isAutoConnecting = true;

        // 显示状态消息
        RoomClientState.Instance.StatusMessage = "自动连接中...";

        // 异步连接
        _ = AutoConnectAsync();
    }

    /// <summary>
    /// 执行自动连接
    /// </summary>
    private async Task AutoConnectAsync()
    {
        try
        {
            await ConnectAsync();
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 自动连接失败: {ex.Message}");
            RoomClientState.Instance.StatusMessage = $"自动连接失败: {ex.Message}";
        }
        finally
        {
            _isAutoConnecting = false;
        }
    }

    public async Task ConnectAsync()
    {
        var setting = FullAutoSettings.Instance.RoomClientSetting;

        RoomClientState.Instance.StatusMessage = "正在连接...";

        if (await Client.ConnectAsync(setting.ServerUrl))
        {
            RoomClientState.Instance.StatusMessage = "正在认证...";

            // 收集玩家信息并认证
            var playerInfo = CollectPlayerInfo();
            var aeCode = GetAECode();

            if (string.IsNullOrEmpty(aeCode))
            {
                RoomClientState.Instance.StatusMessage = "无法获取AE激活码";
                await Client.DisconnectAsync();
                return;
            }

            if (await Client.AuthenticateAsync(aeCode, playerInfo))
            {
                RoomClientState.Instance.StatusMessage = "连接成功";

                // 初始化玩家状态缓存（避免首次检测时误报变化）
                _lastJob = playerInfo.Job;
                _lastAcrName = playerInfo.AcrName;
                _lastTriggerLineName = playerInfo.TriggerLineName;

                // 获取房间列表
                await Client.GetRoomListAsync();
            }
            else
            {
                RoomClientState.Instance.StatusMessage = Client.ErrorMessage ?? "认证失败";
            }
        }
        else
        {
            RoomClientState.Instance.StatusMessage = Client.ErrorMessage ?? "连接失败";
        }
    }

    public async Task DisconnectAsync()
    {
        await Client.DisconnectAsync();
        RoomClientState.Instance.Reset();
        RoomClientState.Instance.StatusMessage = "已断开连接";
    }

    private PlayerInfo CollectPlayerInfo()
    {
        var me = Core.Me;

        return new PlayerInfo
        {
            CID = CidHelper.GetCid().ToString(),
            Name = me?.Name.ToString() ?? "Unknown",
            WorldId = (int)(me?.HomeWorld.RowId ?? 0),
            Job = GetCurrentJobName(),
            AcrName = GetCurrentAcrName(),
            TriggerLineName = GetCurrentTriggerLineName()
        };
    }

    private string GetCurrentJobName()
    {
        try
        {
            var me = Core.Me;
            if (me == null) return "";
            return JobHelper.GetTranslation(me.CurrentJob());
        }
        catch
        {
            return "";
        }
    }

    private string GetAECode()
    {
        try
        {
            return Share.VIP?.Key ?? "";
        }
        catch
        {
            return "";
        }
    }

    private string GetCurrentAcrName()
    {
        try
        {
            return Data.currRotation?.RotationEntry?.AuthorName ?? "";
        }
        catch
        {
            return "";
        }
    }

    private string GetCurrentTriggerLineName()
    {
        try
        {
            return AI.Instance.TriggerlineData.CurrTriggerLine?.Name ?? "";
        }
        catch
        {
            return "";
        }
    }

    #endregion

    #region 消息处理

    private void OnWebSocketMessage(WSMessage message)
    {
        try
        {
            switch (message.Type)
            {
                case MessageType.RoomList:
                    HandleRoomList(message.Payload);
                    break;

                case MessageType.RoomCreate:
                    HandleRoomCreate(message.Payload);
                    break;

                case MessageType.RoomInfo:
                    HandleRoomInfo(message.Payload);
                    break;

                case MessageType.RoomPlayerJoined:
                    HandlePlayerJoined(message.Payload);
                    break;

                case MessageType.RoomPlayerLeft:
                    HandlePlayerLeft(message.Payload);
                    break;

                case MessageType.RoomKick:
                    HandleKicked(message.Payload);
                    break;

                case MessageType.RoomDisband:
                    HandleRoomDisband(message.Payload);
                    break;

                case MessageType.RoomAssignRole:
                case MessageType.RoomAssignTeam:
                case MessageType.RoomPlayerUpdated:
                    // 刷新房间信息
                    if (RoomClientState.Instance.IsInRoom)
                    {
                        _ = Client.GetRoomInfoAsync(RoomClientState.Instance.CurrentRoomId!);
                    }
                    break;

                case MessageType.Error:
                    HandleError(message.Payload);
                    break;

                case MessageType.AdminGetUsers:
                    HandleAdminGetUsers(message.Payload);
                    break;
            }
        }
        catch
        {
            // 忽略消息处理错误
        }
    }

    private void HandleRoomList(object? payload)
    {
        if (payload == null) return;

        var response = JsonSerializer.Deserialize<RoomListResponse>(payload.ToString()!);
        if (response != null)
        {
            RoomClientState.Instance.RoomList = response.Rooms;
            RoomClientState.Instance.RoomListTotal = response.Total;
            RoomClientState.Instance.CurrentPage = response.Page;
        }
    }

    private void HandleRoomCreate(object? payload)
    {
        if (payload == null) return;

        var response = JsonSerializer.Deserialize<RoomCreateResponse>(payload.ToString()!);
        if (response != null)
        {
            RoomClientState.Instance.CurrentRoomId = response.RoomId;
            RoomClientState.Instance.IsRoomOwner = true;

            // 获取房间详情
            _ = Client.GetRoomInfoAsync(response.RoomId);
        }
    }

    private void HandleRoomInfo(object? payload)
    {
        if (payload == null) return;

        var response = JsonSerializer.Deserialize<RoomInfoResponse>(payload.ToString()!);
        if (response != null)
        {
            RoomClientState.Instance.CurrentRoom = response.Room;
            RoomClientState.Instance.RoomPlayers = response.Players;

            if (response.Room != null)
            {
                RoomClientState.Instance.CurrentRoomId = response.Room.Id;
                RoomClientState.Instance.IsRoomOwner = response.Room.OwnerId == Client.PlayerId;
            }
        }
    }

    private void HandlePlayerJoined(object? payload)
    {
        RoomClientState.Instance.StatusMessage = "有新玩家加入房间";

        // 刷新房间信息
        if (RoomClientState.Instance.IsInRoom)
        {
            _ = Client.GetRoomInfoAsync(RoomClientState.Instance.CurrentRoomId!);
        }
    }

    private void HandlePlayerLeft(object? payload)
    {
        RoomClientState.Instance.StatusMessage = "有玩家离开房间";

        // 刷新房间信息
        if (RoomClientState.Instance.IsInRoom)
        {
            _ = Client.GetRoomInfoAsync(RoomClientState.Instance.CurrentRoomId!);
        }
    }

    private void HandleKicked(object? payload)
    {
        RoomClientState.Instance.ClearRoomState();
        RoomClientState.Instance.StatusMessage = "你已被踢出房间";
    }

    private void HandleRoomDisband(object? payload)
    {
        RoomClientState.Instance.ClearRoomState();
        RoomClientState.Instance.StatusMessage = "房间已被解散";
    }

    private void HandleError(object? payload)
    {
        if (payload == null) return;

        var error = JsonSerializer.Deserialize<WSError>(payload.ToString()!);
        if (error != null)
        {
            RoomClientState.Instance.StatusMessage = error.Message;
        }
    }

    private void HandleAdminGetUsers(object? payload)
    {
        if (payload == null) return;

        var response = JsonSerializer.Deserialize<AdminGetUsersResponse>(payload.ToString()!);
        if (response != null)
        {
            RoomClientState.Instance.AllConnectedUsers = response.Users;
        }
    }

    private void OnConnectionStateChanged(ConnectionState state)
    {
        // 更新友好的状态消息
        switch (state)
        {
            case ConnectionState.Connecting:
                RoomClientState.Instance.StatusMessage = "正在连接...";
                break;
            case ConnectionState.Authenticating:
                RoomClientState.Instance.StatusMessage = "正在认证...";
                break;
            case ConnectionState.Disconnected:
                if (!Client.IsManualDisconnect)
                {
                    RoomClientState.Instance.StatusMessage = "连接已断开";
                }
                RoomClientState.Instance.Reset();

                // 只有非主动断开时才自动重连
                // 注意：如果启用了 AutoConnect，由 CheckAutoConnect 处理重连，避免重复
                var setting = FullAutoSettings.Instance.RoomClientSetting;
                if (setting.AutoReconnect && !setting.AutoConnect && !Client.IsManualDisconnect && !IsDisposed)
                {
                    RoomClientState.Instance.StatusMessage = $"连接已断开，{setting.ReconnectInterval}秒后重连...";
                    _ = AutoReconnectAsync();
                }
                else if (setting.AutoConnect && !Client.IsManualDisconnect && !IsDisposed)
                {
                    // AutoConnect 启用时，由 CheckAutoConnect 处理
                    RoomClientState.Instance.StatusMessage = $"连接已断开，{setting.ReconnectInterval}秒后自动重连...";
                }
                break;
            case ConnectionState.Error:
                RoomClientState.Instance.StatusMessage = Client.ErrorMessage ?? "连接错误";

                // 连接错误时也尝试自动重连
                // 注意：如果启用了 AutoConnect，由 CheckAutoConnect 处理重连，避免重复
                var errorSetting = FullAutoSettings.Instance.RoomClientSetting;
                if (errorSetting.AutoReconnect && !errorSetting.AutoConnect && !Client.IsManualDisconnect && !IsDisposed)
                {
                    _ = AutoReconnectAsync();
                }
                break;
        }
    }

    private async Task AutoReconnectAsync()
    {
        try
        {
            var setting = FullAutoSettings.Instance.RoomClientSetting;
            await Task.Delay(setting.ReconnectInterval * 1000, _pluginCts?.Token ?? CancellationToken.None);

            if (!IsDisposed)
            {
                await ConnectAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch
        {
            // 忽略重连错误
        }
    }

    /// <summary>
    /// 插件是否已销毁
    /// </summary>
    public bool IsDisposed => _pluginCts?.IsCancellationRequested ?? true;

    private void OnWebSocketError(string error)
    {
        RoomClientState.Instance.StatusMessage = error;
    }

    #endregion

    public void Dispose()
    {
        // 取消所有异步任务
        try
        {
            _pluginCts?.Cancel();
        }
        catch
        {
            // 忽略
        }

        // 取消事件订阅
        Client.OnMessage -= OnWebSocketMessage;
        Client.OnStateChanged -= OnConnectionStateChanged;
        Client.OnError -= OnWebSocketError;

        // 断开连接并清理
        Client.Dispose();

        // 清理取消令牌
        _pluginCts?.Dispose();
        _pluginCts = null;

        _initialized = false;
        _instance = null;
    }
}
