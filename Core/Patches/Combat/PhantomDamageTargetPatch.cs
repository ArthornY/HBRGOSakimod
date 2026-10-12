using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class PhantomAttackTargetPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref IReadOnlyList<Creature> __result)
    {
        __result = PhantomDamageTargetPatch.RedirectTargets(__result);
    }
}

[HarmonyPatch]
internal static class PhantomDamageTargetPatch
{
    private static System.Reflection.MethodBase TargetMethod()
    {
        return AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
            .Where(method => method.Name == nameof(CreatureCmd.Damage))
            .Where(method =>
            {
                System.Reflection.ParameterInfo[] parameters =
                    method.GetParameters();
                return parameters.Length is 6 or 7 &&
                    parameters[0].ParameterType ==
                        typeof(PlayerChoiceContext) &&
                    parameters[1].ParameterType ==
                        typeof(IEnumerable<Creature>) &&
                    parameters[2].ParameterType == typeof(decimal) &&
                    parameters[3].ParameterType == typeof(ValueProp) &&
                    parameters[4].ParameterType == typeof(Creature) &&
                    parameters[5].ParameterType == typeof(CardModel);
            })
            .OrderBy(method => method.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(
                typeof(CreatureCmd).FullName,
                nameof(CreatureCmd.Damage));
    }

    private static void Prefix(ref IEnumerable<Creature> targets)
    {
        targets = RedirectTargets(targets);
    }

    internal static IReadOnlyList<Creature> RedirectTargets(
        IEnumerable<Creature> targets)
    {
        return targets
            .Select(target =>
                PhantomHelper.GetDamageDefender(target?.Player) ?? target)
            .Where(target => target != null)
            .Distinct()
            .OrderBy(target => PhantomHelper.IsOwnedMainPhantom(target, target.PetOwner)
                ? 0 : PhantomHelper.IsPhantomMinion(target) ? 1 : 2)
            .ToArray();
    }
}

internal static class PhantomDamageContext
{
    private static readonly Stack<Creature> Targets = new();

    internal static bool IsRedirectingBlock { get; set; }

    internal static void Push(Creature target)
    {
        Targets.Push(target?.HasPower<PhantomPower>() == true
            ? target
            : null);
    }

    internal static Creature PopForBlockResolution()
    {
        return Targets.Count > 0
            ? Targets.Pop()
            : null;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeDamageReceived))]
internal static class PhantomBeforeDamageReceivedPatch
{
    private static void Prefix(Creature target)
    {
        PhantomDamageContext.Push(target);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyUnblockedDamageTarget))]
internal static class PhantomUnblockedDamageTargetPatch
{
    private static void Postfix(
        Creature originalTarget,
        ref Creature __result)
    {
        if (originalTarget?.HasPower<PhantomPower>() == true)
        {
            __result = originalTarget;
        }
    }
}
