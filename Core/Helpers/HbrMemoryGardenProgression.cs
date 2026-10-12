using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// One ordered effect list per card name and player, including names whose last
// copy has been removed. Card instances still own combat state and enchantments.
internal static class HbrMemoryGardenProgression
{
    private sealed class State
    {
        internal string Serialized;
        internal Dictionary<string, int[]> Effects = new(StringComparer.Ordinal);
        internal readonly List<WeakReference<CardModel>> Cards = new();
    }

    private static readonly ConditionalWeakTable<ElectronicSoldierManual, State> States = new();
    private static readonly AccessTools.FieldRef<CardModel, CardEnergyCost> EnergyCostField =
        AccessTools.FieldRefAccess<CardModel, CardEnergyCost>("_energyCost");
    [ThreadStatic] private static int _rebuilding;
    internal static bool IsRebuilding => _rebuilding > 0;

    internal static bool IsRealCard(CardModel card) => card?.IsMutable == true &&
        !card.HasBeenRemovedFromState && HbrMemoryGardenCardCatalog.IsGardenCard(card) &&
        card.Owner is { } player && (player.RunState?.ContainsCard(card) == true ||
            player.Creature.CombatState?.ContainsCard(card) == true);

    internal static int[] GetEffects(CardModel card) => (card switch
    {
        HerSongIsMyScreamHbrgoSaki c => c.UpgradeEffectOrder,
        MatchaParfaitHbrgoSaki c => c.UpgradeEffectOrder,
        LostStarSongHbrgoSaki c => c.UpgradeEffectOrder,
        AnonAidHbrgoSaki c => c.UpgradeEffectOrder,
        SoyoAidHbrgoSaki c => c.UpgradeEffectOrder,
        HalfBodyHbrgoSaki c => c.UpgradeEffectOrder,
        MyLegendBeginsHbrgoSaki c => c.UpgradeEffectOrder,
        HelloWorldHbrgoSaki c => c.UpgradeEffectOrder,
        SaviorGrandEntranceHbrgoSaki c => c.UpgradeEffectOrder,
        WorldsFinestHbrgoSaki c => c.UpgradeEffectOrder,
        MoreThanOneTruthHbrgoSaki c => c.UpgradeEffectOrder,
        CrushYouLikeABugHbrgoSaki c => c.UpgradeEffectOrder,
        SummerGrassDreamHbrgoSaki c => c.UpgradeEffectOrder,
        BoostUpHbrgoSaki c => c.UpgradeEffectOrder,
        _ => Array.Empty<int>()
    }).Take(card?.CurrentUpgradeLevel ?? 0).ToArray();

    private static State GetState(ElectronicSoldierManual manual)
    {
        State state = States.GetOrCreateValue(manual);
        string serialized = manual.MemoryGardenSharedUpgrades ?? string.Empty;
        if (state.Serialized == serialized) return state;
        state.Effects = string.IsNullOrEmpty(serialized)
            ? new(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, int[]>>(serialized) ?? new(StringComparer.Ordinal);
        state.Serialized = serialized;
        return state;
    }

    private static void Save(ElectronicSoldierManual manual, State state)
    {
        string serialized = JsonSerializer.Serialize(new SortedDictionary<string, int[]>(state.Effects, StringComparer.Ordinal));
        manual.SetMemoryGardenSharedUpgrades(serialized);
        state.Serialized = serialized;
    }

    private static CardModel[] GetCopies(Player player, State state, CardModel additional = null)
    {
        state.Cards.RemoveAll(reference => !reference.TryGetTarget(out CardModel card) || !IsRealCard(card));
        IEnumerable<CardModel> cards = player.Deck.Cards.Concat(
            player.PlayerCombatState?.AllCards ?? Enumerable.Empty<CardModel>());
        cards = cards.Concat(state.Cards.Select(reference => reference.TryGetTarget(out CardModel card) ? card : null));
        if (additional != null) cards = cards.Append(additional);
        return cards.Where(card => card?.IsMutable == true && !card.HasBeenRemovedFromState &&
                ReferenceEquals(card.Owner, player) && HbrMemoryGardenCardCatalog.IsGardenCard(card))
            .Distinct().ToArray();
    }

    internal static void Synchronize(Player player)
    {
        if (IsRebuilding || player?.GetRelic<ElectronicSoldierManual>() is not { } manual) return;
        foreach (CardModel card in GetCopies(player, GetState(manual))) Synchronize(card);
    }

    internal static void Synchronize(CardModel card)
    {
        if (IsRebuilding || !IsRealCard(card) || card.Owner.GetRelic<ElectronicSoldierManual>() is not { } manual) return;
        State state = GetState(manual);
        if (!state.Cards.Any(reference => reference.TryGetTarget(out CardModel known) && ReferenceEquals(known, card)))
            state.Cards.Add(new(card));
        CardModel[] copies = GetCopies(card.Owner, state, card).Where(copy => copy.Id == card.Id).ToArray();
        if (!state.Effects.TryGetValue(card.Id.Entry, out int[] effects))
        {
            // Old saves may have divergent copies. Keep the most-upgraded real
            // copy (deck order breaks ties), rather than inventing extra effects.
            effects = GetEffects(copies.OrderByDescending(copy => copy.CurrentUpgradeLevel).First());
            state.Effects[card.Id.Entry] = effects;
            Save(manual, state);
        }
        foreach (CardModel copy in copies)
        {
            ApplyEffects(copy, HbrMemoryGardenCombatDowngrade.FilterEffects(copy, effects));
            ThirtyACollectionUpgrades.SynchronizeClaims(copy);
        }
    }

    // Only callers that actually changed a real card may publish. Preview and
    // serialization replay never go through this path and never consume RNG.
    internal static void Publish(CardModel card)
    {
        if (IsRebuilding || !IsRealCard(card) || card.Owner.GetRelic<ElectronicSoldierManual>() is not { } manual) return;
        State state = GetState(manual);
        state.Effects.TryGetValue(card.Id.Entry, out int[] previous);
        state.Effects[card.Id.Entry] = HbrMemoryGardenCombatDowngrade.MergePublishedEffects(
            card, previous ?? Array.Empty<int>(), GetEffects(card));
        Save(manual, state);
        Synchronize(card);
    }

    internal static int[] GetRemovableEffects(CardModel card) => GetRemovableEffects(card, true);

    internal static int[] GetRemovableEffects(CardModel card, bool includeCombatSuppressed)
    {
        int[] effects = GetEffects(card);
        int[] reserved = effects.Concat(HbrMemoryGardenDampen.GetPendingEffects(card))
            .Concat(includeCombatSuppressed
                ? HbrMemoryGardenCombatDowngrade.GetSuppressedEffects(card)
                : Array.Empty<int>()).ToArray();
        return effects.Where(effect =>
            !(card is AnonAidHbrgoSaki && effect == 4 && reserved.Contains(5)) &&
            !(card is SoyoAidHbrgoSaki && effect == 0 && reserved.Contains(1)) &&
            !(card is WorldsFinestHbrgoSaki &&
                (effect == 0 && (reserved.Contains(1) || reserved.Contains(2)) ||
                 effect == 1 && reserved.Contains(2)))).ToArray();
    }

    internal static bool TryDowngrade(CardModel card)
    {
        if (!IsRealCard(card)) return false;
        Synchronize(card);
        int[] removable = GetRemovableEffects(card);
        if (removable.Length == 0) return false;
        int effect = removable[removable.Length == 1 ? 0 : card.Owner.RunState.Rng.Niche.NextInt(removable.Length)];
        ApplyEffects(card, GetEffects(card).Where(id => id != effect).ToArray());
        Publish(card);
        return true;
    }

    internal static bool RestoreEffect(CardModel card, int effect)
    {
        if (!IsRealCard(card)) return false;
        return RestoreEffect(card.Owner, card.GetType(), effect);
    }

    internal static bool RestoreEffect(Player player, Type cardType, int effect)
    {
        if (player?.GetRelic<ElectronicSoldierManual>() is not { } manual ||
            !HbrMemoryGardenCardCatalog.IsGardenCard(cardType)) return false;
        State state = GetState(manual);
        string key = ModelDb.GetId(cardType).Entry;
        if (!state.Effects.TryGetValue(key, out int[] effects)) return false;
        if (effects.Contains(effect)) return true;
        CardModel validation = ModelDb.GetById<CardModel>(ModelDb.GetId(cardType)).ToMutable();
        ApplyEffects(validation, effects);
        if (!HbrMemoryGardenUpgradeCatalog.PrepareUpgradeEffect(validation, effect)) return false;
        state.Effects[key] = effects.Append(effect).ToArray();
        Save(manual, state);
        Synchronize(player);
        return true;
    }

    internal static void SynchronizePreview(CardModel preview, Player player)
    {
        if (player?.GetRelic<ElectronicSoldierManual>() is { } manual &&
            GetState(manual).Effects.TryGetValue(preview.Id.Entry, out int[] effects))
            ApplyEffects(preview, effects);
    }

    // This also supports detached UI previews explicitly: it changes only the
    // supplied object and cannot write the run ledger or trigger random choices.
    internal static void ApplyEffects(CardModel card, IReadOnlyList<int> effects)
    {
        int[] previous = GetEffects(card);
        if (previous.SequenceEqual(effects) && card.CurrentUpgradeLevel == effects.Count) return;
        _rebuilding++;
        try
        {
            bool appendOnly = previous.Length <= effects.Count && previous.SequenceEqual(effects.Take(previous.Length));
            CardEnergyCost oldCost = null;
            if (!appendOnly)
            {
                oldCost = card.EnergyCost.Clone(card);
                card.DowngradeInternal();
            }
            for (int i = appendOnly ? previous.Length : 0; i < effects.Count; i++)
            {
                if (!HbrMemoryGardenUpgradeCatalog.PrepareUpgradeEffect(card, effects[i]))
                    throw new InvalidOperationException($"Invalid shared garden upgrade {card.Id}: {effects[i]}");
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
            }
            if (oldCost != null)
            {
                // Keep turn/combat discounts and a captured X payment even when
                // a removed upgrade changes the printed cost or X-cost mode.
                int printedCost = card.EnergyCost.GetWithModifiers(CostModifiers.None);
                CardEnergyCost restoredCost = oldCost.Clone(card);
                restoredCost.SetCustomBaseCost(printedCost);
                EnergyCostField(card) = restoredCost;
                card.InvokeEnergyCostChanged();
            }
        }
        finally { _rebuilding--; }
    }
}
