using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Native fixed effects which use literal values or power stacks instead of
// resource DynamicVars. Each row is an audited semantic boundary, not a global
// command prefix: percent heals, life steal, phase resets and damage stay out.
[HarmonyPatch]
internal static class HbrNativeFixedResourceCommandPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return RegenBody();
        yield return Body(typeof(PaperCutsPower), nameof(PaperCutsPower.AfterDamageGiven));
        yield return Body(typeof(Terminal), nameof(Terminal.AfterRoomEntered));
        yield return Body(typeof(NightTerrors), nameof(NightTerrors.AfterRestSiteHeal));
        yield return Body(typeof(CookRestSiteOption), nameof(CookRestSiteOption.OnSelect));
        yield return Body(typeof(KnowledgeDemon), "PonderMove");
        yield return Body(typeof(WaterfallGiant), "SiphonMove");
        // Its option text writes the 10 literally; see TrialNobleGuiltyTextPatch.
        yield return Body(typeof(Trial), "NobleGuilty");
    }

    private static MethodInfo RegenBody()
    {
        // Beta moved regeneration to an earlier hook. Resolving an inherited
        // empty AfterSideTurnEnd would silently miss its actual healing body.
        MethodInfo body = null;
        foreach (string name in new[] { "AfterSideTurnEnd", "BeforeSideTurnEndEarly" })
        {
            MethodInfo method = typeof(RegenPower).GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Type state = method?.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
            if (state == null) continue;
            if (body != null) throw new InvalidOperationException("Expected one declared native Regen healing hook.");
            body = AccessTools.Method(state, "MoveNext");
        }
        return body ?? throw new MissingMethodException("Missing declared native Regen healing hook.");
    }

    internal static MethodInfo Body(Type type, string name)
    {
        MethodInfo method = AccessTools.Method(type, name);
        Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        return state == null ? method : AccessTools.Method(state, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo call && call.DeclaringType == typeof(CreatureCmd))
            {
                string name = call.Name switch
                {
                    nameof(CreatureCmd.Heal) => nameof(Heal),
                    nameof(CreatureCmd.GainMaxHp) => nameof(GainMaxHp),
                    nameof(CreatureCmd.LoseMaxHp) => nameof(LoseMaxHp),
                    _ => null
                };
                if (name != null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(HbrNativeFixedResourceCommandPatch), name,
                        call.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
                }
            }
            yield return instruction;
        }
    }

    private static Task Heal(Creature creature, decimal amount, bool playAnim) =>
        CreatureCmd.Heal(creature, HbrResourceValueHelper.Fixed(creature, amount), playAnim);
    private static Task GainMaxHp(Creature creature, decimal amount) =>
        CreatureCmd.GainMaxHp(creature, HbrResourceValueHelper.Fixed(creature, amount));
    private static Task LoseMaxHp(PlayerChoiceContext choiceContext, Creature creature, decimal amount, bool isFromCard) =>
        CreatureCommandCompatibility.LoseMaxHp(choiceContext, creature,
            HbrResourceValueHelper.Fixed(creature, amount), isFromCard);
    private static Task LoseMaxHp(PlayerChoiceContext choiceContext, Creature creature, decimal amount, CardModel cardSource) =>
        CreatureCommandCompatibility.LoseMaxHp(choiceContext, creature,
            HbrResourceValueHelper.Fixed(creature, amount), cardSource != null, cardSource);
}

// Corrupted loses a literal 2 HP and Regret 1 HP per card in hand, passed
// straight to the decimal Damage command on their owner. Only that amount
// converts; flags, source and the command itself stay native. Beta adds a
// CardPlay argument, so the wrapper copies whichever signature is present.
[HarmonyPatch]
internal static class HbrNativeFixedHpLossCommandPatch
{
    private static readonly List<DynamicMethod> Wrappers = new();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return HbrNativeFixedResourceCommandPatch.Body(typeof(Corrupted), nameof(Corrupted.OnPlay));
        yield return HbrNativeFixedResourceCommandPatch.Body(typeof(Regret), "OnTurnEndInHand");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        int matches = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo call && call.DeclaringType == typeof(CreatureCmd) &&
                call.Name == nameof(CreatureCmd.Damage) &&
                call.GetParameters().Any(parameter => parameter.ParameterType == typeof(decimal)))
            {
                instruction.operand = Wrapper(call);
                matches++;
            }
            yield return instruction;
        }
        if (matches != 1)
            throw new InvalidOperationException("Expected one fixed HP loss command in " +
                __originalMethod.DeclaringType?.FullName);
    }

    private static MethodInfo Wrapper(MethodInfo command)
    {
        ParameterInfo[] parameters = command.GetParameters();
        int target = Array.FindIndex(parameters, parameter => parameter.Name == "target" &&
            parameter.ParameterType == typeof(Creature));
        int amount = Array.FindIndex(parameters, parameter => parameter.ParameterType == typeof(decimal));
        if (target < 0 || amount < 0)
            throw new InvalidOperationException("Expected a single-target decimal Damage command.");
        DynamicMethod wrapper = new("HbrFixedHpLoss_" + Wrappers.Count, command.ReturnType,
            Array.ConvertAll(parameters, parameter => parameter.ParameterType),
            typeof(HbrNativeFixedHpLossCommandPatch), true);
        ILGenerator il = wrapper.GetILGenerator();
        for (int index = 0; index < parameters.Length; index++)
        {
            if (index == amount) il.Emit(OpCodes.Ldarg, (short)target);
            il.Emit(OpCodes.Ldarg, (short)index);
            if (index == amount)
                il.Emit(OpCodes.Call, AccessTools.Method(typeof(HbrResourceValueHelper), nameof(HbrResourceValueHelper.Fixed)));
        }
        il.Emit(OpCodes.Call, command);
        il.Emit(OpCodes.Ret);
        Wrappers.Add(wrapper);
        return wrapper;
    }
}
