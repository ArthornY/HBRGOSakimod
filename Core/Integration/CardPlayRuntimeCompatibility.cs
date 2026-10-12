#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Integration;

internal static class CardPlayRuntimeCompatibility
{
    private const string StableHookName =
        "ModifyCardPlayResultPileTypeAndPosition";
    private const string BetaHookName = "ModifyCardPlayResultLocation";

    internal static void Install(Harmony harmony)
    {
        MethodInfo? target = AccessTools.Method(typeof(Hook), StableHookName)
            ?? AccessTools.Method(typeof(Hook), BetaHookName);
        if (target == null || !target.ReturnType.IsValueType)
        {
            return;
        }

        MethodInfo postfixFactory = AccessTools.Method(
            typeof(CardPlayRuntimeCompatibility),
            nameof(CreatePostfix))!;
        harmony.Patch(
            target,
            postfix: new HarmonyMethod(postfixFactory));
    }

    private static MethodInfo CreatePostfix(MethodBase originalMethod)
    {
        Type resultType = ((MethodInfo)originalMethod).ReturnType;
        DynamicMethod postfix = new(
            "HBRGOSakiMod_ModifyCardPlayResult",
            typeof(void),
            new[] { resultType.MakeByRefType(), typeof(CardModel) },
            typeof(CardPlayRuntimeCompatibility).Module,
            skipVisibility: true);
        postfix.DefineParameter(1, ParameterAttributes.None, "__result");
        postfix.DefineParameter(2, ParameterAttributes.None, "card");

        MethodInfo modifier = AccessTools.Method(
            typeof(CardPlayRuntimeCompatibility),
            nameof(ModifyResult))!;
        ILGenerator il = postfix.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldobj, resultType);
        il.Emit(OpCodes.Box, resultType);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, modifier);
        il.Emit(OpCodes.Unbox_Any, resultType);
        il.Emit(OpCodes.Stobj, resultType);
        il.Emit(OpCodes.Ret);
        return postfix;
    }

    private static object ModifyResult(object result, CardModel card)
    {
        FieldInfo? pileTypeField = result.GetType().GetField(
                "pileType",
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance)
            ?? result.GetType().GetField(
                "Item1",
                BindingFlags.Public | BindingFlags.Instance);
        // Both game versions reserve the destination before playing the card,
        // but use different hook names and result types. Reuse the shared bridge
        // so the first-play return never references the removed stable-only API.
        if (card is HerSongIsMyScreamHbrgoSaki scream &&
            pileTypeField?.GetValue(result) is PileType originalPile)
        {
            pileTypeField.SetValue(result, scream.GetFirstPlayResultPile(originalPile));
        }
        if (pileTypeField?.GetValue(result) is not PileType.Exhaust)
        {
            return result;
        }

        PhantomPower? phantomPower = card.CombatState?.Allies
            .Select(creature => creature.GetPower<PhantomPower>())
            .FirstOrDefault(power =>
                power != null &&
                !power.IsSummonedMinion &&
                power.Owner.IsAlive &&
                power.Owner.PetOwner == card.Owner);
        if (phantomPower?.TryPreventCardExhaust(card) != true)
        {
            return result;
        }

        pileTypeField.SetValue(result, PileType.Discard);
        return result;
    }
}
