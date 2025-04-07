using System;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using AEAssist.ACT;
using AEAssist.Helper;
using AutoRaidHelper.Settings;

namespace AutoRaidHelper.Hooks
{
    /// <summary>
    /// 实现了对 ActorControlSelfHook 的 hook，回调中输出所有参数信息
    /// </summary>
    public class ActorControlHook : IDisposable
    {
        // 定义与原始 ReceiveActorControl 方法相同签名的委托类型
        private delegate void ActorControlSelfDelegate(uint entityId, ActorControlCategory id, uint arg0, uint arg1, uint arg2,
            uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);

        // 保存 Hook 实例
        private Hook<ActorControlSelfDelegate> actorControlSelfHook;

        public ActorControlHook()
        {
            LogHelper.Print("ActorControlHook created");
            HookActorControl();
        }

        /// <summary>
        /// 利用 SigScanner 创建 Hook，对目标地址的函数进行替换
        /// </summary>
        private void HookActorControl()
        {
            // 注意：此处签名字符串需要替换为实际值
            string sig = "E8 ?? ?? ?? ?? 0F ?? ?? 83 ?? ?? 0F 84 ?? ?? ?? ?? 83 ?? ?? 0F 84 ?? ?? ?? ?? 83 ?? ?? 74";
            var address = Svc.SigScanner.ScanText(sig);
            actorControlSelfHook = Svc.Hook.HookFromAddress<ActorControlSelfDelegate>(address, CustomReceiveActorControl, 0);
            actorControlSelfHook?.Enable();
        }

        /// <summary>
        /// 自定义的 ActorControlSelf hook 回调，先调用原始逻辑，再输出所有参数信息
        /// </summary>
        private void CustomReceiveActorControl(uint entityId, ActorControlCategory id, uint arg0, uint arg1, uint arg2,
    uint arg3, uint arg4, uint arg5, ulong targetId, byte a10)
        {
            // 调用原始函数，确保原有逻辑不被破坏
            actorControlSelfHook.Original(entityId, id, arg0, arg1, arg2, arg3, arg4, arg5, targetId, a10);
            if (FullAutoSettings.Instance.FaGeneralSetting.PrintActorControl)
            {
                // 检查 id 是否在 ActorControlCategory 中定义
                if (!Enum.IsDefined(typeof(ActorControlCategory), id))
                {
                    LogHelper.PrintError($"未定义的 ActorControlCategory id: {id}");
                }

                // 将所有参数信息输出到控制台（或替换为你项目中使用的日志方法）
                LogHelper.Print("ActorControlSelfHook invoked:");
                LogHelper.Print($"  entityId: {entityId}");
                LogHelper.Print($"  id: {id}");
                LogHelper.Print($"  arg0: {arg0}");
                LogHelper.Print($"  arg1: {arg1}");
                LogHelper.Print($"  arg2: {arg2}");
                LogHelper.Print($"  arg3: {arg3}");
                LogHelper.Print($"  arg4: {arg4}");
                LogHelper.Print($"  arg5: {arg5}");
                LogHelper.Print($"  targetId: {targetId}");
                LogHelper.Print($"  a10: {a10}");
            }
        }
        // 释放 Hook 资源
        public void Dispose()
        {
            actorControlSelfHook?.Dispose();
        }
    }
}
