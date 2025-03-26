using ImGuiNET;
using AutoRaidHelper.Settings;
using AEAssist.CombatRoutine.Trigger;
using AEAssist.Helper;
using System.Runtime.Loader;
using AEAssist.CombatRoutine.Module;

namespace AutoRaidHelper.UI
{
    /// <summary>
    /// DebugPrintTab 用于调试输出事件日志，帮助开发者跟踪各类条件事件的调用情况。
    /// 该类通过订阅 TriggerlineData 的事件回调来监听条件参数创建，并根据配置将其打印至日志中。
    /// 同时提供了 UI 界面，可以动态控制输出哪些事件的信息。
    /// </summary>
    public class DebugPrintTab
    {
        /// <summary>
        /// 从全局配置中获取 DebugPrintSettings 配置
        /// 该配置包含了各个调试打印选项，在 UI 中可通过复选框进行开关控制。
        /// </summary>
        public DebugPrintSettings Settings => FullAutoSettings.Instance.DebugPrintSettings;

        /// <summary>
        /// 在模块加载时调用，订阅条件参数创建事件回调
        /// 当 TriggerlineData 产生条件参数时，会调用 OnCondParamsCreateEvent 方法进行调试打印。
        /// </summary>
        /// <param name="loadContext">当前插件的加载上下文</param>
        public void OnLoad(AssemblyLoadContext loadContext)
        {
            // 注册条件参数创建的事件回调，便于调试输出
            TriggerlineData.OnCondParamsCreate += OnCondParamsCreateEvent;
        }

        /// <summary>
        /// 当插件卸载或者模块释放时调用，取消已注册的事件回调，防止事件回调残留造成内存泄漏或异常行为。
        /// </summary>
        public void Dispose()
        {
            // 取消条件参数创建事件回调的注册
            TriggerlineData.OnCondParamsCreate -= OnCondParamsCreateEvent;
        }

        /// <summary>
        /// 绘制 DebugPrintTab 的 UI 界面
        /// 通过复选框调整调试打印总开关以及各个详细事件的打印选项。
        /// 若总开关关闭，则不会显示其它详细选项。
        /// </summary>
        public void Draw()
        {
            // 调整 DebugPrintEnabled 总开关（是否启用事件日志调试输出）
            bool debugEnabled = Settings.DebugPrintEnabled;
            if (ImGui.Checkbox("启用事件日志调试输出", ref debugEnabled))
            {
                // 更新配置，并保存调试输出总开关状态
                Settings.UpdateDebugPrintEnabled(debugEnabled);
            }
            // 如果总开关未启用，则不继续绘制下列详细选项，直接返回
            if (!debugEnabled)
                return;

            ImGui.Text("详细事件打印选项:");

            // 每个复选框用于控制对应类型事件的调试输出开关

            // 调试输出敌对施法事件
            bool enemyCastSpell = Settings.PrintEnemyCastSpell;
            if (ImGui.Checkbox("咏唱事件", ref enemyCastSpell))
                Settings.UpdatePrintEnemyCastSpell(enemyCastSpell);

            // 调试输出地图效果事件
            bool mapEffect = Settings.PrintMapEffect;
            if (ImGui.Checkbox("地图事件", ref mapEffect))
                Settings.UpdatePrintMapEffect(mapEffect);

            // 调试输出连线/系绳事件
            bool tether = Settings.PrintTether;
            if (ImGui.Checkbox("连线事件", ref tether))
                Settings.UpdatePrintTether(tether);

            // 调试输出目标标记（点名头标）事件
            bool targetIcon = Settings.PrintTargetIcon;
            if (ImGui.Checkbox("点名头标", ref targetIcon))
                Settings.UpdatePrintTargetIcon(targetIcon);

            // 调试输出创建单位事件
            bool unitCreate = Settings.PrintUnitCreate;
            if (ImGui.Checkbox("创建单位", ref unitCreate))
                Settings.UpdatePrintUnitCreate(unitCreate);

            // 调试输出删除单位事件
            bool unitDelete = Settings.PrintUnitDelete;
            if (ImGui.Checkbox("删除单位", ref unitDelete))
                Settings.UpdatePrintUnitDelete(unitDelete);

            // 调试输出添加 Buff 状态事件
            bool addStatus = Settings.PrintAddStatus;
            if (ImGui.Checkbox("添加Buff", ref addStatus))
                Settings.UpdatePrintAddStatus(addStatus);

            // 调试输出移除 Buff 状态事件
            bool removeStatus = Settings.PrintRemoveStatus;
            if (ImGui.Checkbox("删除Buff", ref removeStatus))
                Settings.UpdatePrintRemoveStatus(removeStatus);

            // 调试输出能力效果事件
            bool abilityEffect = Settings.PrintAbilityEffect;
            if (ImGui.Checkbox("效果事件", ref abilityEffect))
                Settings.UpdatePrintAbilityEffect(abilityEffect);

            // 调试输出游戏日志（内部游戏日志）事件
            bool gameLog = Settings.PrintGameLog;
            if (ImGui.Checkbox("游戏日志", ref gameLog))
                Settings.UpdatePrintGameLog(gameLog);

            // 调试输出天气变化事件
            bool weatherChanged = Settings.PrintWeatherChanged;
            if (ImGui.Checkbox("天气变化", ref weatherChanged))
                Settings.UpdatePrintWeatherChanged(weatherChanged);

            // 调试输出 ActorControl 事件（通常涉及角色控制相关）
            bool actorControl = Settings.PrintActorControl;
            if (ImGui.Checkbox("ActorControl", ref actorControl))
                Settings.UpdatePrintActorControl(actorControl);
        }

        /// <summary>
        /// 事件回调：根据配置打印调试信息
        /// 当 TriggerlineData 创建条件参数时，将调用此回调方法。
        /// 根据具体的条件参数类型和对应的配置开关，决定是否将其打印到日志中。
        /// </summary>
        /// <param name="condParams">触发条件参数对象，根据其类型可判断是哪种事件</param>
        public void OnCondParamsCreateEvent(ITriggerCondParams condParams)
        {
            // 如果调试总开关未启用，则直接返回，不执行任何打印操作
            if (!Settings.DebugPrintEnabled)
                return;

            // 根据条件参数类型判断，并结合对应配置选项，选择性输出日志

            if (condParams is EnemyCastSpellCondParams spell && Settings.PrintEnemyCastSpell)
                LogHelper.Print($"{spell}");
            if (condParams is OnMapEffectCreateEvent mapEffect && Settings.PrintMapEffect)
                LogHelper.Print($"{mapEffect}");
            if (condParams is TetherCondParams tether && Settings.PrintTether)
                LogHelper.Print($"{tether}");
            if (condParams is TargetIconEffectCondParams iconEffect && Settings.PrintTargetIcon)
                LogHelper.Print($"{iconEffect}");
            if (condParams is UnitCreateCondParams unitCreate && Settings.PrintUnitCreate)
                LogHelper.Print($"{unitCreate}");
            if (condParams is UnitDeleteCondParams unitDelete && Settings.PrintUnitDelete)
                LogHelper.Print($"{unitDelete}");
            if (condParams is AddStatusCondParams addStatus && Settings.PrintAddStatus)
                LogHelper.Print($"{addStatus}");
            if (condParams is RemoveStatusCondParams removeStatus && Settings.PrintRemoveStatus)
                LogHelper.Print($"{removeStatus}");
            if (condParams is ReceviceAbilityEffectCondParams abilityEffect && Settings.PrintAbilityEffect)
                LogHelper.Print($"{abilityEffect}");
            if (condParams is GameLogCondParams gameLog && Settings.PrintGameLog)
                LogHelper.Print($"{gameLog}");
            if (condParams is WeatherChangedCondParams weatherChanged && Settings.PrintWeatherChanged)
                LogHelper.Print($"{weatherChanged}");
            if (condParams is ActorControlCondParams actorControl && Settings.PrintActorControl)
                LogHelper.Print($"{actorControl}");
        }
    }
}
