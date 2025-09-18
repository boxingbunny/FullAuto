using System.Numerics;
using AEAssist;
using AEAssist.CombatRoutine.Trigger;
using AEAssist.Helper;
using Dalamud.Bindings.ImGui;

namespace AutoRaidHelper.Triggers.TriggerAction;

internal class 指定职能tp指定位置 : ITriggerAction
{
    public string DisplayName { get; } = "指定职能tp指定位置";
    public string Remark { get; set; } = string.Empty; // 为 Remark 提供默认值

    // 为每个职能分配单独的坐标
    public Dictionary<string, Vector3> rolePositions = new()
    {
        { "MT", new Vector3() },
        { "ST", new Vector3() },
        { "H1", new Vector3() },
        { "H2", new Vector3() },
        { "D1", new Vector3() },
        { "D2", new Vector3() },
        { "D3", new Vector3() },
        { "D4", new Vector3() }
    };

    public Dictionary<string, bool> roles = new()
    {
        { "MT", false },
        { "ST", false },
        { "H1", false },
        { "H2", false },
        { "D1", false },
        { "D2", false },
        { "D3", false },
        { "D4", false }
    };

    public bool lockPosition;
    public int lockDuration;

    public bool Draw()
    {
        ImGui.Text("指定职能TP");

        foreach (var role in roles.Keys.ToList())
        {
            bool isChecked = roles[role];
            if (ImGui.Checkbox(role, ref isChecked))
            {
                roles[role] = isChecked;
            }

            ImGui.SameLine();
            string positionString = $"{rolePositions[role].X},{rolePositions[role].Y},{rolePositions[role].Z}";
            if (ImGui.InputText($"{role} 坐标 (x,y,z)", ref positionString, 100))
            {
                var parts = positionString.Split(',');
                if (parts.Length == 3 &&
                    float.TryParse(parts[0], out float x) &&
                    float.TryParse(parts[1], out float y) &&
                    float.TryParse(parts[2], out float z))
                {
                    rolePositions[role] = new Vector3(x, y, z);
                }
            }
        }

        ImGui.Checkbox("锁定位置", ref lockPosition);
        if (lockPosition)
        {
            ImGui.InputInt("锁定时间 (毫秒)", ref lockDuration);
        }

        return true;
    }

    public bool Handle()
    {
        Share.DebugPointWithText.Clear();
        foreach (var role in roles)
        {
            if (role.Value)
            {
                Vector3 position = rolePositions[role.Key];
                if (lockPosition)
                {
                    RemoteControlHelper.LockPos(role.Key, position, lockDuration);
                }
                else
                {
                    RemoteControlHelper.SetPos(role.Key, position);
                }
                Share.DebugPointWithText.Add(role.Key, position);
            }
        }
        return true;
    }
}