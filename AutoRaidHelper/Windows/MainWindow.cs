using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using AutoRaidHelper.Helpers;
using AutoRaidHelper.UI;
using AutoRaidHelper.Settings;
using AutoRaidHelper.Utils;
using AutoRaidHelper.RoomClient;
using ECommons.DalamudServices;

namespace AutoRaidHelper.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly CleanBackgroundManager? _backgroundManager;
    private readonly AutomationTab _automationTab;
    private readonly FAControlTab _faControlTab;
    private readonly ToolsTab _toolsTab;
    private readonly ManagementTab _managementTab;
    private readonly FoodBuffTab _foodBuffTab;
    private readonly SettingsTab _settingsTab;
    private readonly RoomClientTab _roomClientTab;

    // 自定义标签栏
    private int _selectedTabIndex = 0;
    private readonly string[] _tabNames = { "自动化", "FA控制", "工具", "管理", "食物警察", "房间客户端", "设置" };
    private readonly Dictionary<string, float> _tabHoverStates = new();

    public MainWindow() : base(
        "全自动小助手###AutoRaidHelperMain",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar)
    {
        Size = new Vector2(800, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(688, 400),
            MaximumSize = new Vector2(2000, 1500)
        };

        // 初始化所有Tab
        _automationTab = new AutomationTab();
        _faControlTab = new FAControlTab();
        _toolsTab = new ToolsTab();
        _managementTab = new ManagementTab();
        _foodBuffTab = new FoodBuffTab();
        _settingsTab = new SettingsTab();
        _roomClientTab = new RoomClientTab();

        // 初始化磨砂玻璃背景管理器
        try
        {
            _backgroundManager = new CleanBackgroundManager(Svc.Log);
            _backgroundManager.Initialize();
            Svc.Log.Info("磨砂玻璃背景管理器初始化成功");
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "磨砂玻璃背景管理器初始化失败");
            _backgroundManager = null;
        }
    }

    public void Dispose()
    {
        _backgroundManager?.Dispose();
        _automationTab.Dispose();
        _toolsTab.Dispose();
        RoomClientManager.Instance.Dispose();
    }

    public void OnUpdate()
    {
        _toolsTab.Update();
        _automationTab.Update();
        _faControlTab.Update();
        _managementTab.Update();
        RoomClientManager.Instance.Update();
    }

    public override void PreDraw()
    {
        var settings = FullAutoSettings.Instance;

        // 隐藏调整大小手柄（右下角的三角形）
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        // 隐藏resize grip（设置为完全透明）
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, new Vector4(0, 0, 0, 0));

        if (settings.UseFrostedGlass)
        {
            Flags |= ImGuiWindowFlags.NoBackground;
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoBackground;
        }
    }

    private void DrawCustomTitleBar()
    {
        var settings = FullAutoSettings.Instance;
        var titleBarHeight = 35f;
        var windowPos = ImGui.GetWindowPos();
        var windowWidth = ImGui.GetWindowWidth();

        var titleBarMin = windowPos;
        var titleBarMax = new Vector2(windowPos.X + windowWidth, windowPos.Y + titleBarHeight);

        var drawList = ImGui.GetWindowDrawList();

        // 标题栏背景颜色（根据是否使用磨砂玻璃调整）
        var titleBgColor = settings.UseFrostedGlass
            ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.2f, 0.3f))
            : ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.15f, 0.9f));

        drawList.AddRectFilled(titleBarMin, titleBarMax, titleBgColor);

        // 标题文字（居中）
        var titleText = "全自动小助手";
        var titleSize = ImGui.CalcTextSize(titleText);
        var titlePos = new Vector2(
            windowPos.X + (windowWidth - titleSize.X) * 0.5f,
            windowPos.Y + (titleBarHeight - titleSize.Y) * 0.5f
        );

        var titleColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0.9f, 1.0f, 1.0f));
        drawList.AddText(titlePos, titleColor, titleText);

        // 关闭按钮
        var closeButtonSize = 25f;
        var closeButtonPos = new Vector2(windowPos.X + windowWidth - closeButtonSize - 5f, windowPos.Y + 5f);
        var closeButtonMin = closeButtonPos;
        var closeButtonMax = new Vector2(closeButtonPos.X + closeButtonSize, closeButtonPos.Y + closeButtonSize);

        var mousePos = ImGui.GetMousePos();
        var isHovered = mousePos.X >= closeButtonMin.X && mousePos.X <= closeButtonMax.X &&
                       mousePos.Y >= closeButtonMin.Y && mousePos.Y <= closeButtonMax.Y;

        var closeButtonColor = isHovered
            ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0.2f, 0.2f, 0.8f))
            : ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.4f, 0.4f, 0.6f));

        drawList.AddRectFilled(closeButtonMin, closeButtonMax, closeButtonColor, 3f);

        // 绘制关闭按钮的X
        var crossSize = 12f;
        var crossCenter = new Vector2(
            closeButtonPos.X + closeButtonSize * 0.5f,
            closeButtonPos.Y + closeButtonSize * 0.5f
        );
        var crossColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f));

        drawList.AddLine(
            new Vector2(crossCenter.X - crossSize * 0.35f, crossCenter.Y - crossSize * 0.35f),
            new Vector2(crossCenter.X + crossSize * 0.35f, crossCenter.Y + crossSize * 0.35f),
            crossColor, 2f
        );
        drawList.AddLine(
            new Vector2(crossCenter.X + crossSize * 0.35f, crossCenter.Y - crossSize * 0.35f),
            new Vector2(crossCenter.X - crossSize * 0.35f, crossCenter.Y + crossSize * 0.35f),
            crossColor, 2f
        );

        // 处理关闭按钮点击
        if (isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            IsOpen = false;
        }

        // 调整光标位置，为内容留出空间
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + titleBarHeight);

        // 处理标题栏拖动
        if (ImGui.IsMouseHoveringRect(titleBarMin, new Vector2(titleBarMax.X - 35f, titleBarMax.Y)) &&
            ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left);
            ImGui.SetWindowPos(new Vector2(windowPos.X + delta.X, windowPos.Y + delta.Y));
            ImGui.ResetMouseDragDelta(ImGuiMouseButton.Left);
        }
    }

    /// <summary>
    /// 绘制自定义圆角标签栏
    /// </summary>
    private void DrawCustomTabBar()
    {
        var drawList = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorScreenPos();
        var windowWidth = ImGui.GetContentRegionAvail().X;

        // 标签栏配置
        const float tabHeight = 32f;
        const float tabPadding = 12f;
        const float tabSpacing = 4f;
        const float tabRounding = 6f;

        // 玻璃态颜色
        var tabInactive = new Vector4(0.15f, 0.15f, 0.2f, 0.2f);
        var tabHovered = new Vector4(0.2f, 0.25f, 0.35f, 0.3f);
        var tabActive = new Vector4(0.25f, 0.3f, 0.4f, 0.4f);
        var tabBorder = new Vector4(1.0f, 1.0f, 1.0f, 0.12f);
        var tabBorderHovered = new Vector4(1.0f, 1.0f, 1.0f, 0.25f);
        var textColor = new Vector4(0.95f, 0.95f, 0.98f, 0.95f);

        float currentX = cursorPos.X + 8f;

        for (int i = 0; i < _tabNames.Length; i++)
        {
            var tabName = _tabNames[i];
            var textSize = ImGui.CalcTextSize(tabName);
            var tabWidth = textSize.X + tabPadding * 2f;

            var tabMin = new Vector2(currentX, cursorPos.Y);
            var tabMax = new Vector2(currentX + tabWidth, cursorPos.Y + tabHeight);

            // 检测鼠标悬停
            var mousePos = ImGui.GetMousePos();
            bool isHovered = mousePos.X >= tabMin.X && mousePos.X <= tabMax.X &&
                           mousePos.Y >= tabMin.Y && mousePos.Y <= tabMax.Y;
            bool isActive = _selectedTabIndex == i;

            // 平滑动画
            string hoverKey = $"tab_{i}";
            if (!_tabHoverStates.ContainsKey(hoverKey))
                _tabHoverStates[hoverKey] = 0f;

            float targetHover = isHovered ? 1f : 0f;
            float currentHover = _tabHoverStates[hoverKey];
            float delta = ImGui.GetIO().DeltaTime;
            float newHover = currentHover + (targetHover - currentHover) * Math.Min(10f * delta, 1f);
            _tabHoverStates[hoverKey] = newHover;

            // 选择背景颜色
            Vector4 bgColor;
            if (isActive)
                bgColor = tabActive;
            else
                bgColor = Vector4.Lerp(tabInactive, tabHovered, newHover);

            // 绘制完整圆角矩形背景
            drawList.AddRectFilled(tabMin, tabMax, ImGui.ColorConvertFloat4ToU32(bgColor), tabRounding);

            // 绘制边框
            var borderColor = Vector4.Lerp(tabBorder, tabBorderHovered, newHover);
            drawList.AddRect(tabMin, tabMax, ImGui.ColorConvertFloat4ToU32(borderColor), tabRounding, ImDrawFlags.None, 1f);

            // 绘制文字（居中）
            var textPos = new Vector2(
                currentX + (tabWidth - textSize.X) * 0.5f,
                cursorPos.Y + (tabHeight - textSize.Y) * 0.5f
            );
            drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(textColor), tabName);

            // 处理点击
            if (isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _selectedTabIndex = i;
            }

            currentX += tabWidth + tabSpacing;
        }

        // 移动光标到标签栏下方
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + tabHeight + 8f);
    }

    public override void PostDraw()
    {
        var settings = FullAutoSettings.Instance;

        // Pop resize grip colors (3 colors)
        ImGui.PopStyleColor(3);

        if (settings.UseFrostedGlass)
        {
            ImGui.PopStyleColor();
        }

        // Pop调整大小手柄的样式变量
        ImGui.PopStyleVar();
    }

    public override void Draw()
    {
        var settings = FullAutoSettings.Instance;

        // 绘制磨砂玻璃背景
        if (settings.UseFrostedGlass && _backgroundManager != null)
        {
            try
            {
                _backgroundManager.DrawBackground(settings.WindowOpacity);
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "绘制磨砂背景时出错");
            }
        }

        // 绘制Debug点（如果启用）
        if (FullAutoSettings.Instance.FaGeneralSetting.PrintDebugInfo)
        {
            DebugPoint.Render();
        }

        // 绘制自定义标题栏
        DrawCustomTitleBar();

        // 绘制自定义标签栏
        DrawCustomTabBar();

        // 获取内容区域高度
        var contentHeight = ImGui.GetContentRegionAvail().Y;

        // 根据选中的标签绘制对应内容
        ImGui.BeginChild("TabContent", new Vector2(0, contentHeight), false, ImGuiWindowFlags.AlwaysUseWindowPadding);

        switch (_selectedTabIndex)
        {
            case 0:
                _automationTab.Draw();
                break;
            case 1:
                _faControlTab.Draw();
                break;
            case 2:
                _toolsTab.Draw();
                break;
            case 3:
                _managementTab.Draw();
                break;
            case 4:
                _foodBuffTab.Draw();
                break;
            case 5:
                _roomClientTab.Draw();
                break;
            case 6:
                _settingsTab.Draw();
                break;
        }

        ImGui.EndChild();
    }

    public void OnLoad(System.Runtime.Loader.AssemblyLoadContext loadContext)
    {
        _automationTab.OnLoad(loadContext);
        _toolsTab.OnLoad(loadContext);

        // 初始化房间客户端管理器
        RoomClientManager.Instance.Initialize();

    }
}
