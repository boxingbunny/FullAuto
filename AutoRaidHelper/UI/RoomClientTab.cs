using System.Numerics;
using AutoRaidHelper.RoomClient;
using AutoRaidHelper.RoomClient.UI;
using AutoRaidHelper.Settings;
using Dalamud.Bindings.ImGui;

namespace AutoRaidHelper.UI;

/// <summary>
/// 房间客户端 Tab - 适配 AutoRaidHelper 的 UI 风格
/// </summary>
public class RoomClientTab
{
    private readonly RoomManagementPanel _roomPanel;
    private readonly AdminPanel _adminPanel;

    public RoomClientTab()
    {
        _roomPanel = new RoomManagementPanel();
        _adminPanel = new AdminPanel();
    }

    public void Draw()
    {
        var client = RoomClientManager.Instance.Client;
        var state = client.State;

        // 连接状态栏
        DrawConnectionStatus(state);
        ImGui.Separator();
        ImGui.Spacing();

        if (state == ConnectionState.Authenticated)
        {
            // 已连接：显示功能标签页
            if (ImGui.BeginTabBar("RoomClientInnerTabs"))
            {
                // 房间管理 Tab
                if (ImGui.BeginTabItem("房间管理"))
                {
                    ImGui.BeginChild("RoomManagementContent", new Vector2(0, -30), false);
                    _roomPanel.Draw();
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }

                // 管理员 Tab (仅管理员可见)
                if (RoomClientState.Instance.IsAdmin && ImGui.BeginTabItem("用户管理"))
                {
                    ImGui.BeginChild("AdminPanelContent", new Vector2(0, -30), false);
                    _adminPanel.Draw();
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }

                // 设置 Tab
                if (ImGui.BeginTabItem("连接设置"))
                {
                    ImGui.BeginChild("SettingsContent", new Vector2(0, -30), false);
                    DrawSettingsUI();
                    ImGui.EndChild();
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
            DrawQuickActions(state);
        }

        // 底部状态消息
        DrawStatusMessage();
    }

    /// <summary>
    /// 绘制连接状态栏
    /// </summary>
    private void DrawConnectionStatus(ConnectionState state)
    {
        var client = RoomClientManager.Instance.Client;

        var stateText = state switch
        {
            ConnectionState.Disconnected => "未连接",
            ConnectionState.Connecting => "连接中...",
            ConnectionState.Connected => "已连接",
            ConnectionState.Authenticating => "认证中...",
            ConnectionState.Authenticated => "已认证",
            ConnectionState.Error => $"错误: {client.ErrorMessage}",
            _ => "未知"
        };

        var stateColor = state switch
        {
            ConnectionState.Authenticated => new Vector4(0.2f, 0.8f, 0.2f, 1),
            ConnectionState.Error => new Vector4(1, 0.2f, 0.2f, 1),
            ConnectionState.Connecting or ConnectionState.Authenticating => new Vector4(1, 0.8f, 0.2f, 1),
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1)
        };

        // 状态指示灯
        var drawList = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorScreenPos();
        var indicatorRadius = 6f;
        drawList.AddCircleFilled(
            new Vector2(cursorPos.X + indicatorRadius, cursorPos.Y + ImGui.GetTextLineHeight() / 2),
            indicatorRadius,
            ImGui.ColorConvertFloat4ToU32(stateColor)
        );
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indicatorRadius * 2 + 8);

        ImGui.TextColored(stateColor, $"状态: {stateText}");

        // 连接/断开按钮
        if (state == ConnectionState.Disconnected || state == ConnectionState.Error)
        {
            ImGui.SameLine();
            if (ImGui.Button("连接服务器"))
            {
                _ = RoomClientManager.Instance.ConnectAsync();
            }
        }
        else if (state == ConnectionState.Authenticated)
        {
            ImGui.SameLine();
            if (ImGui.Button("断开连接"))
            {
                _ = RoomClientManager.Instance.DisconnectAsync();
            }

            // 显示房间信息
            if (RoomClientState.Instance.IsInRoom)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("|");
                ImGui.SameLine();
                ImGui.Text($"房间: {RoomClientState.Instance.CurrentRoom?.Name ?? "未知"}");
            }
        }
    }

    /// <summary>
    /// 绘制设置界面
    /// </summary>
    private void DrawSettingsUI()
    {
        var setting = FullAutoSettings.Instance.RoomClientSetting;

        ImGui.Text("服务器设置");
        ImGui.Indent();

        var serverUrl = setting.ServerUrl;
        ImGui.SetNextItemWidth(400);
        if (ImGui.InputText("服务器地址", ref serverUrl, 256))
        {
            setting.ServerUrl = serverUrl;
        }

        var autoConnect = setting.AutoConnect;
        if (ImGui.Checkbox("自动连接", ref autoConnect))
        {
            setting.AutoConnect = autoConnect;
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("启动插件时自动连接到服务器");
        }

        var autoReconnect = setting.AutoReconnect;
        if (ImGui.Checkbox("断线重连", ref autoReconnect))
        {
            setting.AutoReconnect = autoReconnect;
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("连接断开后自动尝试重新连接");
        }

        if (setting.AutoReconnect)
        {
            ImGui.Indent();
            var reconnectInterval = setting.ReconnectInterval;
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("重连间隔(秒)", ref reconnectInterval))
            {
                if (reconnectInterval >= 1 && reconnectInterval <= 60)
                {
                    setting.ReconnectInterval = reconnectInterval;
                }
            }
            ImGui.Unindent();
        }

        ImGui.Spacing();
        if (ImGui.Button("保存设置"))
        {
            FullAutoSettings.Instance.Save();
            RoomClientState.Instance.StatusMessage = "设置已保存";
        }

        ImGui.Unindent();
    }

    /// <summary>
    /// 绘制快捷操作区域
    /// </summary>
    private void DrawQuickActions(ConnectionState state)
    {
        ImGui.Text("快捷操作");
        ImGui.Indent();

        if (state == ConnectionState.Disconnected || state == ConnectionState.Error)
        {
            // 连接按钮使用醒目的颜色
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.6f, 0.9f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.4f, 0.7f, 1.0f));

            if (ImGui.Button("连接服务器", new Vector2(200, 35)))
            {
                _ = RoomClientManager.Instance.ConnectAsync();
            }

            ImGui.PopStyleColor(3);
        }
        else if (state == ConnectionState.Authenticated)
        {
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1), "已连接");

            if (RoomClientState.Instance.IsInRoom)
            {
                ImGui.Text($"当前房间: {RoomClientState.Instance.CurrentRoom?.Name ?? "未知"}");

                if (ImGui.Button("离开房间"))
                {
                    _ = RoomClientManager.Instance.Client.LeaveRoomAsync();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("断开连接"))
            {
                _ = RoomClientManager.Instance.DisconnectAsync();
            }
        }
        else
        {
            ImGui.TextDisabled("连接中...");
        }

        ImGui.Unindent();
    }

    /// <summary>
    /// 绘制状态消息
    /// </summary>
    private void DrawStatusMessage()
    {
        var statusMessage = RoomClientState.Instance.StatusMessage;
        if (!string.IsNullOrEmpty(statusMessage))
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextWrapped(statusMessage);
        }
    }
}
