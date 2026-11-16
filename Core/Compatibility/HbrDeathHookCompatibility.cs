using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Compatibility;

// The native hook gained a CardModel argument in public-beta v0.113.0. Keep
// our existing death effects independent of that virtual method's signature.
internal interface IHbrAfterDeathListener
{
    Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength);
}

[HarmonyPatch]
internal static class HbrDeathHookCompatibility
{
    private sealed record Signature(
        MethodInfo Method,
        int ChoiceContext,
        int Creature,
        int RemovalPrevented,
        int AnimationLength);

    private static readonly Signature NativeHook = FindNativeHook();

    private static MethodBase TargetMethod() => NativeHook.Method;

    private static bool Prefix(
        AbstractModel __instance,
        object[] __args,
        ref Task __result)
    {
        if (__instance is not IHbrAfterDeathListener listener)
        {
            return true;
        }

        // Native Hook.AfterDeath still owns listener enumeration, task/choice
        // scheduling and execution-finished notifications. Only dispatch the
        // listener here; do not enumerate or finish it a second time.
        __result = listener.AfterDeath(
            (PlayerChoiceContext)__args[NativeHook.ChoiceContext],
            (Creature)__args[NativeHook.Creature],
            (bool)__args[NativeHook.RemovalPrevented],
            (float)__args[NativeHook.AnimationLength]);
        return false;
    }

    private static Signature FindNativeHook()
    {
        foreach (MethodInfo method in typeof(AbstractModel).GetMethods(
                     BindingFlags.Instance | BindingFlags.Public |
                     BindingFlags.DeclaredOnly))
        {
            if (method.Name != nameof(IHbrAfterDeathListener.AfterDeath) ||
                method.ReturnType != typeof(Task))
            {
                continue;
            }

            Type[] parameters = method.GetParameters()
                .Select(parameter => parameter.ParameterType).ToArray();
            Type[] expected =
            [
                typeof(PlayerChoiceContext), typeof(Creature),
                typeof(bool), typeof(float)
            ];
            if (parameters.Length is not (4 or 5) ||
                expected.Any(type => parameters.Count(candidate => candidate == type) != 1) ||
                parameters.Any(type => !expected.Contains(type) && type != typeof(CardModel)) ||
                parameters.Count(type => type == typeof(CardModel)) != parameters.Length - 4)
            {
                continue;
            }

            return new Signature(method,
                Array.IndexOf(parameters, typeof(PlayerChoiceContext)),
                Array.IndexOf(parameters, typeof(Creature)),
                Array.IndexOf(parameters, typeof(bool)),
                Array.IndexOf(parameters, typeof(float)));
        }

        throw new MissingMethodException(typeof(AbstractModel).FullName,
            "AfterDeath(PlayerChoiceContext, Creature, [CardModel], bool, float)");
    }
}
