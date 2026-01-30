using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using AEAssist.Helper;
using AutoRaidHelper.RoomClient.Command;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;

namespace AutoRaidHelper.RoomClient.UI;

/// <summary>
/// 房间管理面板 - 包含大厅视图和房间视图
/// </summary>
public class RoomManagementPanel
{
    private readonly RoomOwnerControls _ownerControls;

    // 创建房间表单
    private string _createRoomName = "";
    private string _createRoomPassword = "";
    private int _createRoomSizeIndex = 1; // 默认8人

    // 加入房间
    private string _joinRoomPassword = "";
    private string _selectedRoomId = "";

    // 发送指令
    private string _commandTargets = "";
    private string _commandMessage = "";

    private static readonly int[] RoomSizes = { 4, 8, 24, 32, 48 };
    private static readonly string[] RoomSizeLabels = { "4人", "8人", "24人", "32人", "48人" };

    public RoomManagementPanel()
    {
        _ownerControls = new RoomOwnerControls();
    }

    public void Draw()
    {
        if (RoomClientState.Instance.IsInRoom)
        {
            DrawRoomView();
        }
        else
        {
            DrawLobbyView();
        }
    }

    #region 大厅视图

    private void DrawLobbyView()
    {
        // 创建房间区域
        if (ImGui.CollapsingHeader("创建房间", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.InputText("房间名称", ref _createRoomName, 64);
            ImGui.InputText("密码(可选)", ref _createRoomPassword, 32);
            ImGui.Combo("房间规模", ref _createRoomSizeIndex, RoomSizeLabels, RoomSizeLabels.Length);

            if (ImGui.Button("创建房间##RCT_CreateRoom"))
            {
                if (string.IsNullOrWhiteSpace(_createRoomName))
                {
                    RoomClientState.Instance.StatusMessage = "请输入房间名称";
                }
                else
                {
                    _ = CreateRoomSafeAsync();
                }
            }
        }

        ImGui.Spacing();

        // 房间列表
        if (ImGui.CollapsingHeader("房间列表", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.Button("刷新列表##RCT_RefreshRoomList"))
            {
                _ = RefreshRoomListSafeAsync();
            }

            ImGui.SameLine();
            ImGui.Text($"共 {RoomClientState.Instance.RoomListTotal} 个房间");

            ImGui.BeginChild("RoomList", new Vector2(0, 250), true);

            foreach (var room in RoomClientState.Instance.RoomList)
            {
                var isSelected = _selectedRoomId == room.Id;
                var label = $"{room.Name} ({room.PlayerCount}/{room.Size}) - {room.OwnerName}";
                if (room.HasPassword) label += " [密码]";

                if (ImGui.Selectable(label, isSelected))
                {
                    _selectedRoomId = room.Id;
                }
            }

            ImGui.EndChild();

            // 加入房间
            if (!string.IsNullOrEmpty(_selectedRoomId))
            {
                var selectedRoom = RoomClientState.Instance.RoomList.Find(r => r.Id == _selectedRoomId);
                if (selectedRoom != null)
                {
                    if (selectedRoom.HasPassword)
                    {
                        ImGui.InputText("房间密码", ref _joinRoomPassword, 32);
                    }

                    if (ImGui.Button("加入房间##RCT_JoinRoom"))
                    {
                        _ = JoinRoomAsync(selectedRoom.Id, selectedRoom.HasPassword ? _joinRoomPassword : "");
                    }
                }
            }
        }
    }

    #endregion

    #region 房间视图

    // 小队标签映射
    private static readonly string[] TeamLabels = { "A", "B", "C", "D", "E", "F" };

    private void DrawRoomView()
    {
        var state = RoomClientState.Instance;
        var room = state.CurrentRoom;

        if (room == null) return;

        // 房间信息头部
        ImGui.Text($"房间: {room.Name} ({room.PlayerCount}/{room.Size})");
        ImGui.Text($"房主: {room.OwnerName}");

        // 操作按钮
        if (ImGui.Button("离开房间##RCT_LeaveRoom"))
        {
            _ = LeaveRoomAsync();
        }

        if (state.IsRoomOwner)
        {
            ImGui.SameLine();
            if (ImGui.Button("解散房间##RCT_DisbandRoom"))
            {
                _ = DisbandRoomAsync();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("刷新##RCT_RefreshRoom"))
        {
            _ = RoomClientManager.Instance.Client.GetRoomInfoAsync(room.Id);
        }

        ImGui.SameLine();
        var inviteButtonText = room.Size <= 8 ? "邀请小队##RCT_BatchInvite" : "邀请团队##RCT_BatchInvite";
        if (ImGui.Button(inviteButtonText))
        {
            _ = BatchInvitePartyAsync(room.Id);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(room.Size <= 8 ? "邀请当前小队成员加入房间" : "邀请当前团队成员加入房间");
        }

        ImGui.Separator();

        // 根据房间规模决定显示方式
        if (room.Size > 8)
        {
            // 大房间：按小队分组显示
            DrawGroupedPlayerList(state, room);
        }
        else
        {
            // 小房间：单一列表显示
            DrawSimplePlayerList(state, room);
        }

        // 指令发送面板
        DrawCommandPanel();
    }

    /// <summary>
    /// 绘制指令发送面板
    /// </summary>
    private void DrawCommandPanel()
    {
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("发送指令##RCT_CommandPanel"))
        {
            ImGui.InputText("目标##RCT_CmdTargets", ref _commandTargets, 64);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("留空=所有人, A=A队, MT=所有MT, A+MT=A队MT, MT,ST=所有MT和ST");
            }

            ImGui.InputText("消息##RCT_CmdMessage", ref _commandMessage, 256);

            if (ImGui.Button("发送##RCT_SendCommand"))
            {
                if (!string.IsNullOrEmpty(_commandMessage))
                {
                    _ = SendCommandAsync(_commandTargets, _commandMessage);
                }
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("发送聊天消息指令给目标玩家\n目标玩家会在游戏内发送此消息");
            }
        }
    }

    /// <summary>
    /// 发送指令
    /// </summary>
    private async Task SendCommandAsync(string targets, string message)
    {
        RoomClientState.Instance.StatusMessage = "正在发送指令...";
        try
        {
            var success = await RoomCommandManager.Instance.SendChatMessageAsync(targets, message);
            RoomClientState.Instance.StatusMessage = success ? "指令已发送" : "发送失败";
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 发送指令失败: {ex.Message}");
            RoomClientState.Instance.StatusMessage = $"发送失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 绘制简单玩家列表（8人及以下房间）
    /// </summary>
    private void DrawSimplePlayerList(RoomClientState state, RoomInfo room)
    {
        ImGui.Text("玩家列表:");

        // 房主模式有更多列（名称、职业、队伍、职能、ACR、时间轴、操作）
        // 非房主模式（名称、职业、队伍、职能、ACR、时间轴）
        int columnCount = state.IsRoomOwner ? 7 : 6;

        if (ImGui.BeginTable("PlayerTable", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable,
                new Vector2(0, 350)))
        {
            SetupPlayerTableColumns(state.IsRoomOwner);
            ImGui.TableHeadersRow();

            foreach (var player in state.RoomPlayers)
            {
                ImGui.TableNextRow();
                _ownerControls.DrawPlayerRow(player, room, state.IsRoomOwner);
            }

            ImGui.EndTable();
        }
    }

    /// <summary>
    /// 绘制分组玩家列表（大于8人房间）
    /// </summary>
    private void DrawGroupedPlayerList(RoomClientState state, RoomInfo room)
    {
        // 按小队分组玩家
        var groupedPlayers = GroupPlayersByTeam(state.RoomPlayers, room.Size);

        // 计算可用高度并分配给各组
        float availableHeight = 400;
        float groupHeight = availableHeight / (groupedPlayers.Count > 0 ? groupedPlayers.Count : 1);
        groupHeight = Math.Max(groupHeight, 80); // 最小高度

        foreach (var group in groupedPlayers)
        {
            var teamName = group.Key;
            var players = group.Value;

            // 使用 CollapsingHeader 显示分组
            if (ImGui.CollapsingHeader($"{teamName} ({players.Count}人)##Team_{teamName}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                int columnCount = state.IsRoomOwner ? 7 : 6;

                if (ImGui.BeginTable($"PlayerTable_{teamName}", columnCount,
                        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable,
                        new Vector2(0, Math.Min(groupHeight, players.Count * 25 + 30))))
                {
                    SetupPlayerTableColumns(state.IsRoomOwner);
                    ImGui.TableHeadersRow();

                    foreach (var player in players)
                    {
                        ImGui.TableNextRow();
                        _ownerControls.DrawPlayerRow(player, room, state.IsRoomOwner);
                    }

                    ImGui.EndTable();
                }
            }
        }
    }

    /// <summary>
    /// 设置玩家表格列
    /// </summary>
    private void SetupPlayerTableColumns(bool isOwner)
    {
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 60);
        ImGui.TableSetupColumn("职业", ImGuiTableColumnFlags.WidthFixed, 60);
        ImGui.TableSetupColumn("队伍", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("职能", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("ACR", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("时间轴", ImGuiTableColumnFlags.WidthFixed, 100);
        if (isOwner)
        {
            ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 60);
        }
    }

    /// <summary>
    /// 按小队分组玩家
    /// </summary>
    private Dictionary<string, List<RoomPlayer>> GroupPlayersByTeam(List<RoomPlayer> players, int roomSize)
    {
        var result = new Dictionary<string, List<RoomPlayer>>();

        // 根据房间规模确定可用队伍数量
        int maxTeams = GetMaxTeams(roomSize);

        // 初始化分组（保证顺序：未分配 -> A -> B -> ...）
        result["未分配"] = new List<RoomPlayer>();
        for (int i = 0; i < maxTeams; i++)
        {
            result[$"{TeamLabels[i]}队"] = new List<RoomPlayer>();
        }

        // 分配玩家到各组
        foreach (var player in players)
        {
            if (string.IsNullOrEmpty(player.TeamId))
            {
                result["未分配"].Add(player);
            }
            else
            {
                var teamKey = $"{player.TeamId}队";
                if (result.ContainsKey(teamKey))
                {
                    result[teamKey].Add(player);
                }
                else
                {
                    // 如果队伍ID不在预期范围内，放入未分配
                    result["未分配"].Add(player);
                }
            }
        }

        // 移除空的分组（可选：保留空分组以显示完整结构）
        // 这里选择保留空分组，让用户看到所有可用的队伍
        return result;
    }

    /// <summary>
    /// 根据房间规模获取最大队伍数
    /// </summary>
    private static int GetMaxTeams(int roomSize)
    {
        return roomSize switch
        {
            4 or 8 => 1,    // 只有 A 队
            24 => 3,        // A/B/C
            32 => 4,        // A/B/C/D
            48 => 6,        // A/B/C/D/E/F
            _ => 1
        };
    }

    #endregion

    #region 安全异步包装（捕获异常，避免 fire-and-forget 丢失异常）

    /// <summary>
    /// 安全地创建房间（捕获异常并记录）
    /// </summary>
    private async Task CreateRoomSafeAsync()
    {
        try
        {
            await CreateRoomAsync();
        }
        catch (OperationCanceledException)
        {
            // 操作被取消，不是错误
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 创建房间异常: {ex}");
            RoomClientState.Instance.StatusMessage = $"创建房间异常: {ex.Message}";
        }
    }

    /// <summary>
    /// 安全地刷新房间列表（捕获异常并记录）
    /// </summary>
    private async Task RefreshRoomListSafeAsync()
    {
        try
        {
            await RoomClientManager.Instance.Client.GetRoomListAsync();
        }
        catch (OperationCanceledException)
        {
            // 操作被取消，不是错误
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 刷新房间列表异常: {ex}");
            RoomClientState.Instance.StatusMessage = $"刷新房间列表异常: {ex.Message}";
        }
    }

    #endregion

    #region 操作方法

    private async Task CreateRoomAsync()
    {
        RoomClientState.Instance.StatusMessage = "正在创建房间...";
        var size = (RoomSize)RoomSizes[_createRoomSizeIndex];
        var ack = await RoomClientManager.Instance.Client.CreateRoomAsync(_createRoomName, size, _createRoomPassword);

        if (ack?.Success == true)
        {
            RoomClientState.Instance.StatusMessage = "房间创建成功";
            _createRoomName = "";
            _createRoomPassword = "";
        }
        else
        {
            RoomClientState.Instance.StatusMessage = $"创建失败: {ack?.Error ?? "未知错误"}";
        }
    }

    private async Task JoinRoomAsync(string roomId, string password)
    {
        var ack = await RoomClientManager.Instance.Client.JoinRoomAsync(roomId, password);

        if (ack?.Success == true)
        {
            RoomClientState.Instance.CurrentRoomId = roomId;
            RoomClientState.Instance.StatusMessage = "加入房间成功";
            _joinRoomPassword = "";
            _selectedRoomId = "";

            await RoomClientManager.Instance.Client.GetRoomInfoAsync(roomId);
        }
        else
        {
            RoomClientState.Instance.StatusMessage = $"加入失败: {ack?.Error ?? "未知错误"}";
        }
    }

    private async Task LeaveRoomAsync()
    {
        var ack = await RoomClientManager.Instance.Client.LeaveRoomAsync();

        if (ack?.Success == true)
        {
            RoomClientState.Instance.ClearRoomState();
            RoomClientState.Instance.StatusMessage = "已离开房间";
        }
        else
        {
            RoomClientState.Instance.StatusMessage = $"离开失败: {ack?.Error ?? "未知错误"}";
        }
    }

    private async Task DisbandRoomAsync()
    {
        var roomId = RoomClientState.Instance.CurrentRoomId;
        if (string.IsNullOrEmpty(roomId)) return;

        var ack = await RoomClientManager.Instance.Client.DisbandRoomAsync(roomId);

        if (ack?.Success == true)
        {
            RoomClientState.Instance.ClearRoomState();
            RoomClientState.Instance.StatusMessage = "房间已解散";
        }
        else
        {
            RoomClientState.Instance.StatusMessage = $"解散失败: {ack?.Error ?? "未知错误"}";
        }
    }

    /// <summary>
    /// 批量邀请当前小队/团队成员加入房间
    /// </summary>
    private async Task BatchInvitePartyAsync(string roomId)
    {
        RoomClientState.Instance.StatusMessage = "正在邀请队友...";

        try
        {
            // 获取当前小队/团队成员的 CID
            var cids = GetPartyMemberCIDs();
            if (cids.Count == 0)
            {
                RoomClientState.Instance.StatusMessage = "没有可邀请的队友";
                return;
            }

            var response = await RoomClientManager.Instance.Client.BatchInviteAsync(roomId, cids);

            if (response != null)
            {
                // 构建结果消息
                var messages = new List<string>();
                if (response.Joined.Count > 0)
                {
                    messages.Add($"成功邀请 {response.Joined.Count} 人");
                }
                if (response.NotConnected > 0)
                {
                    messages.Add($"{response.NotConnected} 人未连接服务器");
                }
                if (response.AlreadyInRoom.Count > 0)
                {
                    messages.Add($"{response.AlreadyInRoom.Count} 人已在其他房间");
                }
                if (response.RoomFull.Count > 0)
                {
                    messages.Add($"{response.RoomFull.Count} 人因房间满员未能加入");
                }

                RoomClientState.Instance.StatusMessage = messages.Count > 0
                    ? string.Join(", ", messages)
                    : "没有可邀请的队友";

                // 刷新房间信息
                if (response.Joined.Count > 0)
                {
                    await RoomClientManager.Instance.Client.GetRoomInfoAsync(roomId);
                }
            }
            else
            {
                RoomClientState.Instance.StatusMessage = "邀请失败";
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[RoomClient] 批量邀请失败: {ex.Message}");
            RoomClientState.Instance.StatusMessage = $"邀请失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取当前小队/团队成员的 CID 列表（不包括自己）
    /// </summary>
    private List<string> GetPartyMemberCIDs()
    {
        var cids = new List<string>();
        var myCid = Svc.ClientState.LocalContentId;

        foreach (var member in Svc.Party)
        {
            // ContentId 为 0 表示无效成员
            if (member.ContentId == 0)
                continue;

            // 跳过自己（将 ulong 转换为 long 进行比较）
            if (member.ContentId == (long)myCid)
                continue;

            cids.Add(member.ContentId.ToString());
        }

        return cids;
    }

    #endregion
}
