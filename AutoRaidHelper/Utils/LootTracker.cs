using AEAssist.Helper;
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
        private static readonly Dictionary<uint, LootRecord> LootRecords = new();
        private static readonly HashSet<uint> ProcessedItems = new();
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
                ProcessedItems.Clear();
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
                // 检查 Payload 结构：PlayerPayload + "获得了" + ItemPayload
                if (!message.TextValue.Contains("获得了"))
                    return;
                
                var playerPayload = message.Payloads.OfType<PlayerPayload>().FirstOrDefault();
                var itemPayload = message.Payloads.OfType<ItemPayload>().FirstOrDefault();
                
                if (playerPayload == null || itemPayload == null || itemPayload.ItemId == 0)
                    return;
                
                // 防止重复记录
                if (!ProcessedItems.Add(itemPayload.ItemId))
                    return;

                // 获取物品信息
                var itemName = GetItemName(itemPayload.ItemId);
                
                LootRecords[itemPayload.ItemId] = new LootRecord
                {
                    ItemName = itemName,
                    WinnerName = playerPayload.PlayerName,
                    Time = DateTime.Now
                };
                
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
            var records = LootRecords.Values.OrderBy(x => x.Time).ToList();
            
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

        public static void Clear()
        {
            LootRecords.Clear();
            ProcessedItems.Clear();
        }
    }
}