using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using AutoRaidHelper.Settings;

namespace AutoRaidHelper.Utils;

public class LootRollingManager : IDisposable
{
    private static readonly RollResult[] _rollArray =
    [
        RollResult.Needed,
        RollResult.Greeded,
        RollResult.Passed
    ];

    private DateTime _nextRollTime = DateTime.Now;
    private RollResult _rollOption = RollResult.UnAwarded;
    private int _need = 0, _greed = 0, _pass = 0;

    public LootRollingManager()
    {
        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.Chat.CheckMessageHandled += OnChatMessage;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Chat.CheckMessageHandled -= OnChatMessage;
    }

    /// <summary>
    /// 处理批量 Roll 命令
    /// </summary>
    public void HandleRollCommand(string mode)
    {
        var result = GetRollResult(mode);
        if (!result.HasValue) return;
        _rollOption = _rollArray[result.Value % 3];
        Svc.Chat.Print($"[ARH] 开始批量 {GetRollModeName(result.Value)} 所有物品...");
    }

    /// <summary>
    /// 处理自动 Roll 命令
    /// </summary>
    public void HandleAutoRollCommand(string[] args)
    {
        var settings = FullAutoSettings.Instance.LootRollingSettings;

        if (args.Length == 0)
        {
            // 切换自动 Roll 状态
            settings.AutoRollEnabled = !settings.AutoRollEnabled;
            FullAutoSettings.Instance.Save();
            Svc.Chat.Print($"[ARH] 自动 Roll: {(settings.AutoRollEnabled ? "已开启" : "已关闭")}");
            return;
        }

        var subArg = args[0].ToLower();

        switch (subArg)
        {
            case "on":
                settings.AutoRollEnabled = true;
                FullAutoSettings.Instance.Save();
                Svc.Chat.Print("[ARH] 自动 Roll: 已开启");
                break;

            case "off":
                settings.AutoRollEnabled = false;
                FullAutoSettings.Instance.Save();
                Svc.Chat.Print("[ARH] 自动 Roll: 已关闭");
                break;

            case "need":
            case "greed":
            case "pass":
                var result = GetRollResult(subArg);
                if (result.HasValue)
                {
                    settings.AutoRollMode = result.Value;
                    FullAutoSettings.Instance.Save();
                    Svc.Chat.Print($"[ARH] 自动 Roll 模式: {GetRollModeName(result.Value)}");
                }
                break;

            default:
                Svc.Chat.Print("[ARH] 用法: /arh autoroll [on|off|need|greed|pass]");
                break;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // 处理批量 Roll
        if (_rollOption != RollResult.UnAwarded)
        {
            RollLoot();
        }

        // 自动 Roll 功能在 OnChatMessage 中触发
    }

    private void RollLoot()
    {
        if (_rollOption == RollResult.UnAwarded) return;
        if (DateTime.Now < _nextRollTime) return;

        // 在过场动画中不 Roll
        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent]) return;

        var settings = FullAutoSettings.Instance.LootRollingSettings;
        _nextRollTime = DateTime.Now.AddMilliseconds(Math.Max(1500, new Random()
            .Next((int)(settings.MinRollDelayInSeconds * 1000),
                (int)(settings.MaxRollDelayInSeconds * 1000))));

        try
        {
            if (LootRoller.RollOneItem(_rollOption, ref _need, ref _greed, ref _pass)) return; // 完成 Roll
            ShowResult(_need, _greed, _pass);
            _need = _greed = _pass = 0;
            _rollOption = RollResult.UnAwarded;
            LootRoller.Clear();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Roll 点时发生错误");
        }
    }

    private void OnChatMessage(XivChatType type, int senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        var settings = FullAutoSettings.Instance.LootRollingSettings;
        if (!settings.AutoRollEnabled || type != (XivChatType)2105) return;

        var textValue = message.TextValue;
        // LogMessage ID 5194 表示可以 Roll 了
        if (textValue != Svc.Data.GetExcelSheet<LogMessage>()!.First(x => x.RowId == 5194).Text) return;
        _nextRollTime = DateTime.Now.AddMilliseconds(new Random()
            .Next((int)(settings.AutoRollMinDelayInSeconds * 1000),
                (int)(settings.AutoRollMaxDelayInSeconds * 1000)));

        _rollOption = _rollArray[settings.AutoRollMode];
    }

    private void ShowResult(int need, int greed, int pass)
    {
        var settings = FullAutoSettings.Instance.LootRollingSettings;

        SeString seString = new(new List<Payload>()
        {
            new TextPayload("[ARH Roll] 需求 "),
            new UIForegroundPayload(575),
            new TextPayload(need.ToString()),
            new UIForegroundPayload(0),
            new TextPayload(" 项, 贪婪 "),
            new UIForegroundPayload(575),
            new TextPayload(greed.ToString()),
            new UIForegroundPayload(0),
            new TextPayload(" 项, 放弃 "),
            new UIForegroundPayload(575),
            new TextPayload(pass.ToString()),
            new UIForegroundPayload(0),
            new TextPayload(" 项")
        });

        if (settings.EnableChatLogMessage)
        {
            Svc.Chat.Print(seString);
        }
        if (settings.EnableErrorToast)
        {
            Svc.Toasts.ShowError(seString);
        }
        if (settings.EnableNormalToast)
        {
            Svc.Toasts.ShowNormal(seString);
        }
        if (settings.EnableQuestToast)
        {
            Svc.Toasts.ShowQuest(seString);
        }
    }

    private int? GetRollResult(string str)
    {
        if (str.Contains("need", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (str.Contains("greed", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (str.Contains("pass", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }
        return null;
    }

    private string GetRollModeName(int mode)
    {
        return mode switch
        {
            0 => "Need",
            1 => "Greed",
            2 => "Pass",
            _ => "Unknown"
        };
    }
}