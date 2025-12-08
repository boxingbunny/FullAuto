using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using OmenTools.Helpers;
using AEAssist;
using AEAssist.Helper;

namespace AutoRaidHelper.Utils;

public static class ARHRemoteControlHelper
{
    public static readonly Dictionary<string, PathFindHelper> PathFinders = new();

    private static class RoleMappingService
    {
        public static string GetPlayerNameByRole(string roleIdentifier)
        {
            // 简单处理：如果不是预设角色组，就认为它已经是玩家名
            if (!new HashSet<string> { "D1", "D2", "D3", "D4", "H1", "H2", "MT", "ST" }.Contains(roleIdentifier))
            {
                return roleIdentifier;
            }

            return Core.Me.Name.ToString();
        }
    }


    /// <summary>
    /// 核心寻路方法：根据角色标识符或名称，检查当前玩家是否匹配，如果匹配则启动寻路。
    /// </summary>
    /// <param name="roleIdentifier">角色标识符 (如 H1, MT) 或玩家名称。</param>
    /// <param name="targetPos">目标坐标。</param>
    /// <param name="stopPrecision">停止精度。</param>
    public static void MoveTo(string roleIdentifier, Vector3 targetPos, float stopPrecision = 0.2f)
    {
        string currentClientPlayerName = Core.Me.Name.ToString();
        string targetPlayerName;

        if (string.IsNullOrWhiteSpace(roleIdentifier) || roleIdentifier.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            // 如果为空或 ALL，则目标是当前玩家自己 (因为每个客户端只控制自己)
            targetPlayerName = currentClientPlayerName;
        }
        else
        {
            // 将角色标识符 (如 "MT") 转换为实际的玩家名称
            targetPlayerName = RoleMappingService.GetPlayerNameByRole(roleIdentifier);
        }

        // 检查当前客户端是否应该移动
        if (!currentClientPlayerName.Equals(targetPlayerName, StringComparison.OrdinalIgnoreCase))
        {
            // 如果当前玩家不是目标玩家，则停止寻路并退出
            StopMovementFor(currentClientPlayerName);
            return;
        }

        // 启动寻路逻辑 (只有匹配成功的客户端才会执行)
        if (!PathFinders.ContainsKey(currentClientPlayerName))
        {
            PathFinders[currentClientPlayerName] = new PathFindHelper();
        }

        var helper = PathFinders[currentClientPlayerName];

        // 配置并启用 PathFindHelper
        helper.DesiredPosition = targetPos;
        helper.Precision = stopPrecision;
        helper.IsAutoMove = true;
        helper.Enabled = true;
    }

    /// <summary>
    /// 便利重载：接收 x, y, z 坐标
    /// </summary>
    public static void MoveTo(string regexRole, float x, float y, float z, float stopPrecision = 0.2f)
    {
        Vector3 targetPos = new Vector3(x, y, z);
        MoveTo(regexRole, targetPos, stopPrecision);
    }

    /// <summary>
    /// 周期性检查函数：检查角色是否到达目标位置并停止寻路。
    /// </summary>
    public static void CheckMovementStatus()
    {
        string playerName = Core.Me.Name.ToString();

        if (PathFinders.TryGetValue(playerName, out var helper))
        {
            if (!helper.Enabled) return;

            float distanceSq = Vector3.DistanceSquared(Core.Me.Position, helper.DesiredPosition);
            float precisionSq = helper.Precision * helper.Precision;

            if (distanceSq < precisionSq)
            {
                // 角色已到达，调用停止方法
                StopMovementFor(playerName);
            }
        }
    }

    /// <summary>
    /// 停止指定角色的移动（关闭函数劫持）
    /// </summary>
    public static void StopMovementFor(string playerName)
    {
        if (PathFinders.TryGetValue(playerName, out var helper))
        {
            helper.Enabled = false;
        }
    }

    /// <summary>
    /// 在插件卸载时调用，清理所有 PathFindHelper 实例。
    /// </summary>
    public static void DisposeAll()
    {
        foreach (var helper in PathFinders.Values)
        {
            helper.Dispose();
        }
        PathFinders.Clear();
    }
}