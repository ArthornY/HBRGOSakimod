using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

public readonly record struct ThirtyGCardState(bool Graduated, int GraduatedBattlesRemaining,
    bool ReleaseOnSecondTurn, int HeartfeltExtraPlays, bool WholeBattleNullified = false, bool PermanentHeartfelt = false);

// Card identity is serialized through a named native SavedProperties string.
// Persistent scheduling belongs to this player's relic; no deck indices or
// canonical model IDs are used to distinguish two physical copies.
public static class ThirtyGProposalState
{
    internal sealed class Identity { internal string Id; }
    internal sealed class Entry
    {
        public int GraduatedRemaining { get; set; }
        public bool ReleaseSecondTurn { get; set; }
        public bool Released { get; set; }
        public int PendingHeart { get; set; }
        public int Heart { get; set; }
        public bool MiyaProposal { get; set; }
        public int PermanentHeart { get; set; }
        public int Lita { get; set; }
        public bool Dressed { get; set; }
        public bool ComboPending { get; set; }
        public bool DressPride { get; set; }
        public int LastDressRoll { get; set; }
        public bool LastDressSuccess { get; set; }
    }
    internal sealed class Data
    {
        public int NextIdentity { get; set; }
        public int RulesVersion { get; set; }
        public int ProposalSerial { get; set; }
        public int LastAct { get; set; } = -1;
        public string BattleKey { get; set; } = "";
        public bool InBattle { get; set; }
        public bool LitaEnabled { get; set; }
        public int LitaSerial { get; set; }
        public int DressRollSerial { get; set; }
        public Dictionary<string, Entry> Cards { get; set; } = new(StringComparer.Ordinal);
        public List<string> RoundTargets { get; set; } = new();
    }
    private sealed class Cache
    {
        internal string Saved;
        internal Data Data;
        internal readonly Dictionary<string, CardModel> Holders = new(StringComparer.Ordinal);
    }
    private static readonly ConditionalWeakTable<CardModel, Identity> Identities = new();
    private static readonly ConditionalWeakTable<ThirtyGCollectionRelic, Cache> Caches = new();
    internal sealed class PlayScope
    {
        internal CardModel Card;
        internal PlayScope Previous;
        internal bool Active = true;
        internal bool Nullified;
        internal bool Heartfelt;
        internal bool OwnsStage;
        internal IDisposable ModeSuppression;
    }
    private static readonly AsyncLocal<PlayScope> Playing = new();
    public static event Action<CardModel> Changed;
    internal static CardModel Physical(CardModel card)
    {
        if (card?.IsMutable != true) return card;
        // A real permanent copy is its own physical object, even when a clone
        // operation temporarily retains the original's DeckVersion pointer.
        if (card.Pile?.Type == PileType.Deck && card.Owner?.Deck == card.Pile && card.Pile.Cards.Contains(card)) return card;
        return !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
    }
    internal static string IdentityOf(CardModel card) => card != null && Identities.TryGetValue(Physical(card), out var identity) ? identity.Id : null;
    internal static void RestoreIdentity(CardModel card, string id)
    { if (card?.IsMutable == true && !string.IsNullOrEmpty(id)) Identities.GetOrCreateValue(card).Id = id; }
    internal static Data Read(ThirtyGCollectionRelic relic)
    {
        Cache cache = Caches.GetOrCreateValue(relic);
        if (cache.Data == null || !ReferenceEquals(cache.Saved, relic.ProposalData))
        {
            cache.Data = string.IsNullOrEmpty(relic.ProposalData) ? new() : JsonSerializer.Deserialize<Data>(relic.ProposalData) ?? new();
            cache.Saved = relic.ProposalData;
        }
        if (cache.Data.RulesVersion == 0)
        {
            // Old saves scheduled one or two disabled encounters followed by
            // temporary hearts. Retain the physical recipient and advance it
            // into the corresponding new next-play/permanent-Miya state.
            foreach (var pair in cache.Data.Cards)
            {
                Entry entry = pair.Value;
                CardModel card = relic.Owner?.Deck?.Cards.FirstOrDefault(card => IdentityOf(card) == pair.Key);
                bool miya = card is Models.Capabilities.IThirtyGMemberCard { ThirtyGMember: Models.Capabilities.ThirtyGMember.KiryuMiya } ||
                    card != null && HbrMemoryGarden.CharacterIdOf(card) == "miya";
                if (miya && (entry.GraduatedRemaining > 0 || entry.PendingHeart > 0 || entry.Heart > 0))
                {
                    entry.MiyaProposal = cache.Data.InBattle && entry.GraduatedRemaining > 0 && !entry.Released;
                    entry.PermanentHeart = entry.MiyaProposal ? 0 : 1;
                }
                entry.GraduatedRemaining = miya ? entry.MiyaProposal ? 1 : 0 : entry.GraduatedRemaining > 0 && !entry.Released ? 1 : 0;
                entry.Heart = !miya && (entry.Heart > 0 || entry.GraduatedRemaining == 0 && entry.PendingHeart > 0) ? 1 : 0;
                entry.PendingHeart = 0; entry.ReleaseSecondTurn = false; entry.Released = false;
            }
            cache.Data.RulesVersion = 1;
            Save(relic, cache.Data);
        }
        return cache.Data;
    }
    internal static void Save(ThirtyGCollectionRelic relic, Data data)
    {
        string json = JsonSerializer.Serialize(data); relic.SetProposalData(json);
        Cache cache = Caches.GetOrCreateValue(relic); cache.Data = data; cache.Saved = json;
    }
    internal static void EnsureIdentities(Player player)
    {
        if (player?.GetRelic<ThirtyGCollectionRelic>() is not { } relic || player.Deck == null) return;
        Data data = Read(relic); bool changed = false;
        Cache cache = Caches.GetOrCreateValue(relic);
        CardModel[] cards = player.Deck.Cards.Where(card => card.Owner == player && !card.HasBeenRemovedFromState && HbrMemoryGardenCardCatalog.IsGardenCard(card)).ToArray();
        HashSet<CardModel> current = new(cards);
        foreach (string id in cache.Holders.Keys.ToArray())
            if (!current.Contains(cache.Holders[id]) || IdentityOf(cache.Holders[id]) != id) cache.Holders.Remove(id);
        // Rehydrate unique saved identities before handling newly inserted
        // copies. An existing physical holder wins regardless of deck order.
        foreach (CardModel card in cards)
            if (IdentityOf(card) is { } id && !cache.Holders.ContainsKey(id))
                cache.Holders[id] = card.DeckVersion is { } original && current.Contains(original) && IdentityOf(original) == id ? original : card;
        foreach (CardModel card in cards)
        {
            string id = IdentityOf(card);
            if (string.IsNullOrEmpty(id) || !id.StartsWith(player.NetId + ":", StringComparison.Ordinal) ||
                cache.Holders.TryGetValue(id, out CardModel holder) && !ReferenceEquals(holder, card))
            {
                id = player.NetId + ":" + ++data.NextIdentity;
                while (cache.Holders.ContainsKey(id)) id = player.NetId + ":" + ++data.NextIdentity;
                RestoreIdentity(card, id); changed = true;
            }
            cache.Holders[id] = card;
        }
        if (changed) Save(relic, data);
    }
    internal static string EnsureIdentity(CardModel card, ThirtyGCollectionRelic relic)
    {
        string id = IdentityOf(card); if (!string.IsNullOrEmpty(id)) return id;
        Data data = Read(relic); id = relic.Owner.NetId + ":" + ++data.NextIdentity;
        RestoreIdentity(Physical(card), id); Save(relic, data); return id;
    }
    public static ThirtyGCardState Get(CardModel card)
    {
        if (card?.IsMutable != true || IdentityOf(card) is not { } id ||
            card.Owner?.GetRelic<ThirtyGCollectionRelic>() is not { } relic) return default;
        Data data = Read(relic);
        if (!data.Cards.TryGetValue(id, out Entry entry)) return default;
        bool active = data.InBattle && card.Owner.Creature?.CombatState != null;
        return new(active && entry.GraduatedRemaining > 0,
            entry.GraduatedRemaining > 0 ? 1 : 0, false,
            entry.GraduatedRemaining <= 0 ? Math.Clamp((active ? entry.Heart : 0) + entry.PermanentHeart, 0, 1) : 0,
            active && entry.MiyaProposal, entry.PermanentHeart > 0);
    }
    public static bool IsDisabled(CardModel card) => Get(card).Graduated;
    internal static bool HasNextPlayDiscount(CardModel card) => IsDisabled(card) &&
        IdentityOf(card) is { } id && card.Owner?.GetRelic<ThirtyGCollectionRelic>() is { } relic &&
        Read(relic).Cards.TryGetValue(id, out Entry entry) && !entry.MiyaProposal;
    internal static int ReplayBonus(CardModel card)
    {
        var state = Get(card);
        if (state.HeartfeltExtraPlays <= 0 || state.PermanentHeartfelt) return state.HeartfeltExtraPlays;
        for (PlayScope scope = Playing.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(Physical(scope.Card), Physical(card))) return scope.OwnsStage ? 1 : 0;
        return state.HeartfeltExtraPlays;
    }
    internal static PlayScope BeginPlay(CardModel card)
    {
        if (card?.IsMutable != true) return null;
        bool ownsStage = true;
        for (PlayScope current = Playing.Value; current != null; current = current.Previous)
            if (current.Active && ReferenceEquals(Physical(current.Card), Physical(card))) { ownsStage = false; break; }
        var state = Get(card);
        if (!state.Graduated && state.HeartfeltExtraPlays <= 0) return null;
        PlayScope scope = new() { Card = card, Previous = Playing.Value, Nullified = state.Graduated,
            Heartfelt = state.HeartfeltExtraPlays > 0, OwnsStage = ownsStage,
            ModeSuppression = state.Graduated ? ModeTransitionHelper.SuppressModeTransition(card) : null };
        Playing.Value = scope; return scope;
    }
    internal static void RestoreCaller(PlayScope scope)
    { if (scope != null && ReferenceEquals(Playing.Value, scope)) Playing.Value = scope.Previous; }
    internal static void ClosePlay(PlayScope scope)
    {
        if (scope?.Active == true) { scope.Active = false; scope.ModeSuppression?.Dispose(); }
        RestoreCaller(scope);
    }
    internal static async Task FinishPlay(Task original, PlayScope scope)
    {
        try
        {
            await original;
            if (!scope.OwnsStage) return;
            CardModel card = scope.Card;
            if (IdentityOf(card) is not { } id || card.Owner?.GetRelic<ThirtyGCollectionRelic>() is not { } relic) return;
            Data data = Read(relic);
            if (!data.InBattle || !data.Cards.TryGetValue(id, out Entry entry) || entry.MiyaProposal) return;
            if (scope.Nullified && entry.GraduatedRemaining > 0) { entry.GraduatedRemaining = 0; entry.Heart = 1; }
            else if (scope.Heartfelt) entry.Heart = 0;
            Save(relic, data); Notify(Physical(card));
        }
        finally { ClosePlay(scope); }
    }
    internal static void Notify(CardModel card)
    {
        card?.InvokeEnergyCostChanged();
        if (card?.Owner?.PlayerCombatState is { } combat)
            foreach (CardModel mirror in combat.AllPiles.SelectMany(pile => pile.Cards).Distinct())
                if (!ReferenceEquals(card, mirror) && ReferenceEquals(Physical(card), Physical(mirror))) mirror.InvokeEnergyCostChanged();
        Changed?.Invoke(card);
    }
    internal static void NotifyOwner(Player player)
    {
        if (player?.Deck == null) return;
        foreach (CardModel card in player.Deck.Cards) { Notify(card); ChieLitaState.NotifyVisual(card); }
    }
}
