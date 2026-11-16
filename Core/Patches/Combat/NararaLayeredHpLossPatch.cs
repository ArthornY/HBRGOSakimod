using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
internal static class NararaLayeredHpLossPatch
{
    // Run before both native HP loss and the existing large-health override.
    // Their damage receipts must contain the actual amount, not discarded spill.
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Creature __instance, ref decimal amount) =>
        amount = NararaLayeredHpHelper.ClampHpLoss(__instance, amount);
}
