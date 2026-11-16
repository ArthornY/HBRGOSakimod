using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

// Suspend all five piles outside native combat hooks. Keep the original objects
// so their local costs, enchantments, state and exact pile order survive OD.
internal sealed class OverclockSuspendedHand
{
    private sealed class ConsumedOverclockCards
    {
        internal readonly HashSet<CardModel> DeckOrigins = new(ReferenceEqualityComparer.Instance);
    }

    private static readonly ConditionalWeakTable<ICombatState, ConsumedOverclockCards> ConsumedCards = new();
    private readonly Player _player;
    private readonly PlayerCombatState _playerCombatState;
    private readonly ICombatState _combatState;
    private readonly CardPile[] _piles;
    private readonly CardModel[][] _pileCards;
    private readonly HashSet<CardModel> _unavailableDeckCards;
    private CardModel[] _cards;
    private CardModel[] _overclockDeckCards = Array.Empty<CardModel>();
    private readonly int _handCardCount;
    private bool _deckPrepared;
    private bool _finished;

    public int SavedEnergy { get; }
    public int CardCount => _finished ? 0 : _handCardCount;

    private OverclockSuspendedHand(Player player)
    {
        _player = player;
        _playerCombatState = player.PlayerCombatState;
        _combatState = player.Creature.CombatState;
        _piles = _playerCombatState.AllPiles.ToArray();
        _pileCards = _piles.Select(pile => pile.Cards.ToArray()).ToArray();
        _cards = _pileCards.SelectMany(cards => cards).ToArray();
        _unavailableDeckCards = GetUnavailableDeckCards();
        _handCardCount = _playerCombatState.Hand.Cards.Count;
        SavedEnergy = _playerCombatState.Energy;
    }

    // Call on the game thread after pending plays and choices finish. Native
    // pile events update tracking/UI, without draw, discard or turn callbacks.
    public static bool TrySuspend(Player player, out OverclockSuspendedHand suspended)
    {
        suspended = null;
        if (player?.PlayerCombatState == null || player.Creature.CombatState == null ||
            !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding) return false;
        suspended = new OverclockSuspendedHand(player);
        try
        {
            for (int index = 0; index < suspended._piles.Length; index++)
                foreach (CardModel card in suspended._pileCards[index])
                    suspended._piles[index].RemoveInternal(card);
        }
        catch
        {
            // Also restores a partially detached snapshot after a pile event fails.
            suspended.TryRestore();
            throw;
        }
        CardModel[] originals = suspended._cards;
        UpdateLocalHand(player, hand =>
        {
            foreach (CardModel card in originals) RemoveHandVisual(hand, card);
        });
        return true;
    }

    // Native card-only combat setup clones Deck, binds DeckVersion and shuffles.
    // Preserve upgrades and deep-cloned enchantments without resetting energy,
    // block, PlayerCombatState, powers, or replaying generated/combat-start hooks.
    internal CardModel[] PrepareOverclockDeck()
    {
        if (_finished || !IsCurrentLiveCombat())
            throw new InvalidOperationException("Cannot prepare an OD deck outside its suspended combat.");
        if (_deckPrepared) return _overclockDeckCards;
        if (_piles.Any(pile => !pile.IsEmpty))
            throw new InvalidOperationException("OD deck preparation requires empty combat piles.");
        if (_combatState is not CombatState combat)
            throw new InvalidOperationException("OD deck preparation requires a native combat state.");
        try
        {
            _player.PopulateCombatStateCompat(_player.RunState.Rng.Shuffle, combat);
            _overclockDeckCards = _playerCombatState.AllCards.ToArray();
            // A fresh deck must not bring back this battle's played powers or
            // exhausted cards. Match the permanent origin, never the model ID:
            // a second unplayed copy of the same card must remain available.
            foreach (CardModel card in _overclockDeckCards)
                if (card.DeckVersion is { } origin && _unavailableDeckCards.Contains(origin))
                    card.RemoveFromState();
            _overclockDeckCards = _playerCombatState.AllCards.ToArray();
            // PopulateCombatState adds models directly. OD has no opening deck
            // animation to finish those adds, but native pile buttons count
            // CardAddFinished rather than ContentsChanged.
            foreach (CardPile pile in _piles)
                if (pile.Type != PileType.Hand)
                    foreach (CardModel _ in pile.Cards.ToArray()) pile.InvokeCardAddFinished();
            _deckPrepared = true;
            return _overclockDeckCards;
        }
        catch
        {
            _overclockDeckCards = _playerCombatState.AllCards.ToArray();
            TryRestore();
            throw;
        }
    }

    private HashSet<CardModel> GetUnavailableDeckCards()
    {
        var unavailable = new HashSet<CardModel>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < _piles.Length; index++)
            if (_piles[index].Type == PileType.Exhaust)
                foreach (CardModel card in _pileCards[index])
                    if (card.DeckVersion is { } origin) unavailable.Add(origin);
        foreach (var entry in CombatManager.Instance.History.CardPlaysFinished)
        {
            CardModel card = entry.CardPlay.Card;
            if (ReferenceEquals(card.Owner, _player) && card.Type == CardType.Power &&
                card.DeckVersion is { } origin &&
                !MoreThanOneTruthCombatHelper.CanReenterExtraTurnDeck(origin)) unavailable.Add(origin);
        }
        if (ConsumedCards.TryGetValue(_combatState, out ConsumedOverclockCards consumed))
            unavailable.UnionWith(consumed.DeckOrigins);
        return unavailable;
    }

    public bool TryRestore()
    {
        if (_finished) return false;
        if (!IsCurrentLiveCombat()) { Abandon(); return false; }
        // Only request85's explicit boundary receipt can carry a card through
        // the disposable pool. A deck mirror returns its suspended original;
        // it never creates a second combat copy of that physical origin.
        CardModel[] carry = CollectKurosawaCarry();
        var carried = new HashSet<CardModel>(carry, ReferenceEqualityComparer.Instance);
        var originalsSet = new HashSet<CardModel>(_cards, ReferenceEqualityComparer.Instance);
        var preserve = new HashSet<CardModel>(carry.Where(card => !originalsSet.Contains(card)), ReferenceEqualityComparer.Instance);
        ClearOverclockCards(preserve);
        foreach (CardModel card in preserve)
        {
            if (card.Pile?.IsCombatPile == true) card.RemoveFromCurrentPile(true);
            // These are actual temporary cards, rather than an ordinary card
            // whose paused turn-local modifiers must remain untouched.
            card.EndOfTurnCleanup();
        }
        CardModel[] originals = _cards;
        // Failed suspension may leave some originals in their piles: remove
        // these before appending the full snapshot in the saved exact order.
        foreach (CardModel card in originals)
            if (ReferenceEquals(card.Owner, _player) && card.Pile?.IsCombatPile == true)
                card.RemoveFromCurrentPile();
        for (int index = 0; index < _piles.Length; index++)
        {
            foreach (CardModel card in _pileCards[index])
            {
                if (carried.Contains(card) && _piles[index].Type != PileType.Hand) continue;
                if (card.HasBeenRemovedFromState || !ReferenceEquals(card.Owner, _player) ||
                    !_combatState.ContainsCard(card) || card.Pile != null) continue;
                // Permanent self-removal during OD also invalidates detached
                // ordinary copies which native pile cleanup could not find.
                if (card.DeckVersion is { } origin &&
                    (origin.HasBeenRemovedFromState || origin.Pile?.Type != PileType.Deck) ||
                    card is HotaruEndgameCard endgame && endgame.IsPermanentOriginRemoved)
                {
                    card.RemoveFromState();
                    continue;
                }
                // Existing cards must not replay entered-combat/generated hooks.
                _piles[index].AddInternal(card);
                // No travel animation runs for restored non-hand cards. Hand
                // completion remains with AddHandVisual below, exactly once.
                if (_piles[index].Type != PileType.Hand) _piles[index].InvokeCardAddFinished();
            }
        }
        var completedHandCarry = new HashSet<CardModel>(ReferenceEqualityComparer.Instance);
        foreach (CardModel card in carry)
        {
            if (card.HasBeenRemovedFromState || !ReferenceEquals(card.Owner, _player) || !_combatState.ContainsCard(card)) continue;
            if (card.Pile != _playerCombatState.Hand)
            {
                card.RemoveFromCurrentPile(true);
                CardPile target = _playerCombatState.Hand.Cards.Count < CardPile.MaxCardsInHand
                    ? _playerCombatState.Hand : _playerCombatState.DiscardPile;
                target.AddInternal(card);
                target.InvokeCardAddFinished();
            }
            else card.Pile.InvokeCardAddFinished();
            if (card.Pile == _playerCombatState.Hand) completedHandCarry.Add(card);
        }
        // Direct assignment restores energy without gain modifiers, retaining
        // the native EnergyChanged notification.
        _playerCombatState.Energy = SavedEnergy;
        FinishSnapshot();
        UpdateLocalHand(_player, hand =>
        {
            foreach (CardModel card in originals.Concat(carry).Distinct())
                if (ReferenceEquals(card.Pile, _playerCombatState.Hand))
                    AddHandVisual(hand, card, !completedHandCarry.Contains(card));
        });
        return true;
    }

    private CardModel[] CollectKurosawaCarry()
    {
        var result = new List<CardModel>();
        foreach (CardModel candidate in KurosawaLeaderCombatHelper.ConsumeCarry(_player))
        {
            CardModel model = candidate;
            if (candidate.DeckVersion is { } origin)
            {
                if (origin.HasBeenRemovedFromState || origin.Pile?.Type != PileType.Deck || _unavailableDeckCards.Contains(origin)) continue;
                CardModel[] matches = _cards.Where(card => ReferenceEquals(card.DeckVersion, origin) &&
                    !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card)).ToArray();
                // An ambiguous copy lineage cannot take another physical card.
                if (matches.Length != 1) continue;
                model = matches[0];
            }
            if (!model.HasBeenRemovedFromState && ReferenceEquals(model.Owner, _player) &&
                _combatState.ContainsCard(model) && !result.Contains(model)) result.Add(model);
        }
        return result.ToArray();
    }

    // Final flush after OD end effects; never repeat active discard/Sly/exhaust
    // hooks. TryRestore subsequently disposes the independent OD pool.
    public static int FlushRemainingToDiscard(Player player)
    {
        PlayerCombatState state = player?.PlayerCombatState;
        if (state == null || player.Creature.CombatState == null ||
            !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding) return 0;
        CardModel[] residualCards = state.Hand.Cards.ToArray();
        foreach (CardModel card in residualCards)
        {
            state.Hand.RemoveInternal(card);
            state.DiscardPile.AddInternal(card);
            state.DiscardPile.InvokeCardAddFinished();
        }
        UpdateLocalHand(player, hand =>
        {
            foreach (CardModel card in residualCards) RemoveHandVisual(hand, card);
        });
        return residualCards.Length;
    }

    // Do not resurrect cards after death/end/reset. Native cleanup only sees
    // piles, so mark detached originals removed too, without clearing Owner/IDs.
    public void Abandon()
    {
        if (_finished) return;
        if (!IsSameCombatState()) { FinishSnapshot(); return; }
        KurosawaLeaderCombatHelper.DiscardCarry(_player);
        ClearOverclockCards();
        foreach (CardModel card in _cards)
            if (!card.HasBeenRemovedFromState && card.Pile == null &&
                ReferenceEquals(card.Owner, _player) && _combatState.ContainsCard(card))
                card.RemoveFromState();
        FinishSnapshot();
    }

    private bool IsSameCombatState() =>
        ReferenceEquals(_player.PlayerCombatState, _playerCombatState) &&
        ReferenceEquals(_player.Creature.CombatState, _combatState);

    private bool IsCurrentLiveCombat() => IsSameCombatState() &&
        _player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding;

    private void ClearOverclockCards(IReadOnlySet<CardModel> preserved = null)
    {
        // Card.Pile resolves through the current owner state. Never inspect or
        // mutate card piles after that owner has switched to another battle.
        if (!IsSameCombatState()) return;
        if (_deckPrepared)
        {
            // OD copies are disposed below, so remember only cards still in
            // its exhaust pile. A card recovered before OD ends stays usable.
            ConsumedOverclockCards consumed = ConsumedCards.GetValue(_combatState, static _ => new());
            foreach (CardModel card in _playerCombatState.ExhaustPile.Cards)
                if (card.DeckVersion is { } origin) consumed.DeckOrigins.Add(origin);
        }
        var originals = new HashSet<CardModel>(_cards);
        CardModel[] handCards = _playerCombatState.Hand.Cards
            .Where(card => !originals.Contains(card)).ToArray();
        CardModel[] temporary = _overclockDeckCards.Concat(_piles.SelectMany(pile => pile.Cards))
            .Distinct().Where(card => !originals.Contains(card)).ToArray();
        foreach (CardModel card in temporary)
            if (preserved?.Contains(card) != true && !card.HasBeenRemovedFromState && ReferenceEquals(card.Owner, _player) &&
                _combatState.ContainsCard(card) && (card.Pile == null || card.Pile.IsCombatPile))
                // Native removal retains combat/network registration and Owner.
                card.RemoveFromState();
        _overclockDeckCards = Array.Empty<CardModel>();
        UpdateLocalHand(_player, hand =>
        {
            foreach (CardModel card in handCards) RemoveHandVisual(hand, card);
        });
    }

    private void FinishSnapshot()
    {
        _finished = true;
        _cards = Array.Empty<CardModel>();
        _overclockDeckCards = Array.Empty<CardModel>();
        for (int index = 0; index < _pileCards.Length; index++)
            _pileCards[index] = Array.Empty<CardModel>();
    }

    private static NPlayerHand GetLocalHand(Player player)
    {
        if (TestMode.IsOn || !LocalContext.IsMe(player))
        {
            return null;
        }

        NCombatRoom room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room) || !room.IsInsideTree())
        {
            return null;
        }

        NPlayerHand hand = room.Ui?.Hand;
        return hand != null && GodotObject.IsInstanceValid(hand) && hand.IsInsideTree()
            ? hand
            : null;
    }

    private static void UpdateLocalHand(Player player, Action<NPlayerHand> update)
    {
        // Local drag/selection state must never decide a synchronized model
        // operation. Finish model changes first; UI failures cannot roll them
        // back or make this client's success result differ from another peer.
        try
        {
            NPlayerHand hand = GetLocalHand(player);
            if (hand != null) update(hand);
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] OD hand UI refresh failed: {error.Message}");
        }
    }

    private static void RemoveHandVisual(NPlayerHand hand, CardModel card)
    {
        if (hand == null)
        {
            return;
        }

        var holder = hand.GetCardHolder(card);
        if (holder == null)
        {
            return;
        }

        NCard node = holder.CardNode;
        hand.Remove(card);
        // Native holder.Clear detaches NCard but deliberately does not free it.
        if (node != null && GodotObject.IsInstanceValid(node))
        {
            node.QueueFreeSafely();
        }
    }

    private static void AddHandVisual(NPlayerHand hand, CardModel card, bool completeAdd = true)
    {
        if (hand == null || hand.GetCardHolder(card) != null)
        {
            return;
        }

        NCard node = NCard.Create(card);
        if (node == null)
        {
            return;
        }

        NCombatRoom.Instance.Ui.AddChildSafely(node);
        node.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
        node.Position = PileType.Hand.GetTargetPosition(node);
        node.Scale = Vector2.One;
        hand.Add(node);
        if (completeAdd) card.Pile.InvokeCardAddFinished();
    }
}
