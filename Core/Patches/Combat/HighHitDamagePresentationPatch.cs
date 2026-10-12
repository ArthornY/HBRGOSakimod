using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Dropping the per-hit timer alone leaves all its sprites, particles and card
// flights alive in the same frame. Limit only their presentation; the native
// damage commands, plays, pile changes and combat hooks still run individually.
internal static class HighHitDamagePresentation
{
    private sealed class PlayedEffects
    {
        internal readonly HashSet<(object Target, string Effect)> Played = new();
    }

    private static readonly ConditionalWeakTable<object, PlayedEffects> Effects = new();

    internal static bool CanPlay(object target, string effect)
    {
        object batch = HighHitDamageVisualHelper.CurrentFastPresentationBatch;
        if (batch == null) return true;
        PlayedEffects effects = Effects.GetValue(batch, static _ => new PlayedEffects());
        lock (effects.Played) return effects.Played.Add((target, effect));
    }

    internal static bool SkipGeneratedCardVisuals(CardModel card) =>
        HighHitDamageVisualHelper.ShouldSkipPresentationWait() &&
        card?.Pile?.Type != PileType.Hand;

    internal static bool SkipTask(ref Task result)
    {
        if (!HighHitDamageVisualHelper.ShouldSkipPresentationWait()) return true;
        result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayVfx))]
internal static class HighHitDamageVfxPatch
{
    private static bool Prefix(Vector2 position, string path, Control vfxContainer) =>
        HighHitDamagePresentation.CanPlay(vfxContainer, $"vfx:{path}:{position}");
}

[HarmonyPatch(typeof(NHitSparkVfx), nameof(NHitSparkVfx.Create))]
internal static class HighHitDamageSparkPatch
{
    private static bool Prefix(Creature target, ref NHitSparkVfx __result)
    {
        if (HighHitDamagePresentation.CanPlay(target, "hit-spark")) return true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(NBlockSparkVfx), nameof(NBlockSparkVfx.Create))]
internal static class HighHitDamageBlockSparkPatch
{
    private static bool Prefix(Creature target, ref NBlockSparkVfx __result)
    {
        if (HighHitDamagePresentation.CanPlay(target, "block-spark")) return true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim))]
internal static class HighHitDamageAnimationPatch
{
    private static bool Prefix(Creature creature, string triggerName, ref Task __result)
    {
        // Death, revival and boss phase animations must retain their native
        // behavior even when a high-hit attack caused the transition.
        if (triggerName is not ("Attack" or "Hit") ||
            HighHitDamagePresentation.CanPlay(creature, $"animation:{triggerName}")) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame.ScreenShake))]
internal static class HighHitDamageScreenShakePatch
{
    private static bool Prefix(NGame __instance) =>
        HighHitDamagePresentation.CanPlay(__instance, "screen-shake");
}

[HarmonyPatch(typeof(NGame), nameof(NGame.DoHitStop))]
internal static class HighHitDamageHitStopPatch
{
    private static bool Prefix(NGame __instance) =>
        HighHitDamagePresentation.CanPlay(__instance, "hit-stop");
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
internal static class HighHitDamageAutoplayPresentationPatch
{
    private static void Prefix(CardModel card, ref bool skipCardPileVisuals)
    {
        if (HighHitDamagePresentation.SkipGeneratedCardVisuals(card))
            skipCardPileVisuals = true;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class HighHitDamageCardPlayPresentationPatch
{
    // This runs after CardPlayContextPatch opens the scope, but before the
    // mode-switch patch captures arguments for its reverse-patched wrapper.
    [HarmonyPriority(Priority.VeryHigh)]
    private static void Prefix(CardModel __instance, bool isAutoPlay, ref bool skipCardPileVisuals)
    {
        if (isAutoPlay && HighHitDamagePresentation.SkipGeneratedCardVisuals(__instance))
            skipCardPileVisuals = true;
    }
}

[HarmonyPatch(typeof(CardModel), "PlayPowerCardFlyVfx")]
internal static class HighHitDamagePowerFlightPatch
{
    // Native PlayPowerCardFlyVfx creates another NCard when one does not exist,
    // even when the caller already requested skipCardPileVisuals.
    private static bool Prefix(ref Task __result) =>
        HighHitDamagePresentation.SkipTask(ref __result);
}

[HarmonyPatch(typeof(NCard), nameof(NCard.AnimMultiCardPlay))]
internal static class HighHitDamageReplayAnimationPatch
{
    private static bool Prefix(ref Task __result) =>
        HighHitDamagePresentation.SkipTask(ref __result);
}

[HarmonyPatch]
internal static class HighHitDamageGeneratedPlayPilePatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CardPileCmd))
        .Single(method => method.Name == nameof(CardPileCmd.Add) &&
            method.GetParameters() is { Length: >= 5 } args &&
            args[0].ParameterType == typeof(IEnumerable<CardModel>) &&
            args[1].ParameterType == typeof(CardPile) &&
            args.Any(argument => argument.Name == "skipVisuals" && argument.ParameterType == typeof(bool)));

    private static void Prefix(IEnumerable<CardModel> cards, CardPile newPile,
        ref bool skipVisuals, out bool __state)
    {
        bool fast = HighHitDamageVisualHelper.ShouldSkipPresentationWait();
        if (fast && newPile?.Type == PileType.Hand)
            skipVisuals = false; // Real drawn/returned hand cards still need their nodes.
        else if (fast && newPile?.Type == PileType.Play &&
            cards.All(card => card?.Pile?.Type != PileType.Hand))
            skipVisuals = true;
        __state = fast && skipVisuals && newPile?.IsCombatPile == true &&
            newPile.Type != PileType.Hand;
    }

    private static void Postfix(bool __state, ref Task<IReadOnlyList<CardPileAddResult>> __result)
    {
        if (__state) __result = Finish(__result);
    }

    private static async Task<IReadOnlyList<CardPileAddResult>> Finish(
        Task<IReadOnlyList<CardPileAddResult>> original)
    {
        IReadOnlyList<CardPileAddResult> results = await original;
        // With no flight, there is no NCardFlyVfx callback to refresh counters.
        foreach (CardPileAddResult result in results)
            if (result.success)
            {
                result.oldPile?.InvokeCardRemoveFinished();
                result.cardAdded?.Pile?.InvokeCardAddFinished();
            }
        return results;
    }
}
