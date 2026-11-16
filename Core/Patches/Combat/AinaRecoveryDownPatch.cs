using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
internal static class AinaRecoveryDownHealPatch
{
    // Regular healing only. Structural phase HP assignments never enter Heal.
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Creature creature, ref decimal amount) => amount *= HbrRecoveryDownPower.Factor(creature);
}
[HarmonyPatch(typeof(HighDimensionalShellPower), nameof(HighDimensionalShellPower.Restore))]
internal static class AinaRecoveryDownDpPatch
{ private static void Prefix(HighDimensionalShellPower __instance, ref decimal amount) => amount *= HbrRecoveryDownPower.Factor(__instance.Owner); }
[HarmonyPatch(typeof(HighDimensionalShellPower), nameof(HighDimensionalShellPower.RestoreWithCeiling))]
internal static class AinaRecoveryDownOvercapDpPatch
{ private static void Prefix(HighDimensionalShellPower __instance, ref decimal amount) => amount *= HbrRecoveryDownPower.Factor(__instance.Owner); }
