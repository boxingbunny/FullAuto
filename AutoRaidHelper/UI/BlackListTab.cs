using System.Text.Json;
using AEAssist;
using AEAssist.Helper;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoRaidHelper.UI;

/// <summary>
/// BlackListTab 提供队伍成员 CID 获取以及黑名单编辑和踢人功能。
/// 使用 AutomationTab.scale 统一缩放。
/// </summary>
public unsafe class BlackListTab
{
    // 黑名单条目定义
    private class Entry
    {
        public string Name { get; set; } = string.Empty;
        public ulong CID { get; set; }
        public string Comment { get; set; } = string.Empty;
        public int RiskLv { get; set; } = 1;
    }

    private readonly List<Entry> _entries = new();
    private readonly HashSet<ulong> _lastParty = new();
    private readonly string _filePath;

    public BlackListTab()
    {
        _filePath = Path.Combine(Share.CurrentDirectory, @"..\..\Settings\AutoRaidHelper\BlackList.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var list = JsonSerializer.Deserialize<List<Entry>>(json);
                if (list != null)
                {
                    _entries.Clear();
                    _entries.AddRange(list);
                }
            }
        }
        catch (Exception e)
        {
            LogHelper.PrintError($"黑名单加载失败: {e.Message}");
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception e)
        {
            LogHelper.PrintError($"黑名单保存失败: {e.Message}");
        }
    }

    public void Update()
    {
        var infoModule = InfoModule.Instance();
        if (infoModule == null) return;
        var commonList = (InfoProxyCommonList*)infoModule->GetInfoProxyById(InfoProxyId.PartyMember);
        if (commonList == null) return;

        var currentParty = new HashSet<ulong>();
        int count = commonList->CharDataSpan.Length;
        for (int i = 0; i < count; i++)
        {
            var data = commonList->CharDataSpan[i];
            if (data.NameString.Length == 0) continue;
            currentParty.Add(data.ContentId);
        }

        // 检测新加入的成员
        foreach (var cid in currentParty)
        {
            if (_lastParty.Contains(cid)) continue;
            // 找到姓名
            string name = string.Empty;
            for (int i = 0; i < count; i++)
            {
                var d = commonList->CharDataSpan[i];
                if (d.ContentId != cid) continue;
                name = d.NameString;
                break;
            }
            var entry = _entries.Find(e => e.CID == cid || e.Name == name);
            if (entry != null)
            {
                LogHelper.Print($"检测到黑名单玩家: {entry.Name} (CID: {entry.CID})，危险等级: {entry.RiskLv}，备注: {entry.Comment}");
            }
        }

        _lastParty.Clear();
        foreach (var cid in currentParty)
            _lastParty.Add(cid);
    }

    public void Draw()
    {
        float scale = AutomationTab.scale;
        ImGui.Text($"当前队伍类型: {(InfoProxyCrossRealm.IsCrossRealmParty() ? "跨服小队" : "普通组队")}");
        ImGui.Separator();

        var infoModule = InfoModule.Instance();
        if (infoModule == null)
        {
            ImGui.TextDisabled("无法获取 InfoModule");
            return;
        }
        var commonList = (InfoProxyCommonList*)infoModule->GetInfoProxyById(InfoProxyId.PartyMember);
        if (commonList == null)
        {
            ImGui.TextDisabled("无法获取 PartyMember");
            return;
        }

        int count = commonList->CharDataSpan.Length;
        if (count == 0)
        {
            ImGui.Text("(队伍为空)");
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                var data = commonList->CharDataSpan[i];
                if (data.Name.Length == 0 || data.Name[0] == 0) continue;

                string name = data.NameString;
                ulong cid = data.ContentId;

                ImGui.Text(name);
                ImGui.SameLine();

                // —— 复制 CID
                if (ImGui.Button($"获取CID##{cid}"))
                {
                    ImGui.SetClipboardText(cid.ToString());
                    LogHelper.Print($"已复制 {name} CID: {cid}");
                }

                ImGui.SameLine();
                // —— 加入黑名单
                if (ImGui.Button($"添加黑名单##{cid}"))
                {
                    _entries.Add(new Entry { Name = name, CID = cid });
                    Save();
                }

                ImGui.SameLine();
                // —— 移出小队
                if (ImGui.Button($"移出小队##{cid}"))
                {
                    //ActionManager.Instance()->UseAction(ActionType.GeneralAction, 12);
                    
                    var agent = AgentPartyMember.Instance();
                    if (!agent->IsAgentActive())
                    {
                        agent->Show();
                    }

                    if (agent != null && agent->IsAgentActive())
                    {

                        agent->Kick(name, (ushort)agent->GetAddonId(), cid);
                        LogHelper.Print($"已移出小队成员：{name}");
                    }
                    else
                    {
                        LogHelper.PrintError("无法获取 AgentPartyMember");
                    }
                    
                    if (agent->IsAgentActive())
                    {
                        agent->Hide();
                    }
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(new System.Numerics.Vector4(1, 0.6f, 0.2f, 1), "黑名单列表");
        if (ImGui.Button("添加空条目"))
        {
            _entries.Add(new Entry());
            Save();
        }

        if (ImGui.BeginTable("##BlackListTable", 5, ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 100f * scale);
            ImGui.TableSetupColumn("CID", ImGuiTableColumnFlags.WidthFixed, 150f * scale);
            ImGui.TableSetupColumn("Comment", ImGuiTableColumnFlags.WidthFixed, 200f * scale);
            ImGui.TableSetupColumn("RiskLv", ImGuiTableColumnFlags.WidthFixed, 50f * scale);
            ImGui.TableSetupColumn("操作");
            ImGui.TableHeadersRow();

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(100f * scale);
                var nameBuf = e.Name;
                if (ImGui.InputText($"##Name{i}", ref nameBuf, 64))
                {
                    e.Name = nameBuf;
                    Save();
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(150f * scale);
                var cidStr = e.CID.ToString();
                if (ImGui.InputText($"##CID{i}", ref cidStr, 32) && ulong.TryParse(cidStr, out var val))
                {
                    e.CID = val;
                    Save();
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(200f * scale);
                var commentBuf = e.Comment;
                if (ImGui.InputText($"##Comment{i}", ref commentBuf, 128))
                {
                    e.Comment = commentBuf; 
                    Save();
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(50f * scale);
                int sel = e.RiskLv - 1;
                string[] levels = ["1", "2", "3", "4", "5"];
                if (ImGui.Combo($"##Risk{i}", ref sel, levels, levels.Length)) 
                { 
                    e.RiskLv = sel + 1; 
                    Save(); 
                }

                ImGui.TableNextColumn();
                if (ImGui.Button($"移除##{i}"))
                {
                    _entries.RemoveAt(i); 
                    Save();
                    break;
                }
            }
            ImGui.EndTable();
        }
    }
}