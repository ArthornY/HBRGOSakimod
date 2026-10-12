using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

internal static class TerrorEelHealthThreshold
{
    internal static int Calculate(int maxHp) => System.Math.Max(0, maxHp) / 2;

    internal static void Sync(Creature creature)
    {
        if (creature?.Monster is TerrorEel &&
            creature.GetPower<ShriekPower>() is { } shriek)
        {
            // The native amount is based on the eel's unmodified initial HP.
            // Keep its one-shot stun threshold at half of its actual maximum HP.
            shriek.SetAmount((int)System.Math.Min(BigCombatNumbers.NativeStatCap,
                decimal.Floor(LargeCreatureHealthHelper.GetMaxHp(creature) / 2m)));
        }
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ApplyInternal))]
internal static class TerrorEelInitialHealthThresholdPatch
{
    private static void Postfix(PowerModel __instance, Creature owner)
    {
        if (__instance is ShriekPower)
            TerrorEelHealthThreshold.Sync(owner);
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.SetMaxHpInternal))]
internal static class TerrorEelMaxHpThresholdPatch
{
    private static void Postfix(Creature __instance) =>
        TerrorEelHealthThreshold.Sync(__instance);
}
