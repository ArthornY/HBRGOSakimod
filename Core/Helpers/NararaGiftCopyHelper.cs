using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Tracks Give You's temporary copies by card identity, independently of the
/// replaceable energy-cost object used by memory-garden upgrades.
/// </summary>
internal static class NararaGiftCopyHelper
{
    private sealed record GiftState(int Count);
    private sealed record AppliedCost(int Count);

    private static readonly ConditionalWeakTable<CardModel, GiftState> Gifts = new();
    private static readonly ConditionalWeakTable<CardEnergyCost, AppliedCost> AppliedCosts = new();
    private static readonly AccessTools.FieldRef<CardEnergyCost, CardModel> CostOwner =
        AccessTools.FieldRefAccess<CardEnergyCost, CardModel>("_card");

    /// <summary>
    /// Call after creating a deep clone. Apply Give You's surcharge once;
    /// copies of an existing gift retain its surcharge without adding another.
    /// </summary>
    internal static void MarkCopy(CardModel source, CardModel copy)
    {
        if (copy == null || !copy.IsMutable || ReferenceEquals(source, copy))
            return;
        SetGift(copy, 1);
        EnsureTraits(copy);
    }

    internal static bool IsGift(CardModel card) => GetCount(card) > 0;

    internal static int GetCount(CardModel card) =>
        card != null && Gifts.TryGetValue(card, out GiftState state) ? state.Count : 0;

    internal static int GetXSurcharge(CardModel card) =>
        card?.EnergyCost.CostsX == true
            ? GetCount(card)
            : 0;

    internal static int SubtractXSurcharge(CardModel card, int capturedEnergy)
    {
        int surcharge = GetXSurcharge(card);
        return surcharge > 0 ? Math.Max(0, capturedEnergy - surcharge) : capturedEnergy;
    }

    internal static string GetXCostText(CardModel card) =>
        GetXSurcharge(card) is > 0 and var surcharge ? $"X+{surcharge}" : "X";

    // Called only by the two original capture sites. Clones and repeat-series
    // plays retain the already-adjusted value without paying the surcharge twice.
    internal static void CaptureX(CardEnergyCost cost, int energy) =>
        cost.CapturedXValue = SubtractXSurcharge(CostOwner(cost), energy);

    internal static void Inherit(CardModel source, CardModel copy)
    {
        int count = GetCount(source);
        if (count <= 0 || copy == null || !copy.IsMutable || ReferenceEquals(source, copy))
            return;
        SetGift(copy, count);
        EnsureTraits(copy);
    }

    internal static void InheritCost(CardEnergyCost source, CardEnergyCost copy)
    {
        if (copy != null && AppliedCosts.TryGetValue(source, out AppliedCost applied))
            RememberAppliedCost(copy, applied.Count);
    }

    internal static void EnsureTraits(CardModel card)
    {
        int count = GetCount(card);
        if (count <= 0 || !card.IsMutable)
            return;
        EnsureExhaust(card);
        CardEnergyCost cost = card.EnergyCost;
        int applied = AppliedCosts.TryGetValue(cost, out AppliedCost value) ? value.Count : 0;
        if (applied < count)
        {
            // New cost objects lose native local modifiers. A cloned cost object
            // inherits its applied count, so garden restore does not double-add.
            cost.AddThisCombat(count - applied);
            RememberAppliedCost(cost, count);
            card.InvokeEnergyCostChanged();
        }
    }

    private static void EnsureExhaust(CardModel card)
    {
        if (!card.GetKeywordsWithSources(KeywordSources.Local).Contains(CardKeyword.Exhaust))
            card.AddKeyword(CardKeyword.Exhaust);
    }

    private static void SetGift(CardModel card, int count)
    {
        Gifts.Remove(card);
        Gifts.Add(card, new GiftState(count));
    }

    private static void RememberAppliedCost(CardEnergyCost cost, int count)
    {
        AppliedCosts.Remove(cost);
        AppliedCosts.Add(cost, new AppliedCost(count));
    }
}
