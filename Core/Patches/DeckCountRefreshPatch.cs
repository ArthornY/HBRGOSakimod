using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.TopBar;

namespace HBRGOSakiMod.Core.Patches;

internal static class DeckCountRefresh
{
    private static readonly MethodInfo NativeRefresh = AccessTools.DeclaredMethod(
        typeof(NTopBarDeckButton), "OnPileContentsChanged");

    internal static Action Callback(NTopBarDeckButton button) =>
        NativeRefresh.CreateDelegate<Action>(button);
}

[HarmonyPatch(typeof(NTopBarDeckButton), nameof(NTopBarDeckButton.Initialize))]
internal static class DeckCountInitializePatch
{
    private static void Prefix(NTopBarDeckButton __instance, CardPile ____pile)
    {
        if (____pile == null) return;
        Action refresh = DeckCountRefresh.Callback(__instance);
        ____pile.ContentsChanged -= refresh;
        // Native Initialize binds its completion listeners without first
        // releasing them. Keep one subscription when switching players/reusing UI.
        ____pile.CardAddFinished -= refresh;
        ____pile.CardRemoveFinished -= refresh;
    }

    private static void Postfix(NTopBarDeckButton __instance, CardPile ____pile)
    {
        // New permanent cards can enter Deck without a fly-card node, so no
        // CardAddFinished fires. Use the same event/data as NDeckViewScreen.
        ____pile.ContentsChanged += DeckCountRefresh.Callback(__instance);
    }
}

[HarmonyPatch]
internal static class DeckCountExitPatch
{
    private static readonly MethodInfo Exit = AccessTools.DeclaredMethod(
        typeof(NTopBarDeckButton), nameof(NTopBarDeckButton._ExitTree));
    private static bool Prepare() => Exit != null;
    private static MethodBase TargetMethod() => Exit;

    private static void Prefix(NTopBarDeckButton __instance, CardPile ____pile)
    {
        if (____pile != null) ____pile.ContentsChanged -= DeckCountRefresh.Callback(__instance);
        // The native exit method releases its remaining completion listeners.
    }
}

// Beta moved the native completion-listener cleanup from _ExitTree to the
// node's predelete notification. Match that lifecycle without touching parents.
[HarmonyPatch]
internal static class DeckCountPredeletePatch
{
    private static readonly MethodInfo Notification = AccessTools.DeclaredMethod(
        typeof(NTopBarDeckButton), nameof(NTopBarDeckButton._Notification), new[] { typeof(int) });
    private static bool Prepare() => Notification != null;
    private static MethodBase TargetMethod() => Notification;

    private static void Prefix(NTopBarDeckButton __instance, CardPile ____pile, int what)
    {
        if (what == GodotObject.NotificationPredelete && ____pile != null)
            ____pile.ContentsChanged -= DeckCountRefresh.Callback(__instance);
    }
}
