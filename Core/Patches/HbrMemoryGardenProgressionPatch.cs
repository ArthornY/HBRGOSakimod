using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class HbrMemoryGardenCardScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(RunState), "AddCard", new[] { typeof(CardModel) });
        yield return AccessTools.Method(typeof(CombatState), "AddCard", new[] { typeof(CardModel) });
    }

    private static void Postfix(CardModel card) => HbrMemoryGardenProgression.Synchronize(card);
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Downgrade))]
internal static class HbrMemoryGardenDowngradeCommandPatch
{
    private static bool Prefix(CardModel card)
    {
        if (HbrMemoryGardenProgression.IsRebuilding || !HbrMemoryGardenProgression.IsRealCard(card)) return true;
        if (!CombatManager.Instance.IsEnding && HbrMemoryGardenProgression.TryDowngrade(card) && card.Pile?.Type == PileType.Deck)
            card.Owner.RunState.CurrentMapPointHistoryEntry?.GetEntry(card.Owner.NetId).DowngradedCards.Add(card.Id);
        return false;
    }
}
