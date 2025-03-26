using AEAssist.Helper;
using AEAssist;
using System;
using System.IO;
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
        private static readonly string ConfigFilePath = Path.Combine(Share.CurrentDirectory, @"..\..\Settings\AutoRaidHelper\FullAutoSettings.json");

        // 单例实例
        private static FullAutoSettings? _instance;

        // 线程安全锁对象，用于确保多线程环境下单例的唯一性
        private static readonly object _lock = new();

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
        public GeometrySettings GeometrySettings { get; set; } = new GeometrySettings();

        // AutomationTab相关设置：用于存储自动倒计时、退本、排本等自动化功能的配置
        public AutomationSettings AutomationSettings { get; set; } = new AutomationSettings();

        // FaGeneralSetting：基础功能相关的配置信息（例如调试信息输出）
        public FaGeneralSetting FaGeneralSetting { get; set; } = new FaGeneralSetting();

        // DebugPrintSettings：调试打印相关设置，控制输出各类触发事件的调试信息
        public DebugPrintSettings DebugPrintSettings { get; set; } = new DebugPrintSettings();

        /// <summary>
        /// 保存当前配置到配置文件
        /// 先确保配置文件所属目录存在，然后以格式化的 JSON 格式进行序列化保存
        /// </summary>
        public void Save()
        {
            try
            {
                // 获取配置文件所在目录，并创建目录（如果不存在）
                string? dir = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                // 序列化当前配置对象，采用缩进格式写入文件
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                LogHelper.Error("全自动小助手配置文件保存失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 静态加载配置方法
        /// 尝试从配置文件中读取 JSON 数据并反序列化为 FullAutoSettings 对象，
        /// 如果读取失败或文件不存在，则返回一个新的默认实例
        /// </summary>
        /// <returns>加载到的配置实例或全新的默认配置实例</returns>
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
        public int ApexMode { get; set; } = 0;

        // 用于弦长/角度/半径换算的输入参数（默认全为0）
        public float ChordInput { get; set; } = 0f;
        public float AngleInput { get; set; } = 0f;
        public float RadiusInput { get; set; } = 0f;

        // 控制是否添加 Debug 点的开关（默认为 false）
        public bool AddDebugPoints { get; set; } = false;

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
        public bool AutoCountdownEnabled { get; set; } = false;
        public int AutoCountdownDelay { get; set; } = 15;

        // 自动退本状态及延迟（单位：秒）
        public bool AutoLeaveEnabled { get; set; } = false;
        public int AutoLeaveDelay { get; set; } = 1;

        // 自动排本开启状态
        public bool AutoQueueEnabled { get; set; } = false;

        // 选定的副本名称（默认："光暗未来绝境战"）以及自定义副本名称
        public string SelectedDutyName { get; set; } = "光暗未来绝境战";
        public string CustomDutyName { get; set; } = "";

        // 解限功能开关（用于排本命令中追加 "unrest"）
        public bool UnrestEnabled { get; set; } = false;

        // 最终生成的排本命令字符串（自动根据配置拼接组合）
        public string FinalSendDutyName { get; set; } = "";

        // 低保统计数据：Omega 与 Sphene 低保计数
        public int OmegaCompletedCount { get; set; } = 0;
        public int SpheneCompletedCount { get; set; } = 0;

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
        /// 更新退本延迟时间，并保存配置
        /// </summary>
        public void UpdateAutoLeaveDelay(int delay)
        {
            AutoLeaveDelay = delay;
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

        /// <summary>
        /// 更新最终排本命令字符串，并保存配置
        /// </summary>
        public void UpdateFinalSendDutyName(string finalName)
        {
            FinalSendDutyName = finalName;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新 OmegaCompletedCount 并保存配置
        /// </summary>
        public void UpdateOmegaCompletedCount(int count)
        {
            OmegaCompletedCount = count;
            FullAutoSettings.Instance.Save();
        }

        /// <summary>
        /// 更新 SpheneCompletedCount 并保存配置
        /// </summary>
        public void UpdateSpheneCompletedCount(int count)
        {
            SpheneCompletedCount = count;
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

        /// <summary>
        /// 更新 PrintDebugInfo 并保存配置
        /// </summary>
        public void UpdatePrintDebugInfo(bool print)
        {
            PrintDebugInfo = print;
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
        public bool DebugPrintEnabled { get; set; } = false;

        // 以下各开关分别控制不同事件的打印
        public bool PrintEnemyCastSpell { get; set; } = false;
        public bool PrintMapEffect { get; set; } = false;
        public bool PrintTether { get; set; } = false;
        public bool PrintTargetIcon { get; set; } = false;
        public bool PrintUnitCreate { get; set; } = false;
        public bool PrintUnitDelete { get; set; } = false;
        public bool PrintAddStatus { get; set; } = false;
        public bool PrintRemoveStatus { get; set; } = false;
        public bool PrintAbilityEffect { get; set; } = false;
        public bool PrintGameLog { get; set; } = false;
        public bool PrintWeatherChanged { get; set; } = false;
        public bool PrintActorControl { get; set; } = false;

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
    }
}
