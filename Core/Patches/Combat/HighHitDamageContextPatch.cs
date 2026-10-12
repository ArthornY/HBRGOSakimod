using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands.Builders;

namespace HBRGOSakiMod.Core.Patches.Combat;

// AttackContext groups direct damage calls instead of running Execute. Attach
// its boundary to the existing card scope, which survives the caller's awaits.
internal static class HighHitDamageContextSupport
{
    private static readonly FieldInfo CommandField = AccessTools.Field(typeof(AttackContext), "_attackCommand");
    private static readonly ConditionalWeakTable<AttackContext, ContextEntry>
        Contexts = new();

    internal sealed class ContextEntry
    {
        internal HighHitDamageVisualHelper.DirectDamageBatchScope Scope;
        internal int PendingDisposals;
    }

    internal sealed class DisposeCall
    {
        internal AttackContext Context;
        internal ContextEntry Entry;
        internal int Released;
        internal bool Wrapped;
    }

    internal static async Task<AttackContext> Track(Task<AttackContext> original,
        HighHitDamageVisualHelper.CardBatchScope cardScope)
    {
        AttackContext context = await original;
        if (CommandField.GetValue(context) is not AttackCommand command) return context;
        HighHitDamageVisualHelper.DirectDamageBatchScope scope =
            HighHitDamageVisualHelper.RegisterAttackContext(cardScope, command.Attacker,
                command.ModelSource as MegaCrit.Sts2.Core.Models.CardModel, command.DamageProps);
        if (scope != null) Contexts.Add(context, new ContextEntry { Scope = scope });
        return context;
    }

    internal static DisposeCall BeginDispose(AttackContext context)
    {
        if (!Contexts.TryGetValue(context, out ContextEntry entry)) return null;
        Interlocked.Increment(ref entry.PendingDisposals);
        return new DisposeCall { Context = context, Entry = entry };
    }

    internal static ValueTask FinishDispose(DisposeCall call, ValueTask original)
    {
        if (call == null) return original;
        call.Wrapped = true;
        return new ValueTask(Finish(original, call));
    }

    private static async Task Finish(ValueTask original, DisposeCall call)
    {
        try { await original; }
        finally { Release(call); }
    }

    internal static void Release(DisposeCall call)
    {
        if (call == null || Interlocked.Exchange(ref call.Released, 1) != 0) return;
        if (Interlocked.Decrement(ref call.Entry.PendingDisposals) != 0) return;
        Contexts.Remove(call.Context);
        HighHitDamageVisualHelper.CloseDirectDamageBatch(call.Entry.Scope);
    }
}

[HarmonyPatch(typeof(AttackContext), nameof(AttackContext.CreateAsync))]
internal static class HighHitDamageContextCreatePatch
{
    private static void Postfix(ref Task<AttackContext> __result) =>
        __result = HighHitDamageContextSupport.Track(__result, HighHitDamageVisualHelper.CurrentCardBatch);
}

[HarmonyPatch(typeof(AttackContext), nameof(AttackContext.DisposeAsync))]
internal static class HighHitDamageContextDisposePatch
{
    // Register before the replay prefixes: they may dispose the same context
    // again before this invocation has even reached its postfix.
    [HarmonyPriority(Priority.First)]
    private static void Prefix(AttackContext __instance,
        out HighHitDamageContextSupport.DisposeCall __state) =>
        __state = HighHitDamageContextSupport.BeginDispose(__instance);

    // Wrap the final returned task after Legend/Truth prefixes have supplied
    // their replays. The re-entrant native disposal also remains idempotent.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(HighHitDamageContextSupport.DisposeCall __state, ref ValueTask __result) =>
        __result = HighHitDamageContextSupport.FinishDispose(__state, __result);

    private static Exception Finalizer(HighHitDamageContextSupport.DisposeCall __state, Exception __exception)
    {
        if (__exception != null && __state?.Wrapped != true) HighHitDamageContextSupport.Release(__state);
        return __exception;
    }
}
