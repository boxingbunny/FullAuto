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
            LogHelper.Info("[RoomClient] 客户端管理器初始化完成");
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
                LogHelper.Info($"[RoomClient] 检测到玩家信息变化: Job={currentJob}, ACR={currentAcrName}, TriggerLine={currentTriggerLineName}");

                // 更新本地缓存
                _lastJob = currentJob;
                _lastAcrName = currentAcrName;
                _lastTriggerLineName = currentTriggerLineName;

                // 上报服务端
                _ = ReportPlayerInfoChangeAsync(currentJob, currentAcrName, currentTriggerLineName);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 检测玩家信息变化时出错: {ex.Message}");
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
                LogHelper.Info("[RoomClient] 玩家信息已更新到服务端");

                // 如果在房间中，刷新房间信息以更新自己的显示
                if (RoomClientState.Instance.IsInRoom)
                {
                    await Client.GetRoomInfoAsync(RoomClientState.Instance.CurrentRoomId!);
                }
            }
            else
            {
                LogHelper.Error($"[RoomClient] 更新玩家信息失败: {ack?.Error ?? "未知错误"}");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 上报玩家信息时出错: {ex.Message}");
        }
    }

    #region 连接管理

    public async Task ConnectAsync()
    {
        var setting = FullAutoSettings.Instance.RoomClientSetting;

        RoomClientState.Instance.StatusMessage = "正在连接...";

        if (await Client.ConnectAsync(setting.ServerUrl))
        {
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
            // 使用 JobHelper.GetTranslation 获取中文职业名称
            return JobHelper.GetTranslation(me.CurrentJob());
        }
        catch
        {
            return "";
        }
    }

    private string GetAECode()
    {
        // 从 AEAssist 验证系统获取激活码
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
            // 获取当前使用的 ACR 名称
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
            // 获取当前时间轴名称
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
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 处理消息失败: {ex.Message}");
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
            RoomClientState.Instance.StatusMessage = $"错误: {error.Message}";
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
        LogHelper.Info($"[RoomClient] 连接状态变更: {state}, IsManualDisconnect={Client.IsManualDisconnect}");

        if (state == ConnectionState.Disconnected)
        {
            RoomClientState.Instance.Reset();

            // 只有非主动断开时才自动重连，且插件未被销毁
            var setting = FullAutoSettings.Instance.RoomClientSetting;
            if (setting.AutoReconnect && !Client.IsManualDisconnect && !IsDisposed)
            {
                LogHelper.Info($"[RoomClient] 将在 {setting.ReconnectInterval} 秒后自动重连");
                _ = AutoReconnectAsync();
            }
        }
    }

    private async Task AutoReconnectAsync()
    {
        try
        {
            var setting = FullAutoSettings.Instance.RoomClientSetting;
            await Task.Delay(setting.ReconnectInterval * 1000, _pluginCts?.Token ?? CancellationToken.None);

            // 再次检查插件是否已销毁
            if (!IsDisposed)
            {
                await ConnectAsync();
            }
        }
        catch (OperationCanceledException)
        {
            LogHelper.Info("[RoomClient] 自动重连任务已取消");
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 自动重连失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 插件是否已销毁
    /// </summary>
    public bool IsDisposed => _pluginCts?.IsCancellationRequested ?? true;

    private void OnWebSocketError(string error)
    {
        RoomClientState.Instance.StatusMessage = $"连接错误: {error}";
    }

    #endregion

    public void Dispose()
    {
        LogHelper.Info("[RoomClient] 客户端管理器正在销毁...");

        // 取消所有异步任务
        try
        {
            _pluginCts?.Cancel();
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 取消任务时出错: {ex.Message}");
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

        LogHelper.Info("[RoomClient] 客户端管理器已销毁");
    }
}
