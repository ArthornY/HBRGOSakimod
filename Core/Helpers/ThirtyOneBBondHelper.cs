using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneBBondHelper
{
    internal static Task GrantCharm(PlayerChoiceContext context, Creature target, decimal layers,
        Creature applier, CardModel source = null) =>
        layers > 0m ? PowerCmd.Apply<ProtectiveCharmPower>(context, target, layers, applier, source) : Task.CompletedTask;

    internal static decimal GetCharmDefenseBonusPercent(Creature holder) =>
        holder?.Player?.Relics.OfType<ThirtyOneBCollection>().FirstOrDefault() is { IsMelted: false, DisplayAmount: >= 4 } &&
        HbrBattleStatsHelper.IsEnabled(holder)
            ? Math.Max(0, holder.GetPowerAmount<ProtectiveCharmPower>()) * 4m : 0m;

    internal static bool HasPermanent<T>(Player player) where T : CardModel =>
        player?.Deck?.Cards.Any(card => card is T && ReferenceEquals(card.Owner, player) &&
            !card.HasBeenRemovedFromState) == true;

    internal static int SnackKinds(Player player) =>
        (HasPermanent<MatchaParfaitHbrgoSaki>(player) ? 1 : 0) + (HasPermanent<ByakkoRoarHbrgoSaki>(player) ? 1 : 0);

    internal static int SisterKinds(Player player) =>
        (HasPermanent<HellfireBurnHbrgoSaki>(player) ? 1 : 0) + (HasPermanent<FrozenMistMeowHbrgoSaki>(player) ? 1 : 0);
}
