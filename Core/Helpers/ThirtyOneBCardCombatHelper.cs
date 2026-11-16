using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Combat;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace HBRGOSakiMod.Core.Helpers;
internal static class ThirtyOneBCardCombatHelper
{
    private sealed class DeckCache { internal bool Dirty = true; internal bool Active; internal int Count = -1; }
    private static readonly ConditionalWeakTable<CardPile, DeckCache> Decks = new();
    internal static void InvalidatePermanent(Player player)
    { if (player?.Deck != null && Decks.TryGetValue(player.Deck, out DeckCache cache)) cache.Dirty = true; }
    private static bool HasDieTogether(Player player)
    {
        CardPile deck = player?.Deck;
        if (deck == null) return false;
        DeckCache cache = Decks.GetValue(deck, pile =>
        {
            DeckCache state = new(); pile.ContentsChanged += () => state.Dirty = true; return state;
        });
        if (cache.Dirty || cache.Count != deck.Cards.Count)
        {
            cache.Active = deck.Cards.OfType<FreshDeathHbrgoSaki>().Any(card => card.Owner == player &&
                !card.HasBeenRemovedFromState && card.HasUpgradeEffect(FreshDeathHbrgoSaki.DieTogether));
            cache.Count = deck.Cards.Count; cache.Dirty = false;
        }
        return cache.Active;
    }
    internal static decimal GetDamageMultiplier(Creature dealer, Creature target, CardModel source, ValueProp props)
    {
        decimal factor = 1m;
        if (dealer != null && props.IsPoweredAttack() && source?.Owner?.Creature == dealer && target?.Side != dealer.Side &&
            !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _))
        {
            bool hasDp = target?.Powers.OfType<HighDimensionalShellPower>().Any(shell => shell.CurrentDp > 0m) == true;
            if (source is HellfireBurnHbrgoSaki ichigo)
            {
                if (target != null && !hasDp) factor *= ichigo.HasUpgradeEffect(HellfireBurnHbrgoSaki.NoisyEntrustment) ? 1.5m : 1.3m;
                if (ichigo.HasUpgradeEffect(HellfireBurnHbrgoSaki.KillerShot) &&
                    dealer.CombatState.GetOpponentsOf(dealer).Count(enemy => enemy.IsAlive) == 1) factor *= 2m;
            }
            else if (source is FrozenMistMeowHbrgoSaki && hasDp) factor *= 1.3m;
        }
        if (target?.Side == CombatSide.Enemy && target.CombatState?.Players.Any(player => HasDieTogether(player) && player.Creature.IsAlive && player.Creature.Side != target.Side &&
            LargeCreatureHealthHelper.GetCurrentHp(player.Creature) < LargeCreatureHealthHelper.GetMaxHp(player.Creature) * .3m) == true) factor *= 1.3m;
        return factor;
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        source is FrozenMistMeowHbrgoSaki card && source.Owner?.Creature == dealer && props.IsPoweredAttack() &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _) &&
        card.HasUpgradeEffect(FrozenMistMeowHbrgoSaki.HalloweenMeowNight) ? 2m : 1m;
    internal static Task RestorePlayerDp(PlayerChoiceContext context, Creature target, decimal ratio, CardModel source) =>
        target?.Player is { } player ? HbrMatchaDpHelper.Restore(context, player, ratio, convertOverflow: false, source)
            : Task.CompletedTask;
}
