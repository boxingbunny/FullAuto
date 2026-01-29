using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;

namespace FullAuto.UI;

/// <summary>
/// 管理员面板 - 用户管理
/// </summary>
public class AdminPanel
{
    private readonly FullAutoPlugin _plugin;

    // 用户列表数据
    private List<AdminUserInfo> _userList = new();
    private string _statusMessage = "";
    private bool _isLoading = false;

    public AdminPanel(FullAutoPlugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        ImGui.Text("用户管理 (管理员)");
        ImGui.Separator();

        // 工具栏
        DrawToolbar();

        ImGui.Spacing();

        // 状态消息
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            ImGui.TextColored(new Vector4(1, 1, 0, 1), _statusMessage);
            ImGui.Spacing();
        }

        // 用户列表表格
        DrawUserTable();
    }

    private void DrawToolbar()
    {
        if (_isLoading)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("刷新用户列表"))
        {
            _ = RefreshUserListAsync();
        }

        if (_isLoading)
        {
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.Text("加载中...");
        }

        ImGui.SameLine();
        ImGui.Text($"当前在线: {_userList.Count} 人");
    }

    private void DrawUserTable()
    {
        if (ImGui.BeginTable("AdminUserTable", 8,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable,
            new Vector2(0, 350)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("用户名", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("职业", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("世界ID", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("所在房间", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("ACR", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("时间轴", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("连接时长", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            foreach (var user in _userList)
            {
                ImGui.TableNextRow();
                DrawUserRow(user);
            }

            ImGui.EndTable();
        }
    }

    private void DrawUserRow(AdminUserInfo user)
    {
        // 用户名
        ImGui.TableNextColumn();
        ImGui.Text(user.Name);

        // 职业
        ImGui.TableNextColumn();
        ImGui.Text(user.Job ?? "-");

        // 世界ID
        ImGui.TableNextColumn();
        ImGui.Text(user.WorldId.ToString());

        // 所在房间
        ImGui.TableNextColumn();
        ImGui.Text(user.RoomName ?? "(大厅)");

        // ACR
        ImGui.TableNextColumn();
        ImGui.Text(user.AcrName ?? "-");

        // 时间轴
        ImGui.TableNextColumn();
        ImGui.Text(user.TriggerLineName ?? "-");

        // 连接时长
        ImGui.TableNextColumn();
        var duration = DateTime.Now - user.ConnectTime;
        if (duration.TotalHours >= 1)
        {
            ImGui.Text($"{duration.TotalHours:F0}h");
        }
        else
        {
            ImGui.Text($"{duration.TotalMinutes:F0}m");
        }

        // 操作
        ImGui.TableNextColumn();
        DrawKickButton(user);
    }

    private void DrawKickButton(AdminUserInfo user)
    {
        // 不能踢自己
        if (user.Id == _plugin.Client.PlayerId)
        {
            ImGui.TextDisabled("-");
            return;
        }

        ImGui.PushID($"admin_kick_{user.Id}");
        if (ImGui.SmallButton("踢出"))
        {
            _ = KickUserAsync(user.Id, user.Name);
        }
        ImGui.PopID();
    }

    #region 操作方法

    private async Task RefreshUserListAsync()
    {
        _isLoading = true;
        _statusMessage = "正在刷新...";

        try
        {
            var ack = await _plugin.Client.GetAllUsersAsync();
            if (ack?.Success == true)
            {
                // 等待一帧让消息处理完成
                await Task.Delay(100);
                // 从状态获取用户列表（由 OnWebSocketMessage 更新）
                _userList = FullAutoState.Instance.AllConnectedUsers;
                _statusMessage = $"刷新成功 ({_userList.Count} 人)";
            }
            else
            {
                _statusMessage = $"刷新失败: {ack?.Error ?? "未知错误"}";
            }
        }
        catch (Exception ex)
        {
            AEAssist.Helper.LogHelper.Error($"[FullAuto] 刷新用户列表异常: {ex}");
            _statusMessage = $"刷新失败: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task KickUserAsync(string userId, string userName)
    {
        _statusMessage = $"正在踢出 {userName}...";

        var ack = await _plugin.Client.KickUserAsync(userId);
        if (ack?.Success == true)
        {
            _statusMessage = $"已踢出 {userName}";
            // 刷新列表
            await RefreshUserListAsync();
        }
        else
        {
            _statusMessage = $"踢出失败: {ack?.Error ?? "未知错误"}";
        }
    }

    #endregion
}
