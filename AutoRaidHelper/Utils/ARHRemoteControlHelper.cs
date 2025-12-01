using System;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Timers;
using AEAssist;
using AEAssist.Helper;
using OmenTools.Helpers;
using Timer = System.Timers.Timer;

namespace AutoRaidHelper.Utils;

public static class ARHRemoteControlHelper
{
    private static readonly PathFindHelper pathFinder;
    private static Vector3? target;
    private static float precision = 0.2f;
    private static Timer? checkTimer;

    static ARHRemoteControlHelper()
    {
        pathFinder = new PathFindHelper
        {
            Enabled = true,
            IsAutoMove = true
        };
    }

    /// <summary>
    /// 移动到指定坐标 (x, y, z)，带职能筛选
    /// </summary>
    /// <param name="regexRole">
    /// 职能正则。
    /// 例如: "MT" (仅MT), "H[1-2]" (H1/H2), "D.*" (所有D), "" (所有人)
    /// </param>
    public static void MoveTo(string regexRole, float x, float y, float z, float stopPrecision = 0.2f)
    {
        if (Core.Me == null) return;

        if (!string.IsNullOrEmpty(regexRole))
        {
            try
            {
                // 直接获取当前职能
                var myName = Core.Me.Name.ToString();
                var myRole = RemoteControlHelper.GetRoleByPlayerName(myName);

                // 如果获取不到职能，或者职能不匹配正则，则退出
                if (string.IsNullOrEmpty(myRole) || !Regex.IsMatch(myRole, regexRole, RegexOptions.IgnoreCase))
                {
                    return; // 不是我的指令，不动
                }
            }
            catch (Exception)
            {
                // 防止 RemoteControlHelper 调用失败导致崩溃
                return;
            }
        }

        // --- 执行移动 ---
        target = new Vector3(x, y, z);
        precision = stopPrecision;

        pathFinder.Enabled = true;
        pathFinder.DesiredPosition = target.Value;

        // 重置检测计时器
        checkTimer?.Stop();
        checkTimer?.Dispose();

        checkTimer = new Timer(50); // 每50ms检查一次
        checkTimer.Elapsed += (_, __) =>
        {
            // 再次检查 Core.Me 防止下线/过图崩溃
            if (target.HasValue && Core.Me != null)
            {
                if (Vector3.Distance(Core.Me.Position, target.Value) <= precision)
                {
                    Stop(); // 到达目的地，停止
                }
            }
            else
            {
                Stop();
            }
        };
        checkTimer.Start();
    }

    /// <summary>
    /// 移动到指定坐标 (Vector3)
    /// </summary>
    public static void MoveTo(string regexRole, Vector3 destination, float stopPrecision = 0.2f)
    {
        MoveTo(regexRole, destination.X, destination.Y, destination.Z, stopPrecision);
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