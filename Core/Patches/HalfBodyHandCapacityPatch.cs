using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

// The game's limit is a static getter. Replace its callers with an owner-aware
// limit rather than changing a global value that could affect another player.
[HarmonyPatch]
internal static class HalfBodyHandCapacityPatch
{
    private static readonly MethodInfo MaximumGetter = AccessTools.PropertyGetter(
        typeof(CardPile), nameof(CardPile.MaxCardsInHand));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        byte[] token = BitConverter.GetBytes(MaximumGetter.MetadataToken);
        foreach (Type type in typeof(CardPile).Assembly.GetTypes())
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public |
                     BindingFlags.NonPublic | BindingFlags.Instance |
                     BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            byte[] body = method.GetMethodBody()?.GetILAsByteArray();
            if (body == null || !ContainsCall(body, token)) continue;
            if (!PatchProcessor.GetOriginalInstructions(method)
                    .Any(instruction => instruction.Calls(MaximumGetter))) continue;
            // This caller also needs the existing temporary swordheart slot.
            if (type.DeclaringType == typeof(CardPileCmd) &&
                type.Name.StartsWith("<Add>", StringComparison.Ordinal)) continue;
            yield return method;
        }
    }

    private static bool ContainsCall(byte[] body, byte[] token)
    {
        for (int i = 0; i + token.Length < body.Length; i++)
        {
            if (body[i] != OpCodes.Call.Value) continue;
            if (body[i + 1] == token[0] && body[i + 2] == token[1] &&
                body[i + 3] == token[2] && body[i + 4] == token[3]) return true;
        }
        return false;
    }

    internal static IReadOnlyList<CodeInstruction> LoadPlayer(MethodBase method)
    {
        ParameterInfo[] arguments = method.GetParameters();
        int index = Array.FindIndex(arguments, parameter => parameter.ParameterType == typeof(Player));
        if (index >= 0)
            return new[] { new CodeInstruction(OpCodes.Ldarg, index + (method.IsStatic ? 0 : 1)) };

        Type type = method.DeclaringType;
        MethodInfo getOwner = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Owner));
        if (!method.IsStatic && typeof(CardModel).IsAssignableFrom(type))
            return new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Callvirt, getOwner) };

        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo player = fields.SingleOrDefault(field => field.FieldType == typeof(Player));
        if (!method.IsStatic && player != null)
            return new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, player) };

        FieldInfo card = fields.SingleOrDefault(field => field.Name == "<>4__this" &&
            typeof(CardModel).IsAssignableFrom(field.FieldType));
        // Beta CardPileCmd.Add no longer captures owningPlayer. Its per-card
        // local already carries the destination owner, including ownership moves.
        card ??= fields.SingleOrDefault(field => typeof(CardModel).IsAssignableFrom(field.FieldType));
        if (!method.IsStatic && card != null)
            return new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, card),
                new CodeInstruction(OpCodes.Callvirt, getOwner) };

        throw new InvalidOperationException($"Cannot identify the player for the hand limit in {type.FullName}.{method.Name}.");
    }

    internal static IReadOnlyList<CodeInstruction> LoadCurrentCard(MethodBase method)
    {
        FieldInfo card = method.DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(CardModel));
        return new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, card) };
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        MethodInfo replacement = AccessTools.Method(typeof(HalfBodyHandHelper),
            nameof(HalfBodyHandHelper.GetEffectiveMaxCardsInHand));
        IReadOnlyList<CodeInstruction> loadPlayer = LoadPlayer(__originalMethod);
        int getterIndex = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(MaximumGetter)) { yield return instruction; continue; }
            // Draw's middle check runs after choosing its next card. Reserve the
            // incoming memory's own capacity penalty before reporting a draw.
            bool incomingDraw = getterIndex++ == 1 &&
                __originalMethod.DeclaringType.DeclaringType == typeof(CardPileCmd) &&
                __originalMethod.DeclaringType.Name.StartsWith("<Draw", StringComparison.Ordinal);
            IReadOnlyList<CodeInstruction> loads = incomingDraw ? LoadCurrentCard(__originalMethod) : loadPlayer;
            MethodInfo call = incomingDraw ? AccessTools.Method(typeof(HalfBodyHandHelper),
                nameof(HalfBodyHandHelper.GetEffectiveMaxCardsInHandForCard)) : replacement;
            for (int i = 0; i < loads.Count; i++)
            {
                CodeInstruction load = new(loads[i]);
                if (i == 0) { load.labels.AddRange(instruction.labels); load.blocks.AddRange(instruction.blocks); }
                yield return load;
            }
            yield return new CodeInstruction(OpCodes.Call, call);
        }
    }
}
