using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneEBondHelper
{
    private sealed class TurnUse
    {
        internal object FirstAttack;
        internal int ExtraHits;
        internal bool Called;
    }
    private sealed class Battle
    {
        internal ICombatState Combat;
        internal readonly Dictionary<(int OwnTurn, object Extra), TurnUse> Turns = new();
    }
    private sealed class Calling : IDisposable
    {
        internal readonly Calling Previous;
        internal readonly Player Owner;
        internal bool Active = true;
        internal Calling(Player owner) { Owner = owner; Previous = CurrentCall.Value; CurrentCall.Value = this; }
        public void Dispose() { Active = false; if (ReferenceEquals(CurrentCall.Value, this)) CurrentCall.Value = Previous; }
    }
    private static readonly ConditionalWeakTable<Player, Battle> Battles = new();
    private static readonly AsyncLocal<Calling> CurrentCall = new();
    internal static void ResetBattle(Player player) { if (player != null) Battles.Remove(player); }
    private static TurnUse Turn(Player player)
    {
        ICombatState combat = player.Creature.CombatState;
        Battle battle = Battles.GetOrCreateValue(player);
        if (!ReferenceEquals(battle.Combat, combat)) { battle.Combat = combat; battle.Turns.Clear(); }
        var key = (player.PlayerCombatState?.TurnNumber ?? combat.RoundNumber,
            OverclockImmediateTurnCoordinator.GetTurnToken(player));
        if (!battle.Turns.TryGetValue(key, out TurnUse use)) battle.Turns.Add(key, use = new());
        return use;
    }
    private static bool Active(Player player) => player?.Creature is { IsAlive: true, CombatState: not null } &&
        player.GetRelic<ThirtyOneECollection>() is { IsMelted: false } && HbrBattleStatsHelper.IsEnabled(player);
    private static bool IsOwnTurn(Player player) => player.Creature.CombatState.CurrentSide == CombatSide.Player ||
        OverclockImmediateTurnCoordinator.GetTurnToken(player) != null;
    internal static void CapturePlayStart(CardModel card, CardPlayContextHelper.PlayScope scope)
    {
        if (scope == null || card?.Type != CardType.Attack || !Active(card.Owner) || !IsOwnTurn(card.Owner)) return;
        int kinds = ThirtyOneEKeywordRules.GetPermanentDistinctCount(card.Owner);
        if (kinds < 2) return;
        TurnUse turn = Turn(card.Owner);
        if (turn.FirstAttack != null) return;
        turn.FirstAttack = scope;
        turn.ExtraHits = kinds;
    }
    internal static int GetExtraHits(Creature attacker, CardModel card)
    {
        if (card?.Type != CardType.Attack || !ReferenceEquals(card.Owner?.Creature, attacker) || !Active(card.Owner) ||
            !IsOwnTurn(card.Owner) || HotaruCannonStatsHelper.IsCannonAttackSource(attacker, card)) return 0;
        TurnUse turn = Turn(card.Owner);
        object current = CardPlayContextHelper.GetCurrentPlayToken(card);
        if (turn.FirstAttack != null) return ReferenceEquals(turn.FirstAttack, current) ? turn.ExtraHits : 0;
        int kinds = ThirtyOneEKeywordRules.GetPermanentDistinctCount(card.Owner);
        return kinds >= 2 ? kinds : 0;
    }
    private static bool IsCalledPlay(Player owner)
    {
        for (Calling scope = CurrentCall.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Owner, owner)) return true;
        return false;
    }
    internal static void RecordArrival(Player owner, ThirtyOneEMember member)
    {
        if (!Active(owner)) return;
        ThirtyOneECollection relic = owner.GetRelic<ThirtyOneECollection>();
        relic.RecordArrival(member);
        if (ThirtyOneEKeywordRules.GetPermanentDistinctCount(owner) < 6 || !relic.TryReserveAllArrived()) return;
        relic.Flash();
        PersonalImmediateTurnCoordinator.Request(owner, 1);
    }
    internal static async Task AfterCardPlayed(PlayerChoiceContext context, ThirtyOneECollection relic, CardPlay play)
    {
        CardModel card = play?.Card;
        Player owner = relic.Owner;
        if (!Active(owner) || card?.Owner != owner || ThirtyOneEKeywordRules.GetMember(card) is not { } member) return;
        RecordArrival(owner, member);
        int kinds = ThirtyOneEKeywordRules.GetPermanentDistinctCount(owner);
        // The sixth arrival is recorded before an automatic sibling can play.
        // Reserve this battle's reward before scheduling any asynchronous turn.
        if (kinds < 4 || !IsOwnTurn(owner) || IsCalledPlay(owner) || CombatManager.Instance.IsOverOrEnding) return;
        TurnUse turn = Turn(owner);
        if (turn.Called) return;
        // It is the first 31E play, even when its draw pile currently has no
        // eligible sibling. Never retry with a later card in this same turn.
        turn.Called = true;
        CardModel[] choices = PileType.Draw.GetPile(owner).Cards.Where(other => other.Owner == owner &&
            !other.HasBeenRemovedFromState && !other.Keywords.Contains(CardKeyword.Unplayable) &&
            ThirtyOneEKeywordRules.IsThirtyOneECard(other) && other.Id != card.Id).ToArray();
        if (choices.Length == 0) return;
        CardModel chosen = choices[owner.RunState.Rng.CombatCardGeneration.NextInt(0, choices.Length)];
        relic.Flash();
        using Calling call = new(owner);
        // Native AutoPlay owns target/choice randomness, X-cost resource rules,
        // and the result pile. It bypasses energy payment without moving to hand.
        await CardCmd.AutoPlay(context, chosen, null);
    }
}
