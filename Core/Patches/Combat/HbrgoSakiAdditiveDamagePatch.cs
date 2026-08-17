using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrgoSakiAdditiveDamagePatch
{
    private static MethodBase TargetMethod()
    {
        return PrivateAccess.Method(typeof(Hook), "ModifyDamageInternal");
    }

    private static bool Prefix(
        IRunState runState,
        ICombatState combatState,
        Creature target,
        Creature dealer,
        decimal damage,
        ValueProp props,
        CardModel cardSource,
        ModifyDamageHookType modifyDamageHookType,
        ref decimal __result,
        ref List<AbstractModel> modifiers)
    {
        bool isPrecalculatedDamage =
            PrecalculatedDamageContext.Matches(
                cardSource,
                target,
                damage);
        ModeTransitionDamagePreviewState previewState = default;
        bool isModeTransitionPreview =
            !isPrecalculatedDamage &&
            ModeTransitionHelper.TryGetDamagePreview(
                cardSource,
                out previewState);
        decimal modifiedDamage = damage *
            (isModeTransitionPreview
                ? previewState.BaseDamageMultiplier
                : 1m);
        List<AbstractModel> appliedModifiers = new();

        if (!isPrecalculatedDamage &&
            modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
        {
            foreach (AbstractModel model in runState.IterateHookListeners(combatState))
            {
                decimal additiveAmount = model.ModifyDamageAdditive(
                    target,
                    modifiedDamage,
                    props,
                    dealer,
                    cardSource);
                modifiedDamage += additiveAmount;
                if (additiveAmount != 0m)
                {
                    appliedModifiers.Add(model);
                }
            }
        }

        decimal modeMultiplier = isModeTransitionPreview
            ? previewState.ModeDamageMultiplier
            : 1m;
        decimal manualMultiplierOffset = 0m;
        decimal finalMultiplier = 1m;
        AdditiveDamageResult groupedResult = AdditiveDamageResult.Empty;
        if (!isPrecalculatedDamage &&
            modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
        {
            bool completed = false;
            decimal hbrgoReferenceDamage = modifiedDamage;
            HbrgoSakiDamageMultiplierHelper.Begin();
            try
            {
                foreach (AbstractModel model in runState.IterateHookListeners(combatState))
                {
                    if (isModeTransitionPreview &&
                        IsTransitionedWeaponModeDamageModel(model))
                    {
                        continue;
                    }

                    decimal amountForHook = IsHbrgoModel(model)
                        ? hbrgoReferenceDamage
                        : modifiedDamage;
                    decimal multiplier = model.ModifyDamageMultiplicative(
                        target,
                        amountForHook,
                        props,
                        dealer,
                        cardSource);
                    if (IsModeDamageModel(model))
                    {
                        modeMultiplier *= multiplier;
                    }
                    else if (model is ElectronicSoldierManual)
                    {
                        manualMultiplierOffset += multiplier - 1m;
                    }
                    else if (model is HighDimensionalShellPower)
                    {
                        finalMultiplier *= multiplier;
                    }
                    else
                    {
                        modifiedDamage *= multiplier;
                    }

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
                decimal cap = model.ModifyDamageCap(
                    target,
                    props,
                    dealer,
                    cardSource);
                caps.Add((model, cap));
                minimumCap = Math.Min(minimumCap, cap);
            }
        }

        decimal manualMultiplier = Math.Max(
            0m,
            1m + manualMultiplierOffset);
        decimal hbrgoBuffOffset = groupedResult.MultiplierOffset;
        decimal hbrgoMultiplier = Math.Max(
            0m,
            modeMultiplier * manualMultiplier + hbrgoBuffOffset);
        decimal damageBeforeStrongestShield = Math.Min(
            modifiedDamage * hbrgoMultiplier * finalMultiplier,
            minimumCap);
        if (!isPrecalculatedDamage &&
            StrongestShieldDamagePatch.IsResolvingActualDamage)
        {
            StrongestShieldPower strongestShield =
                StrongestShieldPower.TryTriggerIncomingAttackDamageReduction(
                    target,
                    dealer,
                    damageBeforeStrongestShield,
                    props,
                    cardSource);
            if (strongestShield != null)
            {
                hbrgoBuffOffset +=
                    StrongestShieldPower.DamageMultiplier - 1m;
                hbrgoMultiplier = Math.Max(
                    0m,
                    modeMultiplier * manualMultiplier +
                    hbrgoBuffOffset);
                appliedModifiers.Add(strongestShield);
            }
        }

        // ((modes * manual + HBRGO buffs) * external modifiers) * final modifiers.
        modifiedDamage *= hbrgoMultiplier;
        modifiedDamage *= finalMultiplier;
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

        __result = modifiedDamage;
        modifiers = appliedModifiers;
        return false;
    }

    private static bool IsModeDamageModel(AbstractModel model)
    {
        return model is NormalStatePower or
            ReptureDualSwordsPower or
            ReptureBowPower or
            LoliFormPower;
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
            typeof(HbrgoSakiAdditiveDamagePatch).Assembly;
    }
}
