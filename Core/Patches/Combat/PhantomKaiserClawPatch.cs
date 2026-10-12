using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class PhantomKaiserClawEntryPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Rocket), nameof(Rocket.AfterAddedToRoom));
        yield return AccessTools.Method(typeof(Crusher), nameof(Crusher.AfterAddedToRoom));
    }

    private static bool Prefix(Creature ____creature, ref Task __result)
    {
        if (!PhantomKaiserClawHelper.IsIndependentClaw(____creature)) return true;
        __result = PhantomKaiserClawHelper.ApplyEntryPowers(____creature);
        return false;
    }
}

[HarmonyPatch]
internal static class PhantomKaiserClawHurtPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Rocket), nameof(Rocket.AfterCurrentHpChanged));
        yield return AccessTools.Method(typeof(Crusher), nameof(Crusher.AfterCurrentHpChanged));
    }

    private static bool Prefix(Creature ____creature, ref Task __result)
    {
        if (!PhantomKaiserClawHelper.IsIndependentClaw(____creature)) return true;
        // The independent visuals receive HP/death events themselves; never
        // animate (or look up) the enemy boss's arms on behalf of a pet.
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class PhantomKaiserClawDeathPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Rocket), nameof(Rocket.BeforeDeath));
        yield return AccessTools.Method(typeof(Crusher), nameof(Crusher.BeforeDeath));
    }

    private static bool Prefix(MonsterModel __instance, Creature ____creature,
        Creature creature, ref Task __result)
    {
        if (!PhantomKaiserClawHelper.IsIndependentClaw(____creature)) return true;
        if (creature == ____creature) SfxCmd.PlayDeath(__instance);
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class PhantomKaiserClawStrengthPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Rocket), "ChargeUpMove");
        yield return AccessTools.Method(typeof(Crusher), "AdaptMove");
    }

    private static bool Prefix(Creature ____creature, ref Task __result)
    {
        if (!PhantomKaiserClawHelper.IsIndependentClaw(____creature)) return true;
        __result = PhantomKaiserClawHelper.GainStrength(____creature);
        return false;
    }
}

[HarmonyPatch(typeof(Rocket), "RechargeMove")]
internal static class PhantomRocketRechargePatch
{
    private static bool Prefix(Creature ____creature, ref Task __result)
    {
        if (!PhantomKaiserClawHelper.IsIndependentClaw(____creature)) return true;
        // This is a resting turn. Its native implementation only animates the
        // boss background; the original state machine still advances normally.
        __result = Task.CompletedTask;
        return false;
    }
}
