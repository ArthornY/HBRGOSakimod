using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Temporary penalties are a combat-only view over the permanent shared ledger.
// Keying by the combat also makes a later encounter start without any penalty.
internal static class HbrMemoryGardenCombatDowngrade
{
    private sealed class State
    {
        internal readonly Dictionary<Player, Dictionary<string, HashSet<int>>> Players = new();
    }

    private static readonly ConditionalWeakTable<ICombatState, State> States = new();

    private static ICombatState GetCombat(CardModel card)
    {
        ICombatState combat = card?.IsMutable == true && !card.HasBeenRemovedFromState
            ? card.Owner?.Creature.CombatState : null;
        return combat?.ContainsCard(card) == true ? combat : null;
    }

    internal static IReadOnlyCollection<int> GetSuppressedEffects(CardModel card)
    {
        ICombatState combat = GetCombat(card);
        return combat != null && States.TryGetValue(combat, out State state) &&
            state.Players.TryGetValue(card.Owner, out Dictionary<string, HashSet<int>> names) &&
            names.TryGetValue(card.Id.Entry, out HashSet<int> effects)
                ? effects : Array.Empty<int>();
    }

    internal static int[] FilterEffects(CardModel card, IReadOnlyList<int> effects)
    {
        IReadOnlyCollection<int> suppressed = GetSuppressedEffects(card);
        return effects.Where(effect => !suppressed.Contains(effect)).ToArray();
    }

    internal static int[] MergePublishedEffects(CardModel card, int[] previous, int[] current)
    {
        IReadOnlyCollection<int> suppressed = GetSuppressedEffects(card);
        if (suppressed.Count == 0) return current;
        // A real upgrade/downgrade during combat can still publish its change,
        // but must not publish the absence of temporarily hidden upgrades.
        return previous.Where(effect => suppressed.Contains(effect) || current.Contains(effect))
            .Concat(current.Where(effect => !previous.Contains(effect))).ToArray();
    }

    internal static bool TryDowngrade(CardModel card)
    {
        ICombatState combat = GetCombat(card);
        if (combat == null || !HbrMemoryGardenCardCatalog.IsGardenCard(card)) return false;
        HbrMemoryGardenProgression.Synchronize(card);
        int[] removable = HbrMemoryGardenProgression.GetRemovableEffects(card, includeCombatSuppressed: false);
        if (removable.Length == 0) return false;
        int effect = removable[removable.Length == 1 ? 0 : card.Owner.RunState.Rng.Niche.NextInt(removable.Length)];
        State state = States.GetOrCreateValue(combat);
        if (!state.Players.TryGetValue(card.Owner, out Dictionary<string, HashSet<int>> names))
            state.Players[card.Owner] = names = new(StringComparer.Ordinal);
        if (!names.TryGetValue(card.Id.Entry, out HashSet<int> effects))
            names[card.Id.Entry] = effects = new();
        effects.Add(effect);
        if (card.Owner.GetRelic<ElectronicSoldierManual>() != null)
            HbrMemoryGardenProgression.Synchronize(card);
        else
            HbrMemoryGardenProgression.ApplyEffects(card,
                HbrMemoryGardenProgression.GetEffects(card).Where(id => id != effect).ToArray());
        return true;
    }
}
