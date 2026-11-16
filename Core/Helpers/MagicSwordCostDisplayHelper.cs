using System.Collections.Concurrent;
using System.Reflection;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MagicSwordCostDisplayHelper
{
    private static readonly ConcurrentDictionary<System.Type, bool> CostModifiers = new();

    internal static bool TryGetQuote(CardModel card, PileType displayedPile, bool visible, out SwordheartPaymentQuote quote)
    {
        quote = default;
        return visible && displayedPile == PileType.Hand && card?.IsMutable == true &&
            card.Pile?.Type == PileType.Hand && LocalContext.IsMe(card.Owner) &&
            MagicSwordEnergyPaymentHelper.TryGetPaymentQuote(card, out quote);
    }

    internal static bool AffectsCost(PowerModel power) => power is SwordheartPower or SwordheartClarityPower or
        MagicSwordModePower or MagicSwordUniversalityPower or ReptureDualSwordsPower || power != null && CostModifiers.GetOrAdd(power.GetType(), type =>
            Overrides(type, "TryModifyEnergyCostInCombat") || Overrides(type, "TryModifyEnergyCostInCombatLate") ||
            Overrides(type, "TryModifyStarCost"));

    private static bool Overrides(System.Type type, string method) =>
        type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.DeclaringType is { } declared &&
        declared != typeof(AbstractModel);
}
