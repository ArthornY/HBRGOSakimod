using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Patches;

// RestSiteOption.Title is nonvirtual in both native APIs. Add the variable to
// its returned localization object only for this option.
[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Title), MethodType.Getter)]
internal static class MemoryGardenRestSiteTitlePatch
{
    private static void Postfix(RestSiteOption __instance, LocString __result)
    {
        if (__instance is UpgradePointRestSiteOption option) __result.Add("points", option.UpgradePoints);
    }
}

// Native cancellation re-enables the existing buttons, but does not reload
// their title or option availability. Refresh only the point-funded entry.
[HarmonyPatch(typeof(NRestSiteRoom), "EnableOptions")]
internal static class MemoryGardenRestSiteRefreshPatch
{
    private static void Postfix(NRestSiteRoom __instance, Control ____choicesContainer)
    {
        MemoryGardenRestSiteBalanceUi.SetBusy(__instance, false);
        if (____choicesContainer == null) return;
        foreach (NRestSiteButton button in ____choicesContainer.GetChildren().OfType<NRestSiteButton>())
        {
            if (!button.IsNodeReady() || button.Option is not UpgradePointRestSiteOption option) continue;
            MemoryGardenRestSiteBalanceUi.RefreshButton(button, option);
        }
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), nameof(NRestSiteRoom.DisableOptions))]
internal static class MemoryGardenRestSiteBusyPatch
{
    private static void Prefix(NRestSiteRoom __instance) =>
        MemoryGardenRestSiteBalanceUi.SetBusy(__instance, true);
}

[HarmonyPatch(typeof(NRestSiteButton), "Reload")]
internal static class MemoryGardenRestSiteButtonBalancePatch
{
    private static void Postfix(NRestSiteButton __instance, RestSiteOption ____option,
        ref bool ____isUnclickable)
    {
        if (____option is not UpgradePointRestSiteOption option) return;
        // Create caches this flag before AfterRoomEntered gives the fire point.
        // Reload does not normally recompute it, even when its title changes.
        ____isUnclickable = !option.IsEnabled;
        MemoryGardenRestSiteBalanceUi.Track(__instance, option);
    }
}

internal static class MemoryGardenRestSiteBalanceUi
{
    private sealed class Buttons { internal readonly List<WeakReference<NRestSiteButton>> Items = new(); }
    private sealed class RoomState { internal bool Busy; }
    private static readonly ConditionalWeakTable<ElectronicSoldierManual, Buttons> Observers = new();
    private static readonly ConditionalWeakTable<NRestSiteRoom, RoomState> Rooms = new();
    private static readonly AccessTools.FieldRef<NClickableControl, bool> Focused =
        AccessTools.FieldRefAccess<NClickableControl, bool>("<IsFocused>k__BackingField");

    static MemoryGardenRestSiteBalanceUi() =>
        MemoryGardenUpgradePointsHelper.UpgradePointsChanged += RefreshBalance;

    internal static void SetBusy(NRestSiteRoom room, bool busy)
    {
        if (room != null) Rooms.GetOrCreateValue(room).Busy = busy;
    }

    internal static void Track(NRestSiteButton button, UpgradePointRestSiteOption option)
    {
        if (option.PointManual is not { } manual) return;
        List<WeakReference<NRestSiteButton>> buttons = Observers.GetOrCreateValue(manual).Items;
        buttons.RemoveAll(reference => !reference.TryGetTarget(out _));
        if (!buttons.Any(reference => reference.TryGetTarget(out NRestSiteButton item) && ReferenceEquals(item, button)))
            buttons.Add(new(button));
    }

    private static void RefreshBalance(ElectronicSoldierManual manual)
    {
        if (!Observers.TryGetValue(manual, out Buttons buttons)) return;
        foreach (WeakReference<NRestSiteButton> reference in buttons.Items.ToArray())
        {
            if (!reference.TryGetTarget(out NRestSiteButton button) || !GodotObject.IsInstanceValid(button) ||
                !button.IsInsideTree() || !button.IsNodeReady() ||
                button.Option is not UpgradePointRestSiteOption option ||
                !ReferenceEquals(option.PointManual, manual)) continue;
            RefreshButton(button, option);
        }
    }

    internal static void RefreshButton(NRestSiteButton button, UpgradePointRestSiteOption option)
    {
        // Reuse Reload's actual title/shader path. Availability is per option;
        // the native room's temporary selection lock remains independent.
        button.Option = option;
        NRestSiteRoom room = NRestSiteRoom.Instance;
        if (!option.IsEnabled) button.Disable();
        else if (room != null && !Rooms.GetOrCreateValue(room).Busy) button.Enable();
        if (Focused(button)) button.RefreshTextState();
    }
}
