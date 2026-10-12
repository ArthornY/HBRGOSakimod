using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruEndgameCardChoice
{
    private sealed class PendingChoice { internal bool Active; }
    private static readonly ConditionalWeakTable<Millsage, PendingChoice> Pending = new();

    internal static async Task<bool> TryOffer(Player player, HotaruCannonKind kind)
    {
        if (player?.GetRelic<Millsage>() is not { IsMutable: true } relic ||
            !MemoryGardenSelectionScope.IsCurrentRun(player) ||
            player.RunState.CurrentRoom is not RestSiteRoom || !relic.CanOfferEndgameCard(kind))
            return false;
        PendingChoice pending = Pending.GetOrCreateValue(relic);
        if (pending.Active) return false;
        pending.Active = true;
        CardModel[] previews = Array.Empty<CardModel>();
        CardModel granted = null;
        bool acquired = false;
        try
        {
            MemoryGardenSelectionScope scope = MemoryGardenSelectionScope.Capture(player);
            // Preview choices never enter the permanent card scope. Only the
            // selected model is registered and added through the native command.
            previews = new CardModel[]
            {
                ModelDb.Card<HotaruSacrifice>().ToMutable(),
                ModelDb.Card<HotaruRedemption>().ToMutable()
            };
            foreach (CardModel preview in previews) preview.Owner = player;
            CardModel selected = await CardSelectCmd.FromChooseACardScreen(
                new BlockingPlayerChoiceContext(), previews, player, canSkip: true);
            scope.ThrowIfInvalid();
            if (!ReferenceEquals(player.GetRelic<Millsage>(), relic) ||
                !relic.CanOfferEndgameCard(kind)) return false;
            if (selected == null)
            {
                relic.DeclineEndgameOffers();
                return false;
            }
            if (!previews.Any(preview => ReferenceEquals(preview, selected))) return false;
            granted = player.RunState.CreateCard(ModelDb.GetById<CardModel>(selected.Id), player);
            CardPileAddResult result = await CardPileCmd.Add(granted, PileType.Deck);
            acquired = result.success;
            scope.ThrowIfInvalid();
            return acquired;
        }
        finally
        {
            // A pile callback may throw after insertion. Commit the offer only
            // when this exact reward actually reached Deck, so save/load cannot
            // swallow an unfinished dialog or grant the same offer twice.
            acquired |= granted != null && ReferenceEquals(granted.Owner, player) &&
                player.Deck.Cards.Any(card => ReferenceEquals(card, granted));
            if (acquired) relic.TryClaimEndgameOffer(kind);
            else if (granted is { HasBeenRemovedFromState: false, Pile: null })
                granted.RemoveFromState();
            foreach (CardModel preview in previews)
                if (!preview.HasBeenRemovedFromState) preview.RemoveFromState();
            pending.Active = false;
        }
    }
}
