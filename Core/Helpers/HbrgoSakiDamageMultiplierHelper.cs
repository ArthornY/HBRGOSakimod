using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrgoSakiDamageMultiplierHelper
{
    [ThreadStatic]
    private static Stack<AdditiveDamageContext> _contexts;

    internal static void Begin(Creature dealer = null, Creature target = null, bool hbrNumerics = false)
    {
        _contexts ??= new Stack<AdditiveDamageContext>();
        _contexts.Push(new AdditiveDamageContext(dealer, target, hbrNumerics));
    }

    internal static bool IsDefenseDownSource(AbstractModel source, Creature target,
        Creature dealer, decimal multiplier) => multiplier > 1m && target != null && target != dealer &&
        source is PowerModel { Type: PowerType.Debuff } power && power.Owner == target;

    internal static decimal Add(decimal multiplier, AbstractModel source)
    {
        if (_contexts == null || _contexts.Count == 0)
        {
            return multiplier;
        }

        if (multiplier != 1m)
        {
            _contexts.Peek().Add(multiplier, source,
                TemporaryDamageBonusHelper.IsTemporarySource(source, _contexts.Peek().Dealer));
        }

        return 1m;
    }

    internal static decimal AddPermanent(decimal multiplier, AbstractModel source) =>
        AddExplicit(multiplier, source, false, true);

    internal static decimal AddTemporary(decimal multiplier, AbstractModel source) =>
        AddExplicit(multiplier, source, true, false);

    internal static decimal AddNonTemporary(decimal multiplier, AbstractModel source) =>
        AddExplicit(multiplier, source, false, false);

    private static decimal AddExplicit(decimal multiplier, AbstractModel source,
        bool temporary, bool permanent)
    {
        if (_contexts == null || _contexts.Count == 0) return multiplier;
        if (multiplier != 1m) _contexts.Peek().Add(multiplier, source, temporary, permanent);
        return 1m;
    }

    internal static AdditiveDamageResult Complete()
    {
        if (_contexts == null || _contexts.Count == 0)
        {
            return AdditiveDamageResult.Empty;
        }

        AdditiveDamageContext context = _contexts.Pop();
        return new AdditiveDamageResult(
            context.MultiplierOffset,
            context.Sources.Distinct().ToArray(),
            context.PositiveMultiplierOffset,
            context.PermanentMultiplierOffset,
            context.TemporaryPositiveMultiplierOffset,
            context.NegativeMultiplierOffset,
            context.OtherPositiveMultiplierOffset,
            context.DefenseDownMultiplierOffset);
    }

    internal static void Abort()
    {
        if (_contexts?.Count > 0)
        {
            _contexts.Pop();
        }
    }

    private sealed class AdditiveDamageContext
    {
        internal Creature Dealer { get; }
        private Creature Target { get; }
        private bool HbrNumerics { get; }
        internal AdditiveDamageContext(Creature dealer, Creature target, bool hbrNumerics)
        {
            Dealer = dealer;
            Target = target;
            HbrNumerics = hbrNumerics;
        }

        internal decimal DefenseDownMultiplierOffset { get; private set; }

        internal decimal MultiplierOffset { get; private set; }

        internal decimal PositiveMultiplierOffset { get; private set; }

        internal decimal PermanentMultiplierOffset { get; private set; }
        internal decimal TemporaryPositiveMultiplierOffset { get; private set; }
        internal decimal NegativeMultiplierOffset { get; private set; }
        internal decimal OtherPositiveMultiplierOffset { get; private set; }

        internal List<AbstractModel> Sources { get; } = new();

        internal void Add(decimal multiplier, AbstractModel source, bool temporary, bool permanent = false)
        {
            decimal offset = multiplier - 1m;
            if (HbrNumerics && IsDefenseDownSource(source, Target, Dealer, multiplier))
            {
                DefenseDownMultiplierOffset = DamageAmplificationLimits.AddSaturated(
                    DefenseDownMultiplierOffset, offset);
                if (source != null) Sources.Add(source);
                return;
            }
            MultiplierOffset = DamageAmplificationLimits.AddSaturated(MultiplierOffset, offset);
            PositiveMultiplierOffset = DamageAmplificationLimits.AddSaturated(
                PositiveMultiplierOffset, Math.Max(0m, offset));
            if (permanent)
                PermanentMultiplierOffset = DamageAmplificationLimits.AddSaturated(PermanentMultiplierOffset, offset);
            else if (offset < 0m)
                NegativeMultiplierOffset = DamageAmplificationLimits.AddSaturated(NegativeMultiplierOffset, offset);
            else if (temporary)
                TemporaryPositiveMultiplierOffset = DamageAmplificationLimits.AddSaturated(
                    TemporaryPositiveMultiplierOffset, offset);
            else
                OtherPositiveMultiplierOffset = DamageAmplificationLimits.AddSaturated(OtherPositiveMultiplierOffset, offset);
            if (source != null)
            {
                Sources.Add(source);
            }
        }
    }
}

internal readonly struct AdditiveDamageResult
{
    internal static AdditiveDamageResult Empty { get; } =
        new(0m, Array.Empty<AbstractModel>());

    internal decimal MultiplierOffset { get; }

    internal decimal PositiveMultiplierOffset { get; }

    internal decimal PermanentMultiplierOffset { get; }

    internal decimal TemporaryPositiveMultiplierOffset { get; }

    internal decimal NegativeMultiplierOffset { get; }

    internal decimal OtherPositiveMultiplierOffset { get; }

    internal decimal DefenseDownMultiplierOffset { get; }

    internal IReadOnlyList<AbstractModel> Sources { get; }

    internal AdditiveDamageResult(
        decimal multiplierOffset,
        IReadOnlyList<AbstractModel> sources,
        decimal positiveMultiplierOffset = 0m,
        decimal permanentMultiplierOffset = 0m,
        decimal temporaryPositiveMultiplierOffset = 0m,
        decimal negativeMultiplierOffset = 0m,
        decimal otherPositiveMultiplierOffset = 0m,
        decimal defenseDownMultiplierOffset = 0m)
    {
        MultiplierOffset = multiplierOffset;
        PositiveMultiplierOffset = positiveMultiplierOffset;
        PermanentMultiplierOffset = permanentMultiplierOffset;
        TemporaryPositiveMultiplierOffset = temporaryPositiveMultiplierOffset;
        NegativeMultiplierOffset = negativeMultiplierOffset;
        OtherPositiveMultiplierOffset = otherPositiveMultiplierOffset;
        DefenseDownMultiplierOffset = defenseDownMultiplierOffset;
        Sources = sources ?? Array.Empty<AbstractModel>();
    }
}
