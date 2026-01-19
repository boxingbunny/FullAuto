using AEAssist;
using AEAssist.Helper;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCommonList.CharacterData.OnlineStatus;


namespace AutoRaidHelper.Utils;

public static class PartyLeaderHelper
{
    private static readonly object TransferLock = new();

    /// <summary>
    /// 跨服小队成员信息
    /// </summary>
    private readonly struct MemberInfo(string name, ulong contentId, bool isOnline, bool isInDuty)
    {
        public readonly string Name = name;
        public readonly ulong ContentId = contentId;
        public readonly bool IsOnline = isOnline;
        public readonly bool IsInDuty = isInDuty;
    }

    /// <summary>
    /// 获取跨服小队成员信息列表（核心方法）
    /// </summary>
    private static unsafe List<MemberInfo> GetCrossRealmMembers()
    {
        var result = new List<MemberInfo>();
        var crossRealmProxy = InfoProxyCrossRealm.Instance();
        if (crossRealmProxy == null)
            return result;

        var infoModulePtr = InfoModule.Instance();
        if (infoModulePtr == null)
            return result;

        var commonListPtr = (InfoProxyCommonList*)infoModulePtr->GetInfoProxyById(InfoProxyId.PartyMember);
        if (commonListPtr == null)
            return result;

        var groups = crossRealmProxy->CrossRealmGroups;
        foreach (var group in groups)
        {
            int count = group.GroupMemberCount;
            if (commonListPtr->CharDataSpan.Length < count)
                continue;

            for (int i = 0; i < count; i++)
            {
                var member = group.GroupMembers[i];
                var data = commonListPtr->CharDataSpan[i];
                result.Add(new MemberInfo(
                    member.NameString,
                    member.ContentId,
                    data.State.HasFlag(Online),
                    data.State.HasFlag(InDuty)
                ));
            }
        }

        return result;
    }

    /// <summary>
    /// 获取跨服小队中每个成员的状态信息
    /// </summary>
    public static List<(string Name, bool IsOnline, bool IsInDuty)> GetCrossRealmPartyStatus()
    {
        var members = GetCrossRealmMembers();
        return members.Select(m => (m.Name, m.IsOnline, m.IsInDuty)).ToList();
    }

    /// <summary>
    /// 获取队长的名称。队长由 PartyLeader 或 PartyLeaderCrossWorld 标记确定。
    /// </summary>
    public static unsafe string? GetPartyLeaderName()
    {
        var infoModulePtr = InfoModule.Instance();
        if (infoModulePtr == null)
            return null;

        var commonListPtr = (InfoProxyCommonList*)infoModulePtr->GetInfoProxyById(InfoProxyId.PartyMember);
        if (commonListPtr != null)
        {
            foreach (var data in commonListPtr->CharDataSpan)
            {
                if (data.State.HasFlag(PartyLeader) || data.State.HasFlag(PartyLeaderCrossWorld))
                    return data.NameString;
            }
        }

        return null;
    }

    /// <summary>
    /// 检查本地玩家是否是队长
    /// </summary>
    public static bool IsLocalPlayerPartyLeader()
    {
        try
        {
            var leaderName = GetPartyLeaderName();
            if (string.IsNullOrEmpty(leaderName))
                return false;

            var localPlayer = Core.Me;

            var localPlayerName = localPlayer.Name.ToString();
            return string.Equals(localPlayerName, leaderName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取可转移的目标列表（排除自己，只包含在线玩家）
    /// </summary>
    public static string[] GetValidTransferTargets()
    {
        try
        {
            var localPlayer = Core.Me;

            var localPlayerName = localPlayer.Name.ToString();
            var members = GetCrossRealmMembers();

            return members
                .Where(m => m.IsOnline)
                .Where(m => !string.Equals(m.Name, localPlayerName, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Name)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 转移队长权限给指定玩家
    /// </summary>
    public static bool TransferPartyLeader(string targetPlayerName)
    {
        var localPlayer = Core.Me;
        var localPlayerName = localPlayer.Name.ToString();

        // 使用锁防止并发转移
        if (!Monitor.TryEnter(TransferLock, 0))
            return false;

        try
        {
            if (string.IsNullOrEmpty(targetPlayerName))
                return false;

            // 如果没有 RoomId，直接返回
            if (string.IsNullOrEmpty(RemoteControlHelper.RoomId))
                return false;

            var leaderName = GetPartyLeaderName();
            if (string.IsNullOrEmpty(leaderName))
                return false;
            

            // 如果队长就是目标，直接返回
            if (leaderName == targetPlayerName)
                return false;

            // 如果自己就是队长，直接执行
            if (string.Equals(localPlayerName, leaderName, StringComparison.OrdinalIgnoreCase))
                return TransferCrossRealmPartyLeaderLocal(targetPlayerName);

            // 否则给队长发送命令
            var leaderRole = RemoteControlHelper.GetRoleByPlayerName(leaderName);
            if (string.IsNullOrEmpty(leaderRole))
                return false;
            
            RemoteControlHelper.Cmd(leaderRole, $"/arh transferleader {targetPlayerName}");
            return true;
        }
        finally
        {
            Monitor.Exit(TransferLock);
        }
    }

    /// <summary>
    /// 本地执行转移队长权限
    /// </summary>
    private static bool TransferCrossRealmPartyLeaderLocal(string targetPlayerName)
    {
        // 必须是队长才能执行转移
        if (!IsLocalPlayerPartyLeader())
            return false;

        // 检查目标玩家是否在线且在队伍中
        var members = GetCrossRealmMembers();
        var targetMember = members.FirstOrDefault(m => string.Equals(m.Name, targetPlayerName, StringComparison.OrdinalIgnoreCase));

        if (targetMember.Name == null)
            return false;

        if (!targetMember.IsOnline)
            return false;

        // 执行转移
        return TransferCrossRealmPartyLeader(targetPlayerName, targetMember.ContentId);
    }

    /// <summary>
    /// 转移跨服小队队长权限
    /// </summary>
    private static unsafe bool TransferCrossRealmPartyLeader(string targetPlayerName, ulong targetContentId)
    {
        try
        {
            var agentPartyMember = AgentPartyMember.Instance();
            if (agentPartyMember == null)
                return false;

            agentPartyMember->Promote(targetPlayerName, 0, targetContentId);

            // 启动异步确认任务
            _ = Task.Run(() => ConfirmPartyLeaderTransferAsync());
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 异步确认队长转移弹窗
    /// </summary>
    private static async Task ConfirmPartyLeaderTransferAsync()
    {
        const int maxAttempts = 20;
        const int checkInterval = 50;

        for (int i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(checkInterval);

            bool success = false;
            try
            {
                success = TryConfirmDialog();
            }
            catch
            {
                // 忽略异常
            }

            if (success)
                break;
        }
    }

    /// <summary>
    /// 尝试确认对话框
    /// </summary>
    private static unsafe bool TryConfirmDialog()
    {
        var atkStage = AtkStage.Instance();
        if (atkStage == null)
            return false;

        var unitManager = atkStage->RaptureAtkUnitManager;
        if (unitManager == null)
            return false;

        var addon = unitManager->GetAddonByName("SelectYesno");
        if (addon == null || !addon->IsVisible)
            return false;

        var yesnoAddon = (AddonSelectYesno*)addon;
        var values = stackalloc AtkValue[1];
        values[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;
        values[0].Int = 0;

        yesnoAddon->FireCallback(1, values, true);
        return true;
    }
}
