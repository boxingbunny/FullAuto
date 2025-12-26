using AEAssist.Helper;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace AutoRaidHelper.Utils
{
    /// <summary>
    /// Roll点统计工具，监听聊天消息记录物品获得情况
    /// </summary>
    public static class LootTracker
    {
        private static readonly List<LootRecord> LootRecords = new();
        private static bool _initialized;

        private class LootRecord
        {
            public string ItemName { get; set; } = "";
            public string WinnerName { get; set; } = "";
            public DateTime Time { get; set; }
        }

        public static void Initialize()
        {
            if (_initialized) return;
            
            try
            {
                Svc.Chat.ChatMessage += OnChatMessage;
                _initialized = true;
            }
            catch (Exception ex)
            {
                LogHelper.PrintError($"[Roll点追踪] 初始化失败: {ex.Message}");
            }
        }

        public static void Dispose()
        {
            if (!_initialized) return;
            
            try
            {
                Svc.Chat.ChatMessage -= OnChatMessage;
                _initialized = false;
                LootRecords.Clear();
            }
            catch (Exception ex)
            {
                LogHelper.PrintError($"[Roll点追踪] 清理失败: {ex.Message}");
            }
        }

        private static void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            try
            {
                // 必须在副本内
                if (!Svc.Condition[ConditionFlag.BoundByDuty])
                    return;
                
                // 消息必须包含"获得了"
                if (!message.TextValue.Contains("获得了"))
                    return;
                
                // 必须有且只有一个 PlayerPayload 和一个 ItemPayload
                var playerPayloads = message.Payloads.OfType<PlayerPayload>().ToList();
                var itemPayloads = message.Payloads.OfType<ItemPayload>().ToList();
                
                if (playerPayloads.Count != 1 || itemPayloads.Count != 1)
                    return;
                
                var playerPayload = playerPayloads[0];
                var itemPayload = itemPayloads[0];
                
                if (itemPayload.ItemId == 0)
                    return;
                
                // PlayerPayload 必须在 ItemPayload 之前
                var payloads = message.Payloads;
                var pIndex = payloads.IndexOf(playerPayload);
                var iIndex = payloads.IndexOf(itemPayload);
                
                if (pIndex < 0 || iIndex < 0 || pIndex >= iIndex)
                    return;
                
                // 记录战利品
                var itemName = GetItemName(itemPayload.ItemId);
                
                var record = new LootRecord
                {
                    ItemName = itemName,
                    WinnerName = playerPayload.PlayerName,
                    Time = DateTime.Now
                };
                
                LootRecords.Add(record);
                
                LogHelper.Print($"[Roll点] {playerPayload.PlayerName} 获得 {itemName} (ID: {itemPayload.ItemId})");
            }
            catch (Exception ex)
            {
                LogHelper.PrintError($"[Roll点追踪] 异常: {ex.Message}");
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
                LogHelper.Print("[Roll点统计] 暂无记录");
                return;
            }

            LogHelper.Print("========== Roll点统计 ==========");
            
            foreach (var record in records)
            {
                var timeStamp = record.Time.ToString("HH:mm:ss");
                LogHelper.Print($"[{timeStamp}] 玩家: {record.WinnerName} | 物品: {record.ItemName}");
            }
            
            LogHelper.Print("================================");
        }
    }
}