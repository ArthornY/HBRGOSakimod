using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrgoSakiCardLibraryVariantSelector
{
    [ThreadStatic]
    private static NCardPoolFilter _repeatedPortrait;

    private static readonly ConditionalWeakTable<
        NCardLibrary,
        SelectorState> States = new();

    private static readonly Action<NCardLibrary, bool> UpdateFilter =
        PrivateAccess.MethodDelegate<
            NCardLibrary,
            Action<NCardLibrary, bool>>(
                "UpdateFilter",
                typeof(bool));

    internal static void Attach(
        NCardLibrary library,
        Dictionary<NCardPoolFilter, Func<CardModel, bool>> poolFilters,
        Dictionary<CharacterModel, NCardPoolFilter> cardPoolFilters)
    {
        if (States.TryGetValue(library, out _))
        {
            return;
        }

        CharacterModel thirtyOneO = ModelDb.Character<HbrgoSaki31O>();
        CharacterModel thirtyA = ModelDb.Character<HbrgoSaki30A>();
        if (!cardPoolFilters.TryGetValue(
                thirtyOneO,
                out NCardPoolFilter visibleSakiFilter))
        {
            return;
        }

        HbrgoSakiVariant selectedVariant =
            HbrgoSakiVariantPreference.GetPreferredVariant();
        if (selectedVariant == HbrgoSakiVariant.ThirtyA &&
            !HbrgoSakiVariantPreference.IsThirtyAUnlocked())
        {
            selectedVariant = HbrgoSakiVariant.ThirtyOneO;
        }

        SelectorState state = new(visibleSakiFilter, selectedVariant);
        States.Add(library, state);
        library.TreeExiting += () => Close(library);

        // Both variants intentionally share one visible character filter.
        cardPoolFilters[thirtyA] = visibleSakiFilter;
        poolFilters[visibleSakiFilter] = card =>
            HbrgoSakiVariantPreference
                .GetCharacter(state.SelectedVariant)
                .CardPool.AllCardIds.Contains(card.Id) &&
            HbrgoSaki31OCardFilter.IsAvailableToVariant(
                card,
                state.SelectedVariant == HbrgoSakiVariant.ThirtyOneO);

        Refresh(state);
    }

    internal static NCardPoolFilter BeginPortraitRelease(NCardPoolFilter filter)
    {
        NCardPoolFilter previous = _repeatedPortrait;
        _repeatedPortrait = filter.IsSelected ? filter : null;
        return previous;
    }

    internal static void EndPortraitRelease(NCardPoolFilter previous) => _repeatedPortrait = previous;

    internal static bool TryTogglePortrait(NCardLibrary library, NCardPoolFilter filter)
    {
        // Native OnRelease has already toggled IsSelected before emitting the
        // signal. Only a real reactivation changes forms; programmatic clearing
        // and the first click keep the normal library filtering behavior.
        if (!ReferenceEquals(_repeatedPortrait, filter) || filter.IsSelected ||
            !States.TryGetValue(library, out SelectorState state) || !state.IsOpen ||
            !ReferenceEquals(filter, state.VisibleSakiFilter)) return false;
        filter.IsSelected = true;
        SelectVariant(library, state.SelectedVariant == HbrgoSakiVariant.ThirtyOneO
            ? HbrgoSakiVariant.ThirtyA : HbrgoSakiVariant.ThirtyOneO);
        return true;
    }

    internal static void SyncBeforeOpen(
        NCardLibrary library,
        IRunState runState)
    {
        if (!States.TryGetValue(library, out SelectorState state))
        {
            return;
        }

        CharacterModel runCharacter = runState == null
            ? null
            : LocalContext.GetMe(runState)?.Character;
        state.SelectedVariant = runCharacter is HbrgoSaki
            ? HbrgoSakiVariantPreference.FromCharacter(runCharacter)
            : HbrgoSakiVariantPreference.GetPreferredVariant();
        if (state.SelectedVariant == HbrgoSakiVariant.ThirtyA &&
            !HbrgoSakiVariantPreference.IsThirtyAUnlocked())
        {
            state.SelectedVariant = HbrgoSakiVariant.ThirtyOneO;
        }
        state.IsOpen = true;
        HbrgoSakiCardPortraitContext.SetLibraryVariant(library, state.SelectedVariant);
        Refresh(state);
    }

    internal static void Close(NCardLibrary library)
    {
        if (States.TryGetValue(library, out SelectorState state))
        {
            state.IsOpen = false;
        }
        HbrgoSakiCardPortraitContext.ClearLibrary(library);
    }

    internal static void Refresh(NCardLibrary library)
    {
        if (States.TryGetValue(library, out SelectorState state))
        {
            Refresh(state);
        }
    }

    private static void SelectVariant(
        NCardLibrary library,
        HbrgoSakiVariant variant)
    {
        if (!States.TryGetValue(library, out SelectorState state))
        {
            return;
        }

        if (variant == HbrgoSakiVariant.ThirtyA &&
            !HbrgoSakiVariantPreference.IsThirtyAUnlocked())
        {
            Refresh(state);
            return;
        }

        if (state.SelectedVariant == variant)
        {
            Refresh(state);
            return;
        }

        state.SelectedVariant = variant;
        if (state.IsOpen)
        {
            HbrgoSakiCardPortraitContext.SetLibraryVariant(library, variant);
        }
        HbrgoSakiVariantPreference.SetPreferredVariant(variant);
        Refresh(state);
        UpdateFilter(library, false);
    }

    private static string GetCharacterTitle(CharacterModel character)
    {
        return new LocString(
            "characters",
            character.CharacterSelectTitle).GetFormattedText();
    }

    private static void Refresh(SelectorState state)
    {
        CharacterModel current = HbrgoSakiVariantPreference.GetCharacter(state.SelectedVariant);
        bool unlocked = HbrgoSakiVariantPreference.IsThirtyAUnlocked();
        LocString description = LocString.GetIfExists("card_library", unlocked
            ? "HBRGO_SAKI_VARIANT_PORTRAIT" : "HBRGO_SAKI_VARIANT_PORTRAIT_LOCKED");
        if (description != null)
        {
            description.Add("Current", GetCharacterTitle(current));
            description.Add("Next", GetCharacterTitle(
                HbrgoSakiVariantPreference.GetCharacter(state.SelectedVariant == HbrgoSakiVariant.ThirtyOneO
                    ? HbrgoSakiVariant.ThirtyA : HbrgoSakiVariant.ThirtyOneO)));
        }
        state.VisibleSakiFilter.Loc = description ?? new LocString("characters", current.CharacterSelectTitle);
    }

    private sealed class SelectorState
    {
        internal SelectorState(
            NCardPoolFilter visibleSakiFilter,
            HbrgoSakiVariant selectedVariant)
        {
            VisibleSakiFilter = visibleSakiFilter;
            SelectedVariant = selectedVariant;
        }

        internal NCardPoolFilter VisibleSakiFilter { get; }

        internal HbrgoSakiVariant SelectedVariant { get; set; }

        internal bool IsOpen { get; set; }
    }
}

[HarmonyPatch(typeof(NCardPoolFilter), "OnRelease")]
internal static class NCardPoolFilterPortraitReleasePatch
{
    private static void Prefix(NCardPoolFilter __instance, out NCardPoolFilter __state) =>
        __state = HbrgoSakiCardLibraryVariantSelector.BeginPortraitRelease(__instance);

    // Finalizers also restore the input scope if another release callback fails.
    private static void Finalizer(NCardPoolFilter __state) =>
        HbrgoSakiCardLibraryVariantSelector.EndPortraitRelease(__state);
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary._Ready))]
[HarmonyAfter(
    "BaseLib",
    "com.ritsukage.sts2-RitsuLib.framework-character-assets")]
[HarmonyPriority(Priority.Last)]
internal static class NCardLibraryReadyVariantSelectorPatch
{
    private static void Postfix(
        NCardLibrary __instance,
        Dictionary<NCardPoolFilter, Func<CardModel, bool>> ____poolFilters,
        Dictionary<CharacterModel, NCardPoolFilter> ____cardPoolFilters)
    {
        HbrgoSakiCardLibraryVariantSelector.Attach(
            __instance,
            ____poolFilters,
            ____cardPoolFilters);
    }
}

[HarmonyPatch(typeof(NCardLibrary), "UpdateCardPoolFilter")]
internal static class NCardLibraryUpdateCardPoolFilterVariantSelectorPatch
{
    private static bool Prefix(NCardLibrary __instance, NCardPoolFilter __0) =>
        !HbrgoSakiCardLibraryVariantSelector.TryTogglePortrait(__instance, __0);

    private static void Postfix(NCardLibrary __instance)
    {
        HbrgoSakiCardLibraryVariantSelector.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuOpened))]
internal static class NCardLibraryOnSubmenuOpenedVariantSelectorPatch
{
    private static void Prefix(
        NCardLibrary __instance,
        IRunState ____runState)
    {
        HbrgoSakiCardLibraryVariantSelector.SyncBeforeOpen(
            __instance,
            ____runState);
    }

    private static void Postfix(NCardLibrary __instance)
    {
        HbrgoSakiCardLibraryVariantSelector.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuClosed))]
internal static class NCardLibraryClosedPortraitContextPatch
{
    private static void Postfix(NCardLibrary __instance)
    {
        HbrgoSakiCardLibraryVariantSelector.Close(__instance);
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), "UpdateGridNavigation")]
internal static class NCardLibraryGridUpdateGridNavigationPatch
{
    private static readonly FieldInfo CardRowsField =
        PrivateAccess.Field<NCardGrid>("_cardRows");

    private static bool Prefix(NCardLibraryGrid __instance)
    {
        List<List<NGridCardHolder>> rows =
            (List<List<NGridCardHolder>>)CardRowsField.GetValue(__instance);

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            List<NGridCardHolder> row = rows[rowIndex];
            for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                Control holder = row[columnIndex];
                holder.FocusNeighborLeft = columnIndex > 0
                    ? row[columnIndex - 1].GetPath()
                    : null;
                holder.FocusNeighborRight = columnIndex < row.Count - 1
                    ? row[columnIndex + 1].GetPath()
                    : null;
                holder.FocusNeighborTop = GetClosestNeighborPath(rows, rowIndex - 1, columnIndex);
                holder.FocusNeighborBottom = GetClosestNeighborPath(rows, rowIndex + 1, columnIndex);
            }
        }

        return false;
    }

    private static NodePath GetClosestNeighborPath(
        List<List<NGridCardHolder>> rows,
        int rowIndex,
        int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= rows.Count || rows[rowIndex].Count == 0)
        {
            return null;
        }

        List<NGridCardHolder> row = rows[rowIndex];
        return row[Math.Min(columnIndex, row.Count - 1)].GetPath();
    }
}
