using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AEAssist.Helper;

namespace FullAuto;

/// <summary>
/// FullAuto 设置
/// </summary>
public class FullAutoSetting
{
    private static FullAutoSetting? _instance;
    public static FullAutoSetting Instance => _instance ??= Load();

    private static string SettingPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AEAssist", "FullAuto", "setting.json");

    /// <summary>
    /// 服务器地址
    /// </summary>
    [JsonPropertyName("serverUrl")]
    public string ServerUrl { get; set; } = "ws://localhost:8080/api/ws";

    /// <summary>
    /// 自动连接
    /// </summary>
    [JsonPropertyName("autoConnect")]
    public bool AutoConnect { get; set; } = false;

    /// <summary>
    /// 自动重连
    /// </summary>
    [JsonPropertyName("autoReconnect")]
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// 重连间隔(秒)
    /// </summary>
    [JsonPropertyName("reconnectInterval")]
    public int ReconnectInterval { get; set; } = 5;

    public static FullAutoSetting Load()
    {
        try
        {
            if (File.Exists(SettingPath))
            {
                var json = File.ReadAllText(SettingPath);
                var setting = JsonSerializer.Deserialize<FullAutoSetting>(json);
                if (setting != null)
                {
                    return setting;
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 加载设置失败: {ex.Message}");
        }

        return new FullAutoSetting();
    }

    /// <summary>
    /// 同步保存设置（用于简单场景）
    /// </summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(SettingPath, json);
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 保存设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 异步保存设置（推荐用于 UI 操作，避免阻塞主线程）
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(SettingPath, json);
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[FullAuto] 异步保存设置失败: {ex.Message}");
        }
    }
}
