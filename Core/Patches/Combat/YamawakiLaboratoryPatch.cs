using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Native CurrentTarget is assigned after SpendResources. Keep the authoritative
// PlayCardAction's target through validation/payment rather than mutating the
// card model or granting a discount because some unrelated enemy is broken.
internal static class YamawakiTargetCostContext
{
    internal sealed class Scope : IDisposable
    {
        internal CardModel Card;
        internal Creature Target;
        internal Scope Previous;
        internal bool Active = true;
        public void Dispose() { Active = false; RestoreCaller(this); }
    }
    private static readonly AsyncLocal<Scope> Current = new();
    internal static bool IsEligible(CardModel card) => card is WorldIsMyLaboratoryHbrgoSaki world &&
        world.IsMutable && world.Owner != null && world.HasUpgradeEffect(WorldIsMyLaboratoryHbrgoSaki.WillScythe);
    internal static bool HasTargetScope(CardModel card) => Current.Value is { Active: true } scope && ReferenceEquals(scope.Card, card);
    internal static Scope Begin(CardModel card, Creature target)
    {
        Scope scope = new() { Card = card, Target = target, Previous = Current.Value };
        Current.Value = scope; return scope;
    }
    internal static Creature Target(CardModel card) => HasTargetScope(card) ? Current.Value.Target : HbrCardDescriptionContext.GetTarget(card);
    internal static bool ShouldBeFree(CardModel card) => IsEligible(card) && Target(card) is { IsAlive: true } target &&
        target.Side != card.Owner.Creature.Side && target.GetPower<BreakPower>() != null;
    internal static void RestoreCaller(Scope scope)
    { if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous; }
    internal static async Task Finish(Task original, Scope scope)
    { try { await original; } finally { scope.Dispose(); } }
}

[HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.GetWithModifiers))]
internal static class YamawakiBreakCostPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardEnergyCost __instance, CardModel ____card, CostModifiers modifiers, ref int __result)
    {
        if (!__instance.CostsX && __result >= 0 && modifiers.HasFlag(CostModifiers.Global) &&
            YamawakiTargetCostContext.ShouldBeFree(____card)) __result = 0;
    }
}

[HarmonyPatch(typeof(PlayCardAction), "ExecuteAction")]
internal static class YamawakiPlayActionTargetPatch
{
    private static void Prefix(PlayCardAction __instance, out YamawakiTargetCostContext.Scope __state)
    {
        CardModel card = __instance.NetCombatCard.ToCardModelOrNull();
        __state = YamawakiTargetCostContext.IsEligible(card) ? YamawakiTargetCostContext.Begin(card, __instance.Target) : null;
    }
    private static void Postfix(YamawakiTargetCostContext.Scope __state, ref Task __result)
    {
        if (__state == null) return;
        __result = YamawakiTargetCostContext.Finish(__result, __state);
        YamawakiTargetCostContext.RestoreCaller(__state);
    }
    private static Exception Finalizer(Exception __exception, YamawakiTargetCostContext.Scope __state)
    { if (__exception != null) __state?.Dispose(); return __exception; }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlayTargeting))]
internal static class YamawakiCanPlayTargetPatch
{
    private static void Prefix(CardModel __instance, Creature target, out YamawakiTargetCostContext.Scope __state) =>
        __state = YamawakiTargetCostContext.IsEligible(__instance) && !YamawakiTargetCostContext.HasTargetScope(__instance)
            ? YamawakiTargetCostContext.Begin(__instance, target) : null;
    private static void Postfix(YamawakiTargetCostContext.Scope __state) => __state?.Dispose();
    private static Exception Finalizer(Exception __exception, YamawakiTargetCostContext.Scope __state)
    { __state?.Dispose(); return __exception; }
}

[HarmonyPatch]
internal static class YamawakiCanBeginTargetingPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(CardModel), nameof(CardModel.CanPlay),
        new[] { typeof(UnplayableReason).MakeByRefType(), typeof(AbstractModel).MakeByRefType() });
    private static void Prefix(CardModel __instance, out YamawakiTargetCostContext.Scope __state)
    {
        __state = null;
        if (!YamawakiTargetCostContext.IsEligible(__instance) || YamawakiTargetCostContext.HasTargetScope(__instance)) return;
        Creature selected = HbrCardDescriptionContext.GetTarget(__instance);
        // Header validation may offer targeting at zero energy, but it never
        // changes the displayed price or the eventual selected-target payment.
        selected ??= __instance.CombatState?.GetOpponentsOf(__instance.Owner.Creature)
            .FirstOrDefault(enemy => enemy.IsAlive && enemy.GetPower<BreakPower>() != null);
        if (selected != null) __state = YamawakiTargetCostContext.Begin(__instance, selected);
    }
    private static void Postfix(YamawakiTargetCostContext.Scope __state) => __state?.Dispose();
    private static Exception Finalizer(Exception __exception, YamawakiTargetCostContext.Scope __state)
    { __state?.Dispose(); return __exception; }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterShuffle))]
internal static class YamawakiShuffleReceiptPatch
{
    private static void Prefix(Player shuffler) => YamawakiLaboratoryCombatHelper.AfterShuffle(shuffler);
}
