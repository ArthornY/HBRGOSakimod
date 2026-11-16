using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class DemonKingAndFamiliarBondHelper
{
    internal static int GetKinds(Player player) =>
        (ThirtyOneBBondHelper.HasPermanent<WorldIsMyLaboratoryHbrgoSaki>(player) ? 1 : 0) +
        (ThirtyOneBBondHelper.HasPermanent<WorldConquestHbrgoSaki>(player) ? 1 : 0);

    private static DemonKingAndFamiliarBond Bond(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (source is not (WorldIsMyLaboratoryHbrgoSaki or WorldConquestHbrgoSaki) ||
            !source.IsMutable || dealer == null || !props.IsPoweredAttack() || source.Owner?.Creature != dealer ||
            HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _)) return null;
        return source.Owner.GetRelic<DemonKingAndFamiliarBond>() is { IsActive: true } bond ? bond : null;
    }
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        target != null && target.Side != dealer?.Side && Bond(source, dealer, target, props) is { } bond
            ? bond.Bonus(source is WorldConquestHbrgoSaki, target) : 0m;

    // Called only after the DP absorber accepted its pending break and finished
    // applying Break. An already-broken target, manual DP removal or unrelated
    // PowerCmd.Apply<BreakPower> is never enough to earn this exact target receipt.
    internal static void OnDpBroken(Creature target, Creature dealer, CardModel source, ValueProp props, bool alreadyBroken)
    {
        if (alreadyBroken || source is not WorldConquestHbrgoSaki || target?.IsAlive != true ||
            !target.HasPower<BreakPower>() || target.Side == dealer?.Side) return;
        Bond(source, dealer, target, props)?.RecordOpenedTarget(target);
    }
}
