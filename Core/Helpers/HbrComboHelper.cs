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
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>Shares the attack reservation with direct phantom and cannon hit loops.</summary>
internal static class HbrComboHelper
{
    private static readonly AsyncLocal<AttackScope> Current = new();

    internal sealed class AttackScope : IDisposable
    {
        internal readonly AttackScope Previous;
        private readonly Creature _attacker;
        private readonly object _playToken;
        internal readonly AttackCommand Command;
        internal readonly long Order;
        private readonly HbrComboPower[] _powers;
        private readonly HbrEffectPower[] _effects;
        private readonly int _virtualExtraHits;
        private bool _hadHit;
        private bool _completed;
        private bool _disposed;

        internal AttackScope(Creature attacker, CardModel card = null)
        {
            _attacker = attacker;
            _playToken = card != null ? CardPlayContextHelper.GetCurrentPlayToken(card) : null;
            Order = HighHitDamageVisualHelper.AllocateAttackOrder();
            Previous = Current.Value;
            Command = card == null ? new AttackCommand(0m).FromMonster(attacker.Monster)
                : new AttackCommand(0m).FromCardCompat(card);
            _virtualExtraHits = VirtualExtraHits(attacker, card);
            _powers = Powers(attacker).Where(power => power.ReservedAttack == null).ToArray();
            foreach (HbrComboPower power in _powers) power.ReserveForAttack(Command);
            _effects = CountedEffects(attacker).Where(power => !power.HasEffectReservation).ToArray();
            foreach (HbrEffectPower power in _effects) power.ReserveForEffectAttack(Command);
            Current.Value = this;
        }

        internal int HitCount(int baseHits)
        {
            baseHits = AddHits(baseHits, _virtualExtraHits);
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
            foreach (HbrEffectPower power in _effects)
                await power.CompleteEffectAttack(Command, _hadHit);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (HbrComboPower power in _powers) power.ReleaseReservation(Command);
            foreach (HbrEffectPower power in _effects) power.ReleaseEffectAttack(Command);
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }

        internal bool Matches(Creature attacker) => !_disposed && ReferenceEquals(_attacker, attacker);
        internal bool Covers(AttackCommand attack) => Matches(attack?.Attacker) &&
            (Command.ModelSource == null || ReferenceEquals(Command.ModelSource, attack.ModelSource) &&
                ReferenceEquals(_playToken, CardPlayContextHelper.GetCurrentPlayToken(Command.ModelSource as CardModel)));
        internal int VirtualHits => _virtualExtraHits;
        internal bool Active => !_disposed;
    }

    private static IEnumerable<HbrComboPower> Powers(Creature attacker) =>
        attacker?.Powers.OfType<HbrComboPower>().Where(power => power.Amount > 0)
            .OrderBy(power => power.DoublesHitCount) ?? Enumerable.Empty<HbrComboPower>();
    internal static (AttackCommand Command, long Order) GetCurrentAttackIdentity() =>
        Current.Value is { Active: true } scope ? (scope.Command, scope.Order) : (null, 0L);
    internal static bool CurrentDirectCovers(AttackCommand command) =>
        Current.Value is { Active: true } scope && scope.Covers(command);
    private static IEnumerable<HbrEffectPower> CountedEffects(Creature attacker) =>
        attacker?.Powers.OfType<HbrEffectPower>().Where(power => power.Amount > 0 &&
            power.CurrentEffectProfile.Exit == HbrEffectExit.Count) ?? Enumerable.Empty<HbrEffectPower>();

    internal static AttackScope BeginAttack(Creature attacker) =>
        attacker?.Monster == null || !Powers(attacker).Any() && !CountedEffects(attacker).Any() && VirtualExtraHits(attacker, null) <= 0
            ? null : new AttackScope(attacker);

    internal static AttackScope BeginCardAttack(CardModel card) =>
        card?.Owner?.Creature is not { } attacker ||
            !Powers(attacker).Any() && !CountedEffects(attacker).Any() && VirtualExtraHits(attacker, card) <= 0
            ? null : new AttackScope(attacker, card);

    private static int AddHits(int hits, int extra) => hits <= 0 || extra <= 0
        ? hits : (int)Math.Min(int.MaxValue, (long)hits + extra);

    internal static int VirtualExtraHits(Creature attacker, CardModel card) =>
        attacker == null ? 0 : (attacker.HasPower<ReptureDualSwordsPower>() ? 3 : 0) +
            (DualSwordsChaseHelper.IsChaseAttack(attacker, card) ? 1 : 0) +
            ThirtyOneEBondHelper.GetExtraHits(attacker, card);

    internal static int NativeVirtualExtraHits(AttackCommand attack, bool projectMode = false)
    {
        if (attack?.Attacker == null || !attack.DamageProps.HasFlag(ValueProp.Move) ||
            attack.DamageProps.HasFlag(ValueProp.Unpowered)) return 0;
        // A card-owned direct loop already expanded its sequence once. Its
        // nested native commands must not each add another set of form hits.
        for (AttackScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Covers(attack)) return 0;
        CardModel card = attack.ModelSource as CardModel;
        bool dualSwords = attack.Attacker.HasPower<ReptureDualSwordsPower>();
        if (projectMode && card?.Owner?.Creature == attack.Attacker)
        {
            // The normal play wrapper enters a restricted card's mode before
            // attacking. Predict that same mode for previews, while explicit
            // suppression (e.g. Unfettered) retains the current mode's hits.
            ReptureCardMode required = ModeTransitionHelper.BeginCardPlay(card).RequiredMode;
            if (required != ReptureCardMode.None) dualSwords = required == ReptureCardMode.DualSwords;
        }
        return (dualSwords ? 3 : 0) + (DualSwordsChaseHelper.IsChaseAttack(attack.Attacker, card) ? 1 : 0) +
            ThirtyOneEBondHelper.GetExtraHits(attack.Attacker, card);
    }

    internal static int AddVirtualNativeHits(AttackCommand attack, int hits) =>
        AddHits(hits, NativeVirtualExtraHits(attack,
            !HighHitDamageVisualHelper.IsResolvingAttack(attack) &&
            !DualSwordsChaseHelper.IsResolvingPlay(attack?.ModelSource as CardModel)));

    internal static int PreviewHitCount(Creature attacker, int baseHits) =>
        PreviewHitCountAfterAttacks(attacker, baseHits, 0);

    internal static int PreviewHitCountAfterAttacks(Creature attacker, int baseHits, int priorAttacks)
    {
        // A form's virtual medium never spends uses, even across an intent's
        // successive actions. Chase requires a concrete physical source card.
        baseHits = AddHits(baseHits, VirtualExtraHits(attacker, null));
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
        int virtualExtra = doublesOnly == true ? 0 : scope?.VirtualHits ?? VirtualExtraHits(attacker, null);
        if (virtualExtra > 0)
        {
            expanded = new List<T>(segments);
            int desired = AddHits(segments.Count, virtualExtra);
            while (expanded.Count < desired) expanded.Add(segments[^1]);
        }
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
        foreach (HbrEffectPower power in command.Attacker.Powers.OfType<HbrEffectPower>())
            power.ReleaseEffectAttack(command);
    }

    private static AttackScope Find(Creature attacker)
    {
        for (AttackScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Matches(attacker)) return scope;
        return null;
    }
}
