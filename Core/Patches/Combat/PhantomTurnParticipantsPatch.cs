using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

internal static class PhantomTurnParticipants
{
    internal static MethodInfo ResolveHook(Type participantsType, params string[] names)
    {
        Type[] parameterTypes = { typeof(ICombatState), typeof(CombatSide), participantsType };
        MethodInfo[] methods = typeof(Hook).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (string name in names)
        {
            MethodInfo method = methods.FirstOrDefault(candidate => candidate.Name == name
                && candidate.ReturnType == typeof(Task)
                && candidate.GetParameters().Select(parameter => parameter.ParameterType)
                    .SequenceEqual(parameterTypes));
            if (method != null) return method;
        }

        throw new MissingMethodException(
            $"Phantom turn participants could not find a compatible Hook method ({string.Join(" or ", names)}). "
            + $"Expected public static Task (ICombatState, CombatSide, {participantsType}); "
            + $"loaded game assembly: {typeof(Hook).Assembly.FullName}.");
    }

    internal static IReadOnlyList<Creature> IncludePhantoms(ICombatState combatState,
        CombatSide side, IEnumerable<Creature> participants)
    {
        List<Creature> result = participants.ToList();
        HashSet<Creature> original = result.ToHashSet();
        foreach (Creature phantom in combatState.Allies)
        {
            if (phantom.IsAlive && phantom.Side == side &&
                phantom.PetOwner is { } player && original.Contains(player.Creature) &&
                phantom.HasPower<PhantomPower>() && !result.Contains(phantom))
            {
                result.Add(phantom);
            }
        }
        return result;
    }
}

// Only real side-turn boundaries count; an extra phantom action is not another turn.
[HarmonyPatch]
internal static class PhantomStartTurnParticipantsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return PhantomTurnParticipants.ResolveHook(typeof(IReadOnlyList<Creature>), "BeforeSideTurnStart");
        yield return PhantomTurnParticipants.ResolveHook(typeof(IReadOnlyList<Creature>), "AfterSideTurnStart");
    }

    private static void Prefix(ICombatState __0, CombatSide __1, ref IReadOnlyList<Creature> __2) =>
        __2 = PhantomTurnParticipants.IncludePhantoms(__0, __1, __2);
}

[HarmonyPatch]
internal static class PhantomEndTurnParticipantsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // v0.111 renamed these dispatchers; select one compatible method per phase.
        yield return PhantomTurnParticipants.ResolveHook(typeof(IEnumerable<Creature>),
            "BeforeTurnEnd", "BeforeSideTurnEnd");
        yield return PhantomTurnParticipants.ResolveHook(typeof(IEnumerable<Creature>),
            "AfterTurnEnd", "AfterSideTurnEnd");
    }

    private static void Prefix(ICombatState __0, CombatSide __1, ref IEnumerable<Creature> __2) =>
        __2 = PhantomTurnParticipants.IncludePhantoms(__0, __1, __2);
}
