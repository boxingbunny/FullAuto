using System.Numerics;
using System.Reflection;
using AEAssist.Helper;
using AutoRaidHelper.Settings;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;

namespace AutoRaidHelper.Windows;

public class FloatingIconWindow : Window, IDisposable
{
    private Dalamud.Interface.Textures.ISharedImmediateTexture? _iconTexture;
    private readonly MainWindow _mainWindow;
    private Vector2 _iconSize = new(100, 100);
    private bool _isDragging;
    private Vector2 _dragStartWindowPos;

    public FloatingIconWindow(MainWindow mainWindow) : base(
        "FloatingIcon###AutoRaidHelperFloatingIcon",
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoBackground |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.NoMove)
    {
        _mainWindow = mainWindow;

        Size = _iconSize;
        SizeCondition = ImGuiCond.Always;

        // 默认位置在屏幕右侧中间
        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.FirstUseEver;

        // 默认显示悬浮窗
        IsOpen = true;

        LoadIcon();
    }

    private void LoadIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "AutoRaidHelper.Resources.FA.png";

            _iconTexture = Svc.Texture.GetFromManifestResource(assembly, resourceName);
            if (_iconTexture == null)
                LogHelper.Print($"找不到嵌入资源: {resourceName}");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex.Message);
            LogHelper.Print("加载悬浮图标失败");
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        var sizeSetting = Math.Clamp(FullAutoSettings.Instance.FloatingIconSize, 40f, 240f);
        var desiredSize = new Vector2(sizeSetting, sizeSetting);
        if (_iconSize != desiredSize)
        {
            _iconSize = desiredSize;
            Size = _iconSize;
        }

        if (_iconTexture == null)
        {
            ImGui.Text("图标加载失败");
            return;
        }

        var wrap = _iconTexture.GetWrapOrEmpty();

        ImGui.SetCursorPos(Vector2.Zero);
        ImGui.InvisibleButton("###AutoRaidHelperFloatingIconButton", _iconSize);

        var isHovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var alpha = isHovered ? 1.0f : 0.7f;
        var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha));
        ImGui.GetWindowDrawList().AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, tint);

        // 左键点击打开主窗口
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            _mainWindow.IsOpen = !_mainWindow.IsOpen;
        }

        // 右键拖动
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _isDragging = true;
            _dragStartWindowPos = ImGui.GetWindowPos();
            ImGui.ResetMouseDragDelta(ImGuiMouseButton.Right);
        }

        if (_isDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Right);
                var newPos = _dragStartWindowPos + delta;
                var viewport = ImGui.GetMainViewport();
                var minPos = viewport.WorkPos;
                var maxPos = viewport.WorkPos + viewport.WorkSize - _iconSize;
                newPos.X = Math.Clamp(newPos.X, minPos.X, maxPos.X);
                newPos.Y = Math.Clamp(newPos.Y, minPos.Y, maxPos.Y);
                ImGui.SetWindowPos(newPos);
            }
            else
            {
                _isDragging = false;
                ImGui.ResetMouseDragDelta(ImGuiMouseButton.Right);
            }
        }

        // 悬停提示
        if (isHovered)
        {
            ImGui.SetTooltip("左键: 打开/关闭主窗口\n右键拖动: 移动图标");
        }
    }
}
