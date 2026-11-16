using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Actual damage commands own a replayable critical sample. Previews have no
// sample and never advance RNG; recalculations of the same hit reuse it.
internal static class HbrCriticalDamageContext
{
    private static readonly AsyncLocal<Scope> Current = new();

    internal sealed class Scope
    {
        internal Creature Dealer;
        internal CardModel Card;
        internal ValueProp Props;
        internal int? Sample;
        internal bool Active = true;
        internal Scope Previous;
        internal readonly Dictionary<Creature, bool> CriticalTargets = new();
    }

    internal static Scope Begin(Creature dealer, CardModel card, ValueProp props)
    {
        if (!props.IsPoweredAttack() || !HbrBattleStatsHelper.IsEnabled(dealer) ||
            EnemyStatScalingHelper.IsEnemy(dealer)) return null;
        Scope scope = new() { Dealer = dealer, Card = card, Props = props,
            Previous = Current.Value };
        Current.Value = scope;
        return scope;
    }

    internal static bool IsCritical(Creature target, Creature dealer, CardModel card,
        ValueProp props, decimal luck, decimal enemyParameter)
    {
        bool forcedSword = HisameCloseSlashCombatHelper.ForcesCritical(target, dealer, card, props) ||
            IreneExposeCombatHelper.ForcesCritical(target, dealer, card, props);
        if ((!StrongestShieldDamagePatch.IsResolvingActualDamage || HotaruCannonStatsHelper.IsDamagePreview) &&
            !HotaruCannonStatsHelper.IsReactionDamage && forcedSword) return true;
        Scope scope = Current.Value;
        if (scope?.Active != true || !ReferenceEquals(scope.Dealer, dealer) ||
            !ReferenceEquals(scope.Card, card) || scope.Props != props ||
            !StrongestShieldDamagePatch.IsResolvingActualDamage ||
            HotaruCannonStatsHelper.IsDamagePreview || HotaruCannonStatsHelper.IsReactionDamage)
            return false;
        if (forcedSword || ThirtyOneCMachinaHelper.ForcesCritical(dealer, card, props))
        {
            if (target != null) scope.CriticalTargets[target] = true;
            return true;
        }
        // Sample lazily, after the pipeline excludes resolved HP reactions.
        var run = dealer?.CombatState?.RunState;
        if (run == null) return false;
        scope.Sample ??= run.Rng.Niche.NextInt(10000);
        decimal bonus = DamageAmplificationLimits.AddSaturated(
            ElectronicSoldierManual.GetEquipmentCriticalRate(dealer),
            DamageAmplificationLimits.AddSaturated(HbrCriticalRateUpPower.GetTotalPercent(dealer),
                IrohaDyeCombatHelper.GetCriticalRatePercent(card, dealer, target, props)));
        bool critical = scope.Sample.Value <
            HbrOriginalNumericFormula.CriticalProbability(luck, enemyParameter, bonus) * 10000m;
        if (target != null) scope.CriticalTargets[target] = critical;
        return critical;
    }

    // Read only the critical decision already made by this calculation. This
    // final-damage stage never rolls again and never turns a preview, a die
    // critical-success effect or resolved damage into an original HBR crit.
    internal static decimal GetCriticalDamageMultiplier(Creature target, Creature dealer,
        CardModel card, ValueProp props)
    {
        if ((!StrongestShieldDamagePatch.IsResolvingActualDamage || HotaruCannonStatsHelper.IsDamagePreview) &&
            !HotaruCannonStatsHelper.IsReactionDamage &&
            (HisameCloseSlashCombatHelper.ForcesCritical(target, dealer, card, props) ||
             IreneExposeCombatHelper.ForcesCritical(target, dealer, card, props)))
            return HbrOriginalNumericFormula.CriticalMultiplier(DamageAmplificationLimits.AddSaturated(
                HbrCriticalDamageUpPower.GetTotalPercent(dealer), DamageAmplificationLimits.AddSaturated(
                    KanzakiShowtimeCombatHelper.GetCriticalDamageBonusPercent(target, dealer, card, props),
                    IrohaDyeCombatHelper.GetCriticalDamagePercent(card, dealer, target, props)))) /
                HbrOriginalNumericFormula.CriticalMultiplier();
        if (!StrongestShieldDamagePatch.IsResolvingActualDamage || HotaruCannonStatsHelper.IsDamagePreview ||
            HotaruCannonStatsHelper.IsReactionDamage) return 1m;
        if (HbrEffectSkillDamageContext.IsCurrentEffectCalculation || HbrEffectSkillDamageContext.HasEffectCommand)
            return HbrEffectSkillDamageContext.GetCriticalDamageMultiplier(target, dealer, card, props);
        Scope scope = Current.Value;
        return scope?.Active == true && ReferenceEquals(scope.Dealer, dealer) &&
            ReferenceEquals(scope.Card, card) && scope.Props == props && target != null &&
            scope.CriticalTargets.TryGetValue(target, out bool critical) && critical
                ? HbrOriginalNumericFormula.CriticalMultiplier(DamageAmplificationLimits.AddSaturated(
                    HbrCriticalDamageUpPower.GetTotalPercent(dealer),
                    DamageAmplificationLimits.AddSaturated(
                        KanzakiShowtimeCombatHelper.GetCriticalDamageBonusPercent(target, dealer, card, props),
                        IrohaDyeCombatHelper.GetCriticalDamagePercent(card, dealer, target, props)))) /
                    HbrOriginalNumericFormula.CriticalMultiplier() : 1m;
    }

    internal static decimal ApplyCriticalDamageMultiplier(decimal amount, Creature target,
        Creature dealer, CardModel card, ValueProp props)
    {
        if (GetCriticalDamageMultiplier(target, dealer, card, props) == 1m) return amount;
        Creature source = dealer;
        if (HbrEffectSkillDamageContext.IsCurrentEffectCalculation &&
            HbrEffectSkillDamageContext.TryGetStatSource(out Creature effectSource)) source = effectSource;
        // Keep the original 1.5+bonus numerator intact. Multiplying a rounded
        // 4/3 ratio could turn an exact two-point critical into 1.999... and
        // lose a point at the final floor.
        decimal critical = HbrOriginalNumericFormula.CriticalMultiplier(
            DamageAmplificationLimits.AddSaturated(HbrCriticalDamageUpPower.GetTotalPercent(source),
                DamageAmplificationLimits.AddSaturated(
                    KanzakiShowtimeCombatHelper.GetCriticalDamageBonusPercent(target, dealer, card, props),
                    IrohaDyeCombatHelper.GetCriticalDamagePercent(card, dealer, target, props))));
        return DamageAmplificationLimits.MultiplySaturated(amount, critical) /
            HbrOriginalNumericFormula.CriticalMultiplier();
    }

    internal static void RestoreCaller(Scope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }

    internal static void Close(Scope scope)
    {
        if (scope == null) return;
        scope.Active = false;
        RestoreCaller(scope);
    }

    internal static async Task<IEnumerable<DamageResult>> Finish(
        Task<IEnumerable<DamageResult>> original, Scope scope)
    {
        try { return await original; }
        finally { Close(scope); }
    }
}
