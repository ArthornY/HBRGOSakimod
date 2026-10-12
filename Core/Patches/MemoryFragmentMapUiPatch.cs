using System;
using System.Linq;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Map;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace HBRGOSakiMod.Core.Patches;

internal static class MemoryFragmentMapUi
{
    internal const string LegendName = "HbrMemoryFragmentLegendItem";
    internal const string LocPrefix = "HBRGO_MEMORY_FRAGMENT";
    // UI-only event discriminator, never assigned to a map point or serialized.
    internal const MapPointType LegendHighlight = (MapPointType)(-3140);
    internal static HoverTip HoverTip() => new(
        new LocString("static_hover_tips", LocPrefix + ".title"),
        new LocString("static_hover_tips", LocPrefix + ".description"));

    internal static void EnsureLegend(NMapScreen screen, IRunState runState)
    {
        Control parent = screen.GetNodeOrNull<Control>("MapLegend/LegendItems");
        if (parent == null) return;
        NMapLegendItem existing = parent.GetNodeOrNull<NMapLegendItem>(LegendName);
        if (!MemoryFragmentMapHelper.IsEnabled(runState))
        {
            if (existing != null) existing.QueueFree();
            return;
        }
        if (existing != null) return;
        NMapLegendItem[] original = parent.GetChildren().OfType<NMapLegendItem>().ToArray();
        NMapLegendItem source = original.FirstOrDefault(item => item.Name == "UnknownLegendItem");
        if (source?.Duplicate((int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts)) is not NMapLegendItem copy)
            return;
        copy.Name = LegendName;
        if (parent is not Container && original.LastOrDefault() is { } last)
        {
            copy.Position = last.Position + new Vector2(0, Math.Max(last.Size.Y, last.CustomMinimumSize.Y) + 8f);
            parent.CustomMinimumSize = new Vector2(parent.CustomMinimumSize.X,
                Math.Max(parent.CustomMinimumSize.Y, copy.Position.Y + Math.Max(copy.Size.Y, copy.CustomMinimumSize.Y)));
        }
        parent.AddChild(copy);
        NMapLegendItem[] entries = parent.GetChildren().OfType<NMapLegendItem>().ToArray();
        for (int index = 0; index < entries.Length; index++)
        {
            entries[index].FocusNeighborTop = entries[Math.Max(0, index - 1)].GetPath();
            entries[index].FocusNeighborBottom = entries[Math.Min(entries.Length - 1, index + 1)].GetPath();
            entries[index].FocusNeighborRight = entries[index].GetPath();
        }
    }
}

[HarmonyPatch(typeof(NNormalMapPoint), "UpdateIcon")]
internal static class MemoryFragmentNodeIconPatch
{
    private static void Postfix(NNormalMapPoint __instance, IRunState ____runState,
        TextureRect ____icon, TextureRect ____outline)
    {
        if (!MemoryFragmentMapHelper.IsMemoryPoint(____runState, __instance.Point))
            return;
        HbrMemoryGardenIconHelper.Apply(____icon);
        if (GodotObject.IsInstanceValid(____outline))
        {
            // Native hover tweens only its tint. Clear the old ? texture too,
            // so another visibility refresh cannot reveal the wrong outline.
            ____outline.Texture = null;
            ____outline.Hide();
        }
    }
}

[HarmonyPatch(typeof(NNormalMapPoint), "OnFocus")]
internal static class MemoryFragmentNodeHoverPatch
{
    private static void Postfix(NNormalMapPoint __instance, IRunState ____runState)
    {
        if (__instance.State != MapPointState.Traveled &&
            MemoryFragmentMapHelper.IsMemoryPoint(____runState, __instance.Point))
            NHoverTipSet.CreateAndShow(__instance, MemoryFragmentMapUi.HoverTip());
    }
}

[HarmonyPatch(typeof(NNormalMapPoint), "OnHighlightPointType")]
internal static class MemoryFragmentNodeHighlightPatch
{
    private static readonly Action<NNormalMapPoint> Hover = AccessTools.MethodDelegate<Action<NNormalMapPoint>>(
        AccessTools.Method(typeof(NNormalMapPoint), "AnimHover"));
    private static readonly Action<NNormalMapPoint> Unhover = AccessTools.MethodDelegate<Action<NNormalMapPoint>>(
        AccessTools.Method(typeof(NNormalMapPoint), "AnimUnhover"));

    private static bool Prefix(NNormalMapPoint __instance, IRunState ____runState, MapPointType pointType)
    {
        if (!MemoryFragmentMapHelper.IsMemoryPoint(____runState, __instance.Point)) return true;
        if (pointType == MemoryFragmentMapUi.LegendHighlight) Hover(__instance);
        else Unhover(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Initialize))]
internal static class MemoryFragmentLegendInstallPatch
{
    private static void Postfix(NMapScreen __instance, RunState runState) =>
        MemoryFragmentMapUi.EnsureLegend(__instance, runState);
}

[HarmonyPatch(typeof(NMapLegendItem), "SetMapPointType")]
internal static class MemoryFragmentLegendTypePatch
{
    private static bool Prefix(string name, ref MapPointType ____pointType)
    {
        if (name != MemoryFragmentMapUi.LegendName) return true;
        ____pointType = MemoryFragmentMapUi.LegendHighlight;
        return false;
    }
}

[HarmonyPatch(typeof(NMapLegendItem), "SetLocalizedFields")]
internal static class MemoryFragmentLegendTextPatch
{
    private static bool Prefix(NMapLegendItem __instance, string name, ref HoverTip ____hoverTip)
    {
        if (name != MemoryFragmentMapUi.LegendName) return true;
        __instance.GetNode<MegaLabel>("MegaLabel").SetTextAutoSize(
            new LocString("static_hover_tips", MemoryFragmentMapUi.LocPrefix + ".title").GetFormattedText());
        ____hoverTip = MemoryFragmentMapUi.HoverTip();
        return false;
    }
}

[HarmonyPatch(typeof(NMapLegendItem), nameof(NMapLegendItem._Ready))]
internal static class MemoryFragmentLegendIconPatch
{
    private static void Postfix(NMapLegendItem __instance)
    {
        if (__instance.Name == MemoryFragmentMapUi.LegendName)
            HbrMemoryGardenIconHelper.Apply(__instance.GetNodeOrNull<TextureRect>("Icon"));
    }
}

[HarmonyPatch(typeof(NTopBarRoomIcon), "UpdateIcon")]
internal static class MemoryFragmentTopBarIconPatch
{
    private static void Postfix(IRunState ____runState, TextureRect ____roomIcon, TextureRect ____roomIconOutline)
    {
        if (____runState?.BaseRoom?.IsVictoryRoom == true ||
            !MemoryFragmentMapHelper.IsMemoryPoint(____runState, ____runState?.CurrentMapPoint))
            return;
        HbrMemoryGardenIconHelper.Apply(____roomIcon);
        if (GodotObject.IsInstanceValid(____roomIconOutline)) ____roomIconOutline.Hide();
    }
}

[HarmonyPatch(typeof(NTopBarRoomIcon), "GetHoverTipPrefixForRoomType")]
internal static class MemoryFragmentTopBarTipPatch
{
    private static bool Prefix(IRunState ____runState, ref string __result)
    {
        if (!MemoryFragmentMapHelper.IsMemoryPoint(____runState, ____runState?.CurrentMapPoint)) return true;
        __result = MemoryFragmentMapUi.LocPrefix;
        return false;
    }
}

[HarmonyPatch(typeof(NContinueRunInfo), "ShowInfo")]
internal static class MemoryFragmentContinueFloorPatch
{
    private static void Postfix(SerializableRun save, MegaRichTextLabel ____progressLabel)
    {
        if (save.Players?.Any(player => ModelDb.GetById<CharacterModel>(player.CharacterId)
                is HbrgoSaki31O or HbrgoSaki30A or HbrgoHotaru) != true) return;

        // Native room counts are queried without a run only by this summary.
        // Actual completed history handles +2 maps, old maps and special maps;
        // live layout and TotalFloor already use the graph/history directly.
        int floor = save.VisitedMapCoords.Count;
        for (int index = 0; index < save.CurrentActIndex; index++)
        {
            int actual = index < save.MapPointHistory.Count ? save.MapPointHistory[index].Count : 0;
            floor += actual > 0 ? actual :
                ModelDb.GetById<ActModel>(save.Acts[index].Id).GetNumberOfFloors(save.Players.Count > 1);
        }
        string actTitle = ModelDb.GetById<ActModel>(save.Acts[save.CurrentActIndex].Id).Title.GetFormattedText();
        string floorLabel = new LocString("main_menu_ui", "CONTINUE_RUN_INFO.floor").GetFormattedText();
        ____progressLabel.Text = $"{actTitle} [blue]- {floorLabel} {floor}[/blue]";
    }
}
