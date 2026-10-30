using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>Shares the attack reservation with direct phantom and cannon hit loops.</summary>
internal static class HbrComboHelper
{
    private static readonly AsyncLocal<AttackScope> Current = new();

    internal sealed class AttackScope : IDisposable
    {
        internal readonly AttackScope Previous;
        private readonly Creature _attacker;
        internal readonly AttackCommand Command;
        private readonly HbrComboPower[] _powers;
        private bool _hadHit;
        private bool _completed;
        private bool _disposed;

        internal AttackScope(Creature attacker, CardModel card = null)
        {
            _attacker = attacker;
            Previous = Current.Value;
            Command = card == null ? new AttackCommand(0m).FromMonster(attacker.Monster)
                : new AttackCommand(0m).FromCardCompat(card);
            _powers = Powers(attacker).Where(power => power.ReservedAttack == null).ToArray();
            foreach (HbrComboPower power in _powers) power.ReserveForAttack(Command);
            Current.Value = this;
        }

        internal int HitCount(int baseHits)
        {
            foreach (HbrComboPower power in _powers)
                baseHits = power.TransformHitCount(baseHits, power.GetReservedApplications(Command));
            return baseHits;
        }

        internal void MarkHit() => _hadHit = true;

        internal void Record(AttackCommand attack)
        {
            if (attack.Results.Any(results => results.Any(result =>
                    result.Props.HasFlag(MegaCrit.Sts2.Core.ValueProps.ValueProp.Move) &&
                    !result.Props.HasFlag(MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered)))) MarkHit();
        }

        internal async Task ReplayExtra(IReadOnlyList<Func<Task<AttackCommand>>> attacks, Func<bool> canContinue)
        {
            IReadOnlyList<Func<Task<AttackCommand>>> expanded = TransformAttackSequence(_attacker, attacks);
            for (int index = attacks.Count; index < expanded.Count && canContinue(); index++)
                Record(await expanded[index]());
        }

        internal Task Complete(PlayerChoiceContext context)
        {
            if (_completed) return Task.CompletedTask;
            _completed = true;
            return Consume(context);
        }

        private async Task Consume(PlayerChoiceContext context)
        {
            foreach (HbrComboPower power in _powers)
                await power.CompleteReservation(context, Command, _hadHit);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (HbrComboPower power in _powers) power.ReleaseReservation(Command);
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }

        internal bool Matches(Creature attacker) => !_disposed && ReferenceEquals(_attacker, attacker);
    }

    private static IEnumerable<HbrComboPower> Powers(Creature attacker) =>
        attacker?.Powers.OfType<HbrComboPower>().Where(power => power.Amount > 0)
            .OrderBy(power => power.DoublesHitCount) ?? Enumerable.Empty<HbrComboPower>();

    internal static AttackScope BeginAttack(Creature attacker) =>
        attacker?.Monster == null || !Powers(attacker).Any() ? null : new AttackScope(attacker);

    internal static AttackScope BeginCardAttack(CardModel card) =>
        card?.Owner?.Creature is not { } attacker || !Powers(attacker).Any()
            ? null : new AttackScope(attacker, card);

    internal static int PreviewHitCount(Creature attacker, int baseHits) =>
        PreviewHitCountAfterAttacks(attacker, baseHits, 0);

    internal static int PreviewHitCountAfterAttacks(Creature attacker, int baseHits, int priorAttacks)
    {
        foreach (HbrComboPower power in Powers(attacker))
            if (power.Amount > Math.Max(0, priorAttacks))
                baseHits = power.TransformHitCount(baseHits, power.ApplicationsPerAttack);
        return baseHits;
    }

    internal static IReadOnlyList<decimal> TransformSegments(Creature attacker,
        IReadOnlyList<decimal> segments, bool? doublesOnly = null) =>
        TransformAttackSequence(attacker, segments, doublesOnly);

    internal static IReadOnlyList<T> TransformAttackSequence<T>(Creature attacker,
        IReadOnlyList<T> segments, bool? doublesOnly = null)
    {
        if (segments == null || segments.Count == 0) return segments;
        AttackScope scope = Find(attacker);
        List<T> expanded = null;
        // Appended hits reuse the last base hit; doubling then repeats the entire sequence.
        foreach (HbrComboPower power in Powers(attacker))
        {
            if (doublesOnly.HasValue && power.DoublesHitCount != doublesOnly.Value) continue;
            int applications = scope == null ? power.ApplicationsPerAttack
                : power.GetReservedApplications(scope.Command);
            int desired = power.TransformHitCount(expanded?.Count ?? segments.Count, applications);
            if (desired == (expanded?.Count ?? segments.Count)) continue;
            expanded ??= new List<T>(segments);
            T[] source = power.DoublesHitCount ? expanded.ToArray() : new[] { expanded[^1] };
            for (int index = 0; expanded.Count < desired; index++) expanded.Add(source[index % source.Length]);
        }
        return expanded ?? segments;
    }

    internal static void MarkHit(Creature attacker) => Find(attacker)?.MarkHit();

    internal static void CancelAttack(AttackCommand command)
    {
        if (command?.Attacker == null) return;
        foreach (HbrComboPower power in command.Attacker.Powers.OfType<HbrComboPower>())
            power.ReleaseReservation(command);
    }

    private static AttackScope Find(Creature attacker)
    {
        for (AttackScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Matches(attacker)) return scope;
        return null;
    }
}
