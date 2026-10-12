using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class MonsterCardRecipientPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type[] types =
        {
            typeof(Aeonglass), typeof(Chomper), typeof(EyeWithTeeth),
            typeof(HauntedShip), typeof(LeafSlimeS), typeof(LeafSlimeM),
            typeof(MechaKnight), typeof(Myte), typeof(PhrogParasite),
            typeof(SlimedBerserker), typeof(TestSubject), typeof(TwigSlimeM),
            typeof(Vantom), typeof(Wriggler), typeof(PainfulStabsPower),
            typeof(WitheringPresencePower), typeof(FlatHand)
        };
        foreach (Type type in types)
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
            {
                MethodInfo body = GetAsyncBody(method);
                if (body != null && PatchProcessor.GetOriginalInstructions(body)
                        .Any(instruction => IsCardInsertion(instruction.operand as MethodInfo)))
                {
                    yield return body;
                }
            }
        }
    }

    internal static MethodInfo GetAsyncBody(MethodInfo method) =>
        method.GetCustomAttribute<AsyncStateMachineAttribute>() is { } attribute
            ? AccessTools.Method(attribute.StateMachineType, "MoveNext")
            : null;

    private static bool IsCardInsertion(MethodInfo method) =>
        method?.DeclaringType == typeof(CardPileCmd) &&
        method.Name == nameof(CardPileCmd.AddToCombatAndPreview) &&
        method.IsGenericMethod && method.GetParameters().Length == 5;

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        // Patch callers, not shared generic method bodies, to retain each status-card type.
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && IsCardInsertion(method))
            {
                string replacement = method.GetParameters()[0].ParameterType == typeof(Creature)
                    ? nameof(MonsterCardRecipientHelper.AddToPlayer)
                    : nameof(MonsterCardRecipientHelper.AddToPlayers);
                yield return new CodeInstruction(instruction)
                {
                    opcode = OpCodes.Call,
                    operand = AccessTools.Method(typeof(MonsterCardRecipientHelper), replacement)
                        .MakeGenericMethod(method.GetGenericArguments())
                };
            }
            else
            {
                yield return instruction;
            }
        }
    }
}

[HarmonyPatch]
internal static class MonsterDirectCardRecipientPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Noisebot), "NoiseMove");
        yield return AccessTools.Method(typeof(SoulFysh), "BeckonMove");
        yield return AccessTools.Method(typeof(SoulFysh), "GazeMove");
        yield return AccessTools.Method(typeof(SpectralKnight), "HexMove");
        yield return AccessTools.Method(typeof(CeremonialBeast), "BeastCryMove");
        yield return AccessTools.Method(typeof(MagiKnight), "DampenMove");
    }

    private static void Prefix(ref IReadOnlyList<Creature> targets)
    {
        // These lists are only used for card insertion or card afflictions.
        // Hex/Ringing/Dampen dereference the recipient's player deck when applied.
        // Keep the move itself so animations and state transitions still finish.
        targets = MonsterCardRecipientHelper.PlayerTargets(targets).ToArray();
    }
}

[HarmonyPatch(typeof(KnowledgeDemon), "ChooseCurse")]
internal static class MonsterCurseRecipientPatch
{
    private static bool Prefix(Creature target, ref Task __result)
    {
        if (MonsterCardRecipientHelper.IsPlayer(target))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class MonsterLiquifyCardRecipientPatch
{
    private static MethodBase TargetMethod() => MonsterCardRecipientPatch.GetAsyncBody(
        AccessTools.Method(typeof(TheInsatiable), "LiquifyMove"));

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> result = instructions.ToList();
        int[] reads = result.Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction.opcode == OpCodes.Ldfld &&
                item.instruction.operand is FieldInfo field && field.Name == "targets")
            .Select(item => item.index)
            .ToArray();
        if (reads.Length != 2 || reads.Any(index =>
                index + 1 >= result.Count ||
                result[index + 1].operand is not MethodInfo { Name: "GetEnumerator" }))
        {
            throw new InvalidOperationException("Expected separate sandpit and card target loops in LiquifyMove.");
        }

        // Both effects require a player: SandpitPower dereferences Target.Player
        // when moving/eating its target, before the status-card loop even starts.
        // Filter each loop so phantom enemies receive neither player-only effect,
        // while the move still finishes and advances to the next attack normally.
        for (int index = reads.Length - 1; index >= 0; index--)
        {
            result.Insert(reads[index] + 1, new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(MonsterCardRecipientHelper),
                    nameof(MonsterCardRecipientHelper.PlayerTargets))));
        }
        return result;
    }
}
