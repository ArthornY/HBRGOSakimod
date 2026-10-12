using System;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(
    typeof(DamageVar),
    nameof(DamageVar.UpdateCardPreview))]
internal static class ModeTransitionDamagePreviewPatch
{
    private static bool Prefix(
        DamageVar __instance,
        CardModel card,
        CardPreviewMode previewMode,
        Creature target,
        bool runGlobalHooks,
        out IDisposable __state)
    {
        __state = ModeTransitionHelper.BeginDamagePreview(
            card,
            runGlobalHooks);

        if (!ShouldUseSafeMultiTargetPreview(
                card,
                previewMode,
                target,
                runGlobalHooks))
        {
            return true;
        }

        UpdateEnchantedValue(__instance, card);
        Creature[] targets = card.CombatState.HittableEnemies.ToArray();
        decimal? commonDamage = null;
        bool hasCommonDamage = true;
        foreach (Creature previewTarget in targets)
        {
            decimal damage = CalculateDamagePreview(
                __instance,
                card,
                previewTarget,
                previewMode);
            if (commonDamage.HasValue &&
                (int)damage != (int)commonDamage.Value)
            {
                hasCommonDamage = false;
                break;
            }

            commonDamage ??= damage;
        }

        __instance.PreviewValue =
            commonDamage.HasValue && hasCommonDamage
                ? commonDamage.Value
                : CalculateDamagePreview(
                    __instance,
                    card,
                    null,
                    CardPreviewMode.None);
        return false;
    }

    private static Exception Finalizer(
        Exception __exception,
        IDisposable __state)
    {
        __state?.Dispose();
        return __exception;
    }

    private static bool ShouldUseSafeMultiTargetPreview(
        CardModel card,
        CardPreviewMode previewMode,
        Creature target,
        bool runGlobalHooks)
    {
        return runGlobalHooks &&
            target == null &&
            previewMode == CardPreviewMode.MultiCreatureTargeting &&
            card?.CombatState != null &&
            card.Owner?.RunState != null &&
            card.GetType().Assembly ==
                typeof(ModeTransitionDamagePreviewPatch).Assembly &&
            card.TargetType is TargetType.AllEnemies or
                TargetType.RandomEnemy &&
            card.Pile?.Type is PileType.Hand or PileType.Play;
    }

    private static void UpdateEnchantedValue(
        DamageVar damageVar,
        CardModel card)
    {
        if (card.Enchantment == null)
        {
            return;
        }

        decimal enchantedValue = damageVar.BaseValue;
        enchantedValue += card.Enchantment.EnchantDamageAdditive(
            enchantedValue,
            damageVar.Props);
        enchantedValue *= card.Enchantment.EnchantDamageMultiplicative(
            enchantedValue,
            damageVar.Props);
        if (!card.IsEnchantmentPreview)
        {
            damageVar.EnchantedValue = enchantedValue;
        }
    }

    private static decimal CalculateDamagePreview(
        DamageVar damageVar,
        CardModel card,
        Creature target,
        CardPreviewMode previewMode)
    {
        return HookCompatibility.ModifyDamage(
            card.Owner.RunState,
            card.CombatState,
            target,
            card.Owner.Creature,
            damageVar.BaseValue,
            damageVar.Props,
            card,
            ModifyDamageHookType.All,
            previewMode,
            out _);
    }
}
