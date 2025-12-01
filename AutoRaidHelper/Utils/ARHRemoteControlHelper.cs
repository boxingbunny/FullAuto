using System;
using System.Numerics;
using System.Timers;
using OmenTools.Helpers;
using Timer = System.Timers.Timer;

namespace AutoRaidHelper.Utils;
public static class ARHRemoteControlHelper
{
    private static readonly PathFindHelper pathFinder;
    private static Vector3? target;
    private static float precision = 0.2f;
    private static Timer? checkTimer;

    // 外部必须提供获取玩家当前位置的方法
    public static Func<Vector3> GetPlayerPosition = null!;

    static ARHRemoteControlHelper()
    {
        pathFinder = new PathFindHelper
        {
            Enabled = true,
            IsAutoMove = true
        };
    }

    /// <summary>
    /// 移动到指定坐标 (x, y, z)
    /// </summary>
    public static void MoveTo(float x, float y, float z, float stopPrecision = 0.2f)
    {
        if (GetPlayerPosition == null)
            throw new InvalidOperationException("必须先设置 GetPlayerPosition 委托用于获取玩家坐标");

        target = new Vector3(x, y, z);
        precision = stopPrecision;

        pathFinder.Enabled = true;
        pathFinder.DesiredPosition = target.Value;

        checkTimer?.Stop();
        checkTimer?.Dispose();

        checkTimer = new Timer(50); // 每50ms检查一次
        checkTimer.Elapsed += (_, __) =>
        {
            if (target.HasValue)
            {
                var playerPos = GetPlayerPosition();
                if (Vector3.Distance(playerPos, target.Value) <= precision)
                {
                    pathFinder.Enabled = false;
                    target = null;
                    checkTimer?.Stop();
                }
            }
        };
        checkTimer.Start();
    }

    /// <summary>
    /// 移动到指定坐标 (Vector3)
    /// </summary>
    public static void MoveTo(Vector3 destination, float stopPrecision = 0.2f)
    {
        MoveTo(destination.X, destination.Y, destination.Z, stopPrecision);
    }

    /// <summary>
    /// 手动停止移动
    /// </summary>
    public static void Stop()
    {
        pathFinder.Enabled = false;
        target = null;
        checkTimer?.Stop();
        checkTimer?.Dispose();
        checkTimer = null;
    }

    /// <summary>
    /// 插件卸载时调用
    /// </summary>
    public static void Dispose()
    {
        Stop();
        pathFinder?.Dispose();
    }
}
