using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Holding Shift (or pressing the right stick) shows the full hover text. When
/// the key changes while a compact hover is open, the tooltip is rebuilt by
/// calling the same native method that opened it, so position and lifetime
/// stay with the game. Only owners whose tooltip is still open are rebuilt.
/// </summary>
internal static class HbrHoverDetail
{
    private sealed class Entry
    {
        internal Control Owner;
        internal Action Reshow;
        internal bool Expanded;
    }

    private static readonly List<Entry> Tracked = new();
    private static readonly FieldInfo ActiveSetsField = AccessTools.Field(typeof(NHoverTipSet), "_activeHoverTips");
    private static readonly MethodInfo CardHolderTips = AccessTools.Method(typeof(NCardHolder), "CreateHoverTips", Type.EmptyTypes);
    private static readonly MethodInfo MerchantCardTip = AccessTools.Method(typeof(NMerchantCard), "CreateHoverTip", Type.EmptyTypes);
    private static readonly MethodInfo InspectCardDisplay = AccessTools.Method(typeof(NInspectCardScreen), "UpdateCardDisplay", Type.EmptyTypes);
    private static readonly MethodInfo PowerTips = AccessTools.Method(typeof(NPower), "ShowPowerHoverTips", new[] { typeof(CombatState) });
    private static readonly MethodInfo CreatureTips = AccessTools.Method(typeof(NCreature), "ShowCreatureHoverTips", new[] { typeof(CombatState) });
    private static readonly MethodInfo RelicFocus = AccessTools.Method(typeof(NRelicInventoryHolder), "OnFocus", Type.EmptyTypes);
    private static readonly PropertyInfo ControllerManager = AccessTools.Property(typeof(NControllerManager), "Instance");
    private static readonly PropertyInfo UsingController =
        AccessTools.Property(typeof(NControllerManager), "IsUsingController") ??
        AccessTools.Property(typeof(NControllerManager), "IsUsingDirectionalNavigation");

    private static ulong _heldFrame = ulong.MaxValue;
    private static bool _held;
    private static bool _refreshing;
    private static int _builds;

    /// <summary>Counts compact or full builds, so a call site knows it built tips.</summary>
    internal static int Builds => _builds;

    internal static void NoteBuild() => _builds++;

    /// <summary>True while the player holds the key for full text.</summary>
    internal static bool IsHeld()
    {
        ulong frame = Engine.GetProcessFrames();
        if (frame == _heldFrame) return _held;
        _heldFrame = frame;
        _held = ReadHeld();
        return _held;
    }

    private static bool ReadHeld()
    {
        try
        {
            if (Input.IsKeyPressed(Key.Shift)) return true;
            foreach (int device in Input.GetConnectedJoypads())
                if (Input.IsJoyButtonPressed(device, JoyButton.RightStick)) return true;
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Hover detail key read failed: {error.Message}");
        }
        return false;
    }

    /// <summary>The hint names the right stick only while a controller drives the UI.</summary>
    internal static bool IsUsingController()
    {
        try
        {
            object manager = ControllerManager?.GetValue(null);
            return manager != null && UsingController?.GetValue(manager) is true &&
                Input.GetConnectedJoypads().Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Called after a patched call site built its tips through the helper.</summary>
    internal static void Track(object instance)
    {
        (Control owner, Action reshow) = Resolve(instance);
        if (owner == null || reshow == null) return;
        // Closed tooltips need no rebuild; keep only the ones still on screen.
        Tracked.RemoveAll(entry => entry.Owner == owner || !IsOpen(entry.Owner));
        Tracked.Add(new Entry { Owner = owner, Reshow = reshow, Expanded = IsHeld() });
    }

    private static (Control, Action) Resolve(object instance) => instance switch
    {
        NCardHolder holder when CardHolderTips != null => (holder, () =>
        {
            NHoverTipSet.Remove(holder);
            CardHolderTips.Invoke(holder, null);
        }),
        NMerchantCard card when MerchantCardTip != null => (card, () =>
        {
            NHoverTipSet.Remove(card);
            MerchantCardTip.Invoke(card, null);
        }),
        // UpdateCardDisplay clears every tooltip before creating its own.
        NInspectCardScreen screen when InspectCardDisplay != null => (screen, () => InspectCardDisplay.Invoke(screen, null)),
        // Creature tooltips are owned by the hitbox; ShowHoverTips replaces them itself.
        NPower power when PowerTips != null => (CreatureHitbox(power), () => PowerTips.Invoke(power, new object[] { null })),
        NCreature creature when CreatureTips != null => (creature.Hitbox, () => CreatureTips.Invoke(creature, new object[] { null })),
        NRelicInventoryHolder relic when RelicFocus != null => (relic, () =>
        {
            NHoverTipSet.Remove(relic);
            RelicFocus.Invoke(relic, null);
        }),
        _ => (null, null)
    };

    private static Control CreatureHitbox(NPower power) =>
        power?.Model?.Owner == null ? null : NCombatRoom.Instance?.GetCreatureNode(power.Model.Owner)?.Hitbox;

    /// <summary>Runs every frame while any tooltip is open.</summary>
    internal static void Poll()
    {
        if (_refreshing || Tracked.Count == 0) return;
        bool held = IsHeld();
        Entry[] entries = null;
        foreach (Entry entry in Tracked)
        {
            if (entry.Expanded == held) continue;
            entries ??= Tracked.ToArray();
            break;
        }
        if (entries == null) return;
        _refreshing = true;
        try
        {
            foreach (Entry entry in entries)
            {
                if (entry.Expanded == held) continue;
                entry.Expanded = held;
                if (!IsOpen(entry.Owner)) continue;
                entry.Reshow();
            }
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Hover detail refresh failed: {error.Message}");
        }
        finally
        {
            _refreshing = false;
            Tracked.RemoveAll(entry => !GodotObject.IsInstanceValid(entry.Owner) || !IsOpen(entry.Owner));
        }
    }

    private static bool IsOpen(Control owner)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree()) return false;
        return ActiveSetsField?.GetValue(null) is Dictionary<Control, NHoverTipSet> sets &&
            sets.TryGetValue(owner, out NHoverTipSet set) && GodotObject.IsInstanceValid(set) &&
            !set.IsQueuedForDeletion();
    }
}
