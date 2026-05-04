using System.Reflection;
using Dalamud.Interface.Windowing;

namespace AutoRaidHelper.Utils;

internal static class WindowSystemCompat
{
    public static void AddWindow(WindowSystem windowSystem, Window window)
    {
        Invoke(windowSystem, "AddWindow", [window]);
    }

    public static void RemoveAllWindows(WindowSystem windowSystem)
    {
        Invoke(windowSystem, "RemoveAllWindows", []);
    }

    private static object? Invoke(WindowSystem windowSystem, string methodName, object?[] args)
    {
        var method = windowSystem
            .GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(candidate => MethodMatches(candidate, methodName, args));

        if (method == null)
        {
            throw new MissingMethodException(windowSystem.GetType().FullName, methodName);
        }

        return method.Invoke(windowSystem, args);
    }

    private static bool MethodMatches(MethodInfo candidate, string methodName, object?[] args)
    {
        if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
        {
            return false;
        }

        var parameters = candidate.GetParameters();
        if (parameters.Length != args.Length)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            var argument = args[i];
            if (argument == null)
            {
                if (parameters[i].ParameterType.IsValueType &&
                    Nullable.GetUnderlyingType(parameters[i].ParameterType) == null)
                {
                    return false;
                }

                continue;
            }

            if (!parameters[i].ParameterType.IsInstanceOfType(argument))
            {
                return false;
            }
        }

        return true;
    }
}
