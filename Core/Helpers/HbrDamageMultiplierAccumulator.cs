using System;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

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

    internal decimal ModeMultiplier { get; private set; }
    internal decimal ManualOffset { get; private set; }
    internal decimal FinalMultiplier { get; private set; }
    internal decimal TemporaryMultiplier { get; private set; }
    internal decimal OtherMultiplier { get; private set; }
    internal decimal RawModeMultiplier { get; private set; }
    internal decimal RawManualOffset { get; private set; }
    internal decimal RawOtherMultiplier { get; private set; }

    internal HbrDamageMultiplierAccumulator(Creature dealer, decimal modeMultiplier,
        HbrDamageMultiplierPolicy policy)
    {
        _dealer = dealer;
        _policy = policy;
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
            // The target's DP reduction remains outside outgoing amplification.
            // Its raw ledger update has the same position as before extraction.
            FinalMultiplier *= multiplier;
            rawDamage *= Math.Max(1m, multiplier);
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

    internal readonly decimal Resolve(AdditiveDamageResult group, bool capped, bool positiveOnly = false) =>
        TemporaryDamageBonusHelper.ResolveMultiplier(
            DamageAmplificationLimits.MultiplySaturated(
                positiveOnly ? RawModeMultiplier : ModeMultiplier,
                positiveOnly ? 1m + RawManualOffset : Math.Max(0m, 1m + ManualOffset)),
            group, TemporaryMultiplier, capped, positiveOnly);
}
