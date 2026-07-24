using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary._Ready))]
internal static class NCardLibraryReadyPatch
{
    private static readonly FieldInfo CardPoolFiltersField = AccessTools.Field(
        typeof(NCardLibrary),
        "_cardPoolFilters");

    private static readonly FieldInfo PoolFiltersField = AccessTools.Field(
        typeof(NCardLibrary),
        "_poolFilters");

    private static readonly FieldInfo ColorlessFilterField = AccessTools.Field(
        typeof(NCardLibrary),
        "_colorlessFilter");

    private static readonly FieldInfo LastHoveredControlField = AccessTools.Field(
        typeof(NCardLibrary),
        "_lastHoveredControl");

    private static readonly MethodInfo UpdateCardPoolFilterMethod = AccessTools.Method(
        typeof(NCardLibrary),
        "UpdateCardPoolFilter");

    private static void Postfix(NCardLibrary __instance)
    {
        CharacterModel character = ModelDb.Character<HbrgoSaki>();
        Dictionary<CharacterModel, NCardPoolFilter> cardPoolFilters =
            (Dictionary<CharacterModel, NCardPoolFilter>)CardPoolFiltersField.GetValue(__instance);
        if (cardPoolFilters.ContainsKey(character))
        {
            return;
        }

        NCardPoolFilter template = (NCardPoolFilter)ColorlessFilterField.GetValue(__instance);
        NCardPoolFilter filter = CreateFilter(template, __instance);

        cardPoolFilters.Add(character, filter);

        Dictionary<NCardPoolFilter, Func<CardModel, bool>> poolFilters =
            (Dictionary<NCardPoolFilter, Func<CardModel, bool>>)PoolFiltersField.GetValue(__instance);
        poolFilters.Add(filter, card => card.Pool is HBRGOSakiCardPool);
    }

    private static NCardPoolFilter CreateFilter(NCardPoolFilter template, NCardLibrary library)
    {
        NCardPoolFilter filter = (NCardPoolFilter)template.Duplicate();
        filter.Name = "HBRGOSakiPool";
        filter.Visible = true;
        filter.Loc = new LocString("characters", "HBRGO_SAKI.title");

        template.GetParent().AddChild(filter);
        ApplyIcon(filter);

        filter.Connect(
            NCardPoolFilter.SignalName.Toggled,
            Callable.From<NCardPoolFilter>(selectedFilter =>
            {
                UpdateCardPoolFilterMethod.Invoke(library, new object[] { selectedFilter });
            }));

        filter.Connect(
            Control.SignalName.FocusEntered,
            Callable.From(() => LastHoveredControlField.SetValue(library, filter)));

        return filter;
    }

    private static void ApplyIcon(NCardPoolFilter filter)
    {
        Texture2D icon = ResourceLoader.Load<Texture2D>(
            ImageHelper.GetImagePath("ui/top_panel/character_icon_hbrgo_saki.png"));

        if (icon == null)
        {
            return;
        }

        filter.GetNode<TextureRect>("Image").Texture = icon;
        filter.GetNode<TextureRect>("Image/Shadow").Texture = icon;
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.RefreshVisibility))]
internal static class NCardLibraryGridRefreshVisibilityPatch
{
    private static readonly FieldInfo UnlockedCardsField = AccessTools.Field(
        typeof(NCardLibraryGrid),
        "_unlockedCards");

    private static void Postfix(NCardLibraryGrid __instance)
    {
        HashSet<CardModel> unlockedCards = (HashSet<CardModel>)UnlockedCardsField.GetValue(__instance);
        unlockedCards.Add(ModelDb.Card<ChihayaClassmateHbrgoSaki>());
        unlockedCards.Add(ModelDb.Card<SwitchHbrgoSaki>());
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), "UpdateGridNavigation")]
internal static class NCardLibraryGridUpdateGridNavigationPatch
{
    private static readonly FieldInfo CardRowsField = AccessTools.Field(
        typeof(NCardGrid),
        "_cardRows");

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
