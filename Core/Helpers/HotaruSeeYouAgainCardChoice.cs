using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.CardPools;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruSeeYouAgainCardChoice
{
    internal static CardModel[] GetSacrificeCandidates() => ModelDb.CardPool<HbrgoHotaruCardPool>()
        .AllCards
        // AllCards is the native pool before FilterThroughEpochs injects its
        // temporary colorless fallback. Ownership also excludes shared cards.
        .Where(card => card.Pool is HbrgoHotaruCardPool &&
            card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
        .DistinctBy(card => card.Id)
        .OrderBy(card => card.Id.Entry, StringComparer.Ordinal)
        .ToArray();

    internal static async Task SelectAndGenerate(PlayerChoiceContext context,
        CardModel source, bool upgrade)
    {
        var player = source.Owner;
        var combat = player?.Creature?.CombatState;
        if (combat == null || player.Creature.IsAlive != true ||
            CombatManager.Instance.IsOverOrEnding) return;

        CardModel[] previews = Array.Empty<CardModel>();
        CardModel generated = null;
        try
        {
            previews = GetSacrificeCandidates().Select(card => card.ToMutable()).ToArray();
            // Only the chosen card enters combat state. Browsing does not
            // create a permanent card or run AfterCreated on every option.
            foreach (CardModel preview in previews) preview.Owner = player;
            if (previews.Length == 0) return;
            CardModel selected = (await CardSelectCmd.FromSimpleGrid(context, previews, player,
                new CardSelectorPrefs(new LocString("cards", "HOTARU_SEE_YOU_AGAIN.selectionScreenPrompt"), 1)))
                .FirstOrDefault();
            if (selected == null || !previews.Any(preview => ReferenceEquals(preview, selected)) ||
                player.Creature.IsAlive != true ||
                !ReferenceEquals(player.Creature.CombatState, combat) ||
                CombatManager.Instance.IsOverOrEnding) return;

            generated = combat.CreateCard(ModelDb.GetById<CardModel>(selected.Id), player);
            if (upgrade && generated.IsUpgradable) CardCmd.Upgrade(generated);
            await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, player);
        }
        finally
        {
            foreach (CardModel preview in previews)
                if (!preview.HasBeenRemovedFromState) preview.RemoveFromState();
            // Native insertion can finish before a later hook fails. Keep
            // any card already inserted into a combat pile in that case.
            if (generated is { HasBeenRemovedFromState: false, Pile: null })
                generated.RemoveFromState();
        }
    }
}
