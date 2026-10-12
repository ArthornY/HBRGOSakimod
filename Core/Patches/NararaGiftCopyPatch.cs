using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
internal static class NararaGiftModelClonePatch
{
    private static void Postfix(AbstractModel __instance, AbstractModel __result)
    {
        if (__instance is CardModel source && __result is CardModel copy)
            NararaGiftCopyHelper.Inherit(source, copy);
    }
}

[HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.Clone))]
internal static class NararaGiftCostClonePatch
{
    private static void Postfix(CardEnergyCost __instance, CardEnergyCost __result) =>
        NararaGiftCopyHelper.InheritCost(__instance, __result);
}

[HarmonyPatch]
internal static class NararaGiftUpgradeTraitsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.UpgradeInternal));
        yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.DowngradeInternal));
    }

    private static void Postfix(CardModel __instance) =>
        NararaGiftCopyHelper.EnsureTraits(__instance);
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.HasEnoughResourcesFor))]
internal static class NararaGiftXAffordabilityPatch
{
    // The extra charge is energy, including when an effect could otherwise pay
    // an ordinary fixed-cost deficit with stars or swordheart.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        PlayerCombatState __instance, CardModel card,
        ref UnplayableReason reason, ref bool __result)
    {
        if (NararaGiftCopyHelper.GetXSurcharge(card) > __instance.Energy)
        {
            reason |= UnplayableReason.EnergyCostTooHigh;
            __result = false;
        }
    }
}

[HarmonyPatch]
internal static class NararaGiftXCapturePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodInfo method in new[]
        {
            AccessTools.Method(typeof(CardModel), "SpendEnergy"),
            AccessTools.Method(typeof(CardCmd), nameof(CardCmd.AutoPlay))
        })
        {
            AsyncStateMachineAttribute stateMachine =
                method?.GetCustomAttribute<AsyncStateMachineAttribute>() ??
                throw new MissingMethodException("Missing card energy-capture state machine.");
            yield return AccessTools.Method(stateMachine.StateMachineType, "MoveNext");
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        MethodInfo setter = AccessTools.PropertySetter(
            typeof(CardEnergyCost), nameof(CardEnergyCost.CapturedXValue));
        MethodInfo replacement = AccessTools.Method(
            typeof(NararaGiftCopyHelper), nameof(NararaGiftCopyHelper.CaptureX));
        List<CodeInstruction> result = instructions.ToList();
        int replaced = 0;
        for (int index = 0; index < result.Count; index++)
        {
            if (!result[index].Calls(setter))
                continue;
            result[index] = new CodeInstruction(result[index])
            {
                opcode = OpCodes.Call,
                operand = replacement
            };
            replaced++;
        }
        if (replaced != 1)
            throw new InvalidOperationException(
                $"Expected one X capture in {__originalMethod.DeclaringType?.FullName}; found {replaced}.");
        return result;
    }
}

[HarmonyPatch(typeof(DualSwordsCopyHelper), nameof(DualSwordsCopyHelper.RecordXEnergySpent))]
internal static class NararaGiftDualSwordsXCapturePatch
{
    // This helper explicitly receives total energy spent, while its consumers
    // overwrite CapturedXValue. Store the already-adjusted X for gift copies.
    private static void Prefix(CardModel card, ref int energySpent) =>
        energySpent = NararaGiftCopyHelper.SubtractXSurcharge(card, energySpent);
}

[HarmonyPatch(typeof(DualSwordsCopyHelper), nameof(DualSwordsCopyHelper.TakeXEnergySpent))]
internal static class NararaGiftDualSwordsXFallbackPatch
{
    // Blade Dance, Dragon Arrow and Pale Moon pass total energy spent as their
    // fallback; garden X cards pass CapturedXValue. Both must use the same
    // already-adjusted capture, including the second play in a replay series.
    private static void Prefix(CardModel card, ref int fallback)
    {
        if (NararaGiftCopyHelper.GetXSurcharge(card) > 0)
            fallback = card.EnergyCost.CapturedXValue;
    }
}

[HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
internal static class NararaGiftXCostLabelPatch
{
    private sealed record OriginalMinimum(int Size);
    private static readonly ConditionalWeakTable<MegaLabel, OriginalMinimum> FontMinimums = new();

    private static void Prefix(NCard __instance, MegaLabel ____energyLabel)
    {
        bool showSurcharge = __instance.Visibility == ModelVisibility.Visible &&
            NararaGiftCopyHelper.GetXSurcharge(__instance.Model) > 0;
        if (showSurcharge)
        {
            OriginalMinimum original = FontMinimums.GetValue(
                ____energyLabel, label => new OriginalMinimum(label.MinFontSize));
            // The original 46-pixel energy label has a 22px minimum, which cannot
            // fit larger X+n values. Let MegaLabel fit this instance naturally.
            ____energyLabel.MinFontSize = Math.Min(original.Size, 8);
        }
        else if (FontMinimums.TryGetValue(____energyLabel, out OriginalMinimum original))
        {
            // NCard nodes are pooled: restore the normal range before the base
            // method renders another card or the hidden-card question mark.
            ____energyLabel.MinFontSize = original.Size;
            FontMinimums.Remove(____energyLabel);
        }
    }

    private static void Postfix(NCard __instance, MegaLabel ____energyLabel)
    {
        if (__instance.Visibility == ModelVisibility.Visible &&
            NararaGiftCopyHelper.GetXSurcharge(__instance.Model) > 0)
            ____energyLabel.SetTextAutoSize(NararaGiftCopyHelper.GetXCostText(__instance.Model));
    }
}
