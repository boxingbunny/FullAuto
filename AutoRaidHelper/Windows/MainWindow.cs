using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using AutoRaidHelper.Helpers;
using AutoRaidHelper.UI;
using AutoRaidHelper.Settings;
using AutoRaidHelper.Utils;
using ECommons.DalamudServices;

namespace AutoRaidHelper.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly CleanBackgroundManager? _backgroundManager;
    private readonly GeometryTab _geometryTab;
    private readonly AutomationTab _automationTab;
    private readonly FaGeneralSettingTab _faGeneralSettingTab;
    private readonly FaManualTab _faManualTab;
    private readonly DebugPrintTab _debugPrintTab;
    private readonly BlackListTab _blackListTab;
    private readonly FoodBuffTab _foodBuffTab;
    private readonly UISettingsTab _uiSettingsTab;
    private readonly aboutTab _aboutTab;

    public MainWindow() : base(
        "全自动小助手###AutoRaidHelperMain",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar)
    {
        Size = new Vector2(800, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(2000, 1500)
        };

        // 初始化所有Tab
        _geometryTab = new GeometryTab();
        _automationTab = new AutomationTab();
        _faGeneralSettingTab = new FaGeneralSettingTab();
        _faManualTab = new FaManualTab();
        _debugPrintTab = new DebugPrintTab();
        _blackListTab = new BlackListTab();
        _foodBuffTab = new FoodBuffTab();
        _uiSettingsTab = new UISettingsTab();
        _aboutTab = new aboutTab();

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
        _debugPrintTab.Dispose();
    }

    public void OnUpdate()
    {
        _geometryTab.Update();
        _automationTab.Update();
        _faManualTab.Update();
        _blackListTab.Update();
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

        // 绘制Tab标题栏（固定不动）
        if (ImGui.BeginTabBar("MainTabBar"))
        {
            // 使用GetContentRegionAvail获取剩余可用空间
            var availRegion = ImGui.GetContentRegionAvail();
            var contentHeight = availRegion.Y;

            if (ImGui.BeginTabItem("几何计算"))
            {
                // 创建可滚动的子窗口来包含Tab内容，隐藏滚动条但保留滚动功能
                ImGui.BeginChild("GeometryContent", new Vector2(0, contentHeight), false,
                    ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);
                _geometryTab.Draw();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("自动化"))
            {
                ImGui.BeginChild("AutomationContent", new Vector2(0, contentHeight), false,
                    ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);
                _automationTab.Draw();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("FA全局设置"))
            {
                ImGui.BeginChild("FaGeneralContent", new Vector2(0, contentHeight), false,
                    ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);
                _faGeneralSettingTab.Draw();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("FA手动操作"))
            {
                ImGui.BeginChild("FaManualContent", new Vector2(0, contentHeight), false,
                    ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);
                _faManualTab.Draw();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("日志监听"))
            {
                ImGui.BeginChild("DebugPrintContent", new Vector2(0, contentHeight), false,
                    ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);
                _debugPrintTab.Draw();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("黑名单管理"))
            {
                ImGui.BeginChild("BlackListContent", new Vector2(0, contentHeight), false,
                    ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);
                _blackListTab.Draw();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("食物警察"))
            {
                ImGui.BeginChild("FoodBuffContent", new Vector2(0, contentHeight), false,
                    ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);
                _foodBuffTab.Draw();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("UI设置"))
            {
                ImGui.BeginChild("UISettingsContent", new Vector2(0, contentHeight), false,
                    ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);
                _uiSettingsTab.Draw();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("关于"))
            {
                ImGui.BeginChild("About", new Vector2(0, contentHeight), false,
                    ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar);
                _aboutTab.Draw();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    public void OnLoad(System.Runtime.Loader.AssemblyLoadContext loadContext)
    {
        _automationTab.OnLoad(loadContext);
        _debugPrintTab.OnLoad(loadContext);
    }
}
