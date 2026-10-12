using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ApplyInternal))]
internal static class PhantomPlowApplicationPatch
{
    private static void Prefix(PowerModel __instance, Creature owner, ref decimal amount)
    {
        // Keep blocked applications blocked; only replace an actual phantom's HP threshold.
        if (__instance is PlowPower && amount > 0m &&
            PhantomCeremonialBeastHelper.IsPhantomBeast(owner))
            amount = PhantomCeremonialBeastHelper.GetPlowThreshold(owner);
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.SetMaxHpInternal))]
internal static class PhantomPlowMaxHpPatch
{
    private static void Postfix(Creature __instance) =>
        PhantomCeremonialBeastHelper.RefreshPlowThreshold(__instance);
}
