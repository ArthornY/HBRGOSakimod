using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyAHighSongSynergy
{
    internal static async Task Resolve(CardPlay cardPlay)
    {
        // High Song removes its token in OnPlay; its completed play still
        // qualifies even though that token is no longer in a combat pile.
        if (cardPlay?.Card is not HighSongHbrgoSaki highSong)
        {
            return;
        }

        Player player = highSong.Owner;
        if (!CanResolve(player) ||
            player.GetRelic<ThirtyACollection>() is not { IsMelted: false } collection ||
            collection.DisplayAmount < ThirtyAKeywordRules.TotalKinds)
        {
            return;
        }

        CardModel[] candidates = player.PlayerCombatState.DrawPile.Cards
            .Where(card => card.Owner == player && !card.HasBeenRemovedFromState &&
                ThirtyAKeywordRules.IsThirtyACard(card))
            .ToArray();
        if (candidates.Length == 0)
        {
            return;
        }

        CardModel selected = candidates[player.RunState.Rng.Niche.NextInt(0, candidates.Length)];
        // Move the existing combat card so its upgrades and six-sense attributes
        // stay intact. Native Add also applies the current hand-capacity rules.
        CardPileAddResult result = await CardPileCmd.Add(selected, PileType.Hand);
        if (result.success && CanResolve(player) && selected.Owner == player &&
            !selected.HasBeenRemovedFromState && selected.Pile == player.PlayerCombatState.Hand)
        {
            selected.EnergyCost.SetThisTurn(0);
            selected.InvokeEnergyCostChanged();
        }
    }

    private static bool CanResolve(Player player) =>
        player?.PlayerCombatState != null && player.Creature.IsAlive &&
        player.Creature.CombatState != null && !CombatManager.Instance.IsOverOrEnding;
}
