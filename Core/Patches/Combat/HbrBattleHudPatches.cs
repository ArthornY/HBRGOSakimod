using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

// The HBR battle HUD (ui-concepts/battle-hud-practical-20261104). Only the look and the slots move:
// every native state machine, input path, hotkey and hover tip still runs, and vanilla characters,
// whose HUD is never attached, are untouched.

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Activate))]
internal static class HbrBattleHudActivatePatch
{
    private static void Postfix(NCombatUi __instance, CombatState state)
    {
        Player me = LocalContext.GetMe(state);
        if (HbrBattleHudArt.UsesHud(me)) NHbrBattleHud.Attach(__instance, state, me);
    }
}

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.AnimOut))]
internal static class HbrBattleHudAnimOutPatch
{
    private static void Postfix(NCombatUi __instance) => NHbrBattleHud.Of(__instance)?.AnimOut();
}

[HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton.Initialize))]
internal static class HbrEndTurnDiscAttachPatch
{
    // Positional, so a renamed parameter in another game version still binds.
    private static void Postfix(NEndTurnButton __instance, CombatState __0)
    {
        if (HbrBattleHudArt.UsesHud(LocalContext.GetMe(__0))) NHbrEndTurnDisc.Attach(__instance);
    }
}

// The disc hangs from the bottom-right corner instead of the native 1604/846 ratio of the screen.
[HarmonyPatch(typeof(NEndTurnButton), "ShowPos", MethodType.Getter)]
internal static class HbrEndTurnDiscShowPosPatch
{
    private static void Postfix(NEndTurnButton __instance, ref Vector2 __result)
    {
        if (NHbrEndTurnDisc.Of(__instance) != null)
            __result = NHbrEndTurnDisc.ShowPosition(__instance.GetViewport().GetVisibleRect().Size);
    }
}

[HarmonyPatch(typeof(NEndTurnButton), "HidePos", MethodType.Getter)]
internal static class HbrEndTurnDiscHidePosPatch
{
    private static void Postfix(NEndTurnButton __instance, ref Vector2 __result)
    {
        if (NHbrEndTurnDisc.Of(__instance) != null)
            __result = NHbrEndTurnDisc.HidePosition(__instance.GetViewport().GetVisibleRect().Size);
    }
}

[HarmonyPatch(typeof(NEndTurnButton), "AnimIn")]
internal static class HbrEndTurnDiscAnimInPatch
{
    private static void Prefix(NEndTurnButton __instance) => NHbrEndTurnDisc.Of(__instance)?.PrepareShow();

    private static void Postfix(NEndTurnButton __instance) => NHbrEndTurnDisc.Of(__instance)?.SlideIn();
}

[HarmonyPatch(typeof(NEndTurnButton), "AnimOut")]
internal static class HbrEndTurnDiscAnimOutPatch
{
    private static void Postfix(NEndTurnButton __instance) => NHbrEndTurnDisc.Of(__instance)?.SlideOut();
}

// Every native change of enablement, shine, turn or votes re-reads the button's state.
[HarmonyPatch]
internal static class HbrEndTurnDiscRefreshPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[]
                 {
                     "OnEnable", "OnDisable", "StartOrStopPulseVfx", "AfterPlayerEndedTurn",
                     "AfterPlayerUnendedTurn", "OnTurnStarted"
                 })
        {
            MethodInfo method = AccessTools.Method(typeof(NEndTurnButton), name);
            if (method != null) yield return method;
        }
    }

    private static void Postfix(NEndTurnButton __instance) => NHbrEndTurnDisc.Of(__instance)?.Refresh();
}

[HarmonyPatch(typeof(NEndTurnButton), "OnFocus")]
internal static class HbrEndTurnDiscFocusPatch
{
    private static void Postfix(NEndTurnButton __instance) => NHbrEndTurnDisc.Of(__instance)?.SetHovered(true);
}

[HarmonyPatch(typeof(NEndTurnButton), "OnUnfocus")]
internal static class HbrEndTurnDiscUnfocusPatch
{
    private static void Postfix(NEndTurnButton __instance) => NHbrEndTurnDisc.Of(__instance)?.SetHovered(false);
}

[HarmonyPatch(typeof(NEndTurnButton), "OnPress")]
internal static class HbrEndTurnDiscPressPatch
{
    private static void Postfix(NEndTurnButton __instance) => NHbrEndTurnDisc.Of(__instance)?.SetPressed(true);
}

[HarmonyPatch(typeof(NEndTurnButton), "OnRelease")]
internal static class HbrEndTurnDiscReleasePatch
{
    private static void Postfix(NEndTurnButton __instance) => NHbrEndTurnDisc.Of(__instance)?.SetPressed(false);
}

// Piles are styled for the player whose cards they count, also inside card selection screens.
[HarmonyPatch(typeof(NCombatCardPile), nameof(NCombatCardPile.Initialize))]
internal static class HbrPileAttachPatch
{
    private static void Postfix(NCombatCardPile __instance, Player __0)
    {
        if (HbrBattleHudArt.UsesHud(__0)) NHbrPileButtonVisuals.Attach(__instance);
    }
}

[HarmonyPatch(typeof(NCombatCardPile), "AddCard")]
internal static class HbrPileAddCardPatch
{
    private static void Postfix(NCombatCardPile __instance) => NHbrPileButtonVisuals.Of(__instance)?.SyncCount(bump: true);
}

[HarmonyPatch(typeof(NCombatCardPile), "RemoveCard")]
internal static class HbrPileRemoveCardPatch
{
    private static void Postfix(NCombatCardPile __instance) => NHbrPileButtonVisuals.Of(__instance)?.SyncCount(bump: false);
}

[HarmonyPatch(typeof(NCombatCardPile), "OnFocus")]
internal static class HbrPileFocusPatch
{
    private static void Postfix(NCombatCardPile __instance) => NHbrPileButtonVisuals.Of(__instance)?.SetHovered(true);
}

[HarmonyPatch(typeof(NCombatCardPile), "OnUnfocus")]
internal static class HbrPileUnfocusPatch
{
    private static void Postfix(NCombatCardPile __instance) => NHbrPileButtonVisuals.Of(__instance)?.SetHovered(false);
}

[HarmonyPatch(typeof(NCombatCardPile), "OnPress")]
internal static class HbrPilePressPatch
{
    private static void Postfix(NCombatCardPile __instance) => NHbrPileButtonVisuals.Of(__instance)?.SetPressed(true);
}

[HarmonyPatch(typeof(NCombatCardPile), "OnRelease")]
internal static class HbrPileReleasePatch
{
    private static void Postfix(NCombatCardPile __instance) => NHbrPileButtonVisuals.Of(__instance)?.SetPressed(false);
}

[HarmonyPatch(typeof(NCombatCardPile), nameof(NCombatCardPile.AnimIn))]
internal static class HbrPileAnimInPatch
{
    private static void Postfix(NCombatCardPile __instance) => NHbrPileButtonVisuals.Of(__instance)?.FadeIn();
}

[HarmonyPatch(typeof(NCombatCardPile), nameof(NCombatCardPile.AnimOut))]
internal static class HbrPileAnimOutPatch
{
    private static void Postfix(NCombatCardPile __instance) => NHbrPileButtonVisuals.Of(__instance)?.FadeOut();
}

// The exhaust pile recomputes its slots on every window resize; keep its short HBR slide.
[HarmonyPatch(typeof(NExhaustPileButton), "SetAnimInOutPositions")]
internal static class HbrExhaustPileSlotsPatch
{
    private static void Postfix(NExhaustPileButton __instance) => NHbrPileButtonVisuals.Of(__instance)?.FixHidePosition();
}

[HarmonyPatch(typeof(NPlayerTurnBanner), nameof(NPlayerTurnBanner._Ready))]
internal static class HbrPlayerTurnBannerPatch
{
    private static void Postfix(NPlayerTurnBanner __instance)
    {
        if (NHbrBattleHud.Current is not { } hud) return;
        (int turn, int extra) = hud.RefreshTurn(bump: true);
        if (extra > 0) NHbrTurnBanner.Attach(__instance, HbrTurnBannerKind.Extra, extra);
        else NHbrTurnBanner.Attach(__instance, HbrTurnBannerKind.Player, turn);
    }
}

[HarmonyPatch(typeof(NEnemyTurnBanner), nameof(NEnemyTurnBanner._Ready))]
internal static class HbrEnemyTurnBannerPatch
{
    private static void Postfix(NEnemyTurnBanner __instance)
    {
        if (NHbrBattleHud.Current != null) NHbrTurnBanner.Attach(__instance, HbrTurnBannerKind.Enemy, 0);
    }
}
