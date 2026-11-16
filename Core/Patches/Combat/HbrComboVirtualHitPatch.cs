using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Add the form's unlimited medium and the physical card's chase hit before
// native power listeners apply real additions and all their doublings.
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyAttackHitCount))]
internal static class HbrComboVirtualHitPatch
{
    private static void Prefix(AttackCommand __1, ref int __2) =>
        __2 = HbrComboHelper.AddVirtualNativeHits(__1, __2);
}
