using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Patches.Combat;

internal static class MomentOfReignMaxHpTarget
{
    internal static MomentOfReign Get(Creature creature) =>
        creature?.Player is { } player && ReferenceEquals(player.Creature, creature)
            ? player.GetRelic<MomentOfReign>()
            : null;
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp))]
internal static class MomentOfReignGainMaxHpPatch
{
    // Must run before DeflectorProtection converts the same gain into DP.
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Creature creature, decimal amount, ref Task __result)
    {
        MomentOfReign reign = MomentOfReignMaxHpTarget.Get(creature);
        if (reign == null || amount < 0m) return true;
        decimal delta = MomentOfReign.GetRequestedMaxHpChange(creature, amount, PhantomMaxHpChange.Gain);
        if (delta == 0) return true;
        __result = reign.RedirectMaxHpChange(delta, PhantomMaxHpChange.Gain);
        return false;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp))]
internal static class MomentOfReignLoseMaxHpPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(PlayerChoiceContext choiceContext, Creature creature,
        decimal amount, bool isFromCard, ref Task __result)
    {
        MomentOfReign reign = MomentOfReignMaxHpTarget.Get(creature);
        if (reign == null || amount < 0m) return true;
        decimal delta = MomentOfReign.GetRequestedMaxHpChange(creature, amount, PhantomMaxHpChange.Lose);
        if (delta == 0) return true;
        __result = reign.RedirectMaxHpChange(delta, PhantomMaxHpChange.Lose, choiceContext, isFromCard);
        return false;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp))]
internal static class MomentOfReignSetMaxHpPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Creature creature, ref decimal amount, ref Task<decimal> __result)
    {
        MomentOfReign reign = MomentOfReignMaxHpTarget.Get(creature);
        if (reign != null)
        {
            if (MomentOfReign.GetRequestedMaxHpChange(creature, amount, PhantomMaxHpChange.Set) == 0)
                return true;
            __result = Redirect(reign, creature, amount);
            return false;
        }
        if (creature?.PetOwner?.GetRelic<MomentOfReign>() is { } phantomReign &&
            PhantomHelper.IsOwnedMainPhantom(creature, phantomReign.Owner))
            phantomReign.NormalizeActivePhantomMaxHp(creature, ref amount);
        return true;
    }

    private static async Task<decimal> Redirect(MomentOfReign reign, Creature creature, decimal amount)
    {
        await reign.RedirectMaxHpChange(
            MomentOfReign.GetRequestedMaxHpChange(creature, amount, PhantomMaxHpChange.Set),
            PhantomMaxHpChange.Set);
        // The summoner did not change max HP; returning a gain could make callers heal them.
        return 0m;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxAndCurrentHp))]
internal static class MomentOfReignSetMaxAndCurrentHpPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Creature creature, decimal amount, ref Task __result)
    {
        MomentOfReign reign = MomentOfReignMaxHpTarget.Get(creature);
        if (reign == null) return true;
        decimal delta = MomentOfReign.GetRequestedMaxHpChange(creature, amount, PhantomMaxHpChange.SetAndCurrent);
        if (delta == 0) return true;
        __result = reign.RedirectMaxHpChange(delta, PhantomMaxHpChange.SetAndCurrent);
        return false;
    }
}
