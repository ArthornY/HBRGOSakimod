using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

internal static class RanaByakkoCombatHelper
{
    private sealed class MatchaState { internal bool WhiteGunUsed; internal int Improvisation = -1; }
    private sealed class Battle
    {
        internal object Combat;
        internal bool ResolvingCritical;
        internal readonly Queue<MatchaParfaitHbrgoSaki[]> PendingCriticals = new();
        internal readonly Dictionary<CardModel, MatchaState> Cards = new();
    }
    private static readonly ConditionalWeakTable<Player, Battle> Battles = new();
    private static readonly ConditionalWeakTable<List<Reward>, HashSet<Player>> RewardSources = new();
    private static readonly AsyncLocal<PawPrintScope> CurrentPawPrint = new();

    private static Battle GetBattle(Player player)
    {
        Battle state = Battles.GetOrCreateValue(player);
        object combat = player.Creature.CombatState;
        if (!ReferenceEquals(state.Combat, combat))
        {
            state.Combat = combat;
            state.ResolvingCritical = false;
            state.PendingCriticals.Clear();
            state.Cards.Clear();
        }
        return state;
    }

    private static MatchaState GetCardState(MatchaParfaitHbrgoSaki card)
    {
        Battle battle = GetBattle(card.Owner);
        CardModel key = card.DeckVersion ?? card;
        if (!battle.Cards.TryGetValue(key, out MatchaState state))
        {
            state = new MatchaState
            {
                WhiteGunUsed = card.WhiteGunUsedThisCombat,
                Improvisation = card.ImprovisationEffect
            };
            battle.Cards.Add(key, state);
        }
        return state;
    }

    internal static void InitializeMatchaBattle(MatchaParfaitHbrgoSaki card)
    {
        if (card.Owner == null) return;
        Battle battle = GetBattle(card.Owner);
        battle.Cards[card.DeckVersion ?? card] = new MatchaState
        { WhiteGunUsed = false, Improvisation = card.ImprovisationEffect };
        // Personal turns rebuild their temporary deck from permanent cards.
        // Publish this battle's choice immediately, before the first play, so
        // those copies display the same choice and inherit the once-only flag.
        if (card.DeckVersion is MatchaParfaitHbrgoSaki origin && origin.Owner == card.Owner)
            origin.SetBattleState(false, card.ImprovisationEffect);
    }

    internal static void SynchronizeMatchaBattleState(MatchaParfaitHbrgoSaki card)
    {
        MatchaState state = GetCardState(card);
        if (card.HasUpgradeEffect(MatchaParfaitHbrgoSaki.Improvisation))
        {
            if (state.Improvisation < 0)
                state.Improvisation = card.Owner.RunState.Rng.Niche.NextInt(3);
            card.SetImprovisation(state.Improvisation);
            if (card.DeckVersion is MatchaParfaitHbrgoSaki origin && origin.Owner == card.Owner)
                origin.SetImprovisation(state.Improvisation);
        }
        if (state.WhiteGunUsed) card.MarkWhiteGunUsed();
    }

    internal static bool TryUseWhiteGun(MatchaParfaitHbrgoSaki card)
    {
        MatchaState state = GetCardState(card);
        if (state.WhiteGunUsed || card.WhiteGunUsedThisCombat) return false;
        state.WhiteGunUsed = true;
        card.MarkWhiteGunUsed();
        if (card.DeckVersion is MatchaParfaitHbrgoSaki origin && origin.Owner == card.Owner)
            origin.MarkWhiteGunUsed();
        return true;
    }

    internal static bool HasDp(Creature creature) => creature != null &&
        (creature.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m ||
         creature.Player?.GetRelic<DeflectorProtection>()?.Dp > 0);

    internal static decimal GetDestructionMultiplier(CardModel card) => card switch
    {
        MatchaParfaitHbrgoSaki matcha when matcha.HasUpgradeEffect(MatchaParfaitHbrgoSaki.Improvisation)
            && matcha.ImprovisationEffect == 2 => 2m,
        ByakkoRoarHbrgoSaki => 1.5m,
        _ => 1m
    };

    // Caller has already checked the raw d100 critical interval. No final-roll
    // or absolute-critical test is performed here. Snapshot the hand so a free
    // play cannot add itself or a freshly drawn card to the same batch.
    internal static async Task ResolveRawCritical(PlayerChoiceContext context, Player player)
    {
        if (player?.PlayerCombatState == null || player.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
        await ResolveCriticalBatch(context, player, SnapshotCriticalHand(player));
    }

    private static MatchaParfaitHbrgoSaki[] SnapshotCriticalHand(Player player) =>
        PileType.Hand.GetPile(player).Cards.OfType<MatchaParfaitHbrgoSaki>()
            .Where(card => card.HasUpgradeEffect(MatchaParfaitHbrgoSaki.InterestingWoman)).ToArray();

    private static async Task ResolveCriticalBatch(PlayerChoiceContext context, Player player,
        MatchaParfaitHbrgoSaki[] hand)
    {
        if (player?.PlayerCombatState == null || player.Creature.IsDead ||
            CombatManager.Instance.IsOverOrEnding || hand.Length == 0) return;
        Battle battle = GetBattle(player);
        if (battle.ResolvingCritical) return;
        battle.ResolvingCritical = true;
        try
        {
            foreach (MatchaParfaitHbrgoSaki card in hand)
            {
                if (player.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) break;
                if (card.Owner == player && card.Pile?.Type == PileType.Hand && !card.HasBeenRemovedFromState)
                    await CardCmd.AutoPlay(context, card, null);
            }
        }
        finally { battle.ResolvingCritical = false; }
    }

    internal static void QueueRawCritical(Player player)
    {
        if (player?.Creature.CombatState == null || player.PlayerCombatState == null) return;
        Battle battle = GetBattle(player);
        // A crit caused by this automatic batch cannot enqueue the same batch
        // again after its async frame unwinds.
        if (battle.ResolvingCritical) return;
        // Eligibility belongs to the instant the die resolves. Cannon and
        // defense-roll tails may draw or discard before the awaited drain.
        MatchaParfaitHbrgoSaki[] hand = SnapshotCriticalHand(player);
        if (hand.Length > 0) battle.PendingCriticals.Enqueue(hand);
    }

    internal static async Task ResolvePendingRawCritical(PlayerChoiceContext context, Player player)
    {
        if (player?.PlayerCombatState == null) return;
        Battle battle = GetBattle(player);
        if (battle.ResolvingCritical) return;
        while (battle.PendingCriticals.Count > 0 && player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            MatchaParfaitHbrgoSaki[] hand = battle.PendingCriticals.Dequeue();
            await ResolveCriticalBatch(context, player, hand);
        }
    }

    internal static void AddWildCatRewards(Player player, List<Reward> rewards, AbstractRoom room)
    {
        if (player?.Deck == null || rewards == null || room is not CombatRoom) return;
        HashSet<Player> processed = RewardSources.GetOrCreateValue(rewards);
        if (processed.Contains(player)) return;
        foreach (MatchaParfaitHbrgoSaki card in player.Deck.Cards.OfType<MatchaParfaitHbrgoSaki>())
        {
            if (card.Owner != player || card.HasBeenRemovedFromState ||
                !card.HasUpgradeEffect(MatchaParfaitHbrgoSaki.WildCat)) continue;
            processed.Add(player);
            // Native card rewards retain their normal skip button, generation
            // rules and per-character reward pools, including elite/Boss rooms.
            rewards.Add(NativeRewardApiCompatibility.Card(
                CardCreationOptions.ForRoom(player, room.RoomType), 3, player));
            break;
        }
    }

    internal sealed class PawPrintScope : IDisposable
    {
        internal readonly MatchaParfaitHbrgoSaki Card;
        internal readonly PawPrintScope Previous;
        internal readonly HashSet<Creature> Touched = new();
        internal PawPrintScope(MatchaParfaitHbrgoSaki card)
        { Card = card; Previous = CurrentPawPrint.Value; CurrentPawPrint.Value = this; }
        public void Dispose()
        { if (ReferenceEquals(CurrentPawPrint.Value, this)) CurrentPawPrint.Value = Previous; }
    }
    internal static PawPrintScope BeginPawPrint(MatchaParfaitHbrgoSaki card) => new(card);

    internal static void BeforeMatchaDamage(CardModel source, IEnumerable<Creature> targets)
    {
        PawPrintScope scope = CurrentPawPrint.Value;
        if (source is not MatchaParfaitHbrgoSaki card || !ReferenceEquals(scope?.Card, card) ||
            !card.HasUpgradeEffect(MatchaParfaitHbrgoSaki.PawPrint)) return;
        foreach (Creature target in targets)
        {
            if (target == null || !scope.Touched.Add(target) || !HbrBattleStatsHelper.IsEnabled(target)) continue;
            foreach (FragilePower fragile in target.Powers.OfType<FragilePower>().Where(power => power.Amount > 0).ToArray())
            {
                fragile.ConfigureHbr(DamageAmplificationLimits.AddSaturated(fragile.HbrEffectPercent, 10m),
                    fragile.ModifierAmount);
                target.InvokePowerModified(fragile, 0, true);
            }
        }
    }
}
