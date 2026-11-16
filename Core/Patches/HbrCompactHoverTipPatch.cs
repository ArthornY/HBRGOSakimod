using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class HbrCompactHoverTipPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return RequireMethod(typeof(NPower), "OnHovered", Type.EmptyTypes);
        yield return RequireMethod(typeof(NPower), "ShowPowerHoverTips", new[] { typeof(CombatState) });
        yield return RequireMethod(typeof(NCardHolder), "CreateHoverTips", Type.EmptyTypes);
        yield return RequireMethod(typeof(NPreviewCardHolder), "CreateHoverTips", Type.EmptyTypes);
        yield return RequireMethod(typeof(NInspectCardScreen), "UpdateCardDisplay", Type.EmptyTypes);
        yield return RequireMethod(typeof(NMerchantCard), "CreateHoverTip", Type.EmptyTypes);
        yield return RequireMethod(typeof(NCreature), "OnFocus", Type.EmptyTypes);
        yield return RequireMethod(typeof(NCreature), "ShowCreatureHoverTips", new[] { typeof(CombatState) });
        yield return RequireMethod(typeof(NRelicInventoryHolder), "OnFocus", Type.EmptyTypes);
    }

    private static MethodInfo RequireMethod(Type type, string name, Type[] parameters) =>
        AccessTools.DeclaredMethod(type, name, parameters)
        ?? throw new MissingMethodException(type.FullName, name);

    private static (Type Model, string Helper) Source(Type caller) =>
        caller == typeof(NPower) ? (typeof(PowerModel), nameof(HbrCompactHoverTipHelper.ForPower))
        : caller == typeof(NCreature) ? (typeof(Creature), nameof(HbrCompactHoverTipHelper.ForCreature))
        : caller == typeof(NRelicInventoryHolder) ? (typeof(RelicModel), nameof(HbrCompactHoverTipHelper.ForRelic))
        : (typeof(CardModel), nameof(HbrCompactHoverTipHelper.ForCard));

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        (Type modelType, string helperName) = Source(__originalMethod.DeclaringType);
        MethodInfo getter = AccessTools.PropertyGetter(modelType, nameof(CardModel.HoverTips));
        MethodInfo replacement = RequireMethod(typeof(HbrCompactHoverTipHelper), helperName,
            new[] { modelType });

        List<CodeInstruction> result = instructions.ToList();
        int[] matches = result.Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction.Calls(getter))
            .Select(item => item.index)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one {modelType.Name}.HoverTips call in " +
                $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}; found {matches.Length}.");
        }

        // Only these UI callers use compact tips; full descriptions and menu callers stay intact.
        int index = matches[0];
        result[index] = new CodeInstruction(result[index])
        {
            opcode = OpCodes.Call,
            operand = replacement
        };
        return result;
    }

    private static void Prefix(out int __state) => __state = HbrHoverDetail.Builds;

    // Remember who opened the tooltip, so holding the full-text key can rebuild it.
    private static void Postfix(object __instance, int __state)
    {
        if (HbrHoverDetail.Builds != __state) HbrHoverDetail.Track(__instance);
    }
}
