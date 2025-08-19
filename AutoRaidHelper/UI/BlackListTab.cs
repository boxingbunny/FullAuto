using System.Numerics;
using System.Text.Json;
using AEAssist;
using AEAssist.Helper;
using AEAssist.MemoryApi;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace AutoRaidHelper.UI;

public unsafe class BlackListTab
{
    private class Entry
    {
        public string Name { get; set; } = string.Empty;
        public ulong CID { get; set; }
        public string Comment { get; set; } = string.Empty;
        public int RiskLv { get; set; } = 1;
    }

    private readonly List<Entry> _entries = new();
    private readonly string _filePath;

    // 黑名单命中
    private readonly HashSet<ulong> _hitCids = new();
    private readonly Dictionary<ulong, string> _cidToName = new();
    public static int LastHitCount { get; private set; }
    // 玩家列表缓存
    private DateTime _nextPlayerRefresh = DateTime.MinValue;
    private readonly List<(string Name, ulong CID)> _cachedPlayers = new();
    // 玩家数量
    private int _lastPlayerCount;

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

        var proxy = (InfoProxy24*)infoModule->GetInfoProxyById((InfoProxyId)24);
        if (proxy == null) return;

        var span = proxy->CharDataSpan;
        int len = span.Length;
        _lastPlayerCount = len;

        _hitCids.Clear();
        _cidToName.Clear();

        for (int i = 0; i < len; i++)
        {
            ref readonly var d = ref span[i];
            if (d.Name.Length == 0 || d.Name[0] == 0) continue;

            string name = d.NameString;
            ulong cid = d.ContentId;
            if (cid == 0) continue;

            _cidToName[cid] = name;

            if (_entries.Any(e => e.CID != 0 && e.CID == cid))
                _hitCids.Add(cid);
            LastHitCount = _hitCids.Count;
        }
    }

    public void Draw()
    {
        float scale = AutomationTab.scale;

        if (Core.Resolve<MemApiMap>().GetCurrTerrId() != 1252)
        {
            ImGui.TextDisabled("不在新月岛内");
            return;
        }

        ImGui.Text($"岛内玩家：{_lastPlayerCount} 人");

        // 黑名单命中
        if (_hitCids.Count > 0)
        {
            ImGui.Text("扫到黑名单玩家：");
            var yellow = new Vector4(1f, 1f, 0f, 1f);
            foreach (var cid in _hitCids.OrderBy(c => c))
            {
                if (_cidToName.TryGetValue(cid, out var name) && !string.IsNullOrEmpty(name))
                    ImGui.TextColored(yellow, $"{name} (CID:{cid})");
            }
        }
        
        ImGui.Separator();

        //  玩家列表：每 20 秒刷新缓存
        if (DateTime.Now >= _nextPlayerRefresh)
        {
            _nextPlayerRefresh = DateTime.Now.AddSeconds(20);
            _cachedPlayers.Clear();

            var infoModule = InfoModule.Instance();
            if (infoModule != null)
            {
                var proxy = (InfoProxy24*)infoModule->GetInfoProxyById((InfoProxyId)24);
                if (proxy != null)
                {
                    var span = proxy->CharDataSpan;
                    for (int i = 0; i < span.Length; i++)
                    {
                        ref readonly var d = ref span[i];
                        if (d.Name.Length == 0 || d.Name[0] == 0) continue;

                        _cachedPlayers.Add((d.NameString, d.ContentId));
                    }
                }
            }
        }

        // 渲染玩家缓存
        foreach (var (name, cid) in _cachedPlayers)
        {
            ImGui.TextUnformatted(name);
            ImGui.SameLine();

            if (ImGui.Button($"复制CID##{cid}"))
                ImGui.SetClipboardText(cid.ToString());

            ImGui.SameLine();

            bool alreadyIn = cid != 0 && _entries.Any(e => e.CID == cid);
            if (alreadyIn || cid == 0)
            {
                ImGui.BeginDisabled();
                ImGui.Button(alreadyIn ? $"已在黑名单##{cid}" : $"CID无效##{cid}");
                ImGui.EndDisabled();
            }
            else
            {
                if (ImGui.Button($"加入黑名单##{cid}"))
                {
                    _entries.Add(new Entry { Name = name, CID = cid });
                    Save();
                }
            }
        }

        // 黑名单表格
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(new Vector4(1, 0.6f, 0.2f, 1), "黑名单列表");

        if (ImGui.Button("添加空条目"))
        {
            _entries.Add(new Entry());
            Save();
        }

        if (ImGui.BeginTable("##BlackListTable", 5, ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 120f * scale);
            ImGui.TableSetupColumn("CID", ImGuiTableColumnFlags.WidthFixed, 160f * scale);
            ImGui.TableSetupColumn("Comment", ImGuiTableColumnFlags.WidthFixed, 240f * scale);
            ImGui.TableSetupColumn("RiskLv", ImGuiTableColumnFlags.WidthFixed, 60f * scale);
            ImGui.TableSetupColumn("操作");
            ImGui.TableHeadersRow();

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var nameBuf = e.Name;
                if (ImGui.InputText($"##Name{i}", ref nameBuf, 64))
                {
                    e.Name = nameBuf;
                    Save();
                }

                ImGui.TableNextColumn();
                var cidStr = e.CID.ToString();
                if (ImGui.InputText($"##CID{i}", ref cidStr, 32) && ulong.TryParse(cidStr, out var val))
                {
                    e.CID = val;
                    Save();
                }

                ImGui.TableNextColumn();
                var commentBuf = e.Comment;
                if (ImGui.InputText($"##Comment{i}", ref commentBuf, 128))
                {
                    e.Comment = commentBuf;
                    Save();
                }

                ImGui.TableNextColumn();
                int sel = Math.Clamp(e.RiskLv - 1, 0, 4);
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