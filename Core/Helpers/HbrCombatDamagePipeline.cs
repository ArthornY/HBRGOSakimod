using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Patches.Combat;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Characters, phantoms and owner-sourced cannons enter through the same native
// hook. Source adapters retain their identity and action scopes; this pipeline
// owns the shared calculation and keeps each native modifier stage in order.
internal static class HbrCombatDamagePipeline
{
    internal static decimal Calculate(
        IRunState runState,
        ICombatState combatState,
        Creature target,
        Creature dealer,
        decimal damage,
        ValueProp props,
        CardModel cardSource,
        ModifyDamageHookType modifyDamageHookType,
        CardPlay cardPlay,
        out List<AbstractModel> modifiers)
    {
        bool isPrecalculatedDamage =
            PrecalculatedDamageContext.Matches(
                cardSource,
                target,
                damage) || HpOnlyDamageContext.IsCurrentHpDamage(
                    target, dealer, cardSource, props);
        ModeTransitionDamagePreviewState previewState = default;
        bool isModeTransitionPreview =
            !isPrecalculatedDamage &&
            ModeTransitionHelper.TryGetDamagePreview(
                cardSource,
                out previewState);
        bool hbrNumerics = HbrBattleStatsHelper.IsEnabled(dealer) ||
            HbrBattleStatsHelper.IsEnabled(target);
        decimal baseDamage = !isPrecalculatedDamage &&
            modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive)
                ? HbrDamageSkillAdapter.ConvertBaseDamage(target, dealer, damage, props, cardSource)
                : damage;
        decimal modifiedDamage = baseDamage *
            (isModeTransitionPreview
                ? previewState.BaseDamageMultiplier
                : 1m);
        List<AbstractModel> appliedModifiers = new();
        // Capture pre-reduction damage alongside the live hooks, without evaluating any hook twice.
        decimal rawDamage = Math.Max(0m, baseDamage) * (isModeTransitionPreview
            ? Math.Max(1m, previewState.BaseDamageMultiplier) : 1m);

        // Apply to the base value before Strength and the existing damage multipliers.
        if (!isPrecalculatedDamage && modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive) &&
            props.IsPoweredAttack() && cardSource?.Owner?.Creature == dealer)
        {
            decimal madnessMultiplier = SixSenseCardCapability.GetMadnessDamageMultiplier(cardSource);
            modifiedDamage *= madnessMultiplier;
            rawDamage *= Math.Max(1m, madnessMultiplier);
            if (madnessMultiplier != 1m) appliedModifiers.Add(cardSource);
        }

        if (!isPrecalculatedDamage &&
            modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
        {
            foreach (AbstractModel model in runState.IterateHookListeners(combatState))
            {
                if (PhantomHelper.IsSummonerOwnedDamageModifier(
                        model,
                        dealer))
                {
                    continue;
                }

                decimal additiveAmount =
                    PhantomIncomingDamageHelper.ModifyDamageAdditive(
                    model,
                    target,
                    modifiedDamage,
                    props,
                    dealer,
                    cardSource,
                    cardPlay);
                additiveAmount = HbrDamageSkillAdapter.ConvertFlatDamageBonus(model, dealer,
                    additiveAmount, hbrNumerics && props.IsPoweredAttack());
                modifiedDamage += additiveAmount;
                rawDamage += Math.Max(0m, additiveAmount);
                if (additiveAmount != 0m)
                {
                    appliedModifiers.Add(model);
                }
            }
        }

        HbrDamageMultiplierAccumulator multipliers = new(dealer,
            isModeTransitionPreview ? previewState.ModeDamageMultiplier : 1m,
            HbrDamageMultiplierPolicy.Combat, hbrNumerics, target);
        decimal enemyDamageMultiplier = 1m;
        decimal multiplierBaseDamage = modifiedDamage;
        decimal rawMultiplierBaseDamage = rawDamage;
        AdditiveDamageResult groupedResult = AdditiveDamageResult.Empty;
        if (!isPrecalculatedDamage &&
            modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
        {
            // Shared by intent previews and actual damage. Apply after additive
            // bonuses, before reductions/caps, and never to already resolved hits.
            enemyDamageMultiplier = EnemyStatScalingHelper.GetDamageMultiplier(dealer) *
                SaviorGrandEntranceCombatHelper.GetDamageMultiplier(target, dealer, props) *
                WorldsFinestCombatHelper.GetDamageMultiplier(target) *
                CrushYouLikeABugCombatHelper.GetDpDamageMultiplier(cardSource, target, props);
            modifiedDamage *= enemyDamageMultiplier;
            rawDamage *= enemyDamageMultiplier;
            multiplierBaseDamage = modifiedDamage;
            rawMultiplierBaseDamage = rawDamage;
            bool completed = false;
            decimal hbrgoReferenceDamage = modifiedDamage;
            HbrgoSakiDamageMultiplierHelper.Begin(dealer, target, hbrNumerics);
            try
            {
                HotaruCannonStatsHelper.ContributeCannonRollMultiplier(target, dealer, cardSource, props);
                foreach (AbstractModel model in HotaruContestVulnerabilityHelper.EnumerateDamageModifiers(
                    runState.IterateHookListeners(combatState), target))
                {
                    if (PhantomHelper.IsSummonerOwnedDamageModifier(
                            model,
                            dealer))
                    {
                        continue;
                    }

                    if (isModeTransitionPreview &&
                        IsTransitionedWeaponModeDamageModel(model))
                    {
                        continue;
                    }

                    decimal amountForHook = IsHbrgoModel(model)
                        ? hbrgoReferenceDamage
                        : modifiedDamage;
                    decimal multiplier =
                        PhantomIncomingDamageHelper
                            .ModifyDamageMultiplicative(
                        model,
                        target,
                        amountForHook,
                        props,
                        dealer,
                        cardSource,
                        cardPlay);
                    if (model is MegaCrit.Sts2.Core.Models.Powers.VulnerablePower &&
                        SaviorGrandEntranceCombatHelper.HasEnemyUpgradeEffect(target, 5))
                        multiplier = 1m + (multiplier - 1m) * 2m;
                    multipliers.Add(model, multiplier, ref modifiedDamage, ref rawDamage);

                    if (multiplier != 1m)
                    {
                        appliedModifiers.Add(model);
                    }
                }

                groupedResult = HbrgoSakiDamageMultiplierHelper.Complete();
                completed = true;
            }
            finally
            {
                if (!completed)
                {
                    HbrgoSakiDamageMultiplierHelper.Abort();
                }
            }
        }

        List<(AbstractModel Model, decimal Cap)> caps = new();
        decimal minimumCap = decimal.MaxValue;
        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Cap))
        {
            foreach (AbstractModel model in runState.IterateHookListeners(combatState))
            {
                if (PhantomHelper.IsSummonerOwnedDamageModifier(
                        model,
                        dealer))
                {
                    continue;
                }

                decimal cap = PhantomIncomingDamageHelper.ModifyDamageCap(
                    model,
                    target,
                    props,
                    dealer,
                    cardSource,
                    cardPlay);
                caps.Add((model, cap));
                minimumCap = Math.Min(minimumCap, cap);
            }
        }

        decimal hbrgoMultiplier = multipliers.Resolve(groupedResult, TemporaryDamageBonusHelper.AppliesTo(dealer));
        modifiedDamage = DamageAmplificationLimits.MultiplySaturated(multiplierBaseDamage, multipliers.OtherMultiplier);
        decimal damageBeforeStrongestShield = Math.Min(
            DamageAmplificationLimits.MultiplySaturated(
                DamageAmplificationLimits.MultiplySaturated(modifiedDamage, hbrgoMultiplier), multipliers.FinalMultiplier),
            minimumCap);
        if (!isPrecalculatedDamage &&
            StrongestShieldDamagePatch.IsResolvingActualDamage)
        {
            Creature reductionOwner = PhantomIncomingDamageHelper.GetReductionOwner(target);
            IEnumerable<StrongestShieldPower> shields = target.Powers.OfType<StrongestShieldPower>();
            if (reductionOwner != null)
                shields = shields.Concat(reductionOwner.Powers.OfType<StrongestShieldPower>());
            foreach (StrongestShieldPower strongestShield in shields)
            {
                if (!strongestShield.TryReduceIncomingAttack(
                    strongestShield.Owner,
                    dealer,
                    damageBeforeStrongestShield,
                    props,
                    cardSource))
                {
                    continue;
                }

                hbrgoMultiplier = Math.Max(0m, DamageAmplificationLimits.AddSaturated(hbrgoMultiplier,
                    DamageAmplificationLimits.MultiplySaturated(
                        strongestShield.IncomingDamageMultiplier - 1m, multipliers.ShieldReductionMultiplier)));
                appliedModifiers.Add(strongestShield);
            }
        }

        // Aggregate temporary additions and whole-result multipliers once, then
        // apply the target's defenses and the existing single-hit cap.
        modifiedDamage = DamageAmplificationLimits.MultiplySaturated(modifiedDamage, hbrgoMultiplier);
        modifiedDamage = DamageAmplificationLimits.MultiplySaturated(modifiedDamage, multipliers.FinalMultiplier);
        decimal cannonFinalScale = HotaruCannonStatsHelper.GetFinalDamageScale(target, dealer, cardSource, props,
            out bool isCannonDamage);
        modifiedDamage = DamageAmplificationLimits.MultiplySaturated(modifiedDamage, cannonFinalScale);
        // A cannon's own scaling can reduce a hit below one, including zero
        // Soul Power. Explicit damage prevention and target caps still win.
        if (isCannonDamage && hbrgoMultiplier > 0m && multipliers.OtherMultiplier > 0m &&
            multipliers.FinalMultiplier > 0m && enemyDamageMultiplier > 0m)
            modifiedDamage = Math.Max(1m, modifiedDamage);
        foreach (AbstractModel source in groupedResult.Sources)
        {
            if (!appliedModifiers.Contains(source))
            {
                appliedModifiers.Add(source);
            }
        }

        decimal activeCap = decimal.MaxValue;
        foreach ((AbstractModel model, decimal cap) in caps)
        {
            if (cap >= activeCap)
            {
                continue;
            }

            activeCap = cap;
            if (modifiedDamage > cap)
            {
                modifiedDamage = cap;
                appliedModifiers.Add(model);
            }
        }

        // Shield notifications can synchronously calculate another preview. Publish
        // last, so a nested preview cannot replace this actual hit's captured value.
        modifiedDamage = DamageLimitHelper.CapSingleHit(modifiedDamage);
        decimal rawMultiplier = multipliers.Resolve(groupedResult, TemporaryDamageBonusHelper.AppliesTo(dealer), positiveOnly: true);
        decimal capturedRawDamage = DamageAmplificationLimits.MultiplySaturated(
            DamageAmplificationLimits.MultiplySaturated(rawMultiplierBaseDamage, multipliers.RawOtherMultiplier), rawMultiplier);
        capturedRawDamage = DamageAmplificationLimits.MultiplySaturated(capturedRawDamage, cannonFinalScale);
        if (isCannonDamage) capturedRawDamage = Math.Max(1m, capturedRawDamage);
        capturedRawDamage = DamageLimitHelper.CapSingleHit(capturedRawDamage);
        if (hbrNumerics)
        {
            // Resolve every multiplier and cap first. The shared output is an
            // integer for previews, HP/DP spending and per-hit damage receipts.
            modifiedDamage = decimal.Floor(Math.Max(0m, modifiedDamage));
            capturedRawDamage = decimal.Floor(Math.Max(0m, capturedRawDamage));
        }
        ItsMygoIncomingDamageCapture.Capture(target, capturedRawDamage);
        modifiers = appliedModifiers;
        return modifiedDamage;
    }

    private static bool IsTransitionedWeaponModeDamageModel(
        AbstractModel model)
    {
        return model is NormalStatePower or
            ReptureDualSwordsPower or
            ReptureBowPower;
    }

    private static bool IsHbrgoModel(AbstractModel model)
    {
        return model?.GetType().Assembly ==
            typeof(HbrCombatDamagePipeline).Assembly;
    }
}
