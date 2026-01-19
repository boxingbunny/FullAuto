using AEAssist;
using AEAssist.Extension;
using AEAssist.Helper;
using AutoRaidHelper.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using System.Numerics;

namespace AutoRaidHelper.UI
{
    public class FaManualTab
    {
        private readonly string[] _roles = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
        private readonly bool[] _roleSelected = new bool[8];
        private bool _lbTriggered;
        private DateTime _lastCmdTime = DateTime.MinValue;
        private string? _pendingLbRole;
        private string _transferLeaderTarget = "";

        public void Update()
        {
            if (!_lbTriggered || string.IsNullOrEmpty(_pendingLbRole))
                return;

            var currentLimitBreakBars = Core.Me.LimitBreakCurrentValue() / 10000;
            if (currentLimitBreakBars < 3)
            {
                _lbTriggered = false;
                _pendingLbRole = null;
                return;
            }

            if ((DateTime.Now - _lastCmdTime).TotalMilliseconds >= 100)
            {
                RemoteControlHelper.Cmd(_pendingLbRole, "/ac 极限技 <t>");
                _lastCmdTime = DateTime.Now;
            }
        }

        public void Draw()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 6f));
            DrawSectionTitle(FontAwesomeIcon.Users, "对象选择");

            // 两行：第一行文字，第二行圆点（对齐）
            if (ImGui.BeginTable("##RoleSelectTable", _roles.Length, ImGuiTableFlags.SizingFixedFit))
            {
                var roleNameMap = BuildRoleNameMap();
                float colWidth = 38f;
                for (int i = 0; i < _roles.Length; i++)
                {
                    ImGui.TableSetupColumn($"##RoleCol{i}", ImGuiTableColumnFlags.WidthFixed, colWidth);
                }

                ImGui.TableNextRow();
                for (int i = 0; i < _roles.Length; i++)
                {
                    ImGui.TableSetColumnIndex(i);
                    var text = _roles[i];
                    float textWidth = ImGui.CalcTextSize(text).X;
                    float cellX = ImGui.GetCursorPosX();
                    float centerX = cellX + colWidth * 0.5f;
                    ImGui.SetCursorPosX(centerX - textWidth * 0.5f);
                    ImGui.TextColored(GetRoleColor(text), text);
                    if (ImGui.IsItemHovered() && roleNameMap.TryGetValue(text, out var name) && name is not null)
                        ImGui.SetTooltip(name);
                }

                ImGui.TableNextRow();
                for (int i = 0; i < _roles.Length; i++)
                {
                    ImGui.TableSetColumnIndex(i);
                    float cellX = ImGui.GetCursorPosX();
                    float centerX = cellX + colWidth * 0.5f;
                    ImGui.SetCursorPosX(centerX - 32f * 0.5f);
                    DrawRoleDot(_roles[i], ref _roleSelected[i]);
                }

                ImGui.EndTable();
            }

            // 第三行：快捷按钮
            DrawColoredButton("全选", WarningColor, ToggleAll);
            ImGui.SameLine();
            DrawColoredButton("双T", TankColor, () => ToggleRoles(0, 1));
            ImGui.SameLine();
            DrawColoredButton("双H", HealerColor, () => ToggleRoles(2, 3));
            ImGui.SameLine();
            DrawColoredButton("DPS", DpsColor, () => ToggleRoles(4, 5, 6, 7));
            ImGui.SameLine();
            if (ImGui.Button("取消选择"))
            {
                SetAll(false);
            }

            SectionSpacing();

            DrawSectionTitle(FontAwesomeIcon.LocationArrow, "移动操作");

            if (ImGui.Button("全员TP至本机"))
            {
                var me = Core.Me;
                RemoteControlHelper.SetPos("", me.Position);
            }
            ImGui.SameLine();
            if (ImGui.Button("当前目标八方"))
            {
                var targetObj = Core.Me.TargetObject;
                if (targetObj == null)
                    return;

                var party = PartyHelper.Party.Where(_ => true).ToList();
                if (party.Count == 0)
                    return;

                var center = targetObj.Position;
                var radius = MathF.Max(1f, targetObj.HitboxRadius + 2f);
                Utilities.Protean(center, radius, party);
            }
            ImGui.SameLine();
            if (ImGui.Button("D找TH"))
            {
                var roleMap = BuildRoleMap();
                if (roleMap.TryGetValue("MT", out var mt)) RemoteControlHelper.SetPos("D1", mt.Position);
                if (roleMap.TryGetValue("ST", out var st)) RemoteControlHelper.SetPos("D2", st.Position);
                if (roleMap.TryGetValue("H1", out var h1)) RemoteControlHelper.SetPos("D3", h1.Position);
                if (roleMap.TryGetValue("H2", out var h2)) RemoteControlHelper.SetPos("D4", h2.Position);
            }
            ImGui.SameLine();
            if (ImGui.Button("双奶分摊"))
            {
                var roleMap = BuildRoleMap();
                if (roleMap.TryGetValue("H1", out var h1)) RemoteControlHelper.SetPos("MT|D1|D3", h1.Position);
                if (roleMap.TryGetValue("H2", out var h2)) RemoteControlHelper.SetPos("ST|D2|D4", h2.Position);
            }

            SectionSpacing();

            DrawSectionTitle(FontAwesomeIcon.Bolt, "LB操作");

            DrawColoredButton("TLB", TankColor, () =>
            {
                var role = SelectTankForLb();
                if (!string.IsNullOrEmpty(role))
                    StartLimitBreak(role);
            });
            ImGui.SameLine();
            DrawColoredButton("HLB", HealerColor, () =>
            {
                var role = SelectHealerForLb();
                if (!string.IsNullOrEmpty(role))
                    StartLimitBreak(role);
            });
            ImGui.SameLine();
            DrawColoredButton("近战LB", DpsColor, () =>
            {
                var role = SelectMeleeDpsForLb();
                if (!string.IsNullOrEmpty(role))
                    StartLimitBreak(role);
            });

            SectionSpacing();

            DrawSectionTitle(FontAwesomeIcon.ShieldAlt, "DR相关");

            if (ImGui.Button("全员开启无敌模式"))
            {
                RemoteControlHelper.Cmd("","/pdr load InvulnerableMode");
            }
            ImGui.SameLine();
            if (ImGui.Button("全员无敌"))
            {
                RemoteControlHelper.Cmd("","/pdr invulnerable");
            }
            ImGui.SameLine();
            if (ImGui.Button("选中队员无敌"))
            {
                var selected = BuildSelectedRoleRegex();
                if (!string.IsNullOrEmpty(selected))
                    RemoteControlHelper.Cmd(selected, "/pdr invulnerable");
            }

            SectionSpacing();

            DrawSectionTitle(FontAwesomeIcon.Crown, "队长转移");
            DrawPartyLeaderTransferSection();

            ImGui.PopStyleVar();
        }

        private void SetAll(bool value)
        {
            Array.Fill(_roleSelected, value);
        }

        private void ToggleAll()
        {
            bool allSelected = _roleSelected.All(v => v);
            SetAll(!allSelected);
        }

        private void ToggleRoles(params int[] indices)
        {
            bool allSelected = indices.All(i => i >= 0 && i < _roleSelected.Length && _roleSelected[i]);
            bool targetValue = !allSelected;
            foreach (var i in indices)
            {
                if (i < 0 || i >= _roleSelected.Length)
                    continue;
                _roleSelected[i] = targetValue;
            }
        }

        private static Dictionary<string, IBattleChara> BuildRoleMap()
        {
            var map = new Dictionary<string, IBattleChara>();
            foreach (var member in PartyHelper.Party)
            {
                var role = RemoteControlHelper.GetRoleByPlayerName(member.Name.ToString());
                if (string.IsNullOrEmpty(role))
                    continue;
                map[role] = member;
            }
            return map;
        }

        private static Dictionary<string, string> BuildRoleNameMap()
        {
            var map = new Dictionary<string, string>();
            foreach (var member in PartyHelper.Party)
            {
                if (member is null)
                    continue;
                var role = RemoteControlHelper.GetRoleByPlayerName(member.Name.ToString());
                if (string.IsNullOrEmpty(role))
                    continue;
                map[role] = member.Name.ToString();
            }
            return map;
        }

        private string BuildSelectedRoleRegex()
        {
            var selected = new List<string>();
            for (int i = 0; i < _roles.Length; i++)
            {
                if (_roleSelected[i])
                    selected.Add(_roles[i]);
            }
            return string.Join("|", selected);
        }

        private static bool IsAlive(IBattleChara member)
        {
            return member.CurrentHp > 0;
        }

        private static string? SelectTankForLb()
        {
            var roleMap = BuildRoleMap();
            if (roleMap.TryGetValue("MT", out var mt) && IsAlive(mt))
                return "MT";
            if (roleMap.TryGetValue("ST", out var st) && IsAlive(st))
                return "ST";
            return null;
        }

        private static string? SelectHealerForLb()
        {
            var roleMap = BuildRoleMap();
            if (roleMap.TryGetValue("H1", out var h1) && IsAlive(h1))
                return "H1";
            if (roleMap.TryGetValue("H2", out var h2) && IsAlive(h2))
                return "H2";
            return null;
        }

        private static string? SelectMeleeDpsForLb()
        {
            var roleMap = BuildRoleMap();
            string[] order = { "D1", "D2", "D3", "D4" };
            foreach (var role in order)
            {
                if (!roleMap.TryGetValue(role, out var member))
                    continue;
                if (!IsAlive(member))
                    continue;
                if (PartyHelper.CastableMelees.Any(m => m.EntityId == member.EntityId))
                    return role;
            }
            return null;
        }

        private void StartLimitBreak(string role)
        {
            var currentLimitBreakBars = Core.Me.LimitBreakCurrentValue() / 10000;
            if (currentLimitBreakBars < 3)
                return;

            _pendingLbRole = role;
            _lbTriggered = true;
            _lastCmdTime = DateTime.Now;
            RemoteControlHelper.Cmd(role, "/ac 极限技 <t>");
        }

        private static readonly Vector4 TitleColor = new(0.85f, 0.85f, 0.95f, 1f);
        private static readonly Vector4 TankColor = new(0.35f, 0.65f, 1f, 1f);
        private static readonly Vector4 HealerColor = new(0.35f, 0.85f, 0.45f, 1f);
        private static readonly Vector4 DpsColor = new(0.95f, 0.3f, 0.3f, 1f);
        private static readonly Vector4 WarningColor = new(0.98f, 0.82f, 0.5f, 1f);

        private static void DrawSectionTitle(FontAwesomeIcon icon, string title)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(icon.ToIconString());
            ImGui.PopFont();
            ImGui.SameLine(0, 6f);
            ImGui.TextColored(TitleColor, title);
        }

        private static void SectionSpacing()
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        private static void DrawColoredButton(string label, Vector4 color, Action onClick)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            if (ImGui.Button(label))
                onClick();
            ImGui.PopStyleColor();
        }

        private static Vector4 GetRoleColor(string role)
        {
            return role switch
            {
                "MT" or "ST" => TankColor,
                "H1" or "H2" => HealerColor,
                _ => DpsColor
            };
        }

        private static void DrawRoleDot(string role, ref bool value)
        {
            var drawList = ImGui.GetWindowDrawList();
            var color = GetRoleColor(role);
            var pos = ImGui.GetCursorScreenPos();
            float hitSize = 32f;
            float size = 18f;
            float radius = size * 0.5f;
            var center = new Vector2(pos.X + hitSize * 0.5f, pos.Y + hitSize * 0.5f);

            ImGui.InvisibleButton($"##roleDot_{role}", new Vector2(hitSize, hitSize));
            if (ImGui.IsItemClicked())
                value = !value;

            uint fill = ImGui.ColorConvertFloat4ToU32(value ? color : new Vector4(0.12f, 0.12f, 0.12f, 1f));
            uint outline = ImGui.ColorConvertFloat4ToU32(new Vector4(0.25f, 0.25f, 0.25f, 1f));

            drawList.AddCircleFilled(center, radius, fill);
            drawList.AddCircle(center, radius, outline, 16, 1.0f);
        }

        private void DrawPartyLeaderTransferSection()
        {
            const float comboWidth = 150f;

            // 显示当前队长状态
            var currentLeader = PartyLeaderHelper.GetPartyLeaderName();
            var isLeader = PartyLeaderHelper.IsLocalPlayerPartyLeader();

            if (!string.IsNullOrEmpty(currentLeader))
            {
                ImGui.SameLine(0, 6f);
                var leaderColor = isLeader ? HealerColor : DpsColor;
                ImGui.TextColored(leaderColor, $"当前队长: {currentLeader}");
            }

            ImGui.Spacing();

            // 获取所有在线成员（除了队长）
            var partyStatus = PartyLeaderHelper.GetCrossRealmPartyStatus();
            var validTargets = partyStatus
                .Where(m => m.IsOnline)
                .Where(m => !string.Equals(m.Name, currentLeader, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Name)
                .ToList();

            // 检查是否有可转移目标
            if (validTargets.Count == 0)
            {
                ImGui.TextColored(WarningColor, "没有可转移的目标");
                return;
            }

            // 目标选择下拉框
            ImGui.SetNextItemWidth(comboWidth);
            if (ImGui.BeginCombo("##TransferLeader", string.IsNullOrEmpty(_transferLeaderTarget) ? "选择玩家..." : _transferLeaderTarget))
            {
                foreach (var target in validTargets)
                {
                    bool isSelected = _transferLeaderTarget == target;
                    if (ImGui.Selectable(target, isSelected))
                    {
                        _transferLeaderTarget = target;
                    }
                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine();

            // 转移队长按钮
            if (ImGui.Button("转移队长"))
            {
                ExecuteTransfer();
            }
        }

        private void ExecuteTransfer()
        {
            if (string.IsNullOrEmpty(_transferLeaderTarget))
            {
                LogHelper.Print("请先选择目标玩家");
                return;
            }

            PartyLeaderHelper.TransferPartyLeader(_transferLeaderTarget);
            _transferLeaderTarget = "";
        }

    }
}
