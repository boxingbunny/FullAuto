using Dalamud.Bindings.ImGui;
using AutoRaidHelper.Settings;
using System.Numerics;

namespace AutoRaidHelper.UI;

/// <summary>
/// LootRollingTab 用于展示和管理战利品 Roll 点功能的 UI
/// 移植自 LazyLoot 插件的功能
/// </summary>
public class LootRollingTab
{
    /// <summary>
    /// 获取全局单例 FullAutoSettings 中保存的 LootRollingSettings 配置实例
    /// </summary>
    public LootRollingSettings Settings => FullAutoSettings.Instance.LootRollingSettings;

    /// <summary>
    /// 绘制 LootRollingTab 的 UI 界面
    /// </summary>
    public void Draw()
    {
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "箱子自动Roll点");
        ImGui.Separator();
        ImGui.Spacing();

        // 命令说明
        if (ImGui.CollapsingHeader("命令说明"))
        {
            ImGui.Indent();
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "批量 Roll 命令：");
            ImGui.BulletText("/arh need - 一键 需求 所有物品");
            ImGui.BulletText("/arh greed - 一键 贪婪 所有物品");
            ImGui.BulletText("/arh pass - 一键 放弃 所有物品");
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "自动 Roll 命令：");
            ImGui.BulletText("/arh autoroll - 开启/关闭自动 Roll");
            ImGui.BulletText("/arh autoroll on - 开启自动 Roll");
            ImGui.BulletText("/arh autoroll off - 关闭自动 Roll");
            ImGui.BulletText("/arh autoroll need - 设置自动 需求");
            ImGui.BulletText("/arh autoroll greed - 设置自动 贪婪");
            ImGui.BulletText("/arh autoroll pass - 设置自动 放弃");
            ImGui.Unindent();
            ImGui.Spacing();
        }

        ImGui.Separator();
        ImGui.Spacing();

        // 基础设置
        if (ImGui.CollapsingHeader("基础设置", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();

            // 自动 Roll 开关
            var autoRollEnabled = Settings.AutoRollEnabled;
            if (ImGui.Checkbox("启用自动 Roll", ref autoRollEnabled))
            {
                Settings.AutoRollEnabled = autoRollEnabled;
                FullAutoSettings.Instance.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("开启后，战利品出现时会自动按照设定的模式 Roll 点");
            }

            // 自动 Roll 模式
            if (Settings.AutoRollEnabled)
            {
                ImGui.Indent();
                var autoRollMode = Settings.AutoRollMode;
                if (ImGui.RadioButton("自动 需求", ref autoRollMode, 0))
                {
                    Settings.AutoRollMode = 0;
                    FullAutoSettings.Instance.Save();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("自动 贪婪", ref autoRollMode, 1))
                {
                    Settings.AutoRollMode = 1;
                    FullAutoSettings.Instance.Save();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("自动 放弃", ref autoRollMode, 2))
                {
                    Settings.AutoRollMode = 2;
                    FullAutoSettings.Instance.Save();
                }
                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Roll 延迟设置
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Roll 延迟设置（秒）：");

            var minDelay = Settings.MinRollDelayInSeconds;
            if (ImGui.SliderFloat("最小延迟##manual", ref minDelay, 0.1f, 5.0f, "%.1f"))
            {
                Settings.MinRollDelayInSeconds = minDelay;
                FullAutoSettings.Instance.Save();
            }

            var maxDelay = Settings.MaxRollDelayInSeconds;
            if (ImGui.SliderFloat("最大延迟##manual", ref maxDelay, 0.1f, 5.0f, "%.1f"))
            {
                Settings.MaxRollDelayInSeconds = maxDelay;
                FullAutoSettings.Instance.Save();
            }

            ImGui.Spacing();

            var autoMinDelay = Settings.AutoRollMinDelayInSeconds;
            if (ImGui.SliderFloat("自动 Roll 最小延迟", ref autoMinDelay, 0.5f, 10.0f, "%.1f"))
            {
                Settings.AutoRollMinDelayInSeconds = autoMinDelay;
                FullAutoSettings.Instance.Save();
            }

            var autoMaxDelay = Settings.AutoRollMaxDelayInSeconds;
            if (ImGui.SliderFloat("自动 Roll 最大延迟", ref autoMaxDelay, 0.5f, 10.0f, "%.1f"))
            {
                Settings.AutoRollMaxDelayInSeconds = autoMaxDelay;
                FullAutoSettings.Instance.Save();
            }

            ImGui.Unindent();
            ImGui.Spacing();
        }

        // 输出设置
        if (ImGui.CollapsingHeader("输出设置"))
        {
            ImGui.Indent();

            var enableChatLog = Settings.EnableChatLogMessage;
            if (ImGui.Checkbox("聊天框消息", ref enableChatLog))
            {
                Settings.EnableChatLogMessage = enableChatLog;
                FullAutoSettings.Instance.Save();
            }

            var enableQuestToast = Settings.EnableQuestToast;
            if (ImGui.Checkbox("任务提示", ref enableQuestToast))
            {
                Settings.EnableQuestToast = enableQuestToast;
                FullAutoSettings.Instance.Save();
            }

            var enableNormalToast = Settings.EnableNormalToast;
            if (ImGui.Checkbox("普通提示", ref enableNormalToast))
            {
                Settings.EnableNormalToast = enableNormalToast;
                FullAutoSettings.Instance.Save();
            }

            var enableErrorToast = Settings.EnableErrorToast;
            if (ImGui.Checkbox("错误提示", ref enableErrorToast))
            {
                Settings.EnableErrorToast = enableErrorToast;
                FullAutoSettings.Instance.Save();
            }

            ImGui.Unindent();
            ImGui.Spacing();
        }

        // 智能过滤规则
        if (ImGui.CollapsingHeader("智能过滤规则"))
        {
            ImGui.Indent();
            ImGui.TextWrapped("以下规则会自动 放弃 符合条件的物品：");
            ImGui.Spacing();

            // 装等过滤
            var ignoreItemLevel = Settings.RestrictionIgnoreItemLevelBelow;
            if (ImGui.Checkbox("放弃 低于指定装等的物品", ref ignoreItemLevel))
            {
                Settings.RestrictionIgnoreItemLevelBelow = ignoreItemLevel;
                FullAutoSettings.Instance.Save();
            }
            if (Settings.RestrictionIgnoreItemLevelBelow)
            {
                ImGui.Indent();
                var itemLevelValue = Settings.RestrictionIgnoreItemLevelBelowValue;
                if (ImGui.InputInt("装等阈值", ref itemLevelValue))
                {
                    Settings.RestrictionIgnoreItemLevelBelowValue = itemLevelValue;
                    FullAutoSettings.Instance.Save();
                }
                ImGui.Unindent();
            }

            ImGui.Spacing();

            // 已解锁物品
            var ignoreUnlocked = Settings.RestrictionIgnoreItemUnlocked;
            if (ImGui.Checkbox("放弃 所有已解锁的物品", ref ignoreUnlocked))
            {
                Settings.RestrictionIgnoreItemUnlocked = ignoreUnlocked;
                FullAutoSettings.Instance.Save();
            }

            var ignoreMounts = Settings.RestrictionIgnoreMounts;
            if (ImGui.Checkbox("放弃 已解锁的坐骑", ref ignoreMounts))
            {
                Settings.RestrictionIgnoreMounts = ignoreMounts;
                FullAutoSettings.Instance.Save();
            }

            var ignoreMinions = Settings.RestrictionIgnoreMinions;
            if (ImGui.Checkbox("放弃 已解锁的宠物", ref ignoreMinions))
            {
                Settings.RestrictionIgnoreMinions = ignoreMinions;
                FullAutoSettings.Instance.Save();
            }

            var ignoreOrch = Settings.RestrictionIgnoreOrchestrionRolls;
            if (ImGui.Checkbox("放弃 已解锁的管弦乐琴乐谱", ref ignoreOrch))
            {
                Settings.RestrictionIgnoreOrchestrionRolls = ignoreOrch;
                FullAutoSettings.Instance.Save();
            }

            var ignoreTTCards = Settings.RestrictionIgnoreTripleTriadCards;
            if (ImGui.Checkbox("放弃 已解锁的九宫幻卡", ref ignoreTTCards))
            {
                Settings.RestrictionIgnoreTripleTriadCards = ignoreTTCards;
                FullAutoSettings.Instance.Save();
            }

            var ignoreEmote = Settings.RestrictionIgnoreEmoteHairstyle;
            if (ImGui.Checkbox("放弃 已解锁的表情/发型", ref ignoreEmote))
            {
                Settings.RestrictionIgnoreEmoteHairstyle = ignoreEmote;
                FullAutoSettings.Instance.Save();
            }

            var ignoreBardings = Settings.RestrictionIgnoreBardings;
            if (ImGui.Checkbox("放弃 已解锁的陆行鸟装甲", ref ignoreBardings))
            {
                Settings.RestrictionIgnoreBardings = ignoreBardings;
                FullAutoSettings.Instance.Save();
            }

            var ignoreFaded = Settings.RestrictionIgnoreFadedCopy;
            if (ImGui.Checkbox("放弃 已解锁的褪色乐谱", ref ignoreFaded))
            {
                Settings.RestrictionIgnoreFadedCopy = ignoreFaded;
                FullAutoSettings.Instance.Save();
            }

            ImGui.Spacing();

            // 职业相关
            var ignoreOtherJob = Settings.RestrictionOtherJobItems;
            if (ImGui.Checkbox("放弃 当前职业无法使用的物品", ref ignoreOtherJob))
            {
                Settings.RestrictionOtherJobItems = ignoreOtherJob;
                FullAutoSettings.Instance.Save();
            }

            // 周常限制
            var ignoreWeekly = Settings.RestrictionWeeklyLockoutItems;
            if (ImGui.Checkbox("放弃 周常限制物品", ref ignoreWeekly))
            {
                Settings.RestrictionWeeklyLockoutItems = ignoreWeekly;
                FullAutoSettings.Instance.Save();
            }

            ImGui.Spacing();

            // 装等相关
            var lowerThanJob = Settings.RestrictionLootLowerThanJobIlvl;
            if (ImGui.Checkbox("放弃 低于职业平均装等的物品", ref lowerThanJob))
            {
                Settings.RestrictionLootLowerThanJobIlvl = lowerThanJob;
                FullAutoSettings.Instance.Save();
            }
            if (Settings.RestrictionLootLowerThanJobIlvl)
            {
                ImGui.Indent();
                var threshold = Settings.RestrictionLootLowerThanJobIlvlTreshold;
                if (ImGui.InputInt("装等差值阈值", ref threshold))
                {
                    Settings.RestrictionLootLowerThanJobIlvlTreshold = threshold;
                    FullAutoSettings.Instance.Save();
                }
                ImGui.Unindent();
            }

            var isUpgrade = Settings.RestrictionLootIsJobUpgrade;
            if (ImGui.Checkbox("放弃 非升级装备", ref isUpgrade))
            {
                Settings.RestrictionLootIsJobUpgrade = isUpgrade;
                FullAutoSettings.Instance.Save();
            }

            ImGui.Spacing();

            // 军票价值
            var seals = Settings.RestrictionSeals;
            if (ImGui.Checkbox("放弃 军票价值低于指定值的物品", ref seals))
            {
                Settings.RestrictionSeals = seals;
                FullAutoSettings.Instance.Save();
            }
            if (Settings.RestrictionSeals)
            {
                ImGui.Indent();
                var sealsAmnt = Settings.RestrictionSealsAmnt;
                if (ImGui.InputInt("军票价值阈值", ref sealsAmnt))
                {
                    Settings.RestrictionSealsAmnt = sealsAmnt;
                    FullAutoSettings.Instance.Save();
                }
                ImGui.Unindent();
            }

            ImGui.Unindent();
            ImGui.Spacing();
        }

        // 高级设置
        if (ImGui.CollapsingHeader("高级设置"))
        {
            ImGui.Indent();

            var diagnostics = Settings.DiagnosticsMode;
            if (ImGui.Checkbox("诊断模式", ref diagnostics))
            {
                Settings.DiagnosticsMode = diagnostics;
                FullAutoSettings.Instance.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("开启后会在聊天框输出详细的 Roll 点决策信息");
            }

            var noEmergency = Settings.NoPassEmergency;
            if (ImGui.Checkbox("禁用紧急 放弃", ref noEmergency))
            {
                Settings.NoPassEmergency = noEmergency;
                FullAutoSettings.Instance.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("禁用后，如果 Roll 失败将不会自动 放弃");
            }

            ImGui.Unindent();
        }
    }
}