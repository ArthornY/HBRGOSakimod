using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Stable hands the relic a DamageResult. Beta moved it to AfterBlockBroken,
// so preserve that same per-result evidence around only the native callback.
internal static class HbrHandDrillBlockReceiptContext
{
    internal sealed class Frame
    {
        internal Frame Previous;
        internal Creature Target;
        internal bool Active = true;
        internal bool Claimed;
        internal bool Known;
        internal bool Broken;
    }
    private static readonly AsyncLocal<Frame> Pending = new();
    private static readonly AsyncLocal<Frame> Current = new();

    internal static Frame BeginIntent(Creature target, DamageResult result)
    {
        bool known = HpOnlyDamageContext.TryGetNativeBlockBreak(result, out bool broken);
        Frame frame = new() { Previous = Pending.Value, Target = target, Known = known, Broken = broken };
        Pending.Value = frame;
        return frame;
    }
    internal static void EndIntent(Frame frame)
    {
        frame.Active = false;
        if (ReferenceEquals(Pending.Value, frame)) Pending.Value = frame.Previous;
    }
    internal static Frame EnterCallback(object[] arguments)
    {
        Frame intent = Pending.Value;
        Frame previous = Current.Value;
        if (intent?.Active != true && previous?.Active != true) return null;
        Creature target = arguments.OfType<Creature>().FirstOrDefault();
        bool claimed = intent?.Active == true && !intent.Claimed && ReferenceEquals(intent.Target, target);
        if (claimed) intent.Claimed = true;
        Frame frame = new()
        {
            Previous = previous, Target = target,
            Known = claimed && intent.Known, Broken = claimed && intent.Broken
        };
        Current.Value = frame;
        return frame;
    }
    internal static void Restore(Frame frame)
    {
        if (frame != null && ReferenceEquals(Current.Value, frame)) Current.Value = frame.Previous;
    }
    internal static void Close(Frame frame)
    {
        if (frame == null) return;
        frame.Active = false;
        Restore(frame);
    }
    internal static bool IsProvenNotBroken(Creature target) => Current.Value is { Active: true, Known: true, Broken: false } frame &&
        ReferenceEquals(frame.Target, target);
}

[HarmonyPatch]
internal static class HbrHandDrillDamageNotificationPatch
{
    private delegate Task NativeNotification(ICombatState combat, PlayerChoiceContext context, Creature target, Creature breaker);
    private static NativeNotification _native;

    private static bool Prepare() => AccessTools.DeclaredMethod(typeof(HandDrill), "AfterBlockBroken") != null;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Optional beta API. Stable's relic already receives its own result.
        if (AccessTools.DeclaredMethod(typeof(HandDrill), "AfterBlockBroken") != null)
            yield return NativeDamageApiCompatibility.MoveNext;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        MethodInfo original = AccessTools.Method(typeof(Hook), nameof(Hook.AfterBlockBroken),
            new[] { typeof(ICombatState), typeof(PlayerChoiceContext), typeof(Creature), typeof(Creature) });
        if (original == null) throw new MissingMethodException("Expected beta AfterBlockBroken notification.");
        _native = original.CreateDelegate<NativeNotification>();
        FieldInfo resultField = __originalMethod.DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(DamageResult) && field.Name.StartsWith("<unblockedDamageResult>", StringComparison.Ordinal));
        MethodInfo adapter = AccessTools.Method(typeof(HbrHandDrillDamageNotificationPatch), nameof(Notify));
        int matches = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(original)) { yield return instruction; continue; }
            CodeInstruction load = new(OpCodes.Ldarg_0);
            load.labels.AddRange(instruction.labels);
            load.blocks.AddRange(instruction.blocks);
            yield return load;
            yield return new CodeInstruction(OpCodes.Ldfld, resultField);
            yield return new CodeInstruction(OpCodes.Call, adapter);
            matches++;
        }
        if (matches != 1) throw new InvalidOperationException("Expected one damage-result block-break notification.");
    }

    private static async Task Notify(ICombatState combat, PlayerChoiceContext context, Creature target,
        Creature breaker, DamageResult result)
    {
        var intent = HbrHandDrillBlockReceiptContext.BeginIntent(target, result);
        try { await _native(combat, context, target, breaker); }
        finally { HbrHandDrillBlockReceiptContext.EndIntent(intent); }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockBroken))]
internal static class HbrHandDrillCallbackScopePatch
{
    private static void Prefix(object[] __args, out HbrHandDrillBlockReceiptContext.Frame __state) =>
        __state = HbrHandDrillBlockReceiptContext.EnterCallback(__args);
    private static void Postfix(HbrHandDrillBlockReceiptContext.Frame __state, ref Task __result)
    {
        if (__state == null) return;
        try { __result = Finish(__result, __state); }
        finally { HbrHandDrillBlockReceiptContext.Restore(__state); }
    }
    private static async Task Finish(Task original, HbrHandDrillBlockReceiptContext.Frame frame)
    {
        try { await original; }
        finally { HbrHandDrillBlockReceiptContext.Close(frame); }
    }
    private static Exception Finalizer(Exception __exception, HbrHandDrillBlockReceiptContext.Frame __state)
    {
        if (__exception != null) HbrHandDrillBlockReceiptContext.Close(__state);
        else HbrHandDrillBlockReceiptContext.Restore(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class HbrHandDrillTrueBlockPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(typeof(HandDrill), "AfterBlockBroken") ??
        AccessTools.DeclaredMethod(typeof(HandDrill), "AfterDamageGiven");
    private static bool Prefix(object[] __args, ref Task __result)
    {
        DamageResult result = __args.OfType<DamageResult>().FirstOrDefault();
        bool reject = result != null
            ? HpOnlyDamageContext.TryGetNativeBlockBreak(result, out bool broken) && !broken
            : HbrHandDrillBlockReceiptContext.IsProvenNotBroken(__args.OfType<Creature>().FirstOrDefault());
        if (!reject) return true;
        __result = Task.CompletedTask;
        return false;
    }
}
