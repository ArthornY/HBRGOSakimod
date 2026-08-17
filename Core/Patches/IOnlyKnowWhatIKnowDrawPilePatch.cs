using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NCardPileScreen), "OnPileContentsChanged")]
internal static class IOnlyKnowWhatIKnowDrawPilePatch
{
    private static readonly FieldInfo GridField =
        PrivateAccess.Field<NCardPileScreen>("_grid");

    private static bool Prefix(NCardPileScreen __instance)
    {
        CardPile pile = __instance.Pile;
        if (pile.Type != PileType.Draw ||
            pile.Cards.Count == 0 ||
            pile.Cards[0].Owner?.Creature?.HasPower<IOnlyKnowWhatIKnowPower>() != true)
        {
            return true;
        }

        NCardGrid grid = (NCardGrid)GridField.GetValue(__instance);
        grid.SetCards(
            pile.Cards,
            pile.Type,
            new List<SortingOrders> { SortingOrders.Ascending });
        return false;
    }
}
