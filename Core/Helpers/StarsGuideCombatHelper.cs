using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Helpers;

internal static class StarsGuideCombatHelper
{
    internal static int ActiveBondCount(Player player) => player?.Relics
        .Where(relic => !relic.IsMelted).OfType<IActiveDuoBond>().Count(bond => bond.IsBondActive) ?? 0;
    internal static decimal RecoveryRatio(StarsGuideYouHbrgoSaki card, int roll) =>
        card.BaseRecoveryRatio * (card.HasUpgradeEffect(StarsGuideYouHbrgoSaki.Fortune) && roll >= 48 ? 2m : 1m);
}
