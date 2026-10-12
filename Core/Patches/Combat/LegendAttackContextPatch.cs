using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
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

    internal sealed class State
    {
        internal AttackCommand Command;
        internal readonly List<Segment> Segments = new();
    }

    internal sealed record Segment(PlayerChoiceContext Context, Creature[] Targets,
        decimal Damage, ValueProp Props, Creature Dealer, CardModel Card);

    internal static async Task<AttackContext> Track(Task<AttackContext> task)
    {
        AttackContext context = await task;
        if (CommandField?.GetValue(context) is not AttackCommand command ||
            command.Attacker?.Powers.OfType<MyLegendNextAttackBasePower>()
                .Any(power => power.GetReservedApplications(command) > 0) != true)
            return context;

        State state = new() { Command = command };
        Contexts.Add(context, state);
        Commands.Add(command, state);
        return context;
    }

    internal static State Find(Creature dealer, CardModel card, ValueProp props)
    {
        if (dealer == null || card == null || !props.HasFlag(ValueProp.Move)) return null;
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
        result = new ValueTask(ReplayAndDispose(context, state));
        return true;
    }

    private static async Task ReplayAndDispose(AttackContext context, State state)
    {
        try
        {
            if (state.Segments.Count == 0 || state.Command.Attacker?.IsAlive != true) return;
            List<Segment> expanded = new(state.Segments);
            foreach (MyLegendNextAttackBasePower power in state.Command.Attacker.Powers
                         .OfType<MyLegendNextAttackBasePower>()
                         .OrderBy(power => power is MyLegendNextAttackDoublePower ? 1 : 0))
            {
                int applications = power.GetReservedApplications(state.Command);
                int desired = power.TransformHitCount(expanded.Count, applications);
                Segment[] source = power is MyLegendNextAttackDoublePower
                    ? expanded.ToArray() : new[] { expanded[^1] };
                for (int index = 0; expanded.Count < desired; index++)
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
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Where(method => method.Name == nameof(CreatureCmd.Damage))
        .Where(method =>
        {
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length is 6 or 7 &&
                   parameters[0].ParameterType == typeof(PlayerChoiceContext) &&
                   parameters[1].ParameterType == typeof(IEnumerable<Creature>) &&
                   parameters[2].ParameterType == typeof(decimal) &&
                   parameters[3].ParameterType == typeof(ValueProp) &&
                   parameters[4].ParameterType == typeof(Creature) &&
                   parameters[5].ParameterType == typeof(CardModel);
        })
        .OrderBy(method => method.GetParameters().Length).FirstOrDefault() ??
        throw new MissingMethodException(typeof(CreatureCmd).FullName, nameof(CreatureCmd.Damage));

    private static void Prefix(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets,
        decimal amount, ValueProp props, Creature dealer, CardModel cardSource,
        out (LegendAttackContextSupport.State State, LegendAttackContextSupport.Segment Segment) __state)
    {
        LegendAttackContextSupport.State state = LegendAttackContextSupport.Find(dealer, cardSource, props);
        __state = state == null ? default : (state,
            new LegendAttackContextSupport.Segment(choiceContext, targets.ToArray(), amount, props, dealer, cardSource));
    }

    private static void Postfix(
        (LegendAttackContextSupport.State State, LegendAttackContextSupport.Segment Segment) __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        if (__state.State != null)
            __result = LegendAttackContextSupport.Record(__result, __state.State, __state.Segment);
    }
}
