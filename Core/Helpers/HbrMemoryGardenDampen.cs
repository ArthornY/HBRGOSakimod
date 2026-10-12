using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrMemoryGardenDampen
{
    private readonly record struct PendingEffect(string CardEntry, int EffectId);

    internal static void Apply(Player player)
    {
        if (player?.GetRelic<ElectronicSoldierManual>() is not { } manual ||
            player.PlayerCombatState == null) return;

        List<PendingEffect> pending = ReadPending(manual);
        // Dampen's native loop visits each physical copy. Shared progress must
        // lose exactly one effect per name, not one for every card in the deck.
        CardModel[] cards = player.PlayerCombatState.AllCards
            .Where(card => HbrMemoryGardenCardCatalog.IsGardenCard(card) &&
                HbrMemoryGardenProgression.IsRealCard(card))
            .GroupBy(card => card.Id.Entry)
            .Select(group => group.First())
            .ToArray();
        foreach (CardModel card in cards)
        {
            if (pending.Any(entry => entry.CardEntry == card.Id.Entry)) continue;
            HbrMemoryGardenProgression.Synchronize(card);
            int[] before = HbrMemoryGardenProgression.GetEffects(card);
            if (!HbrMemoryGardenProgression.TryDowngrade(card)) continue;
            int[] after = HbrMemoryGardenProgression.GetEffects(card);
            foreach (int removed in before.Except(after))
                pending.Add(new PendingEffect(card.Id.Entry, removed));
            WritePending(manual, pending);
        }
    }

    internal static IReadOnlyList<int> GetPendingEffects(CardModel card)
    {
        if (card?.IsMutable != true ||
            card.Owner?.GetRelic<ElectronicSoldierManual>() is not { } manual)
            return Array.Empty<int>();
        return ReadPending(manual)
            .Where(entry => entry.CardEntry == card.Id.Entry)
            .Select(entry => entry.EffectId)
            .ToArray();
    }

    internal static IReadOnlyList<int> FilterAvailableEffects(CardModel card, IReadOnlyList<int> available)
    {
        int[] pending = GetPendingEffects(card)
            .Concat(HbrMemoryGardenCombatDowngrade.GetSuppressedEffects(card)).ToArray();
        if (pending.Length == 0) return available;
        return available.Where(effect => !pending.Contains(effect) &&
            !(card is MyLegendBeginsHbrgoSaki &&
                (effect == 3 && pending.Contains(4) || effect == 4 && pending.Contains(3))) &&
            !(card is SummerGrassDreamHbrgoSaki &&
                (effect == 0 && pending.Contains(3) || effect == 3 && pending.Contains(0))) &&
            !(card is CrushYouLikeABugHbrgoSaki &&
                (effect == 0 && pending.Contains(4) || effect == 4 && pending.Contains(0))))
            .ToArray();
    }

    internal static void RestorePending(Player player)
    {
        if (player?.GetRelic<ElectronicSoldierManual>() is not { } manual) return;
        List<PendingEffect> pending = ReadPending(manual);
        if (pending.Count == 0) return;

        foreach (PendingEffect entry in pending.ToArray())
        {
            Type cardType = HbrMemoryGardenCardCatalog.CardTypes
                .FirstOrDefault(type => ModelDb.GetId(type).Entry == entry.CardEntry);
            if (cardType == null) continue;
            // Restore the one lost effect without overwriting upgrades earned
            // since Dampen was applied, even if every physical copy is gone.
            if (!HbrMemoryGardenProgression.RestoreEffect(player, cardType, entry.EffectId)) continue;
            pending.Remove(entry);
            WritePending(manual, pending);
        }
    }

    private static List<PendingEffect> ReadPending(ElectronicSoldierManual manual)
    {
        List<PendingEffect> pending = new();
        foreach (string segment in (manual.MemoryGardenDampenRestores ?? string.Empty)
                     .Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = segment.LastIndexOf(':');
            if (separator <= 0 ||
                !int.TryParse(segment.AsSpan(separator + 1), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int effectId) || effectId < 0 || effectId > 6)
                continue;
            PendingEffect entry = new(segment[..separator], effectId);
            if (!pending.Contains(entry)) pending.Add(entry);
        }
        return pending;
    }

    private static void WritePending(ElectronicSoldierManual manual, IEnumerable<PendingEffect> pending) =>
        manual.SetMemoryGardenDampenRestores(string.Join('|', pending.Select(entry =>
            entry.CardEntry + ":" + entry.EffectId.ToString(CultureInfo.InvariantCulture))));
}
