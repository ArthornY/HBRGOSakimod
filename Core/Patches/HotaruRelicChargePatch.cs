using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs.History;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain),
    new[] { typeof(RelicModel), typeof(Player), typeof(int) })]
internal static class HotaruRelicChargePatch
{
    private static bool Prefix(RelicModel relic, Player player, ref Task<RelicModel> __result)
    {
        if (player == null || relic is not (Millsage or HotaruIridescence)) return true;
        RelicModel existing = relic is Millsage
            ? player.GetRelic<Millsage>()
            : player.GetRelic<HotaruIridescence>();
        if (existing == null) return true;
        relic.AssertMutable();
        if (relic.Owner != null && relic.Owner != player) return true;
        player.RunState.CurrentMapPointHistoryEntry?.GetEntry(player.NetId)
            .RelicChoices.Add(new ModelChoiceHistoryEntry(relic.Id, wasPicked: true));
        bool changed = existing is Millsage millsage ? millsage.GainCharge() : ((HotaruIridescence)existing).GainCharge();
        __result = CompleteCharge(existing, player, changed);
        return false;
    }

    private static async Task<RelicModel> CompleteCharge(RelicModel existing, Player player, bool changed)
    {
        if (changed && player.GetRelic<Millsage>() is { RetiredCannonCount: > 0 } relic &&
            player.Creature.CombatState != null)
            await relic.RefreshEndgameCannon();
        return existing;
    }
}

[HarmonyPatch]
internal static class HotaruRelicDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicEventDescription));
    }

    private static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (__instance is HotaruIridescence iridescence) iridescence.AddDescriptionVariables(__result);
        else if (__instance is Millsage millsage && millsage.Layers >= Millsage.MaximumLayers)
            __result = new LocString("relics", "MILLSAGE.fullChargeDescription");
        else if (__instance is StrikeDummy && __instance.IsMutable &&
            HbrBattleStatsHelper.IsEnabled(__instance.Owner))
        {
            LocString replacement = new("relics", "STRIKE_DUMMY.hbrDescription");
            replacement.AddVariablesFrom(__result);
            __result = replacement;
        }
        else if (__instance is ThirtyOneACollection && __instance.IsMutable &&
            HbrBattleStatsHelper.IsEnabled(__instance.Owner))
        {
            // Dodge layers stand for 100 HBR points, so the Block and Dodge rules state them.
            LocString replacement = new("relics", "THIRTY_ONE_A_COLLECTION.hbrDescription");
            replacement.AddVariablesFrom(__result);
            __result = replacement;
        }
        else if (__instance is ElectronicSoldierManual manual &&
            (!manual.IsMutable || manual.IsHbrNumericsEnabled))
        {
            LocString replacement = new("relics", __instance.Id.Entry + ".hbrDescription");
            replacement.AddVariablesFrom(__result);
            __result = replacement;
        }
    }
}
