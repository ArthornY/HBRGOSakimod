using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Patches;

internal static class TopBarPowerHoverRedirect
{
    // Native power hover calls are synchronous. Scope only their creature-tip
    // call, leaving icon animation, compact descriptions and subscriptions intact.
    [ThreadStatic] private static NPower _current;
    private static readonly ConditionalWeakTable<NPower, NHoverTipSet> Tips = new();

    internal static void Enter(NPower icon, out NPower previous)
    {
        previous = _current;
        bool owns = Owns(icon);
        if (!owns) Remove(icon);
        _current = owns ? icon : null;
    }

    internal static void Restore(NPower previous) => _current = previous;

    internal static void ClearForContainer(NPowerContainer container)
    {
        if (!Usable(container)) return;
        foreach (Node child in container.GetChildren())
            if (child is NPower icon) Remove(icon);
        if (Usable(_current) && ReferenceEquals(_current.Container, container))
        {
            Remove(_current);
            _current = null;
        }
    }

    internal static void Remove(NPower icon)
    {
        if (icon == null || !Tips.TryGetValue(icon, out _)) return;
        Tips.Remove(icon);
        NHoverTipSet.Remove(icon);
    }

    internal static bool Show(NCreature creature, IEnumerable<IHoverTip> hoverTips)
    {
        NPower icon = _current;
        if (!Matches(creature, icon)) return false;
        // Match the native creature guard while anchoring the same tip list to
        // the moved icon. CreateAndShow still owns targeting and overflow guards.
        if (NCombatRoom.Instance?.Ui?.Hand?.InCardPlay == true) return true;
        Remove(icon);
        NHoverTipSet tips = NativeHoverTipCompatibility.CreateAndShowBelow(icon, hoverTips);
        if (tips != null)
        {
            Tips.Add(icon, tips);
            tips.SetFollowOwner();
        }
        return true;
    }

    internal static bool Hide(NCreature creature)
    {
        NPower icon = _current;
        if (!Matches(creature, icon)) return false;
        Remove(icon);
        return true;
    }

    private static bool Matches(NCreature creature, NPower icon) =>
        Owns(icon) && Usable(creature) && ReferenceEquals(creature.Entity, icon.Model.Owner);

    private static bool Owns(NPower icon) => Usable(icon) &&
        NTopBarPowerLayout.Current?.Owns(icon.Container) == true;

    private static bool Usable(Node node) => node != null && GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();
}

[HarmonyPatch]
internal static class TopBarPowerHoverScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(NPower), "OnHovered", Type.EmptyTypes);
        yield return AccessTools.DeclaredMethod(typeof(NPower), "OnUnhovered", Type.EmptyTypes);
        yield return AccessTools.DeclaredMethod(typeof(NPower), "ShowPowerHoverTips", new[] { typeof(CombatState) });
    }

    private static void Prefix(NPower __instance, MethodBase __originalMethod, out NPower __state)
    {
        TopBarPowerHoverRedirect.Enter(__instance, out __state);
        if (__originalMethod.Name == "OnUnhovered") TopBarPowerHoverRedirect.Remove(__instance);
    }

    private static Exception Finalizer(NPower __state, Exception __exception)
    {
        TopBarPowerHoverRedirect.Restore(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.ShowHoverTips))]
internal static class TopBarPowerShowHoverPatch
{
    private static bool Prefix(NCreature __instance, IEnumerable<IHoverTip> hoverTips) =>
        !TopBarPowerHoverRedirect.Show(__instance, hoverTips);
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.HideHoverTips))]
internal static class TopBarPowerHideHoverPatch
{
    private static bool Prefix(NCreature __instance) => !TopBarPowerHoverRedirect.Hide(__instance);
}

[HarmonyPatch(typeof(NPower), nameof(NPower._ExitTree))]
internal static class TopBarPowerHoverExitPatch
{
    private static void Prefix(NPower __instance) => TopBarPowerHoverRedirect.Remove(__instance);
}
