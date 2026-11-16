using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Map;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;

namespace HBRGOSakiMod.Core.Patches;

internal static class ThirtyOneDScoutingMapUi
{
    internal const string LegendName = "HbrThirtyOneDScoutingLegendItem";
    internal const string BadgeName = "HbrThirtyOneDScoutingBadge";
    internal const string BadgePath = "res://images/ui/thirty_one_d/scout_badge.png";
    internal const string LocPrefix = "HBRGO_THIRTY_ONE_D_SCOUTING";
    internal const MapPointType LegendHighlight = (MapPointType)(-3156);
    private static readonly FieldInfo Points = AccessTools.Field(typeof(NMapScreen), "_mapPointDictionary");
    private static readonly FieldInfo Run = AccessTools.Field(typeof(NMapScreen), "_runState");
    private static readonly Action<NNormalMapPoint> UpdateIcon = AccessTools.MethodDelegate<Action<NNormalMapPoint>>(
        AccessTools.Method(typeof(NNormalMapPoint), "UpdateIcon"));
    internal static readonly Action<NNormalMapPoint> Hover = AccessTools.MethodDelegate<Action<NNormalMapPoint>>(
        AccessTools.Method(typeof(NNormalMapPoint), "AnimHover"));
    internal static readonly Action<NNormalMapPoint> Unhover = AccessTools.MethodDelegate<Action<NNormalMapPoint>>(
        AccessTools.Method(typeof(NNormalMapPoint), "AnimUnhover"));
    private static Texture2D _badge;

    internal static Texture2D BadgeTexture()
    {
        if (!GodotObject.IsInstanceValid(_badge)) _badge = ResourceLoader.Load<Texture2D>(BadgePath);
        return GodotObject.IsInstanceValid(_badge) ? _badge : null;
    }
    internal static bool CanView(IRunState run) => run != null && HbrThirtyOneDScoutingHelper.CanView(LocalContext.GetMe(run));
    internal static HbrScoutedPoint Known(IRunState run, MapPoint point) => CanView(run)
        ? HbrThirtyOneDScoutingHelper.GetKnownPoint(run, point) : null;
    internal static HoverTip LegendTip() => new(new LocString("static_hover_tips", LocPrefix + ".title"),
        new LocString("static_hover_tips", LocPrefix + ".description"));

    // Called only when the shared scouting ledger changes, not from _Process.
    internal static void Refresh(IRunState run)
    {
        NMapScreen screen = NMapScreen.Instance;
        if (!GodotObject.IsInstanceValid(screen) || !ReferenceEquals(Run.GetValue(screen), run)) return;
        if (Points.GetValue(screen) is Dictionary<MapCoord, NMapPoint> points)
            foreach (NNormalMapPoint point in points.Values.OfType<NNormalMapPoint>())
                if (GodotObject.IsInstanceValid(point)) UpdateIcon(point);
        EnsureLegend(screen, run);
    }
    internal static string IconName(RoomType type) => type switch
    {
        RoomType.Monster => "map_monster", RoomType.Shop => "map_shop", RoomType.Treasure => "map_chest",
        _ => "map_unknown"
    };
    internal static MapPointType PointType(HbrScoutedPoint known) => known.RoomType switch
    {
        RoomType.Monster => MapPointType.Monster, RoomType.Shop => MapPointType.Shop,
        RoomType.Treasure => MapPointType.Treasure, _ => MapPointType.Unknown
    };
    internal static (float Diameter, float X, float Y) BadgePlacement(float width, float height)
    {
        float nativeSize = Math.Min(width, height);
        float diameter = (nativeSize > 0f ? nativeSize : 64f) / 3f;
        return (diameter, -diameter * .85f, -diameter * .15f);
    }
    internal static void Apply(NNormalMapPoint point, IRunState run, TextureRect icon, TextureRect outline)
    {
        if (!GodotObject.IsInstanceValid(icon)) return;
        HbrScoutedPoint known = Known(run, point.Point);
        TextureRect badge = icon.GetNodeOrNull<TextureRect>(BadgeName);
        if (known == null)
        {
            if (GodotObject.IsInstanceValid(badge)) badge.Hide();
            return;
        }
        if (known.IsMemory)
        {
            HbrMemoryGardenIconHelper.Apply(icon);
            if (GodotObject.IsInstanceValid(outline)) { outline.Texture = null; outline.Hide(); }
        }
        else if (known.OriginalType == MapPointType.Unknown)
        {
            string name = IconName(known.RoomType);
            icon.Texture = ResourceLoader.Load<Texture2D>("res://images/atlases/ui_atlas.sprites/map/icons/" + name + ".tres");
            if (GodotObject.IsInstanceValid(outline))
            {
                outline.Texture = ResourceLoader.Load<Texture2D>("res://images/atlases/compressed.sprites/map/" + name + "_outline.tres");
                outline.Show();
            }
        }
        if (!GodotObject.IsInstanceValid(badge))
        {
            badge = new TextureRect { Name = BadgeName, Texture = BadgeTexture(), MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                AnchorLeft = 1f, AnchorRight = 1f, AnchorTop = 0f, AnchorBottom = 0f, ZIndex = 5 };
            icon.AddChild(badge);
        }
        (float diameter, float x, float y) = BadgePlacement(icon.Size.X, icon.Size.Y);
        badge.Size = Vector2.One * diameter;
        badge.Position = new Vector2(x, y);
        badge.Show();
    }
    internal static string MemoryDescription(HbrStoryDefinition definition)
    {
        string[] ids = definition.Pages.SelectMany(page => page.MetCharacters).Distinct(StringComparer.Ordinal).ToArray();
        string[] names = HbrStoryCharacterCatalog.Entries.Where(entry => ids.Contains(entry.Id)).Select(entry => entry.Name).ToArray();
        string[] cards = HbrMemoryGardenCardCatalog.CardTypes.Select(type => ModelDb.GetById<CardModel>(ModelDb.GetId(type)))
            .Where(card => ids.Contains(HbrMemoryGarden.CharacterIdOf(card))).Select(card => card.Title).ToArray();
        string text = "记忆碎片：" + definition.Title;
        if (names.Length > 0) text += "\n可遇见：" + string.Join("、", names);
        if (cards.Length > 0) text += "\n记忆之庭：" + string.Join("、", cards);
        return text;
    }
    internal static string RewardRecipient(IEnumerable<HbrScoutedRelic> rewards, ulong[] playerIds)
    {
        HbrScoutedRelic[] entries = rewards.ToArray();
        int bonus = entries.Count(reward => reward.IsBonus);
        if (playerIds.Length <= 1 && bonus == 0) return "";
        List<string> labels = entries.Where(reward => !reward.IsBonus).Select(reward =>
        {
            int player = Array.IndexOf(playerIds, reward.PlayerId);
            return "玩家" + (player >= 0 ? (player + 1).ToString() : reward.PlayerId.ToString());
        }).Distinct().ToList();
        if (bonus > 0) labels.Add("额外遗物" + (bonus > 1 ? "×" + bonus : ""));
        return string.Join("、", labels) + "：";
    }
    internal static IEnumerable<IHoverTip> Tips(IRunState run, HbrScoutedPoint known)
    {
        EventModel story = HbrThirtyOneDScoutingHelper.ResolveEvent(known);
        string description = known.RoomType switch
        {
            RoomType.Monster => "战斗", RoomType.Shop => "商店", RoomType.Treasure => "宝箱",
            _ => story?.Title.GetFormattedText() ?? "事件"
        };
        if (known.IsMemory && story is HbrStoryEvent memory) description = MemoryDescription(memory.Definition);
        yield return new HoverTip(new LocString("static_hover_tips", LocPrefix + ".title"), HbrCompactText.Literal("text", description));
        if (known.RoomType != RoomType.Treasure || known.Relics == null) yield break;
        ulong[] players = run.Players.Select(player => player.NetId).ToArray();
        foreach (var rewards in known.Relics.GroupBy(reward => reward.RelicId, StringComparer.Ordinal))
        {
            RelicModel relic = HbrThirtyOneDScoutingHelper.ResolveRelic(rewards.First());
            if (relic == null) continue;
            string recipient = RewardRecipient(rewards, players);
            if (relic.HoverTips.FirstOrDefault() is HoverTip tip)
            {
                // Native relic tips do not carry a texture. Add the real icon
                // explicitly; retain each relic's distinct native localization
                // identity so NHoverTipSet.RemoveDupes keeps every reward.
                HoverTip withIcon = new(relic.Title, tip.Description, relic.Icon);
                withIcon.SetCanonicalModel(relic.CanonicalInstance);
                yield return HoverTipCompatibility.Copy(withIcon, recipient + relic.Title.GetFormattedText(), tip.Description);
            }
        }
    }
    internal static void EnsureLegend(NMapScreen screen, IRunState run)
    {
        Control parent = screen.GetNodeOrNull<Control>("MapLegend/LegendItems");
        if (parent == null) return;
        NMapLegendItem existing = parent.GetNodeOrNull<NMapLegendItem>(LegendName);
        if (!CanView(run)) { if (existing != null) existing.QueueFree(); return; }
        if (existing != null) return;
        NMapLegendItem[] original = parent.GetChildren().OfType<NMapLegendItem>().ToArray();
        if (original.FirstOrDefault(item => item.Name == "UnknownLegendItem")?.Duplicate(
                (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts)) is not NMapLegendItem copy) return;
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
internal static class ThirtyOneDScoutingNodeIconPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NNormalMapPoint __instance, IRunState ____runState, TextureRect ____icon, TextureRect ____outline) =>
        ThirtyOneDScoutingMapUi.Apply(__instance, ____runState, ____icon, ____outline);
}
[HarmonyPatch(typeof(NNormalMapPoint), "OnFocus")]
internal static class ThirtyOneDScoutingNodeHoverPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NNormalMapPoint __instance, IRunState ____runState)
    {
        if (ThirtyOneDScoutingMapUi.Known(____runState, __instance.Point) is not { } known) return;
        NHoverTipSet.Remove(__instance);
        NHoverTipSet.CreateAndShow(__instance, ThirtyOneDScoutingMapUi.Tips(____runState, known));
    }
}
[HarmonyPatch(typeof(NNormalMapPoint), "OnHighlightPointType")]
internal static class ThirtyOneDScoutingNodeHighlightPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(NNormalMapPoint __instance, IRunState ____runState, MapPointType pointType)
    {
        if (ThirtyOneDScoutingMapUi.Known(____runState, __instance.Point) is not { } known) return true;
        bool match = pointType == ThirtyOneDScoutingMapUi.LegendHighlight || (known.IsMemory
            ? pointType == MemoryFragmentMapUi.LegendHighlight : pointType == ThirtyOneDScoutingMapUi.PointType(known));
        if (match) ThirtyOneDScoutingMapUi.Hover(__instance); else ThirtyOneDScoutingMapUi.Unhover(__instance);
        return false;
    }
}
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Initialize))]
internal static class ThirtyOneDScoutingLegendInstallPatch
{
    private static void Postfix(NMapScreen __instance, RunState runState) => ThirtyOneDScoutingMapUi.EnsureLegend(__instance, runState);
}
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
internal static class ThirtyOneDScoutingMapReadyPatch
{
    // Native generation publishes RunState.Map after AfterMapGenerated, so the
    // point controls may be created only after the ledger's initial refresh.
    private static void Postfix(IRunState ____runState) => ThirtyOneDScoutingMapUi.Refresh(____runState);
}
[HarmonyPatch(typeof(NMapLegendItem), "SetMapPointType")]
internal static class ThirtyOneDScoutingLegendTypePatch
{
    private static bool Prefix(string name, ref MapPointType ____pointType)
    {
        if (name != ThirtyOneDScoutingMapUi.LegendName) return true;
        ____pointType = ThirtyOneDScoutingMapUi.LegendHighlight; return false;
    }
}
[HarmonyPatch(typeof(NMapLegendItem), "SetLocalizedFields")]
internal static class ThirtyOneDScoutingLegendTextPatch
{
    private static bool Prefix(NMapLegendItem __instance, string name, ref HoverTip ____hoverTip)
    {
        if (name != ThirtyOneDScoutingMapUi.LegendName) return true;
        __instance.GetNode<MegaLabel>("MegaLabel").SetTextAutoSize(
            new LocString("static_hover_tips", ThirtyOneDScoutingMapUi.LocPrefix + ".title").GetFormattedText());
        ____hoverTip = ThirtyOneDScoutingMapUi.LegendTip(); return false;
    }
}
[HarmonyPatch(typeof(NMapLegendItem), nameof(NMapLegendItem._Ready))]
internal static class ThirtyOneDScoutingLegendIconPatch
{
    private static void Postfix(NMapLegendItem __instance)
    {
        if (__instance.Name != ThirtyOneDScoutingMapUi.LegendName || __instance.GetNodeOrNull<TextureRect>("Icon") is not { } icon) return;
        icon.Texture = ThirtyOneDScoutingMapUi.BadgeTexture();
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    }
}
