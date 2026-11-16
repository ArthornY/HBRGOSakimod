using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class IrohaDyeCombatHelper
{
    internal sealed class Dye { internal int Layers; internal decimal Percent; }
    private sealed record CopyDye(ICombatState Combat, Dye Value);
    private sealed class Battle
    {
        internal readonly Dictionary<CardModel, Dye> Colors = new();
        internal readonly Dictionary<Player, List<decimal>> Promises = new();
        internal readonly HashSet<CardModel> Played = new();
        internal readonly List<WeakReference<CardModel>> Known = new();
        internal readonly ConditionalWeakTable<CardModel, object> Seen = new();
        internal readonly Dictionary<Player, int> Features = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly ConditionalWeakTable<CardModel, CopyDye> Copies = new();
    private static void Track(Battle battle, CardModel card) => battle.Seen.GetValue(card, known =>
    { battle.Known.Add(new(known)); return new object(); });
    private static CardModel[] LiveCards(Battle battle)
    {
        battle.Known.RemoveAll(reference => !reference.TryGetTarget(out _));
        return battle.Known.Select(reference => reference.TryGetTarget(out var card) ? card : null)
            .Where(card => card != null).ToArray();
    }
    private static CardModel Physical(CardModel card) => !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card)
        ? card.DeckVersion ?? card : card;
    internal static Dye Read(CardModel card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat ||
            !Battles.TryGetValue(combat, out Battle battle)) return null;
        // The permanent deck only supplies an identity, never a colored card.
        if (card.Owner.Deck?.Cards.Contains(card) == true) return null;
        CardModel key = Physical(card);
        if (battle.Colors.TryGetValue(key, out Dye state)) return state;
        return Copies.TryGetValue(card, out CopyDye copy) && ReferenceEquals(copy.Combat, combat) ? copy.Value : null;
    }
    internal static void Inherit(CardModel source, CardModel clone)
    {
        Dye state = Read(source);
        if (state == null || source.Owner?.Creature?.CombatState is not { } combat) return;
        Copies.Remove(clone);
        Copies.Add(clone, new(combat, new() { Layers = state.Layers, Percent = state.Percent }));
        Track(Battles.GetOrCreateValue(combat), clone);
    }
    internal static void Paint(CardModel card, decimal percent)
    {
        if (card?.IsMutable != true || card.Type != CardType.Attack || card.Owner?.Creature?.CombatState is not { } combat ||
            !combat.ContainsCard(card) || percent <= 0m) return;
        Battle battle = Battles.GetOrCreateValue(combat);
        CardModel key = Physical(card);
        if (!battle.Colors.TryGetValue(key, out Dye dye))
        {
            Dye inherited = Read(card);
            dye = new() { Layers = inherited?.Layers ?? 0, Percent = inherited?.Percent ?? 0m };
            battle.Colors.Add(key, dye);
        }
        dye.Layers++; dye.Percent = DamageAmplificationLimits.AddSaturated(dye.Percent, percent);
        Track(battle, card);
        RefreshFeatures(card.Owner);
        foreach (CardModel visible in LiveCards(battle).Where(known => known.Owner == card.Owner &&
            ReferenceEquals(Physical(known), key)).ToArray()) IrohaDyeState.Notify(visible);
        if (CombatManager.Instance?.StateTracker != null)
            CombatPreviewRefreshHelper.RefreshCombatState(card.Owner.Creature, nameof(IrohaDyeCombatHelper));
    }
    internal static bool MarkPlayed(CardModel card) => card?.Owner?.Creature?.CombatState is { } combat &&
        Battles.GetOrCreateValue(combat).Played.Add(Physical(card));
    internal static void PromiseDraw(Player player, decimal percent)
    {
        if (player?.Creature?.CombatState is not { } combat) return;
        Battle battle = Battles.GetOrCreateValue(combat);
        if (!battle.Promises.TryGetValue(player, out var promises)) battle.Promises.Add(player, promises = new());
        promises.Add(percent);
    }
    internal static void OnDrawn(CardModel card, bool fromHandDraw)
    {
        // Hook.AfterCardDrawn is dispatched exclusively by native Draw. Its
        // flag distinguishes normal turn draw from effect draw, not generation.
        if (card?.Type != CardType.Attack || card.Owner?.Creature?.CombatState is not { } combat ||
            !Battles.TryGetValue(combat, out Battle battle) || !battle.Promises.Remove(card.Owner, out var promises)) return;
        foreach (decimal percent in promises) Paint(card, percent);
    }
    internal static void RefreshFeatures(Player player)
    {
        if (player?.Creature?.CombatState is not { } combat) return;
        int flags = 0;
        foreach (var card in (player.Deck?.Cards ?? Enumerable.Empty<CardModel>())
            .Concat(player.PlayerCombatState?.AllCards ?? Enumerable.Empty<CardModel>()).OfType<PaintTheWorldMyColorHbrgoSaki>()
            .Where(card => card.Owner == player && !card.HasBeenRemovedFromState))
            foreach (int effect in card.UpgradeEffectOrder.Take(card.CurrentUpgradeLevel)) flags |= 1 << effect;
        Battles.GetOrCreateValue(combat).Features[player] = flags;
    }
    internal static bool Has(Player player, int upgrade)
    {
        if (player?.Creature?.CombatState is not { } combat) return false;
        Battle battle = Battles.GetOrCreateValue(combat);
        if (!battle.Features.ContainsKey(player)) RefreshFeatures(player);
        return (battle.Features[player] & (1 << upgrade)) != 0;
    }
    internal static Player[] CaptureTombstoneOwners(ICombatState combat) => combat.Players
        .Where(player => Has(player, PaintTheWorldMyColorHbrgoSaki.Tombstone)).ToArray();
    internal static void OnFinalDeath(Player[] owners)
    {
        foreach (Player owner in owners)
        {
            CardModel[] candidates = owner.PlayerCombatState.Hand.Cards.Concat(owner.PlayerCombatState.DrawPile.Cards)
                .Concat(owner.PlayerCombatState.DiscardPile.Cards).Where(card => card.Owner == owner && IrohaDyeState.GetLayers(card) > 0)
                .Distinct().ToArray();
            if (candidates.Length == 0) continue;
            CardModel selected = candidates[owner.RunState.Rng.CombatCardSelection.NextInt(0, candidates.Length)];
            Paint(selected, Has(owner, PaintTheWorldMyColorHbrgoSaki.Spectacle) ? 80m : 50m);
        }
    }
    internal static void Clear(ICombatState combat)
    {
        if (combat == null || !Battles.TryGetValue(combat, out Battle battle)) return;
        CardModel[] known = LiveCards(battle);
        Battles.Remove(combat);
        foreach (CardModel card in known) { Copies.Remove(card); IrohaDyeState.Notify(card); }
    }
    internal static void AfterPlay(CardPlay play)
    {
        if (play?.Card?.Owner?.Creature?.CombatState is not { } combat || !Battles.TryGetValue(combat, out _)) return;
        RefreshFeatures(play?.Card?.Owner);
        if (IrohaDyeState.GetLayers(play?.Card) > 0 && Has(play.Card.Owner, PaintTheWorldMyColorHbrgoSaki.Impasto))
            play.Card.Owner.GetRelic<Overclock>()?.GainLevelProgress(5m);
    }
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source?.IsMutable == true && dealer != null && source.Owner?.Creature == dealer && props.IsPoweredAttack() &&
        (target == null || target.Side != dealer.Side) &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _);
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) ? IrohaDyeState.GetDamageBonusPercent(source) : 0m;
    internal static decimal GetCriticalRatePercent(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && CardPlayContextHelper.GetCurrentPlayToken(source) != null && IrohaDyeState.GetLayers(source) > 0 &&
        Has(source.Owner, PaintTheWorldMyColorHbrgoSaki.UltimateColor)
            ? Math.Max(0m, HbrCriticalRateUpPower.CalculatePercent(50m, dealer) - HbrCriticalRateUpPower.GetTotalPercent(dealer)) : 0m;
    internal static decimal GetCriticalDamagePercent(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && CardPlayContextHelper.GetCurrentPlayToken(source) != null && IrohaDyeState.GetLayers(source) > 0 &&
        Has(source.Owner, PaintTheWorldMyColorHbrgoSaki.UltimateColor)
            ? Math.Max(0m, 50m - HbrCriticalDamageUpPower.GetTotalPercent(dealer)) : 0m;
}
