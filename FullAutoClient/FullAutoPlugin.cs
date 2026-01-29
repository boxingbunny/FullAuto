using System;
using System.Numerics;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AEAssist;
using AEAssist.AEPlugin;
using AEAssist.CombatRoutine;
using AEAssist.CombatRoutine.Module;
using AEAssist.Extension;
using AEAssist.Helper;
using AEAssist.JobApi;
using AEAssist.Verify;
using Dalamud.Bindings.ImGui;
using FullAuto.UI;

namespace FullAuto;

/// <summary>
/// FullAuto 插件主类
/// </summary>
public class FullAutoPlugin : IAEPlugin
{
    public static FullAutoPlugin? Instance { get; private set; }

    public WebSocketClient Client { get; } = new();

    private bool _initialized;
    private CancellationTokenSource? _pluginCts; // 插件级别的取消令牌

    // UI 组件
    private RoomManagementPanel? _roomPanel;
    private AdminPanel? _adminPanel;

    // 玩家状态追踪（用于检测变化）
    private string _lastJob = "";
    private string _lastAcrName = "";
    private string _lastTriggerLineName = "";
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private const int UpdateCheckIntervalMs = 1000; // 每秒检查一次

    public PluginSetting BuildPlugin()
    {
        return new PluginSetting
        {
            Name = "FullAuto",
            LimitLevel = VIPLevel.VIP1,
            EventHandleCallback = OnEvent
        };
    }

    public void OnLoad(AssemblyLoadContext loadContext)
    {
        try
        {
            Instance = this;
            _pluginCts = new CancellationTokenSource();
            // _ui = new FullAutoUI(this);

            // 初始化 UI 组件
            _roomPanel = new RoomManagementPanel(this);
            _adminPanel = new AdminPanel(this);

            // 订阅消息事件
            Client.OnMessage += OnWebSocketMessage;
            Client.OnStateChanged += OnConnectionStateChanged;
            Client.OnError += OnWebSocketError;

            _initialized = true;
            LogHelper.Info("[FullAuto] 插件加载完成");
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 插件加载失败: {ex}");
        }
    }

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
                LogHelper.Info($"[FullAuto] 检测到玩家信息变化: Job={currentJob}, ACR={currentAcrName}, TriggerLine={currentTriggerLineName}");

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
            LogHelper.Error($"[FullAuto] 检测玩家信息变化时出错: {ex.Message}");
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
                LogHelper.Info("[FullAuto] 玩家信息已更新到服务端");

                // 如果在房间中，刷新房间信息以更新自己的显示
                if (FullAutoState.Instance.IsInRoom)
                {
                    await Client.GetRoomInfoAsync(FullAutoState.Instance.CurrentRoomId!);
                }
            }
            else
            {
                LogHelper.Error($"[FullAuto] 更新玩家信息失败: {ack?.Error ?? "未知错误"}");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 上报玩家信息时出错: {ex.Message}");
        }
    }

    public void OnPluginUI()
    {
        ImGui.BeginChild("FullAutoPluginUI");

        try
        {
            // 连接状态栏
            DrawConnectionStatus();
            ImGui.Separator();

            if (Client.State == ConnectionState.Authenticated)
            {
                // Tab 导航
                if (ImGui.BeginTabBar("FullAutoTabs"))
                {
                    // 房间管理 Tab
                    if (ImGui.BeginTabItem("房间管理"))
                    {
                        _roomPanel?.Draw();
                        ImGui.EndTabItem();
                    }

                    // 管理员 Tab (仅管理员可见)
                    if (FullAutoState.Instance.IsAdmin && ImGui.BeginTabItem("用户管理"))
                    {
                        _adminPanel?.Draw();
                        ImGui.EndTabItem();
                    }

                    // 设置 Tab
                    if (ImGui.BeginTabItem("设置"))
                    {
                        DrawSettingsUI();
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }
            }
            else
            {
                // 未连接时显示设置和快捷操作
                DrawSettingsUI();
                ImGui.Separator();
                DrawQuickActions();
            }

            // 状态消息
            if (!string.IsNullOrEmpty(FullAutoState.Instance.StatusMessage))
            {
                ImGui.Spacing();
                ImGui.TextWrapped(FullAutoState.Instance.StatusMessage);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] UI错误: {ex.Message}");
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private void DrawConnectionStatus()
    {
        var state = Client.State;
        var stateText = state switch
        {
            ConnectionState.Disconnected => "未连接",
            ConnectionState.Connecting => "连接中...",
            ConnectionState.Connected => "已连接",
            ConnectionState.Authenticating => "认证中...",
            ConnectionState.Authenticated => "已认证",
            ConnectionState.Error => $"错误: {Client.ErrorMessage}",
            _ => "未知"
        };

        var stateColor = state switch
        {
            ConnectionState.Authenticated => new Vector4(0, 1, 0, 1),
            ConnectionState.Error => new Vector4(1, 0, 0, 1),
            ConnectionState.Connecting or ConnectionState.Authenticating => new Vector4(1, 1, 0, 1),
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1)
        };

        ImGui.TextColored(stateColor, $"状态: {stateText}");

        if (state == ConnectionState.Disconnected || state == ConnectionState.Error)
        {
            ImGui.SameLine();
            if (ImGui.Button("连接"))
            {
                _ = ConnectAsync();
            }
        }
        else if (state == ConnectionState.Authenticated)
        {
            ImGui.SameLine();
            if (ImGui.Button("断开"))
            {
                _ = DisconnectAsync();
            }

            if (FullAutoState.Instance.IsInRoom)
            {
                ImGui.SameLine();
                ImGui.Text($"| 房间: {FullAutoState.Instance.CurrentRoom?.Name ?? "未知"}");
            }
        }
    }

    public void OnExternalUI()
    {

    }

    private void DrawSettingsUI()
    {
        var setting = FullAutoSetting.Instance;

        ImGui.Text("服务器设置");
        ImGui.Indent();

        var serverUrl = setting.ServerUrl;
        if (ImGui.InputText("服务器地址", ref serverUrl, 256))
        {
            setting.ServerUrl = serverUrl;
        }

        var autoConnect = setting.AutoConnect;
        if (ImGui.Checkbox("自动连接", ref autoConnect))
        {
            setting.AutoConnect = autoConnect;
        }

        var autoReconnect = setting.AutoReconnect;
        if (ImGui.Checkbox("断线重连", ref autoReconnect))
        {
            setting.AutoReconnect = autoReconnect;
        }

        if (ImGui.Button("保存设置"))
        {
            setting.Save();
            FullAutoState.Instance.StatusMessage = "设置已保存";
        }

        ImGui.Unindent();
    }

    private void DrawQuickActions()
    {
        ImGui.Text("快捷操作");
        ImGui.Indent();

        var state = Client.State;

        if (state == ConnectionState.Disconnected || state == ConnectionState.Error)
        {
            if (ImGui.Button("连接服务器"))
            {
                _ = ConnectAsync();
            }
        }
        else if (state == ConnectionState.Authenticated)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "已连接");

            if (FullAutoState.Instance.IsInRoom)
            {
                ImGui.Text($"当前房间: {FullAutoState.Instance.CurrentRoom?.Name ?? "未知"}");

                if (ImGui.Button("离开房间"))
                {
                    _ = Client.LeaveRoomAsync();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("断开连接"))
            {
                _ = DisconnectAsync();
            }
        }
        else
        {
            ImGui.Text("连接中...");
        }

        ImGui.Unindent();

        // 状态消息
        if (!string.IsNullOrEmpty(FullAutoState.Instance.StatusMessage))
        {
            ImGui.Spacing();
            ImGui.TextWrapped(FullAutoState.Instance.StatusMessage);
        }
    }

    #region 连接管理

    public async Task ConnectAsync()
    {
        var setting = FullAutoSetting.Instance;

        FullAutoState.Instance.StatusMessage = "正在连接...";

        if (await Client.ConnectAsync(setting.ServerUrl))
        {
            // 收集玩家信息并认证
            var playerInfo = CollectPlayerInfo();
            var aeCode = GetAECode();

            if (string.IsNullOrEmpty(aeCode))
            {
                FullAutoState.Instance.StatusMessage = "无法获取AE激活码";
                await Client.DisconnectAsync();
                return;
            }

            if (await Client.AuthenticateAsync(aeCode, playerInfo))
            {
                FullAutoState.Instance.StatusMessage = "连接成功";

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
        FullAutoState.Instance.Reset();
        FullAutoState.Instance.StatusMessage = "已断开连接";
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
                    if (FullAutoState.Instance.IsInRoom)
                    {
                        _ = Client.GetRoomInfoAsync(FullAutoState.Instance.CurrentRoomId!);
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
            LogHelper.Error($"[FullAuto] 处理消息失败: {ex.Message}");
        }
    }

    private void HandleRoomList(object? payload)
    {
        if (payload == null) return;

        var response = JsonSerializer.Deserialize<RoomListResponse>(payload.ToString()!);
        if (response != null)
        {
            FullAutoState.Instance.RoomList = response.Rooms;
            FullAutoState.Instance.RoomListTotal = response.Total;
            FullAutoState.Instance.CurrentPage = response.Page;
        }
    }

    private void HandleRoomCreate(object? payload)
    {
        if (payload == null) return;

        var response = JsonSerializer.Deserialize<RoomCreateResponse>(payload.ToString()!);
        if (response != null)
        {
            FullAutoState.Instance.CurrentRoomId = response.RoomId;
            FullAutoState.Instance.IsRoomOwner = true;

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
            FullAutoState.Instance.CurrentRoom = response.Room;
            FullAutoState.Instance.RoomPlayers = response.Players;

            if (response.Room != null)
            {
                FullAutoState.Instance.CurrentRoomId = response.Room.Id;
                FullAutoState.Instance.IsRoomOwner = response.Room.OwnerId == Client.PlayerId;
            }
        }
    }

    private void HandlePlayerJoined(object? payload)
    {
        FullAutoState.Instance.StatusMessage = "有新玩家加入房间";

        // 刷新房间信息
        if (FullAutoState.Instance.IsInRoom)
        {
            _ = Client.GetRoomInfoAsync(FullAutoState.Instance.CurrentRoomId!);
        }
    }

    private void HandlePlayerLeft(object? payload)
    {
        FullAutoState.Instance.StatusMessage = "有玩家离开房间";

        // 刷新房间信息
        if (FullAutoState.Instance.IsInRoom)
        {
            _ = Client.GetRoomInfoAsync(FullAutoState.Instance.CurrentRoomId!);
        }
    }

    private void HandleKicked(object? payload)
    {
        FullAutoState.Instance.ClearRoomState();
        FullAutoState.Instance.StatusMessage = "你已被踢出房间";
    }

    private void HandleRoomDisband(object? payload)
    {
        FullAutoState.Instance.ClearRoomState();
        FullAutoState.Instance.StatusMessage = "房间已被解散";
    }

    private void HandleError(object? payload)
    {
        if (payload == null) return;

        var error = JsonSerializer.Deserialize<WSError>(payload.ToString()!);
        if (error != null)
        {
            FullAutoState.Instance.StatusMessage = $"错误: {error.Message}";
        }
    }

    private void HandleAdminGetUsers(object? payload)
    {
        if (payload == null) return;

        var response = JsonSerializer.Deserialize<AdminGetUsersResponse>(payload.ToString()!);
        if (response != null)
        {
            FullAutoState.Instance.AllConnectedUsers = response.Users;
        }
    }

    private void OnConnectionStateChanged(ConnectionState state)
    {
        LogHelper.Info($"[FullAuto] 连接状态变更: {state}, IsManualDisconnect={Client.IsManualDisconnect}");

        if (state == ConnectionState.Disconnected)
        {
            FullAutoState.Instance.Reset();

            // 只有非主动断开时才自动重连，且插件未被销毁
            if (FullAutoSetting.Instance.AutoReconnect && !Client.IsManualDisconnect && !IsDisposed)
            {
                LogHelper.Info($"[FullAuto] 将在 {FullAutoSetting.Instance.ReconnectInterval} 秒后自动重连");
                _ = AutoReconnectAsync();
            }
        }
    }

    private async Task AutoReconnectAsync()
    {
        try
        {
            await Task.Delay(FullAutoSetting.Instance.ReconnectInterval * 1000, _pluginCts?.Token ?? CancellationToken.None);

            // 再次检查插件是否已销毁
            if (!IsDisposed)
            {
                await ConnectAsync();
            }
        }
        catch (OperationCanceledException)
        {
            LogHelper.Info("[FullAuto] 自动重连任务已取消");
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 自动重连失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 插件是否已销毁
    /// </summary>
    public bool IsDisposed => _pluginCts?.IsCancellationRequested ?? true;

    private void OnWebSocketError(string error)
    {
        FullAutoState.Instance.StatusMessage = $"连接错误: {error}";
    }

    #endregion

    public void OnEvent(IEvent @event)
    {
        // 处理事件
    }

    public void Dispose()
    {
        LogHelper.Info("[FullAuto] 插件正在销毁...");

        // 取消所有异步任务
        try
        {
            _pluginCts?.Cancel();
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 取消任务时出错: {ex.Message}");
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

        // 保存设置
        FullAutoSetting.Instance.Save();

        _initialized = false;
        Instance = null;

        LogHelper.Info("[FullAuto] 插件已销毁");
    }
}
