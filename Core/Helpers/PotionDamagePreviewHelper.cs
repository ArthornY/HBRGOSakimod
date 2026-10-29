using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using HBRGOSakiMod.Core.Patches.Combat;
using HBRGOSakiMod.Core.Models.Potions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PotionDamagePreviewHelper
{
    private static readonly AccessTools.FieldRef<DynamicVar, decimal> DisplayBaseValue =
        AccessTools.FieldRefAccess<DynamicVar, decimal>("_baseValue");
    private static readonly AccessTools.FieldRef<DynamicVar, decimal> DisplayPreviewValue =
        AccessTools.FieldRefAccess<DynamicVar, decimal>("_previewValue");

    internal static bool HasDamage(PotionModel potion) =>
        potion?.DynamicVars.Values.OfType<DamageVar>().Any() == true;

    internal static bool CanPreview(PotionModel potion)
    {
        if (potion?.IsMutable != true || potion.HasBeenRemovedFromState ||
            !HasDamage(potion))
        {
            return false;
        }

        var owner = potion.Owner;
        ICombatState combat = owner?.Creature?.CombatState;
        return owner?.Creature?.IsAlive == true && combat != null &&
            owner.RunState != null && ReferenceEquals(combat.RunState, owner.RunState) &&
            CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsEnding;
    }

    internal static void UpdateDescription(
        PotionModel potion,
        LocString description,
        Creature target = null)
    {
        // This potion has no damage preview; its authored fixed DP value still
        // needs the same live/canonical resource display as its use command.
        if (description != null && potion is DpPotionHbrgoSaki)
        {
            decimal raw = potion.DynamicVars["Dp"].BaseValue;
            description.Add("ResourceDp", potion.IsMutable && potion.Owner != null
                ? HbrResourceValueHelper.Fixed(potion.Owner.Creature, raw)
                : raw * HbrResourceValueHelper.FixedResourceScale);
        }
        if (description == null || !CanPreview(potion))
        {
            return;
        }

        Creature[] targets = GetGroupTargets(potion);
        ICombatState combat = potion.Owner.Creature.CombatState;
        Creature selectedTarget = target != null &&
            ReferenceEquals(target.CombatState, combat) &&
            combat.Creatures.Contains(target) && potion.IsValidTarget(target)
                ? target : null;
        string rawText = description.GetRawText();

        foreach (DamageVar variable in potion.DynamicVars.Values.OfType<DamageVar>())
        {
            bool usesFormatting = UsesVariableFormatting(rawText, variable.Name);
            if (targets.Length == 0)
            {
                SetDisplayedValue(description, variable,
                    CalculateDamage(potion, variable, selectedTarget), usesFormatting);
                continue;
            }

            decimal[] amounts = targets.Select(creature =>
                CalculateDamage(potion, variable, creature)).ToArray();
            decimal minimum = amounts.Min();
            decimal maximum = amounts.Max();
            if (minimum == maximum)
            {
                SetDisplayedValue(description, variable, minimum, usesFormatting);
            }
            else if (!usesFormatting)
            {
                // A range for plain placeholders avoids promising identical damage
                // to all targets. Numeric formatters are handled separately below.
                description.Add(variable.Name,
                    FormatAmount(minimum) + "–" + FormatAmount(maximum));
            }
            else
            {
                // A plural/diff formatter needs a number, not a range.
                // Keep its untargeted baseline; GetTargetDetails lists actual values.
                SetDisplayedValue(description, variable,
                    CalculateDamage(potion, variable, null), usesFormatting: true);
            }
        }
    }

    internal static string GetTargetDetails(PotionModel potion)
    {
        if (!CanPreview(potion))
        {
            return string.Empty;
        }

        Creature[] targets = GetGroupTargets(potion);
        if (targets.Length < 2)
        {
            return string.Empty;
        }

        DamageVar[] variables = potion.DynamicVars.Values.OfType<DamageVar>().ToArray();
        decimal[][] amounts = targets.Select(target => variables.Select(variable =>
            CalculateDamage(potion, variable, target)).ToArray()).ToArray();
        if (amounts.Skip(1).All(values => values.SequenceEqual(amounts[0])))
        {
            return string.Empty;
        }

        StringBuilder text = new();
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            if (targetIndex > 0)
            {
                text.Append('\n');
            }

            text.Append(EscapeName(targets[targetIndex].Name)).Append(": ");
            for (int variableIndex = 0; variableIndex < variables.Length; variableIndex++)
            {
                if (variableIndex > 0)
                {
                    text.Append("; ");
                }

                if (variables.Length > 1)
                {
                    text.Append(EscapeName(variables[variableIndex].Name)).Append(": ");
                }

                text.Append("[blue]")
                    .Append(FormatAmount(amounts[targetIndex][variableIndex]))
                    .Append("[/blue]");
            }
        }

        return text.ToString();
    }

    internal static decimal CalculateDamage(
        PotionModel potion,
        DamageVar variable,
        Creature target)
    {
        if (variable == null)
        {
            return 0m;
        }

        if (!CanPreview(potion) || target != null &&
            (!ReferenceEquals(target.CombatState, potion.Owner.Creature.CombatState) ||
             !target.IsAlive))
        {
            return Math.Floor(Math.Max(0m,
                DamageLimitHelper.CapSingleHit(variable.BaseValue)));
        }

        // A UI refresh can be nested inside an actual damage hook. Mark this as
        // a preview so shield rolls and raw-damage captures still belong to that hit.
        StrongestShieldDamagePatch.BeginIntentPreview();
        try
        {
            decimal amount = HookCompatibility.ModifyDamage(
                potion.Owner.RunState,
                potion.Owner.Creature.CombatState,
                target,
                potion.Owner.Creature,
                variable.BaseValue,
                variable.Props,
                cardSource: null,
                ModifyDamageHookType.All,
                CardPreviewMode.Normal,
                out _);
            // Native damage truncates nonnegative amounts when spending DP/HP.
            return Math.Floor(Math.Max(0m, DamageLimitHelper.CapSingleHit(amount)));
        }
        finally
        {
            StrongestShieldDamagePatch.EndIntentPreview();
        }
    }

    private static Creature[] GetGroupTargets(PotionModel potion)
    {
        ICombatState combat = potion.Owner.Creature.CombatState;
        if (potion is FoulPotion)
        {
            // Foul Potion also damages players, including its owner, but not pets.
            return combat.Creatures.Where(creature =>
                ReferenceEquals(creature.CombatState, combat) &&
                creature.IsAlive && !creature.IsPet).ToArray();
        }

        return potion.TargetType is TargetType.AllEnemies or TargetType.RandomEnemy
            ? combat.HittableEnemies.Where(creature =>
                ReferenceEquals(creature.CombatState, combat) && creature.IsAlive).ToArray()
            : Array.Empty<Creature>();
    }

    private static bool UsesVariableFormatting(string rawText, string variableName) =>
        Regex.IsMatch(rawText,
            @"\{\s*" + Regex.Escape(variableName) + @"(?:\s*[:.]|\[)",
            RegexOptions.CultureInvariant);

    private static void SetDisplayedValue(
        LocString description,
        DamageVar variable,
        decimal amount,
        bool usesFormatting)
    {
        if (!usesFormatting)
        {
            description.Add(variable.Name, amount);
            return;
        }

        DamageVar display = (DamageVar)variable.Clone();
        // Only this detached display clone bypasses DynamicVar's lower native cap.
        // Preserve the original enchanted value for diff() highlighting.
        DisplayBaseValue(display) = amount;
        DisplayPreviewValue(display) = amount;
        description.Add(display);
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0", CultureInfo.InvariantCulture);

    private static string EscapeName(string name) =>
        name.Replace("[", "[lb]", StringComparison.Ordinal);
}
