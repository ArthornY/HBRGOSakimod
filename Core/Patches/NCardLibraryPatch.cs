using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches;

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
