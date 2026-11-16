using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

public readonly record struct ChieLitaSnapshot(int Lita, bool Dressed, int LastRoll, bool LastSuccess);

public static class ChieLitaState
{
    internal sealed class ManualScope : IDisposable
    {
        internal CardModel Card;
        internal ManualScope Previous;
        internal bool Active = true, Manual, Success, Pride, FaithGranted;
        internal int FinalPlayCount;
        public void Dispose() { Active = false; RestoreCaller(this); }
    }
    private static readonly AsyncLocal<ManualScope> Current = new();
    public static event Action<CardModel> Changed;
    internal static void NotifyVisual(CardModel card) => Changed?.Invoke(card);
    internal static void Notify(CardModel card) { Changed?.Invoke(card); ThirtyGProposalState.Notify(card); }
    private static bool TryEntry(CardModel card, out ThirtyGCollectionRelic relic, out ThirtyGProposalState.Data data, out ThirtyGProposalState.Entry entry)
    {
        relic = null; data = null; entry = null;
        if (card?.IsMutable != true || ThirtyGProposalState.IdentityOf(card) is not { } id ||
            card.Owner?.GetRelic<ThirtyGCollectionRelic>() is not { } ownerRelic) return false;
        relic = ownerRelic; data = ThirtyGProposalState.Read(relic);
        return data.Cards.TryGetValue(id, out entry);
    }
    public static ChieLitaSnapshot Get(CardModel card)
    {
        if (!TryEntry(card, out _, out var data, out var entry)) return default;
        bool active = data.InBattle && card.Owner.Creature?.CombatState != null;
        return new(Math.Clamp(entry.Lita, 0, 100), active && entry.Dressed,
            active ? entry.LastDressRoll : 0, active && entry.LastDressSuccess);
    }
    internal static void OnPermanentDeckChanged(Player player)
    {
        if (player?.GetRelic<ThirtyGCollectionRelic>() is not { } relic || player.Deck == null || !HbrBattleStatsHelper.IsEnabled(player)) return;
        var data = ThirtyGProposalState.Read(relic);
        if (!data.LitaEnabled)
        {
            if (!player.Deck.Cards.OfType<CuteIsInvincibleHbrgoSaki>().Any(card => card.Owner == player && !card.HasBeenRemovedFromState)) return;
            data.LitaEnabled = true; ThirtyGProposalState.Save(relic, data);
        }
        ThirtyGProposalState.EnsureIdentities(player);
        foreach (CardModel card in player.Deck.Cards.Where(card => card.Owner == player && !card.HasBeenRemovedFromState && HbrMemoryGardenCardCatalog.IsGardenCard(card)))
            EnsureAssigned(card, relic);
    }
    internal static int EnsureAssigned(CardModel card, ThirtyGCollectionRelic relic)
    {
        var data = ThirtyGProposalState.Read(relic); if (!data.LitaEnabled) return 0;
        string id = ThirtyGProposalState.EnsureIdentity(card, relic);
        if (!data.Cards.TryGetValue(id, out var entry)) data.Cards[id] = entry = new();
        if (entry.Lita > 0) return entry.Lita;
        var rng = RngCompatibility.CreateNamed(relic.Owner.RunState.Rng, "hbrgo_lita_" + relic.Owner.NetId + "_" + data.LitaSerial++);
        entry.Lita = rng.NextInt(1, 101); ThirtyGProposalState.Save(relic, data); Notify(ThirtyGProposalState.Physical(card));
        return entry.Lita;
    }
    internal static void Dress(CardModel target, CuteIsInvincibleHbrgoSaki source)
    {
        if (target?.Owner != source?.Owner || ThirtyGProposalState.IsDisabled(target) ||
            source.Owner.GetRelic<ThirtyGCollectionRelic>() is not { } relic) return;
        EnsureAssigned(target, relic);
        if (!TryEntry(target, out relic, out var data, out var entry)) return;
        if (source.HasUpgradeEffect(CuteIsInvincibleHbrgoSaki.StillRising) && entry.Lita < 70) entry.Lita = Math.Min(70, entry.Lita + 10);
        entry.Dressed = true;
        entry.ComboPending |= source.HasUpgradeEffect(CuteIsInvincibleHbrgoSaki.WhimsicalWeariness);
        entry.DressPride |= source.HasUpgradeEffect(CuteIsInvincibleHbrgoSaki.LolitaPride);
        ThirtyGProposalState.Save(relic, data); Notify(ThirtyGProposalState.Physical(target));
    }
    internal static ManualScope BeginManual(CardModel card, bool isAutoPlay)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState == null) return null;
        if (!TryEntry(card, out var relic, out var data, out var entry) || !data.InBattle || !entry.Dressed)
            return Find(card) == null ? null : Push(card, false);
        ManualScope scope = Push(card, !isAutoPlay && !ThirtyGProposalState.IsDisabled(card) && !CombatManager.Instance.IsOverOrEnding);
        if (!scope.Manual) return scope;
        var rng = RngCompatibility.CreateNamed(card.Owner.RunState.Rng, "hbrgo_dress_" + card.Owner.NetId + "_" + data.DressRollSerial++);
        entry.LastDressRoll = rng.NextInt(1, 101); entry.LastDressSuccess = entry.LastDressRoll > 100 - entry.Lita;
        scope.Success = entry.LastDressSuccess; scope.Pride = entry.DressPride;
        ThirtyGProposalState.Save(relic, data); Notify(ThirtyGProposalState.Physical(card));
        return scope;
    }
    private static ManualScope Push(CardModel card, bool manual)
    { ManualScope scope = new() { Card = card, Manual = manual, Previous = Current.Value }; Current.Value = scope; return scope; }
    private static ManualScope Find(CardModel card)
    {
        for (ManualScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Card, card)) return scope;
        return null;
    }
    internal static int ReplayBonus(CardModel card) => Find(card) is { Manual: true, Success: true } ? 1 : 0;
    internal static void RestoreCaller(ManualScope scope)
    { if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous; }
    internal static async Task Finish(Task original, ManualScope scope)
    { try { await original; } finally { scope?.Dispose(); } }
    internal static async Task<int> FinishPlayCount(Task<int> original, CardModel card)
    {
        ManualScope scope = Find(card);
        int count = await original;
        if (scope?.Active == true) scope.FinalPlayCount = count;
        return count;
    }
    internal static async Task BeforeCardPlayed(CardPlay play)
    {
        CardModel card = play?.Card;
        if (!TryEntry(card, out var relic, out var data, out var entry) || !data.InBattle || !entry.Dressed ||
            !entry.ComboPending || ThirtyGProposalState.IsDisabled(card)) return;
        entry.ComboPending = false; ThirtyGProposalState.Save(relic, data); Notify(ThirtyGProposalState.Physical(card));
        await HbrComboPower.Grant<HbrComboMediumPower>(new ThrowingPlayerChoiceContext(), card.Owner.Creature, card.Owner.Creature, card);
    }
    internal static void AfterCardPlayed(CardPlay play)
    {
        if (play?.Card is not { } card || Find(card) is not { Manual: true, Success: true, Pride: true, FaithGranted: false } scope ||
            scope.FinalPlayCount <= 1 || play.PlayIndex != scope.FinalPlayCount - 1) return;
        scope.FaithGranted = true; HbrFaithResource.Gain(card.Owner.Creature, 1);
    }
}
