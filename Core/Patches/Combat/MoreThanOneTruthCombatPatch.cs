using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class MoreThanOneTruthPlayScopePatch
{
    private static void Prefix(CardModel __instance, bool isAutoPlay, ResourceInfo resources,
        out MoreThanOneTruthCombatHelper.PlayScope __state) =>
        __state = MoreThanOneTruthCombatHelper.BeginPlay(__instance, isAutoPlay, resources);
    private static void Postfix(ref Task __result, MoreThanOneTruthCombatHelper.PlayScope __state)
    {
        try { __result = MoreThanOneTruthCombatHelper.FinishPlay(__result, __state); }
        finally { MoreThanOneTruthCombatHelper.RestoreCaller(__state); }
    }
    private static Exception Finalizer(Exception __exception, MoreThanOneTruthCombatHelper.PlayScope __state)
    {
        if (__exception != null) MoreThanOneTruthCombatHelper.ClosePlay(__state);
        else MoreThanOneTruthCombatHelper.RestoreCaller(__state);
        return __exception;
    }
}

internal static class MoreThanOneTruthEffectPatching
{
    internal static MethodBase MoveNext(Type type, string method) => AccessTools.Method(
        AccessTools.Method(type, method).GetCustomAttribute<AsyncStateMachineAttribute>().StateMachineType, "MoveNext");
    internal static IEnumerable<CodeInstruction> ReplaceEffect(IEnumerable<CodeInstruction> instructions,
        MethodInfo original, string helper)
    {
        MethodInfo replacement = AccessTools.Method(typeof(MoreThanOneTruthCombatHelper), helper);
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(original)) { yield return instruction; continue; }
            changed++;
            yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = replacement };
        }
        if (changed != 1) throw new InvalidOperationException("Expected one native active-effect invocation.");
    }
}
[HarmonyPatch]
internal static class MoreThanOneTruthActiveCardEffectPatch
{
    // Ritsu already redirects the native OnPlay call to its dispatcher before
    // this assembly's PatchAll. Wrap only that dispatcher's original core call,
    // so framework callbacks stay intact and passive energy stays excluded.
    private static MethodBase TargetMethod() => MoreThanOneTruthEffectPatching.MoveNext(
        typeof(CardOnPlayHook), nameof(CardOnPlayHook.RunCardOnPlayHooks));
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        MoreThanOneTruthEffectPatching.ReplaceEffect(instructions,
            AccessTools.Method(typeof(CardOnPlayHook).GetNestedType("CardOnPlayDelegate", BindingFlags.NonPublic), "Invoke"),
            nameof(MoreThanOneTruthCombatHelper.InvokeActiveRitsuCardEffect));
}
[HarmonyPatch]
internal static class MoreThanOneTruthActivePotionEffectPatch
{
    private static MethodBase TargetMethod() => MoreThanOneTruthEffectPatching.MoveNext(typeof(PotionModel), nameof(PotionModel.OnUseWrapper));
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        MoreThanOneTruthEffectPatching.ReplaceEffect(instructions,
            AccessTools.Method(typeof(PotionModel), "OnUse", new[] { typeof(PlayerChoiceContext), typeof(Creature) }),
            nameof(MoreThanOneTruthCombatHelper.InvokeActivePotionEffect));
}
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyGain))]
internal static class MoreThanOneTruthEnergyGainPatch
{
    private static void Postfix(MegaCrit.Sts2.Core.Entities.Players.Player __1, ref decimal __result) =>
        __result = MoreThanOneTruthCombatHelper.ModifyActiveEnergyGain(__1, __result);
}
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyAttackHitCount))]
internal static class MoreThanOneTruthHitCountPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(AttackCommand __1, ref decimal __result) =>
        __result = MoreThanOneTruthCombatHelper.ModifyHitCount(__1, __result);
}
[HarmonyPatch(typeof(AttackContext), nameof(AttackContext.CreateAsync))]
internal static class MoreThanOneTruthContextCreatePatch
{
    private static void Postfix(ref Task<AttackContext> __result) =>
        __result = MoreThanOneTruthCombatHelper.TrackContext(__result);
}
[HarmonyPatch(typeof(AttackContext), nameof(AttackContext.DisposeAsync))]
internal static class MoreThanOneTruthContextDisposePatch
{
    // Existing Legend context replay runs first; fee hits follow its final
    // segment count, matching the postfix on the native hit-count hook.
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(AttackContext __instance, ref ValueTask __result) =>
        !MoreThanOneTruthCombatHelper.TryCompleteContext(__instance, out __result);
}
[HarmonyPatch]
internal static class MoreThanOneTruthContextDamagePatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Where(method => method.Name == nameof(CreatureCmd.Damage) && method.GetParameters() is var p &&
            p.Length is 6 or 7 && p[1].ParameterType == typeof(IEnumerable<Creature>) &&
            p[2].ParameterType == typeof(decimal) && p[5].ParameterType == typeof(CardModel))
        .OrderByDescending(method => method.GetParameters().Length).First();
    private static void Prefix(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets,
        decimal amount, ValueProp props, Creature dealer, CardModel cardSource,
        out (MoreThanOneTruthCombatHelper.ContextState State, MoreThanOneTruthCombatHelper.Segment Segment) __state)
    {
        MoreThanOneTruthCombatHelper.ContextState state = MoreThanOneTruthCombatHelper.FindContext(dealer, cardSource, props);
        __state = state == null ? default : (state, new(choiceContext, targets.ToArray(), amount, props, dealer, cardSource));
    }
    private static void Postfix(ref Task<IEnumerable<DamageResult>> __result,
        (MoreThanOneTruthCombatHelper.ContextState State, MoreThanOneTruthCombatHelper.Segment Segment) __state)
    {
        if (__state.State != null)
            __result = MoreThanOneTruthCombatHelper.RecordContextHit(__result, __state.State, __state.Segment);
    }
}
