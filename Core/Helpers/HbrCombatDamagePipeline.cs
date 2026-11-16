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
        if (MonakaFriendlyFireHelper.IsFixed(target, dealer, cardSource, props))
        {
            modifiers = new();
            decimal fixedDamage = Math.Max(0m, decimal.Floor(damage));
            ItsMygoIncomingDamageCapture.Capture(target, fixedDamage);
            return fixedDamage;
        }
        using var effectCalculation = HbrEffectSkillDamageContext.EnterCalculation(
            target, dealer, cardSource, damage, props);
        bool effectSkill = HbrEffectSkillDamageContext.IsCurrentEffectCalculation;
        Creature arithmeticDealer = effectSkill && HbrEffectSkillDamageContext.TryGetStatSource(out Creature declaredSource)
            ? declaredSource : dealer;
        Creature ModifierDealer(AbstractModel model) => effectSkill && model != null &&
            (model.GetType().Assembly == typeof(HbrCombatDamagePipeline).Assembly ||
             model is MegaCrit.Sts2.Core.Models.Powers.WeakPower or MegaCrit.Sts2.Core.Models.Powers.VulnerablePower)
                ? arithmeticDealer : dealer;
        // A newly declared raw skill source wins over an enclosing resolved
        // hit, even when its target/card/amount happen to be identical.
        bool isPrecalculatedDamage = !effectSkill &&
            (PrecalculatedDamageContext.Matches(
                cardSource,
                target,
                damage) || HpOnlyDamageContext.IsCurrentHpDamage(
                    target, dealer, cardSource, props));
        ModeTransitionDamagePreviewState previewState = default;
        bool isModeTransitionPreview =
            !isPrecalculatedDamage &&
            ModeTransitionHelper.TryGetDamagePreview(
                cardSource,
                out previewState);
        bool hbrNumerics = HbrBattleStatsHelper.IsEnabled(dealer) ||
            HbrBattleStatsHelper.IsEnabled(target);
        List<AbstractModel> appliedModifiers = new();
        HashSet<AbstractModel> baseVigor = new(ReferenceEqualityComparer.Instance);
        decimal vigorUnits = 0m;
        if (!isPrecalculatedDamage && modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive) &&
            props.IsPoweredAttack() && HbrBattleStatsHelper.IsEnabled(dealer))
        {
            foreach (AbstractModel model in runState.IterateHookListeners(combatState))
            {
                if (model is not MegaCrit.Sts2.Core.Models.Powers.VigorPower ||
                    PhantomHelper.IsSummonerOwnedDamageModifier(model, dealer) || !baseVigor.Add(model)) continue;
                // Query the real native reservation and cannon-allocation hook
                // once, using the original flags and raw command amount.
                decimal units = PhantomIncomingDamageHelper.ModifyDamageAdditive(
                    model, target, damage, props, dealer, cardSource, cardPlay);
                vigorUnits = DamageAmplificationLimits.AddSaturated(vigorUnits, units);
                if (units != 0m) appliedModifiers.Add(model);
            }
        }
        decimal baseDamage = !isPrecalculatedDamage &&
            modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive)
                ? HbrDamageSkillAdapter.ConvertBaseDamageWithSkillBonus(
                    target, dealer, damage, props, cardSource, vigorUnits)
                : damage;
        decimal modifiedDamage = baseDamage;
        // Capture pre-reduction damage alongside the live hooks, without evaluating any hook twice.
        decimal rawDamage = Math.Max(0m, baseDamage);

        if (!isPrecalculatedDamage &&
            modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
        {
            foreach (AbstractModel model in runState.IterateHookListeners(combatState))
            {
                if (baseVigor.Contains(model) || PhantomHelper.IsSummonerOwnedDamageModifier(
                        model,
                        arithmeticDealer))
                {
                    continue;
                }

                decimal additiveAmount =
                    PhantomIncomingDamageHelper.ModifyDamageAdditive(
                    model,
                    target,
                    modifiedDamage,
                    HbrEffectSkillDamageContext.GetModifierProps(model, props),
                    ModifierDealer(model),
                    cardSource,
                    cardPlay);
                additiveAmount = HbrDamageSkillAdapter.ConvertFlatDamageBonus(model, target, arithmeticDealer,
                    cardSource, props, additiveAmount,
                    hbrNumerics && HbrEffectSkillDamageContext.GetModifierProps(model, props).IsPoweredAttack(),
                    allowCritical: true);
                modifiedDamage += additiveAmount;
                rawDamage += Math.Max(0m, additiveAmount);
                if (additiveAmount != 0m)
                {
                    appliedModifiers.Add(model);
                }
            }
        }

        HbrDamageMultiplierAccumulator multipliers = new(arithmeticDealer,
            isModeTransitionPreview ? previewState.ModeDamageMultiplier : 1m,
            HbrDamageMultiplierPolicy.Combat, hbrNumerics, target);
        decimal enemyDamageMultiplier = 1m;
        decimal multiplierBaseDamage = modifiedDamage;
        decimal rawMultiplierBaseDamage = rawDamage;
        AdditiveDamageResult groupedResult = AdditiveDamageResult.Empty;
        if (!isPrecalculatedDamage &&
            modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
        {
            bool directPlayerCard = cardSource != null && cardSource.Owner?.Creature == dealer &&
                HbrEffectSkillDamageContext.GetPersistentEffectProps(props).IsPoweredAttack() &&
                !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, cardSource, props, out _);
            decimal virtualEye = props.IsPoweredAttack()
                ? MindsEyeCombatHelper.GetVirtualPercent(arithmeticDealer) : 0m;
            if (virtualEye != 0m)
                multipliers.Add(ModelDb.Power<MindsEyePower>(), 1m + virtualEye / 100m,
                    ref modifiedDamage, ref rawDamage);
            // Shared by intent previews and actual damage. Apply after additive
            // bonuses, before reductions/caps, and never to already resolved hits.
            enemyDamageMultiplier = EnemyStatScalingHelper.GetDamageMultiplier(arithmeticDealer) *
                ThirtyOneXBondHelper.GetDamageMultiplier(target, arithmeticDealer,
                    HbrEffectSkillDamageContext.GetNativeEffectProps(props)) *
                SaviorGrandEntranceCombatHelper.GetDamageMultiplier(target, arithmeticDealer,
                    HbrEffectSkillDamageContext.GetPersistentEffectProps(props)) *
                WorldsFinestCombatHelper.GetDamageMultiplier(target) *
                ThirtyOneBEnemyActionHelper.GetKuraParryMultiplier(target, dealer, props) *
                CrushYouLikeABugCombatHelper.GetDpDamageMultiplier(cardSource, target,
                    HbrEffectSkillDamageContext.GetPersistentEffectProps(props)) *
                ThirtyOneBCardCombatHelper.GetDamageMultiplier(arithmeticDealer, target, cardSource,
                    HbrEffectSkillDamageContext.GetPersistentEffectProps(props)) *
                (directPlayerCard
                    ? YamawakiLaboratoryCombatHelper.GetHpDamageMultiplier(cardSource, target) *
                        NoblesseObligeCombatHelper.GetHpDamageMultiplier(cardSource, target) *
                        HisameCloseSlashCombatHelper.GetHpDamageMultiplier(cardSource, target) *
                        HisameCloseSlashCombatHelper.GetDpDamageMultiplier(cardSource, target) *
                        MaruyamaStyleCombatHelper.GetDpDamageMultiplier(cardSource, target, dealer, props) *
                        CarolHeroCombatHelper.GetHpDamageMultiplier(cardSource, target, dealer, props) *
                        IreneExposeCombatHelper.GetDpDamageMultiplier(cardSource, target, dealer, props) *
                        WorldConquestCombatHelper.GetConditionalDamageMultiplier(cardSource, dealer, target, props) *
                        FubukiChangeCombatHelper.GetConditionalDamageMultiplier(cardSource, dealer, target, props) *
                        AkariAceCombatHelper.GetConditionalDamageMultiplier(cardSource, dealer, target, props) *
                        MinoriDeliveryCombatHelper.GetConditionalDamageMultiplier(cardSource, dealer, target, props) *
                        MonakaWindForestCombatHelper.GetConditionalDamageMultiplier(cardSource, dealer, target, props)
                    : 1m);
            modifiedDamage *= enemyDamageMultiplier;
            rawDamage *= enemyDamageMultiplier;
            if (hbrNumerics && directPlayerCard)
                multipliers.AddOriginalAttackPercent(IrohaDyeCombatHelper.GetDamageAmplificationPercent(
                    cardSource, dealer, target, props));
            if (hbrNumerics && (props.IsPoweredAttack() || effectSkill) && target?.Side != dealer?.Side)
                multipliers.AddOriginalAttackPercent(HbrCardEffectBonusHelper.GetPercent(cardSource, dealer, props));
            multiplierBaseDamage = modifiedDamage;
            rawMultiplierBaseDamage = rawDamage;
            bool completed = false;
            decimal hbrgoReferenceDamage = modifiedDamage;
            HbrgoSakiDamageMultiplierHelper.Begin(arithmeticDealer, target, hbrNumerics);
            try
            {
                if (HbrEffectSkillDamageContext.GetPersistentEffectProps(props).IsPoweredAttack())
                {
                    decimal spiritBonus = KozueSpiritHelper.GetDamageAmplificationPercent(arithmeticDealer);
                    if (spiritBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + spiritBonus / 100m, null);
                    decimal laboratoryBonus = directPlayerCard
                        ? YamawakiLaboratoryCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (laboratoryBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + laboratoryBonus / 100m, null);
                    decimal conquestBonus = directPlayerCard
                        ? WorldConquestCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (conquestBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + conquestBonus / 100m, null);
                    decimal showtimeBonus = directPlayerCard
                        ? KanzakiShowtimeCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (showtimeBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + showtimeBonus / 100m, null);
                    decimal tengenBonus = directPlayerCard
                        ? TengenBrilliantMoveCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (tengenBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + tengenBonus / 100m, null);
                    decimal niinaBonus = directPlayerCard && hbrNumerics
                        ? NiinaNarcissismCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (niinaBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + niinaBonus / 100m, null);
                    decimal natsumeBonus = directPlayerCard && hbrNumerics
                        ? NatsumeCondemnationCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (natsumeBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + natsumeBonus / 100m, null);
                    decimal leaderBonus = directPlayerCard && hbrNumerics
                        ? LeadingChargeCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (leaderBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + leaderBonus / 100m, null);
                    decimal carolBonus = hbrNumerics && directPlayerCard
                        ? CarolHeroCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (carolBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + carolBonus / 100m, null);
                    decimal ireneBonus = hbrNumerics && directPlayerCard
                        ? IreneExposeCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (ireneBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + ireneBonus / 100m, null);
                    decimal mariaBonus = hbrNumerics && directPlayerCard
                        ? MariaRepentanceCombatHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (hbrNumerics && directPlayerCard && !StrongestShieldDamagePatch.IsResolvingActualDamage &&
                        !HotaruCannonStatsHelper.IsReactionDamage)
                        mariaBonus = MariaRepentanceCombatHelper.GetPreviewKnockBonusPercent(cardSource);
                    if (mariaBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + mariaBonus / 100m, null);
                    decimal familiarBonus = directPlayerCard
                        ? DemonKingAndFamiliarBondHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                    if (familiarBonus != 0m)
                        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + familiarBonus / 100m, null);
                }
                decimal phantomRelicBonus = hbrNumerics
                    ? ThirtyAClassRelicsHelper.GetDamageAmplificationPercent(cardSource, dealer, target, props) : 0m;
                if (phantomRelicBonus != 0m)
                    HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + phantomRelicBonus / 100m, null);
                HotaruCannonStatsHelper.ContributeCannonRollMultiplier(target, dealer, cardSource, props);
                foreach (AbstractModel model in HbrCardEffectPreview.EnumerateDamageModifiers(
                    HotaruContestVulnerabilityHelper.EnumerateDamageModifiers(
                        runState.IterateHookListeners(combatState), target), target, cardSource))
                {
                    if (PhantomHelper.IsSummonerOwnedDamageModifier(
                            model,
                            arithmeticDealer))
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
                        HbrEffectSkillDamageContext.GetModifierProps(model, props),
                        ModifierDealer(model),
                        cardSource,
                        cardPlay);
                    // HBR defense down snapshots Sea Breeze's base bonus on
                    // application. Original Vulnerable retains its own effect;
                    // the old Sea Breeze interaction applies only in legacy mode.
                    if (model is MegaCrit.Sts2.Core.Models.Powers.VulnerablePower vulnerable &&
                        !HbrBattleStatsHelper.IsEnabled(target) &&
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
                        arithmeticDealer))
                {
                    continue;
                }

                decimal cap = PhantomIncomingDamageHelper.ModifyDamageCap(
                    model,
                    target,
                    props,
                    ModifierDealer(model),
                    cardSource,
                    cardPlay);
                caps.Add((model, cap));
                minimumCap = Math.Min(minimumCap, cap);
            }
        }

        decimal hbrgoMultiplier = multipliers.Resolve(groupedResult, TemporaryDamageBonusHelper.AppliesTo(arithmeticDealer));
        modifiedDamage = DamageAmplificationLimits.MultiplySaturated(multiplierBaseDamage, multipliers.OtherMultiplier);
        decimal chaseMultiplier = !isPrecalculatedDamage &&
            modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative) &&
            props.IsPoweredAttack() && target?.Side != dealer?.Side &&
            !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, cardSource, props, out _)
                ? DualSwordsChaseHelper.GetDamageMultiplier(dealer, cardSource) : 1m;
        decimal ApplyHitBonuses(decimal amount)
        {
            amount = DamageAmplificationLimits.MultiplySaturated(amount, chaseMultiplier);
            if (!isPrecalculatedDamage && modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
            {
                amount = HbrCriticalDamageContext.ApplyCriticalDamageMultiplier(amount, target, dealer, cardSource, props);
                amount = DamageAmplificationLimits.MultiplySaturated(amount,
                    ThirtyOneDBondHelper.GetDamageMultiplier(arithmeticDealer, target));
            }
            return amount;
        }
        decimal damageBeforeStrongestShield = Math.Min(
            ApplyHitBonuses(
                DamageAmplificationLimits.MultiplySaturated(
                    DamageAmplificationLimits.MultiplySaturated(modifiedDamage, hbrgoMultiplier),
                    multipliers.FinalMultiplier)),
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
        modifiedDamage = ApplyHitBonuses(modifiedDamage);
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
        decimal rawMultiplier = multipliers.Resolve(groupedResult, TemporaryDamageBonusHelper.AppliesTo(arithmeticDealer), positiveOnly: true);
        decimal capturedRawDamage = DamageAmplificationLimits.MultiplySaturated(
            DamageAmplificationLimits.MultiplySaturated(rawMultiplierBaseDamage, multipliers.RawOtherMultiplier), rawMultiplier);
        capturedRawDamage = DamageAmplificationLimits.MultiplySaturated(capturedRawDamage, cannonFinalScale);
        capturedRawDamage = ApplyHitBonuses(capturedRawDamage);
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
