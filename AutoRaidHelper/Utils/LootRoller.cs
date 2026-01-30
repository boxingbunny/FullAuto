using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using Lumina.Excel.Sheets;
using System.Runtime.InteropServices;
using AutoRaidHelper.Settings;

namespace AutoRaidHelper.Utils;

internal static class LootRoller
{
    unsafe delegate bool RollItemRaw(Loot* lootIntPtr, RollResult option, uint lootItemIndex);

    static RollItemRaw? _rollItemRaw;

    static uint _itemId = 0, _index = 0;

    public static void Clear()
    {
        _itemId = _index = 0;
    }

    /// <summary>
    /// Roll 一个物品，返回是否还有更多物品需要 Roll
    /// </summary>
    public static bool RollOneItem(RollResult option, ref int need, ref int greed, ref int pass)
    {
        if (!GetNextLootItem(out var index, out var loot))
            return false;

        // 获取用户自定义规则
        var userRules = GetPlayerCustomRestrict(loot);
        option = userRules != null
            ? ResultMerge(GetRestrictResult(loot), (RollResult)userRules)
            : ResultMerge(option, GetRestrictResult(loot), GetPlayerRestrict(loot));

        // 紧急 Pass 机制：如果同一个物品 Roll 失败，自动 Pass
        if (_itemId == loot.ItemId && index == _index)
        {
            var settings = FullAutoSettings.Instance.LootRollingSettings;
            if (settings.DiagnosticsMode && !settings.NoPassEmergency)
                Svc.Log.Debug(
                    $"{Svc.Data.GetExcelSheet<Item>().GetRow(loot.ItemId).Name.ToString()} has failed to roll for some reason. Passing for safety. [Emergency pass]");

            if (!settings.NoPassEmergency)
            {
                switch (option)
                {
                    case RollResult.Needed:
                        need--;
                        break;
                    case RollResult.Greeded:
                        greed--;
                        break;
                    default:
                        pass--;
                        break;
                }

                option = RollResult.Passed;
            }
        }

        RollItem(option, index);
        _itemId = loot.ItemId;
        _index = index;

        switch (option)
        {
            case RollResult.Needed:
                need++;
                break;
            case RollResult.Greeded:
                greed++;
                break;
            default:
                pass++;
                break;
        }

        return true;
    }

    private static RollResult GetRestrictResult(LootItem loot)
    {
        var item = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(loot.ItemId);
        if (item == null)
            return RollResult.Passed;

        // 检查物品的最大可 Roll 类型
        var stateMax = loot.RollState switch
        {
            RollState.UpToNeed => RollResult.Needed,
            RollState.UpToGreed => RollResult.Greeded,
            _ => RollResult.Passed,
        };

        if (item.Value.IsUnique && IsItemUnlocked(loot.ItemId))
            stateMax = RollResult.Passed;

        var settings = FullAutoSettings.Instance.LootRollingSettings;
        if (settings.DiagnosticsMode && stateMax == RollResult.Passed)
            Svc.Log.Debug($"{item.Value.Name.ToString()} can only be passed on. [RollState UpToPass]");

        // 检查玩家设置的战利品规则
        var ruleMax = loot.LootMode switch
        {
            LootMode.Normal => RollResult.Needed,
            LootMode.GreedOnly => RollResult.Greeded,
            _ => RollResult.Passed,
        };

        return ResultMerge(stateMax, ruleMax);
    }

    private static unsafe RollResult? GetPlayerCustomRestrict(LootItem loot)
    {
        var lootItem = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(loot.ItemId);
        if (lootItem == null || (lootItem.Value.IsUnique && ItemCount(loot.ItemId) > 0))
            return RollResult.Passed;

        var settings = FullAutoSettings.Instance.LootRollingSettings;

        // 检查物品特定规则
        var itemCustomRestriction =
            settings.Restrictions.Items.FirstOrDefault(x => x.Id == loot.ItemId);
        if (itemCustomRestriction is { Enabled: true })
        {
            if (settings.DiagnosticsMode)
            {
                var action = itemCustomRestriction.RollRule == RollResult.Passed ? "passing" :
                    itemCustomRestriction.RollRule == RollResult.Greeded ? "greeding" :
                    itemCustomRestriction.RollRule == RollResult.Needed ? "needing" : "passing";
                Svc.Log.Debug($"{lootItem.Value.Name.ToString()} is {action}. [Item Custom Restriction]");
            }

            return itemCustomRestriction.RollRule;
        }

        // 检查副本特定规则
        var contentFinderInfo = Svc.Data.GetExcelSheet<ContentFinderCondition>()
            .GetRow(GameMain.Instance()->CurrentContentFinderConditionId);
        var dutyCustomRestriction =
            settings.Restrictions.Duties.FirstOrDefault(x => x.Id == contentFinderInfo.RowId);
        if (dutyCustomRestriction is { Enabled: true })
        {
            if (settings.DiagnosticsMode)
            {
                var action = dutyCustomRestriction.RollRule == RollResult.Passed ? "passing" :
                    dutyCustomRestriction.RollRule == RollResult.Greeded ? "greeding" :
                    dutyCustomRestriction.RollRule == RollResult.Needed ? "needing" : "passing";
                Svc.Log.Debug(
                    $"{lootItem.Value.Name.ToString()} is {action} due to being in {contentFinderInfo.Name}. [Duty Custom Restriction]");
            }

            return dutyCustomRestriction.RollRule;
        }

        return null;
    }

    private static unsafe RollResult GetPlayerRestrict(LootItem loot)
    {
        var lootItem = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(loot.ItemId);
        var settings = FullAutoSettings.Instance.LootRollingSettings;

        UpdateFadedCopy(loot.ItemId, out var orchId);

        if (lootItem == null)
        {
            if (settings.DiagnosticsMode)
                Svc.Log.Debug(
                    $"Passing due to unknown item? Please give this ID to the developers: {loot.ItemId} [Unknown ID]");
            return RollResult.Passed;
        }

        if (lootItem.Value.IsUnique && ItemCount(loot.ItemId) > 0)
        {
            if (settings.DiagnosticsMode)
                Svc.Log.Debug(
                    $"{lootItem.Value.Name.ToString()} has been passed due to being unique and you already possess one. [Unique Item]");

            return RollResult.Passed;
        }

        if (orchId.Count > 0 && orchId.All(x => IsItemUnlocked(x)))
        {
            if (settings.RestrictionIgnoreItemUnlocked)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on all items already unlocked"" enabled. [Pass All Unlocked]");

                return RollResult.Passed;
            }

            if (settings.RestrictionIgnoreFadedCopy
                && lootItem.Value.FilterGroup == 12 && lootItem.Value.ItemUICategory.RowId == 94)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Faded Copies"" enabled. [Pass Faded Copies]");

                return RollResult.Passed;
            }
        }

        if (IsItemUnlocked(loot.ItemId))
        {
            if (settings.RestrictionIgnoreItemUnlocked)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on all items already unlocked"" enabled. [Pass All Unlocked]");

                return RollResult.Passed;
            }

            if (settings.RestrictionIgnoreMounts && lootItem.Value.ItemAction.Value.Action.Value.RowId == 1322)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Mounts"" enabled. [Pass Mounts]");

                return RollResult.Passed;
            }

            if (settings.RestrictionIgnoreMinions && lootItem.Value.ItemAction.Value.Action.Value.RowId == 853)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Minions"" enabled. [Pass Minions]");

                return RollResult.Passed;
            }

            if (settings.RestrictionIgnoreBardings
                && lootItem.Value.ItemAction.Value.Action.Value.RowId == 1013)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Bardings"" enabled. [Pass Bardings]");

                return RollResult.Passed;
            }

            if (settings.RestrictionIgnoreEmoteHairstyle
                && lootItem.Value.ItemAction.Value.Action.Value.RowId == 2633)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Emotes and Hairstyles"" enabled. [Pass Emotes/Hairstyles]");

                return RollResult.Passed;
            }

            if (settings.RestrictionIgnoreTripleTriadCards
                && lootItem.Value.ItemAction.Value.Action.Value.RowId == 3357)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Triple Triad cards"" enabled. [Pass TTCards]");

                return RollResult.Passed;
            }

            if (settings.RestrictionIgnoreOrchestrionRolls
                && lootItem.Value.ItemAction.Value.Action.Value.RowId == 25183)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Orchestrion Rolls"" enabled. [Pass Orchestrion]");

                return RollResult.Passed;
            }
        }

        if (settings.RestrictionSeals)
        {
            if (lootItem.Value.Rarity > 1 && lootItem.Value.PriceLow > 0 && lootItem.Value.ClassJobCategory.RowId > 0)
            {
                var gcSealValue = Svc.Data.Excel.GetSheet<GCSupplyDutyReward>()?.GetRow(lootItem.Value.LevelItem.RowId)
                    .SealsExpertDelivery;
                if (gcSealValue < settings.RestrictionSealsAmnt)
                {
                    if (settings.DiagnosticsMode)
                        Svc.Log.Debug(
                            $@"{lootItem.Value.Name.ToString()} has been passed due to selling for less than {settings.RestrictionSealsAmnt} seals. [Pass Seals]");

                    return RollResult.Passed;
                }
            }
        }

        if (lootItem.Value.EquipSlotCategory.RowId != 0)
        {
            // 检查战利品装等是否低于玩家职业平均装等
            if (settings.RestrictionLootLowerThanJobIlvl && loot.RollState == RollState.UpToNeed)
            {
                if (lootItem.Value.LevelItem.RowId <
                    GetPlayerIlevel() - settings.RestrictionLootLowerThanJobIlvlTreshold)
                {
                    if (settings.DiagnosticsMode &&
                        settings.RestrictionLootLowerThanJobIlvlRollState != 0)
                        Svc.Log.Debug(
                            $@"{lootItem.Value.Name.ToString()} has been passed due to being below your average item level and you have set to pass items below your average job item level. [Pass Item Lower Than Average iLevel]");

                    return settings.RestrictionLootLowerThanJobIlvlRollState == 0
                        ? RollResult.Greeded
                        : RollResult.Passed;
                }
            }

            if (settings.RestrictionIgnoreItemLevelBelow
                && lootItem.Value.LevelItem.RowId < settings.RestrictionIgnoreItemLevelBelowValue)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to having ""Pass on items with an item level below"" enabled and {lootItem.Value.LevelItem.RowId} is less than {settings.RestrictionIgnoreItemLevelBelowValue}. [Pass Item Level]");

                return RollResult.Passed;
            }

            // 检查战利品是否为当前职业的升级装备
            if (settings.RestrictionLootIsJobUpgrade && loot.RollState == RollState.UpToNeed)
            {
                var lootItemSlot = lootItem.Value.EquipSlotCategory.RowId;
                var itemsToVerify = new List<uint>();
                var equippedItems =
                    InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
                for (var i = 0; i < equippedItems->Size; i++)
                {
                    var equippedItem = equippedItems->GetInventorySlot(i);
                    var equippedItemData = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(equippedItem->ItemId);
                    if (equippedItemData == null)
                        continue;
                    if (equippedItemData.Value.EquipSlotCategory.RowId != lootItemSlot)
                        continue;
                    // 收集同部位已装备物品的装等
                    itemsToVerify.Add(equippedItemData.Value.LevelItem.RowId);
                }

                // 如果已装备物品的最低装等高于掉落物品，按用户规则处理
                if (itemsToVerify.Count > 0 && itemsToVerify.Min() > lootItem.Value.LevelItem.RowId)
                {
                    if (settings.DiagnosticsMode && settings.RestrictionLootIsJobUpgradeRollState != 0)
                        Svc.Log.Debug(
                            $@"{lootItem.Value.Name.ToString()} has been passed due to being below the level of your current equipped item level and you have set to pass items below the level of your equipped item. [Pass Item if equipped is of higher level]");

                    return settings.RestrictionLootIsJobUpgradeRollState == 0
                        ? RollResult.Greeded
                        : RollResult.Passed;
                }
            }

            if (settings.RestrictionOtherJobItems
                && loot.RollState != RollState.UpToNeed)
            {
                if (settings.DiagnosticsMode)
                    Svc.Log.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to having ""Pass on items I can't use with current job"" and this item cannot be used with your current job. [Pass Other Job]");

                return RollResult.Passed;
            }
        }

        // PLD 套装特殊处理
        if (settings.RestrictionOtherJobItems
            && lootItem.Value.ItemAction.Value.Action.Value.RowId == 29153
            && !(Player.Object?.ClassJob.RowId is 1 or 19))
        {
            if (settings.DiagnosticsMode)
                Svc.Log.Debug(
                    $@"{lootItem.Value.Name.ToString()} has been passed due to having ""Pass on items I can't use with current job"" and this item cannot be used with your current job. [Pass Other Job (PLD Sets)]");

            return RollResult.Passed;
        }

        return RollResult.UnAwarded;
    }

    public static void UpdateFadedCopy(uint itemId, out List<uint> orchId)
    {
        orchId = new();
        var lumina = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        if (lumina != null)
        {
            if (lumina.Value.FilterGroup == 12 && lumina.Value.ItemUICategory.RowId == 94)
            {
                var recipe = Svc.Data.GetExcelSheet<Recipe>()
                    ?.Where(x => x.Ingredient.Any(y => y.RowId == lumina.Value.RowId)).Select(x => x.ItemResult.Value)
                    .FirstOrDefault();
                if (recipe != null)
                {
                    var settings = FullAutoSettings.Instance.LootRollingSettings;
                    if (settings.DiagnosticsMode)
                        Svc.Log.Debug(
                            $"Updating Faded Copy {lumina.Value.Name} ({itemId}) to Non-Faded {recipe.Value.Name} ({recipe.Value.RowId})");
                    orchId.Add(recipe.Value.RowId);
                    return;
                }
            }
        }
    }

    private static RollResult ResultMerge(params RollResult[] results)
        => results.Max() switch
        {
            RollResult.Needed => RollResult.Needed,
            RollResult.Greeded => RollResult.Greeded,
            _ => RollResult.Passed,
        };

    private static unsafe bool GetNextLootItem(out uint i, out LootItem loot)
    {
        var span = Loot.Instance()->Items;
        var settings = FullAutoSettings.Instance.LootRollingSettings;

        for (i = 0; i < span.Length; i++)
        {
            loot = span[(int)i];

            if (loot.ItemId >= 1000000)
                loot.ItemId -= 1000000;
            if (loot.ChestObjectId is 0 or 0xE0000000)
                continue;
            if (loot.RollResult != RollResult.UnAwarded)
                continue;
            if (loot.RollState is RollState.Rolled or RollState.Unavailable or RollState.Unknown)
                continue;
            if (loot.ItemId == 0)
                continue;
            if (loot.LootMode is LootMode.LootMasterGreedOnly or LootMode.Unavailable)
                continue;

            var checkWeekly = settings.RestrictionWeeklyLockoutItems;

            var lootId = loot.ItemId;
            var contentFinderInfo = Svc.Data.GetExcelSheet<ContentFinderCondition>()
                .GetRow(GameMain.Instance()->CurrentContentFinderConditionId);

            // 加载用户限制规则
            var itemCustomRestriction =
                settings.Restrictions.Items.FirstOrDefault(x =>
                    x.Id == lootId && x is { Enabled: true });
            var dutyCustomRestriction =
                settings.Restrictions.Duties.FirstOrDefault(x =>
                    x.Id == contentFinderInfo.RowId && x is { Enabled: true, RollRule: RollResult.UnAwarded });

            Item? item = null;

            if (settings.DiagnosticsMode)
                // 仅在诊断模式下加载物品
                item = Svc.Data.GetExcelSheet<Item>().GetRow(loot.ItemId);

            if (itemCustomRestriction != null)
            {
                if (itemCustomRestriction.RollRule == RollResult.UnAwarded)
                {
                    if (settings.DiagnosticsMode)
                        Svc.Log.Debug(
                            $"{item?.Name.ToString()} is being ignored. [Item Custom Restriction]");
                    continue;
                }

                checkWeekly = false;
            }

            if (itemCustomRestriction == null && dutyCustomRestriction != null)
            {
                if (dutyCustomRestriction.RollRule == RollResult.UnAwarded)
                {
                    if (settings.DiagnosticsMode)
                        Svc.Log.Debug(
                            $"{item?.Name.ToString()} is being ignored due to being in {contentFinderInfo.Name}. [Duty Custom Restriction]");
                    continue;
                }

                checkWeekly = false;
            }

            // loot.RollValue == 20 表示本周已获得，无法 Roll
            // 我们忽略它，让它自动 Pass，因为用户无法做其他操作
            if (loot.WeeklyLootItem && (byte)loot.RollState != 20 && checkWeekly)
                continue;

            return true;
        }

        loot = default;
        return false;
    }

    private static unsafe void RollItem(RollResult option, uint index)
    {
        try
        {
            _rollItemRaw ??=
                Marshal.GetDelegateForFunctionPointer<RollItemRaw>(
                    Svc.SigScanner.ScanText("41 83 F8 ?? 0F 83 ?? ?? ?? ?? 48 89 5C 24 08"));
            _rollItemRaw?.Invoke(Loot.Instance(), option, index);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Warning at roll");
        }
    }

    public static unsafe int ItemCount(uint itemId)
        => InventoryManager.Instance()->GetInventoryItemCount(itemId);

    public static unsafe bool IsItemUnlocked(uint itemId)
    {
        var exdItem = ExdModule.GetItemRowById(itemId);
        return exdItem is null || UIState.Instance()->IsItemActionUnlocked(exdItem) is 1;
    }

    public static uint ConvertSealsToIlvl(int sealAmnt)
    {
        var sealsSheet = Svc.Data.GetExcelSheet<GCSupplyDutyReward>();
        uint ilvl = 0;
        foreach (var row in sealsSheet)
        {
            if (row.SealsExpertDelivery < sealAmnt)
            {
                ilvl = row.RowId;
            }
        }

        return ilvl;
    }

    /// <summary>
    /// 获取玩家当前职业的平均装等
    /// </summary>
    private static unsafe int GetPlayerIlevel()
    {
        if (Player.Object == null) return 0;

        var equippedItems = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (equippedItems == null) return 0;

        var totalIlvl = 0;
        var count = 0;

        for (var i = 0; i < equippedItems->Size; i++)
        {
            var item = equippedItems->GetInventorySlot(i);
            if (item == null || item->ItemId == 0) continue;

            var itemData = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(item->ItemId);
            if (itemData == null) continue;

            totalIlvl += (int)itemData.Value.LevelItem.RowId;
            count++;
        }

        return count > 0 ? totalIlvl / count : 0;
    }
}