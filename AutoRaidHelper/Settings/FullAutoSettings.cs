using AEAssist.Helper;
using AEAssist;
using System.Text.Json;

namespace AutoRaidHelper.Settings
{
    /// <summary>
    /// FullAutoSettings 是全自动小助手的全局配置管理类。
    /// 本类负责读取、保存所有模块的配置，包括 GeometrySettings、AutomationSettings、FaGeneralSetting 和 DebugPrintSettings。
    /// 为保证全局唯一性，采用单例模式，并通过双重锁定实现线程安全的延迟加载。
    /// </summary>
        public sealed class FullAutoSettings
    {
        // 配置文件的存储路径，通过当前工作目录与相对路径构造出绝对路径
        private static string ConfigFilePath = Path.Combine(Share.CurrentDirectory, @"..\..\Settings\AutoRaidHelper\FullAutoSettings", $"{Share.LocalContentId}.json");

        // 单例实例
        private static FullAutoSettings? _instance;

        // 线程安全锁对象，用于确保多线程环境下单例的唯一性
        private static readonly object _lock = new();

        // 是否为只读模式（文件写入失败后将设置为 true，防止后续反复尝试）
        private static bool _readOnlyMode = false;

        /// <summary>
        /// 获取全局唯一的 FullAutoSettings 实例
        /// 如果实例不存在，则尝试加载配置文件；加载失败时返回新的默认配置实例
        /// </summary>
        public static FullAutoSettings Instance
        {
            get
            {
                if (_instance is null)
                {
                    lock (_lock)
                    {
                        if (_instance is null)
                        {
                            _instance = Load();
                        }
                    }
                }
                return _instance;
            }
        }

        // GeometryTab相关设置：用于存储场地中心、朝向点、角度计算等几何信息的相关配置
        public GeometrySettings GeometrySettings { get; set; } = new();

        // AutomationTab相关设置：用于存储自动倒计时、退本、排本等自动化功能的配置
        public AutomationSettings AutomationSettings { get; set; } = new();

        // FaGeneralSetting：基础功能相关的配置信息（例如调试信息输出）
        public FaGeneralSetting FaGeneralSetting { get; set; } = new();

        // DebugPrintSettings：调试打印相关设置，控制输出各类触发事件的调试信息
        public DebugPrintSettings DebugPrintSettings { get; set; } = new();

        /// <summary>
        /// 保存当前配置到配置文件
        /// 若文件被占用，将进入只读模式，不再尝试写入
        /// </summary>
        public void Save()
        {
            if (_readOnlyMode)
            {
                LogHelper.Info("当前处于只读模式，跳过保存配置。");
                return;
            }

            try
            {
                string? dir = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (IOException ex)
            {
                LogHelper.Error("配置文件被占用，切换为只读模式：" + ex.Message);
                _readOnlyMode = true;
            }
            catch (Exception ex)
            {
                LogHelper.Error("全自动小助手配置文件保存失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 手动解除只读模式（如切换角色或退出副本后调用）
        /// </summary>
        public static void ForceWriteable()
        {
            _readOnlyMode = false;
            LogHelper.Info("已解除只读模式，可以重新保存配置。");
        }

        /// <summary>
        /// 静态加载配置方法
        /// 尝试从配置文件中读取 JSON 数据并反序列化为 FullAutoSettings 对象，
        /// 如果读取失败或文件不存在，则返回一个新的默认实例
        /// </summary>
        private static FullAutoSettings Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var settings = JsonSerializer.Deserialize<FullAutoSettings>(json);
                    return settings ?? new FullAutoSettings();
                }
            }
            catch
            {
                LogHelper.Error("全自动小助手配置文件加载失败");
            }
            return new FullAutoSettings();
        }
    }

    /// <summary>
    /// GeometrySettings 包含 GeometryTab 模块相关的所有配置信息，
    /// 如场地中心、朝向点、夹角顶点模式以及用于计算弦长、角度、半径的输入参数。
    /// 此类还提供了更新各配置项并保存配置的相关方法。
    /// </summary>
    public class GeometrySettings
    {
        // 场地中心下标（默认值为1，对应新(100,0,100)）
        public int SelectedCenterIndex { get; set; } = 1;

        // 朝向点下标（默认值为3，对应北(100,0,99)）
        public int SelectedDirectionIndex { get; set; } = 3;

        // 计算夹角时使用的顶点模式，0表示使用场地中心，1表示使用用户指定的点3
        public int ApexMode { get; set; }

        // 用于弦长/角度/半径换算的输入参数（默认全为0）
        public float ChordInput { get; set; }
        public float AngleInput { get; set; }
        public float RadiusInput { get; set; }

        // 控制是否添加 Debug 点的开关（默认为 false）
        public bool AddDebugPoints { get; set; }

        /// <summary>
        /// 更新场地中心选项，并保存配置
        /// </summary>
        public void UpdateSelectedCenterIndex(int index)
        {
            SelectedCenterIndex = index;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新朝向点选项，并保存配置
        /// </summary>
        public void UpdateSelectedDirectionIndex(int index)
        {
            SelectedDirectionIndex = index;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新夹角顶点模式，并保存配置
        /// </summary>
        public void UpdateApexMode(int mode)
        {
            ApexMode = mode;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新弦长输入值，并保存配置
        /// </summary>
        public void UpdateChordInput(float value)
        {
            ChordInput = value;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新角度输入值，并保存配置
        /// </summary>
        public void UpdateAngleInput(float value)
        {
            AngleInput = value;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新半径输入值，并保存配置
        /// </summary>
        public void UpdateRadiusInput(float value)
        {
            RadiusInput = value;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新是否添加 Debug 点的状态，并保存配置
        /// </summary>
        public void UpdateAddDebugPoints(bool add)
        {
            AddDebugPoints = add;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 重置 GeometrySettings 至默认值，并保存配置
        /// </summary>
        public void Reset()
        {
            SelectedCenterIndex = 1;
            SelectedDirectionIndex = 3;
            ApexMode = 0;
            ChordInput = 0f;
            AngleInput = 0f;
            RadiusInput = 0f;
            AddDebugPoints = false;
            FullAutoSettings.Instance.Save();
        }
    }

    /// <summary>
    /// AutomationSettings 存储了 AutomationTab 模块相关配置，
    /// 包括地图ID、自动倒计时、自动退本与自动排本等各项功能的开关与延迟设置，
    /// 以及副本名称和低保计数等统计数据。
    /// </summary>
    public class AutomationSettings
    {
        // 当前自动功能所在地图的 ID（默认值为 1238）
        public uint AutoFuncZoneId { get; set; } = 1238;

        // 自动倒计时开启与否，以及相应的倒计时延迟（单位：秒）
        public bool AutoCountdownEnabled { get; set; }
        public int AutoCountdownDelay { get; set; } = 15;

        // 自动退本状态及延迟（单位：秒）
        public bool AutoLeaveEnabled { get; set; }
        public bool RunTimeEnabled { get; set; }
        public int AutoLeaveDelay { get; set; } = 1;
        
        public int RunTimeLimit { get; set; } = 5;

        // 是否宝箱R点完成后再退本
        public bool AutoLeaveAfterLootEnabled { get; set; }
        
        // 自动排本开启状态及延迟（单位：秒）
        public bool AutoQueueEnabled { get; set; }
        public int AutoQueueDelay { get; set; } = 3;

        // 选定的副本名称（默认："光暗未来绝境战"）以及自定义副本名称
        public string SelectedDutyName { get; set; } = "光暗未来绝境战";
        public string CustomDutyName { get; set; } = "";

        // 解限功能开关（用于排本命令中追加 "unrest"）
        public bool UnrestEnabled { get; set; }
        public bool AutoKillEnabled { get; set; }

        // 最终生成的排本命令字符串（自动根据配置拼接组合）
        public string FinalSendDutyName { get; set; } = "";

        // 低保统计数据：龙诗、欧米茄、淑芬、伊甸、阿罗阿罗低保计数
        public int DSRCompletedCount { get; set; }
        public int TOPCompletedCount { get; set; }
        public int SpheneCompletedCount { get; set; }
        public int FRUCompletedCount { get; set; }
        public int AloaloCompletedCount { get; set; }
        public int WorqorCompletedCount { get; set; }
        public int UCOBCompletedCount { get; set; }
        public int UWUCompletedCount { get; set; }
        public int TEACompletedCount { get; set; }
        public int RecollectionCompletedCount { get; set; }
        public int EverkeepCompletedCount { get; set; }

        /// <summary>
        /// 更新当前地图 ID，并保存配置
        /// </summary>
        public void UpdateAutoFuncZoneId(uint zoneId)
        {
            AutoFuncZoneId = zoneId;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新倒计时启用状态，并保存配置
        /// </summary>
        public void UpdateAutoCountdownEnabled(bool enabled)
        {
            AutoCountdownEnabled = enabled;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新倒计时延迟时间，并保存配置
        /// </summary>
        public void UpdateAutoCountdownDelay(int delay)
        {
            AutoCountdownDelay = delay;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新退本启用状态，并保存配置
        /// </summary>
        public void UpdateAutoLeaveEnabled(bool enabled)
        {
            AutoLeaveEnabled = enabled;
            FullAutoSettings.Instance.Save();
        }
        
        /// <summary>
        /// 更新退本启用状态，并保存配置
        /// </summary>
        public void UpdateRunTimeEnabled(bool enabled)
        {
            RunTimeEnabled = enabled;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新退本延迟时间，并保存配置
        /// </summary>
        public void UpdateAutoLeaveDelay(int delay)
        {
            AutoLeaveDelay = delay;
            FullAutoSettings.Instance.Save();
        }
        /// <summary>
        /// 更新本次运行限制次数，并保存配置
        /// </summary>
        public void UpdateRunTimeLimit(int runtime)
        {
            RunTimeLimit = runtime;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新宝箱R点完成后再退本状态，并保存配置
        /// </summary>
        public void UpdateAutoLeaveAfterLootEnabled(bool enabled)
        {
            AutoLeaveAfterLootEnabled = enabled;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新排本启用状态，并保存配置
        /// </summary>
        public void UpdateAutoQueueEnabled(bool enabled)
        {
            AutoQueueEnabled = enabled;
            FullAutoSettings.Instance.Save();
        }

        ///<summary>
        ///更新排本延迟时间，并保存配置
        /// </summary>
        public void UpdateAutoQueueDelay(int delay)
        {
            AutoQueueDelay = delay;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新选定副本名称，并保存配置
        /// </summary>
        public void UpdateSelectedDutyName(string dutyName)
        {
            SelectedDutyName = dutyName;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新自定义副本名称，并保存配置
        /// </summary>
        public void UpdateCustomDutyName(string dutyName)
        {
            CustomDutyName = dutyName;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新解限启用状态，并保存配置
        /// </summary>
        public void UpdateUnrestEnabled(bool enabled)
        {
            UnrestEnabled = enabled;
            FullAutoSettings.Instance.Save();
        }
        
        public void UpdateAutoKillEnabled(bool enabled)
        {
            AutoKillEnabled = enabled;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新最终排本命令字符串，并保存配置
        /// </summary>
        public void UpdateFinalSendDutyName(string finalName)
        {
            FinalSendDutyName = finalName;
            FullAutoSettings.Instance.Save();
        }
        
        // 定义一个枚举类型
        public enum DutyType : ushort
        {
            UCOB = 733,
            UWU = 777,
            TEA = 887,
            DSR = 968,
            TOP = 1122,
            FRU = 1238,
            Aloalo = 1180,
            Worqor = 1196,
            Everkeep = 1201,
            Sphene = 1243,
            Recollection = 1271,
        }
        
        public enum DutyCategory
        {
            Ultimate,   // 绝本
            Extreme,    // 极神
            Savage,     // 零式
            Variant,    // 异闻
            Custom      // 自定义
        }

        public record DutyInfo(string Name, DutyCategory Category);
        // 副本预设
        public static readonly List<DutyInfo> DutyPresets =
        [
            // 绝本
            new("巴哈姆特绝境战", DutyCategory.Ultimate),
            new("究极神兵绝境战", DutyCategory.Ultimate),
            new("亚历山大绝境战", DutyCategory.Ultimate),
            new("幻想龙诗绝境战", DutyCategory.Ultimate),
            new("欧米茄绝境验证战", DutyCategory.Ultimate),
            new("光暗未来绝境战", DutyCategory.Ultimate),
            // 极神
            new("艳翼蛇鸟歼殛战", DutyCategory.Extreme),
            new("佐拉加歼殛战", DutyCategory.Extreme),
            new("永恒女王忆想歼灭战", DutyCategory.Extreme),
            new("泽莲尼娅歼殛战", DutyCategory.Extreme),
            // 异闻
            new("异闻阿罗阿罗岛", DutyCategory.Variant),
            new("零式异闻阿罗阿罗岛", DutyCategory.Variant),
            // 自定义
            new("自定义", DutyCategory.Custom)
        ];
        

        public void UpdateDutyCount(DutyType duty, int count)
        {
            switch (duty)
            {
                case DutyType.DSR:
                    DSRCompletedCount = count;
                    break;
                case DutyType.TOP:
                    TOPCompletedCount = count;
                    break;
                case DutyType.Sphene:
                    SpheneCompletedCount = count;
                    break;
                case DutyType.FRU:
                    FRUCompletedCount = count;
                    break;
                case DutyType.Aloalo:
                    AloaloCompletedCount = count;
                    break;
                case DutyType.Worqor:
                    WorqorCompletedCount = count;
                    break;
                case DutyType.UWU:
                    UWUCompletedCount = count;
                    break;
                case DutyType.UCOB:
                    UCOBCompletedCount = count;
                    break;
                case DutyType.TEA:
                    TEACompletedCount = count;
                    break;
                case DutyType.Recollection:
                    RecollectionCompletedCount = count;
                    break;
                case DutyType.Everkeep:
                    EverkeepCompletedCount = count;
                    break;
                default:
                    LogHelper.PrintError("未知的副本类型");
                    return;
            }
            FullAutoSettings.Instance.Save();
        }
    }

    /// <summary>
    /// FaGeneralSetting 包含基础功能相关的配置，例如是否启用绘制坐标点并打印 Debug 信息
    /// </summary>
    public class FaGeneralSetting
    {
        // 控制是否绘制坐标点并打印调试信息（默认启用）
        public bool PrintDebugInfo { get; set; } = true;

        //控制是否打印所有ActorControl信息
        public bool PrintActorControl { get; set; } = false;
        /// <summary>
        /// 更新 PrintDebugInfo 并保存配置
        /// </summary>
        public void UpdatePrintDebugInfo(bool print)
        {
            PrintDebugInfo = print;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新 PrintActorControl 并保存配置
        /// </summary>
        public void UpdatePrintActorControl(bool print)
        {
            PrintActorControl = print;
            FullAutoSettings.Instance.Save();
        }
    }

    /// <summary>
    /// DebugPrintSettings 存储调试打印相关配置，
    /// 包括总开关和针对各个事件类型的打印开关，
    /// 用于在调试时决定是否输出对应事件的调试信息。
    /// </summary>
    public class DebugPrintSettings
    {
        // 总开关：若关闭则不打印任何调试信息（默认关闭）
        public bool DebugPrintEnabled { get; set; }

        // 以下各开关分别控制不同事件的打印
        public bool PrintEnemyCastSpell { get; set; }
        public bool PrintMapEffect { get; set; }
        public bool PrintTether { get; set; }
        public bool PrintTargetIcon { get; set; }
        public bool PrintUnitCreate { get; set; }
        public bool PrintUnitDelete { get; set; }
        public bool PrintAddStatus { get; set; }
        public bool PrintRemoveStatus { get; set; }
        public bool PrintAbilityEffect { get; set; }
        public bool PrintGameLog { get; set; }
        public bool PrintWeatherChanged { get; set; }
        public bool PrintActorControl { get; set; }
        public bool PrintPlayActionTimeline { get; set; }
        public bool PrintEnvControl { get; set; }
        public bool PrintNpcYell { get; set; }

        /// <summary>
        /// 更新 DebugPrintEnabled 总开关，并保存配置
        /// </summary>
        public void UpdateDebugPrintEnabled(bool enabled)
        {
            DebugPrintEnabled = enabled;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintEnemyCastSpell(bool value)
        {
            PrintEnemyCastSpell = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintMapEffect(bool value)
        {
            PrintMapEffect = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintTether(bool value)
        {
            PrintTether = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintTargetIcon(bool value)
        {
            PrintTargetIcon = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintUnitCreate(bool value)
        {
            PrintUnitCreate = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintUnitDelete(bool value)
        {
            PrintUnitDelete = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintAddStatus(bool value)
        {
            PrintAddStatus = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintRemoveStatus(bool value)
        {
            PrintRemoveStatus = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintAbilityEffect(bool value)
        {
            PrintAbilityEffect = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintGameLog(bool value)
        {
            PrintGameLog = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintWeatherChanged(bool value)
        {
            PrintWeatherChanged = value;
            FullAutoSettings.Instance.Save();
        }

        public void UpdatePrintActorControl(bool value)
        {
            PrintActorControl = value;
            FullAutoSettings.Instance.Save();
        }
        public void UpdatePrintPlayActionTimeline(bool value)
        {
            PrintPlayActionTimeline = value;
            FullAutoSettings.Instance.Save();
        }
        public void UpdatePrintEnvControl(bool value)
        {
            PrintEnvControl = value;
            FullAutoSettings.Instance.Save();
        }
        public void UpdatePrintNpcYell(bool value)
        {
            PrintNpcYell = value;
            FullAutoSettings.Instance.Save();
        }
    }
}
