using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

/// <summary>
/// AttackContext's custom hit loops bypass ModifyAttackHitCount. Keep their raw
/// damage segments and resolve the extra hits before AfterAttack consumes Vigor.
/// </summary>
internal static class LegendAttackContextSupport
{
    private static readonly FieldInfo CommandField =
        AccessTools.Field(typeof(AttackContext), "_attackCommand");
    private static readonly ConditionalWeakTable<AttackContext, State> Contexts = new();
    private static readonly ConditionalWeakTable<AttackCommand, State> Commands = new();
    private static readonly ConditionalWeakTable<Creature, List<State>> Actors = new();

    internal sealed class State
    {
        internal AttackCommand Command;
        internal int VirtualExtraHits;
        internal readonly List<Segment> Segments = new();
    }

    internal sealed record Segment(PlayerChoiceContext Context, Creature[] Targets,
        decimal Damage, ValueProp Props, Creature Dealer, CardModel Card);

    internal static async Task<AttackContext> Track(Task<AttackContext> task)
    {
        AttackContext context = await task;
        if (CommandField?.GetValue(context) is not AttackCommand command || command.Attacker == null)
            return context;
        int virtualExtraHits = HbrComboHelper.NativeVirtualExtraHits(command);
        if (virtualExtraHits <= 0 && !command.Attacker.Powers.OfType<MyLegendNextAttackBasePower>()
                .Any(power => power.GetReservedApplications(command) > 0)) return context;

        State state = new() { Command = command, VirtualExtraHits = virtualExtraHits };
        Contexts.Add(context, state);
        Commands.Add(command, state);
        Actors.GetOrCreateValue(command.Attacker).Add(state);
        return context;
    }

    internal static State Find(Creature dealer, CardModel card, ValueProp props)
    {
        if (dealer == null || card == null || !props.HasFlag(ValueProp.Move)) return null;
        if (Actors.TryGetValue(dealer, out List<State> states))
            for (int index = states.Count - 1; index >= 0; index--)
                if (ReferenceEquals(states[index].Command.ModelSource, card)) return states[index];
        foreach (MyLegendNextAttackBasePower power in dealer.Powers.OfType<MyLegendNextAttackBasePower>())
        {
            AttackCommand command = power.ReservedAttack;
            if (command != null && ReferenceEquals(command.ModelSource, card) &&
                Commands.TryGetValue(command, out State state)) return state;
        }

        return null;
    }

    internal static async Task<IEnumerable<DamageResult>> Record(
        Task<IEnumerable<DamageResult>> original, State state, Segment segment)
    {
        IEnumerable<DamageResult> results = await original;
        if (Commands.TryGetValue(state.Command, out State active) && ReferenceEquals(state, active))
            state.Segments.Add(segment);
        return results;
    }

    internal static bool TryComplete(AttackContext context, out ValueTask result)
    {
        result = default;
        if (!Contexts.TryGetValue(context, out State state)) return false;

        // Unregister before replay and re-entering DisposeAsync, so no recursive
        // recording/disposal or extra BeforeAttack notification can occur.
        Contexts.Remove(context);
        Commands.Remove(state.Command);
        if (Actors.TryGetValue(state.Command.Attacker, out List<State> states)) states.Remove(state);
        result = new ValueTask(ReplayAndDispose(context, state));
        return true;
    }

    private static async Task ReplayAndDispose(AttackContext context, State state)
    {
        try
        {
            if (state.Segments.Count == 0 || state.Command.Attacker?.IsAlive != true) return;
            List<Segment> expanded = new(state.Segments);
            if (state.VirtualExtraHits > 0)
            {
                Segment last = expanded.LastOrDefault(segment => !segment.Props.HasFlag(ValueProp.Unpowered));
                if (last != null)
                    for (int index = 0; index < state.VirtualExtraHits; index++) expanded.Add(last);
            }
            foreach (MyLegendNextAttackBasePower power in state.Command.Attacker.Powers
                         .OfType<MyLegendNextAttackBasePower>()
                         .OrderBy(power => power.DoublesHitCount)
                         .ThenBy(power => power is HbrComboPower ? 1 : 0))
            {
                int applications = power.GetReservedApplications(state.Command);
                if (applications <= 0) continue;
                Segment[] eligible = power is HbrComboPower
                    ? expanded.Where(segment => !segment.Props.HasFlag(ValueProp.Unpowered)).ToArray()
                    : expanded.ToArray();
                if (eligible.Length == 0) continue;
                int extra = power.TransformHitCount(eligible.Length, applications) - eligible.Length;
                Segment[] source = power.DoublesHitCount
                    ? eligible : new[] { eligible[^1] };
                for (int index = 0; index < extra; index++)
                    expanded.Add(source[index % source.Length]);
            }

            foreach (Segment segment in expanded.Skip(state.Segments.Count))
            {
                if (!segment.Dealer.IsAlive) break;
                Creature[] livingTargets = segment.Targets.Where(target => target.IsAlive).ToArray();
                if (livingTargets.Length == 0) continue;
                IEnumerable<DamageResult> results = await CreatureCommandCompatibility.Damage(
                    segment.Context, livingTargets, segment.Damage, segment.Props, segment.Dealer, segment.Card);
                context.AddHit(results);
            }
        }
        finally
        {
            await context.DisposeAsync();
        }
    }
}

[HarmonyPatch(typeof(AttackContext), nameof(AttackContext.CreateAsync))]
internal static class LegendAttackContextCreatePatch
{
    private static void Postfix(ref Task<AttackContext> __result) =>
        __result = LegendAttackContextSupport.Track(__result);
}

[HarmonyPatch(typeof(AttackContext), nameof(AttackContext.DisposeAsync))]
internal static class LegendAttackContextDisposePatch
{
    private static bool Prefix(AttackContext __instance, ref ValueTask __result) =>
        !LegendAttackContextSupport.TryComplete(__instance, out __result);
}

[HarmonyPatch]
internal static class LegendAttackContextDamagePatch
{
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;

    private static void Prefix(PlayerChoiceContext choiceContext, object[] __args,
        decimal amount, ValueProp props, Creature dealer, CardModel cardSource,
        out (LegendAttackContextSupport.State State, LegendAttackContextSupport.Segment Segment) __state)
    {
        LegendAttackContextSupport.State state = LegendAttackContextSupport.Find(dealer, cardSource, props);
        __state = state == null ? default : (state,
            new LegendAttackContextSupport.Segment(choiceContext,
                NativeDamageApiCompatibility.ReadTargets(__args).ToArray(), amount, props, dealer, cardSource));
    }

    private static void Postfix(
        (LegendAttackContextSupport.State State, LegendAttackContextSupport.Segment Segment) __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        if (__state.State != null)
            __result = LegendAttackContextSupport.Record(__result, __state.State, __state.Segment);
    }
}
