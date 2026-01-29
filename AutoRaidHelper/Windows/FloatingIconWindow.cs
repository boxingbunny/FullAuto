using System.Numerics;
using System.Reflection;
using AEAssist.Helper;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;

namespace AutoRaidHelper.Windows;

public class FloatingIconWindow : Window, IDisposable
{
    private Dalamud.Interface.Textures.ISharedImmediateTexture? _iconTexture;
    private readonly MainWindow _mainWindow;
    private readonly Vector2 _iconSize = new(100, 100);
    private bool _isDragging;

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
        if (_iconTexture == null)
        {
            ImGui.Text("图标加载失败");
            return;
        }

        var windowPos = ImGui.GetWindowPos();
        var mousePos = ImGui.GetMousePos();

        var isHovered = mousePos.X >= windowPos.X &&
                        mousePos.X <= windowPos.X + _iconSize.X &&
                        mousePos.Y >= windowPos.Y &&
                        mousePos.Y <= windowPos.Y + _iconSize.Y;

        // 绘制图标
        var alpha = isHovered ? 1.0f : 0.7f;
        var wrap = _iconTexture.GetWrapOrEmpty();
        if (wrap != null)
        {
            ImGui.Image(wrap.Handle, _iconSize, Vector2.Zero, Vector2.One, new Vector4(1, 1, 1, alpha));
        }

        // 左键点击打开主窗口
        if (isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _mainWindow.IsOpen = !_mainWindow.IsOpen;
        }

        // 右键拖动
        if (isHovered && ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            if (!_isDragging)
            {
                _isDragging = true;
            }

            if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
            {
                var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Right);
                ImGui.SetWindowPos(new Vector2(windowPos.X + delta.X, windowPos.Y + delta.Y));
                ImGui.ResetMouseDragDelta(ImGuiMouseButton.Right);
            }
        }
        else
        {
            _isDragging = false;
        }

        // 悬停提示
        if (isHovered)
        {
            ImGui.SetTooltip("左键: 打开/关闭主窗口\n右键拖动: 移动图标");
        }
    }
}
