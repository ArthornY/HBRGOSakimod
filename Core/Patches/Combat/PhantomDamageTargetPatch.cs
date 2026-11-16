using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
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
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;

    private static void Prefix(object[] __args)
    {
        IEnumerable<Creature> targets = NativeDamageApiCompatibility.ReadTargets(__args);
        NativeDamageApiCompatibility.WriteTargets(__args, targets, RedirectTargets(targets),
            target => PhantomHelper.GetDamageDefender(target?.Player) ?? target);
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
        HpOnlyDamageContext.RecordPhysicalTarget(target);
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
