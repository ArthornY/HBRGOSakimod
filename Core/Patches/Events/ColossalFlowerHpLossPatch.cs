using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;

namespace HBRGOSakiMod.Core.Patches.Events;

// Colossal Flower's digs lose _prizeDamage[NumberOfDigs] HP from a static table,
// not a variable, and their option text writes the amount literally. Scale the
// table reads that reach the HP loss command or an option's lethal check, and tag
// those options so the literal-number text substitution shows the same amount.
[HarmonyPatch]
internal static class ColossalFlowerHpLossPatch
{
    private static readonly FieldInfo PrizeDamage = AccessTools.Field(typeof(ColossalFlower), "_prizeDamage");
    private static readonly MethodInfo NumberOfDigs = AccessTools.PropertyGetter(typeof(ColossalFlower), "NumberOfDigs");
    private static readonly MethodInfo ThatDoesDamage =
        AccessTools.Method(typeof(EventOption), nameof(EventOption.ThatDoesDamage));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ColossalFlower), "GenerateInitialOptions");
        yield return Body("ReachDeeper");
        yield return Body("DealReachDeeperDamage");
    }

    private static MethodInfo Body(string name)
    {
        MethodInfo method = AccessTools.Method(typeof(ColossalFlower), name);
        Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType ??
            throw new MissingMethodException("Missing native Colossal Flower body " + name);
        return AccessTools.Method(state, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase original)
    {
        List<CodeInstruction> body = instructions.ToList();
        int scaled = 0;
        for (int index = 0; index + 5 < body.Count; index++)
        {
            // ldsfld _prizeDamage; <load flower>; NumberOfDigs; ldelem.i4; int-to-decimal
            if (!body[index].LoadsField(PrizeDamage) || !body[index + 2].Calls(NumberOfDigs) ||
                body[index + 3].opcode != OpCodes.Ldelem_I4 || !IsDecimalConversion(body[index + 4])) continue;
            CodeInstruction loadFlower = body[index + 1];
            int next = index + 5;
            if (body[next].labels.Count > 0)
                throw new InvalidOperationException("Unexpected branch into Colossal Flower damage in " + original.Name);
            if (body[next].Calls(ThatDoesDamage))
            {
                // option, damage -> option, damage, flower -> ThatDoesPrizeDamage
                body[next].opcode = OpCodes.Call;
                body[next].operand = AccessTools.Method(typeof(ColossalFlowerHpLossPatch), nameof(ThatDoesPrizeDamage));
                body.Insert(next, loadFlower.Clone());
            }
            else if (ReachesDamage(body, next))
            {
                body.InsertRange(next, new[]
                {
                    loadFlower.Clone(),
                    new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(ColossalFlowerHpLossPatch), nameof(ScalePrizeDamage)))
                });
            }
            else continue;
            scaled++;
        }
        if (scaled == 0)
            throw new InvalidOperationException("Colossal Flower HP loss read not found in " + original.Name);
        return body;
    }

    private static bool IsDecimalConversion(CodeInstruction instruction) =>
        instruction.operand is MethodInfo { Name: "op_Implicit" } method && method.DeclaringType == typeof(decimal);

    // The damage command's props and null arguments follow the amount directly.
    private static bool ReachesDamage(List<CodeInstruction> body, int index)
    {
        for (int next = index; next < body.Count; next++)
        {
            CodeInstruction instruction = body[next];
            if (instruction.opcode == OpCodes.Ldnull || instruction.LoadsConstant()) continue;
            return instruction.operand is MethodInfo method && method.DeclaringType == typeof(CreatureCmd) &&
                method.Name == nameof(CreatureCmd.Damage);
        }
        return false;
    }

    private static decimal ScalePrizeDamage(decimal damage, ColossalFlower flower) =>
        HbrResourceValueHelper.Fixed(flower.Owner?.Creature, damage);

    private static EventOption ThatDoesPrizeDamage(EventOption option, decimal damage, ColossalFlower flower)
    {
        Creature owner = flower.Owner?.Creature;
        HbrNativeFixedModifierDescriptionPatch.Tag(option.Description, owner, (int)damage);
        return option.ThatDoesDamage(HbrResourceValueHelper.Fixed(owner, damage));
    }
}

// The flower only appears while every player has more HP than all three digs
// cost; the scaled digs raise that threshold by the same rule.
[HarmonyPatch]
internal static class ColossalFlowerMinimumHpPatch
{
    private static readonly MethodInfo CurrentHp =
        AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.CurrentHp));

    // IsAllowed passes a cached lambda to All(); patch the lambda it creates.
    private static MethodBase TargetMethod() =>
        PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(ColossalFlower), "IsAllowed"))
            .Where(instruction => instruction.opcode == OpCodes.Ldftn)
            .Select(instruction => (MethodInfo)instruction.operand)
            .Single(method => method.DeclaringType?.DeclaringType == typeof(ColossalFlower));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> body = instructions.ToList();
        int hp = body.FindIndex(instruction => instruction.Calls(CurrentHp));
        if (hp < 0 || hp + 1 >= body.Count || !body[hp + 1].LoadsConstant() || body[hp + 1].labels.Count > 0)
            throw new InvalidOperationException("Colossal Flower minimum HP check not found.");
        // hp, threshold -> hp, player, threshold -> MinimumHp
        body.Insert(hp + 1, new CodeInstruction(OpCodes.Ldarg_1));
        body.Insert(hp + 3, new CodeInstruction(OpCodes.Call,
            AccessTools.Method(typeof(ColossalFlowerMinimumHpPatch), nameof(MinimumHp))));
        return body;
    }

    private static int MinimumHp(Player player, int threshold) =>
        HbrResourceValueHelper.FixedInt(player.Creature, threshold);
}
