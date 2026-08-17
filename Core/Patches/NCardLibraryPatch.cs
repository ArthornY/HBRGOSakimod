using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
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
    private const string SelectorNodeName =
        "HBRGOSakiCompendiumVariantSelector";

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

        CharacterModel artificialSavior = ModelDb.Character<HbrgoSaki>();
        CharacterModel thirtyOneO = ModelDb.Character<HbrgoSaki31O>();
        if (!cardPoolFilters.TryGetValue(
                artificialSavior,
                out NCardPoolFilter visibleSakiFilter) ||
            !poolFilters.TryGetValue(
                visibleSakiFilter,
                out Func<CardModel, bool> basePoolFilter))
        {
            Log.Warn(
                "Could not attach the HBRGO Saki card-library variant selector: " +
                "the visible character filter was not registered.");
            return;
        }

        // 31O intentionally shares the visible Saki pool button. This alias also
        // keeps NCardLibrary.OnSubmenuOpened from indexing a missing run character.
        cardPoolFilters[thirtyOneO] = visibleSakiFilter;

        Node poolFilterContainer = visibleSakiFilter.GetParent();
        Node sidebarLayout = poolFilterContainer?.GetParent();
        if (poolFilterContainer == null || sidebarLayout == null)
        {
            Log.Warn(
                "Could not attach the HBRGO Saki card-library variant selector: " +
                "the pool-filter layout was not found.");
            return;
        }

        HBoxContainer root = new()
        {
            Name = SelectorNodeName,
            Visible = false,
            Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(0f, 40f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);

        Button thirtyOneOButton = CreateButton(
            GetCharacterTitle(thirtyOneO));
        Button artificialSaviorButton = CreateButton(
            GetCharacterTitle(artificialSavior));
        root.AddChild(thirtyOneOButton);
        root.AddChild(artificialSaviorButton);

        SelectorState state = new(
            root,
            thirtyOneOButton,
            artificialSaviorButton,
            visibleSakiFilter,
            HbrgoSakiVariantPreference.GetPreferredVariant());
        States.Add(library, state);

        poolFilters[visibleSakiFilter] = card =>
            basePoolFilter(card) &&
            (state.SelectedVariant != HbrgoSakiVariant.ThirtyOneO ||
                !HbrgoSaki31OCardFilter.IsExcluded(card));

        thirtyOneOButton.Pressed += () =>
            SelectVariant(library, HbrgoSakiVariant.ThirtyOneO);
        artificialSaviorButton.Pressed += () =>
            SelectVariant(library, HbrgoSakiVariant.ArtificialSavior);

        sidebarLayout.AddChild(root);
        sidebarLayout.MoveChild(
            root,
            poolFilterContainer.GetIndex() + 1);
        Refresh(state);
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
        RefreshButtons(state);
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

        if (state.SelectedVariant == variant)
        {
            RefreshButtons(state);
            return;
        }

        state.SelectedVariant = variant;
        RefreshButtons(state);
        UpdateFilter(library, false);
    }

    private static Button CreateButton(string title)
    {
        Button button = new()
        {
            Text = title,
            TooltipText = title,
            ToggleMode = true,
            ClipText = true,
            CustomMinimumSize = new Vector2(0f, 40f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", 14);
        return button;
    }

    private static string GetCharacterTitle(CharacterModel character)
    {
        return new LocString(
            "characters",
            character.CharacterSelectTitle).GetFormattedText();
    }

    private static void Refresh(SelectorState state)
    {
        state.Root.Visible = state.VisibleSakiFilter.IsSelected;
        RefreshButtons(state);
    }

    private static void RefreshButtons(SelectorState state)
    {
        state.ThirtyOneOButton.ButtonPressed =
            state.SelectedVariant == HbrgoSakiVariant.ThirtyOneO;
        state.ArtificialSaviorButton.ButtonPressed =
            state.SelectedVariant == HbrgoSakiVariant.ArtificialSavior;
    }

    private sealed class SelectorState
    {
        internal SelectorState(
            HBoxContainer root,
            Button thirtyOneOButton,
            Button artificialSaviorButton,
            NCardPoolFilter visibleSakiFilter,
            HbrgoSakiVariant selectedVariant)
        {
            Root = root;
            ThirtyOneOButton = thirtyOneOButton;
            ArtificialSaviorButton = artificialSaviorButton;
            VisibleSakiFilter = visibleSakiFilter;
            SelectedVariant = selectedVariant;
        }

        internal HBoxContainer Root { get; }

        internal Button ThirtyOneOButton { get; }

        internal Button ArtificialSaviorButton { get; }

        internal NCardPoolFilter VisibleSakiFilter { get; }

        internal HbrgoSakiVariant SelectedVariant { get; set; }
    }
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

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.RefreshVisibility))]
internal static class NCardLibraryGridRefreshVisibilityPatch
{
    private static readonly FieldInfo UnlockedCardsField =
        PrivateAccess.Field<NCardLibraryGrid>("_unlockedCards");

    private static void Postfix(NCardLibraryGrid __instance)
    {
        HashSet<CardModel> unlockedCards = (HashSet<CardModel>)UnlockedCardsField.GetValue(__instance);
        unlockedCards.Add(ModelDb.Card<ChihayaClassmateHbrgoSaki>());
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
