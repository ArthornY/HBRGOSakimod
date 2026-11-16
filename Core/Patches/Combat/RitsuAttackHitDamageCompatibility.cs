using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands.Builders;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Installation-time metadata only. No per-hit reflection, wrapper invocation,
// generic factory detour or assembly-wide Ritsu scan is introduced.
internal static class RitsuAttackHitDamageCompatibility
{
    private const string HookName = "STS2RitsuLib.Combat.AttackHits.AttackHitHook";
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private sealed record Binding(MethodInfo Hook, MethodBase[] Callers);
    private static readonly Dictionary<MethodInfo, Binding> Bindings = new();
    private static readonly object Gate = new();

    internal static MethodBase[] GetCallers(MethodInfo native) => GetBinding(native).Callers;
    internal static bool IsHitHook(MethodInfo method, MethodInfo native) =>
        method == GetBinding(native).Hook;

    private static Binding GetBinding(MethodInfo native)
    {
        lock (Gate)
        {
            if (Bindings.TryGetValue(native, out Binding existing)) return existing;
            Type hookType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name is "STS2-RitsuLib.Runtime" or "STS2-RitsuLib")
                .Select(assembly => assembly.GetType(HookName)).FirstOrDefault(type => type != null);
            if (hookType == null)
            {
                try { hookType = Type.GetType(HookName + ", STS2-RitsuLib.Runtime", false); }
                catch (FileNotFoundException) { /* Older Ritsu without this hook retains the native path. */ }
            }
            Binding result;
            if (hookType == null) result = new(null, Array.Empty<MethodBase>());
            else
            {
                ParameterInfo[] n = native.GetParameters();
                MethodInfo[] wrappers = hookType.GetMethods(Declared)
                    .Where(method => method.Name == "DamageWithAttackHitHooks" && method.ReturnType == native.ReturnType &&
                        method.GetParameters() is var p && p.Length == n.Length + 3 &&
                        p.Take(n.Length).Select(parameter => parameter.ParameterType).SequenceEqual(n.Select(parameter => parameter.ParameterType)) &&
                        p[^3].ParameterType == typeof(AttackCommand) && p[^2].ParameterType == typeof(int) && p[^1].ParameterType == typeof(decimal))
                    .ToArray();
                MethodInfo inner = hookType.GetMethods(Declared).SingleOrDefault(method => method.Name == "DamageAndAfterAttackHit");
                Type state = inner?.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
                MethodInfo body = state?.GetMethod("MoveNext", Declared);
                if (wrappers.Length != 1 || body == null || body.ContainsGenericParameters || wrappers[0].ContainsGenericParameters)
                    throw new MissingMethodException("Ritsu per-hit canonical wrapper/after-before-hit async leaf changed.");
                result = new(wrappers[0], new MethodBase[] { wrappers[0], body });
            }
            Bindings.Add(native, result);
            return result;
        }
    }
}
