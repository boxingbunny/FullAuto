using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using ECommons.Reflection;

namespace AutoRaidHelper.Utils
{
    public static class BocchiStateReader
    {
        private const string ModuleFullName = "BOCCHI.Modules.MobFarmer.MobFarmerModule";
        private static WeakReference<object>? _cachedModule;

        /// <summary>
        /// 读取 MobFarmer 当前 Phase 和 Running。
        /// 成功返回 true；失败返回 false，并写出 reason。
        /// </summary>
        public static bool TryRead(out string? phase, out bool running, out string reason)
        {
            phase = null; running = false; reason = string.Empty;

            // 0) 尝试缓存
            if (_cachedModule != null && _cachedModule.TryGetTarget(out var cached))
                return ReadFromModule(cached, out phase, out running, out reason);

            // 1) 取 BOCCHI 插件实例
            if (!DalamudReflector.TryGetDalamudPlugin("BOCCHI", out var bocchi, false, true) &&
                !DalamudReflector.TryGetDalamudPlugin("Bocchi", out bocchi, false, true))
            {
                reason = "未找到 BOCCHI 插件";
                return false;
            }

            // 2) 浅层遍历找 MobFarmerModule
            var module = ShallowFindByFullName(bocchi!, ModuleFullName, 3);
            if (module == null)
            {
                reason = "未找到 MobFarmerModule（可能未加载/命名不符）";
                return false;
            }

            _cachedModule = new WeakReference<object>(module);
            return ReadFromModule(module, out phase, out running, out reason);
        }

        private static bool ReadFromModule(object module, out string? phase, out bool running, out string reason)
        {
            phase = null; running = false; reason = string.Empty;

            var farmer = GetMember(module, "Farmer");
            if (farmer == null) { reason = "Farmer 为空"; return false; }

            var stateMachine = GetMember(farmer, "StateMachine");
            var stateObj = GetProperty(stateMachine, "State");
            if (stateObj == null) { reason = "State 为 null"; return false; }

            phase = stateObj.ToString();
            var runObj = GetProperty(farmer, "Running");
            running = runObj is true;
            return true;
        }

        // ---------- helpers ----------
        private static object? GetMember(object? obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            return t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj)
                ?? t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);
        }

        private static object? GetProperty(object? obj, string name)
        {
            var t = obj?.GetType();
            return t?.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);
        }

        private static object? ShallowFindByFullName(object root, string wantFullName, int maxDepth)
        {
            var q = new Queue<(object o, int d)>();
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            q.Enqueue((root, 0)); seen.Add(root);

            while (q.Count > 0)
            {
                var (cur, d) = q.Dequeue();
                if (cur.GetType().FullName == wantFullName) return cur;
                if (d >= maxDepth) continue;

                foreach (var next in EnumChildren(cur))
                {
                    if (next == null || !seen.Add(next)) continue;
                    q.Enqueue((next, d + 1));
                }
            }
            return null;
        }

        private static IEnumerable<object?> EnumChildren(object obj)
        {
            if (obj is IEnumerable en and not string)
                foreach (var e in en) yield return e;

            var t = obj.GetType();
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!IsTrivial(f.FieldType)) yield return Safe(() => f.GetValue(obj));
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (p.GetMethod != null && p.GetMethod.GetParameters().Length == 0 && !IsTrivial(p.PropertyType))
                    yield return Safe(() => p.GetValue(obj));
        }

        private static bool IsTrivial(Type t) =>
            t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(IntPtr) || t == typeof(UIntPtr);

        private static T? Safe<T>(Func<T> f) { try { return f(); } catch { return default; } }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object?>
        {
            public static readonly ReferenceEqualityComparer Instance = new();
            bool IEqualityComparer<object?>.Equals(object? x, object? y) => ReferenceEquals(x, y);
            int IEqualityComparer<object?>.GetHashCode(object? obj) =>
                obj is null ? 0 : RuntimeHelpers.GetHashCode(obj);
        }
    }

    public enum FarmerPhaseMirror
    {
        Waiting = 0,
        Buffing = 1,
        Gathering = 2,
        Stacking = 3,
        Fighting = 4,
    }

    public static class BocchiStateReaderEx
    {
        public static bool TryReadTyped(out FarmerPhaseMirror phase, out bool running, out string reason)
        {
            phase = FarmerPhaseMirror.Waiting; running = false; reason = "";
            if (!BocchiStateReader.TryRead(out var phaseStr, out running, out reason))
                return false;

            if (!string.IsNullOrEmpty(phaseStr) &&
                Enum.TryParse(phaseStr, ignoreCase: true, out FarmerPhaseMirror p))
            {
                phase = p;
                return true;
            }

            reason = $"无法识别 Phase: {phaseStr}";
            return true; // 仍返回 true，但 phase 维持默认 Waiting
        }
    }
}