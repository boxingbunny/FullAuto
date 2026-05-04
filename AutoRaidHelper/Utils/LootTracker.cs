using AEAssist.Helper;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;

namespace AutoRaidHelper.Utils
{
    /// <summary>
    /// 监听聊天消息，记录副本内物品获得情况。
    /// </summary>
    public static class LootTracker
    {
        private static readonly List<LootRecord> LootRecords = new();
        private static bool _initialized;

        private sealed class LootRecord
        {
            public string ItemName { get; set; } = "";
            public string WinnerName { get; set; } = "";
            public DateTime Time { get; set; }
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                Svc.Chat.ChatMessage += OnChatMessage;
                _initialized = true;
            }
            catch (Exception ex)
            {
                LogHelper.PrintError($"[Roll追踪] 初始化失败: {ex.Message}");
            }
        }

        public static void Dispose()
        {
            if (!_initialized)
                return;

            try
            {
                Svc.Chat.ChatMessage -= OnChatMessage;
                _initialized = false;
                LootRecords.Clear();
            }
            catch (Exception ex)
            {
                LogHelper.PrintError($"[Roll追踪] 清理失败: {ex.Message}");
            }
        }

        private static void OnChatMessage(IHandleableChatMessage chatMessage)
        {
            try
            {
                if (!Svc.Condition[ConditionFlag.BoundByDuty])
                    return;

                var message = chatMessage.Message;
                if (!message.TextValue.Contains("获得", StringComparison.Ordinal))
                    return;

                var playerPayloads = message.Payloads.OfType<PlayerPayload>().ToList();
                var itemPayloads = message.Payloads.OfType<ItemPayload>().ToList();
                if (playerPayloads.Count != 1 || itemPayloads.Count != 1)
                    return;

                var playerPayload = playerPayloads[0];
                var itemPayload = itemPayloads[0];
                if (itemPayload.ItemId == 0)
                    return;

                var payloads = message.Payloads;
                var playerIndex = payloads.IndexOf(playerPayload);
                var itemIndex = payloads.IndexOf(itemPayload);
                if (playerIndex < 0 || itemIndex < 0 || playerIndex >= itemIndex)
                    return;

                var itemName = GetItemName(itemPayload.ItemId);
                LootRecords.Add(new LootRecord
                {
                    ItemName = itemName,
                    WinnerName = playerPayload.PlayerName,
                    Time = DateTime.Now
                });

                LogHelper.Print($"[Roll追踪] {playerPayload.PlayerName} 获得 {itemName} (ID: {itemPayload.ItemId})");
            }
            catch (Exception ex)
            {
                LogHelper.PrintError($"[Roll追踪] 异常: {ex.Message}");
            }
        }

        private static string GetItemName(uint itemId)
        {
            try
            {
                var sheet = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>();
                var item = sheet.GetRow(itemId);
                return item.Name.ToString();
            }
            catch
            {
                return $"物品#{itemId}";
            }
        }

        public static void PrintAllRecords()
        {
            var records = LootRecords.OrderBy(x => x.Time).ToList();
            if (records.Count == 0)
            {
                LogHelper.Print("[Roll统计] 暂无记录");
                return;
            }

            LogHelper.Print("========== Roll统计 ==========");
            foreach (var record in records)
            {
                LogHelper.Print($"[{record.Time:HH:mm:ss}] 玩家: {record.WinnerName} | 物品: {record.ItemName}");
            }

            LogHelper.Print("================================");
        }
    }
}
