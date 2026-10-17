using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Patches.Combat;

internal static class DeflectorProtectionMaxHpTarget
{
    internal static DeflectorProtection Get(Creature creature) =>
        creature?.Player is { } player && ReferenceEquals(player.Creature, creature) &&
        player.GetRelic<MomentOfReign>() == null
            ? player.GetRelic<DeflectorProtection>()
            : null;
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp))]
internal static class DeflectorProtectionMaxHpLossPatch
{
    // Phantom redirection and effects that cancel the loss must resolve first.
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(Creature creature, decimal amount, ref Task __result)
    {
        DeflectorProtection protection = DeflectorProtectionMaxHpTarget.Get(creature);
        if (protection == null || amount <= 0m) return true;

        // Match the existing max-HP gain conversion, including integer rounding.
        protection.LoseMaxDp((int)Math.Min(decimal.Floor(amount), 999999999m));
        __result = Task.CompletedTask;
        return false;
    }
}
