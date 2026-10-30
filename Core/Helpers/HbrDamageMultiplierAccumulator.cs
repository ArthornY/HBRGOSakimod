using System;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal enum HbrDamageMultiplierPolicy
{
    Combat,
    OutgoingSummary
}

// Shared classification for real damage and the owner's outgoing summary.
// The summary has no target and does not replace a target's damage calculation.
internal struct HbrDamageMultiplierAccumulator
{
    private readonly Creature _dealer;
    private readonly HbrDamageMultiplierPolicy _policy;
    private readonly bool _hbrNumerics;
    private readonly Creature _target;
    private decimal _defenseDownOffset;
    private decimal _destructionMultiplier;
    private decimal _nativeTemporaryAttackOffset;
    private decimal _nativeOtherAttackOffset;
    private decimal _targetMultiplier;

    internal decimal ModeMultiplier { get; private set; }
    internal decimal ManualOffset { get; private set; }
    internal decimal FinalMultiplier { get; private set; }
    internal decimal TemporaryMultiplier { get; private set; }
    internal decimal OtherMultiplier { get; private set; }
    internal decimal RawModeMultiplier { get; private set; }
    internal decimal RawManualOffset { get; private set; }
    internal decimal RawOtherMultiplier { get; private set; }
    internal readonly decimal ShieldReductionMultiplier => DamageAmplificationLimits.MultiplySaturated(
        TemporaryMultiplier, _targetMultiplier);

    internal HbrDamageMultiplierAccumulator(Creature dealer, decimal modeMultiplier,
        HbrDamageMultiplierPolicy policy, bool hbrNumerics = false, Creature target = null)
    {
        _dealer = dealer;
        _policy = policy;
        _hbrNumerics = hbrNumerics;
        _target = target;
        _defenseDownOffset = 0m;
        _destructionMultiplier = 1m;
        _nativeTemporaryAttackOffset = _nativeOtherAttackOffset = 0m;
        _targetMultiplier = 1m;
        ModeMultiplier = modeMultiplier;
        ManualOffset = RawManualOffset = 0m;
        FinalMultiplier = TemporaryMultiplier = OtherMultiplier = RawOtherMultiplier = 1m;
        RawModeMultiplier = Math.Max(1m, modeMultiplier);
    }

    internal static bool IsModeSource(AbstractModel model) => model is
        NormalStatePower or ReptureDualSwordsPower or ReptureBowPower or LoliFormPower;

    internal void Add(AbstractModel model, decimal multiplier,
        ref decimal runningDamage, ref decimal rawDamage)
    {
        bool combat = _policy == HbrDamageMultiplierPolicy.Combat;
        if (IsModeSource(model))
        {
            // Preserve the original arithmetic boundary: combat mode products
            // are ordinary decimal products; the summary uses saturation.
            ModeMultiplier = combat ? ModeMultiplier * multiplier
                : DamageAmplificationLimits.MultiplySaturated(ModeMultiplier, multiplier);
            if (combat) RawModeMultiplier *= Math.Max(1m, multiplier);
        }
        else if (model is ElectronicSoldierManual)
        {
            ManualOffset += multiplier - 1m;
            if (combat) RawManualOffset += Math.Max(0m, multiplier - 1m);
        }
        else if (combat && model is HighDimensionalShellPower)
        {
            // HBR's DP pool is not an additional 25% defensive multiplier.
            // The native hook was still evaluated exactly once by the caller.
            if (_hbrNumerics && model.GetType() == typeof(HighDimensionalShellPower)) return;
            // The target's DP reduction remains outside outgoing amplification.
            // Its raw ledger update has the same position as before extraction.
            FinalMultiplier *= multiplier;
            rawDamage *= Math.Max(1m, multiplier);
        }
        else if (_hbrNumerics && model is DestructionRatePower)
        {
            _destructionMultiplier = DamageAmplificationLimits.MultiplySaturated(
                _destructionMultiplier, multiplier);
        }
        else if (_hbrNumerics && model is WeakPower or VulnerablePower)
        {
            // Vanilla effects retain their own multiplicative bucket, alongside
            // (and independently of) the HBR attack/defense-down abilities.
            runningDamage = DamageAmplificationLimits.MultiplySaturated(runningDamage, multiplier);
            rawDamage = DamageAmplificationLimits.MultiplySaturated(rawDamage, Math.Max(1m, multiplier));
            OtherMultiplier = DamageAmplificationLimits.MultiplySaturated(OtherMultiplier, multiplier);
            RawOtherMultiplier = DamageAmplificationLimits.MultiplySaturated(RawOtherMultiplier, Math.Max(1m, multiplier));
        }
        else if (_hbrNumerics && HbrgoSakiDamageMultiplierHelper.IsDefenseDownSource(
            model, _target, _dealer, multiplier))
        {
            _defenseDownOffset = DamageAmplificationLimits.AddSaturated(
                _defenseDownOffset, multiplier - 1m);
        }
        else if (_hbrNumerics && multiplier > 1m)
        {
            if (TemporaryDamageBonusHelper.IsTemporarySource(model, _dealer))
                _nativeTemporaryAttackOffset = DamageAmplificationLimits.AddSaturated(
                    _nativeTemporaryAttackOffset, multiplier - 1m);
            else
                _nativeOtherAttackOffset = DamageAmplificationLimits.AddSaturated(
                    _nativeOtherAttackOffset, multiplier - 1m);
        }
        else
        {
            // Native/external hooks later in this stage observe this evolving
            // amount. Mode/manual/final factors do not update it here.
            runningDamage = DamageAmplificationLimits.MultiplySaturated(runningDamage, multiplier);
            if (combat)
                rawDamage = DamageAmplificationLimits.MultiplySaturated(rawDamage, Math.Max(1m, multiplier));
            if (multiplier > 1m && TemporaryDamageBonusHelper.IsTemporarySource(model, _dealer))
                TemporaryMultiplier = DamageAmplificationLimits.MultiplySaturated(TemporaryMultiplier, multiplier);
            else
            {
                OtherMultiplier = DamageAmplificationLimits.MultiplySaturated(OtherMultiplier, multiplier);
                if (combat)
                    RawOtherMultiplier = DamageAmplificationLimits.MultiplySaturated(RawOtherMultiplier, Math.Max(1m, multiplier));
            }
        }
    }

    internal decimal Resolve(AdditiveDamageResult group, bool capped, bool positiveOnly = false)
    {
        if (_hbrNumerics)
        {
            decimal native = DamageAmplificationLimits.AddSaturated(
                _nativeTemporaryAttackOffset, _nativeOtherAttackOffset);
            group = new AdditiveDamageResult(
                DamageAmplificationLimits.AddSaturated(group.MultiplierOffset, native), group.Sources,
                DamageAmplificationLimits.AddSaturated(group.PositiveMultiplierOffset, native),
                group.PermanentMultiplierOffset,
                DamageAmplificationLimits.AddSaturated(group.TemporaryPositiveMultiplierOffset,
                    _nativeTemporaryAttackOffset), group.NegativeMultiplierOffset,
                DamageAmplificationLimits.AddSaturated(group.OtherPositiveMultiplierOffset,
                    _nativeOtherAttackOffset), group.DefenseDownMultiplierOffset);
        }
        decimal attack = TemporaryDamageBonusHelper.ResolveMultiplier(
            DamageAmplificationLimits.MultiplySaturated(
                positiveOnly ? RawModeMultiplier : ModeMultiplier,
                positiveOnly ? 1m + RawManualOffset : Math.Max(0m, 1m + ManualOffset)),
            group, TemporaryMultiplier, capped, positiveOnly);
        if (!_hbrNumerics) return attack;
        decimal defenseDown = Math.Max(0m, DamageAmplificationLimits.AddSaturated(1m,
            DamageAmplificationLimits.AddSaturated(_defenseDownOffset, group.DefenseDownMultiplierOffset)));
        _targetMultiplier = DamageAmplificationLimits.MultiplySaturated(defenseDown, _destructionMultiplier);
        // Strongest Shield subtracts from the attack bucket after Resolve. Its
        // reduction must pass through these target factors too, otherwise a
        // higher destruction rate would dilute the same shield's reduction.
        return DamageAmplificationLimits.MultiplySaturated(attack, _targetMultiplier);
    }
}
