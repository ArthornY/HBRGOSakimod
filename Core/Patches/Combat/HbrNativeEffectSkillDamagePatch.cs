using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Preserve the native DamageVar adapter, raw storage and command attribution.
// Only its audited effect sources declare a skill calculation scope around the
// final decimal command. Runtime signatures differ by CardPlay on beta.
[HarmonyPatch]
internal static class HbrNativeEffectSkillDamagePatch
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private sealed class Command
    {
        internal Func<object[], Task<IEnumerable<DamageResult>>> Invoke;
        internal int Target;
        internal int Dealer;
        internal int Card;
        internal int Amount;
        internal int Props;
    }
    private static readonly List<Command> Commands = new();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodInfo method in typeof(CreatureCmd).GetMethods(Declared))
        {
            if (method.Name != nameof(CreatureCmd.Damage) ||
                !method.GetParameters().Any(parameter => parameter.ParameterType == typeof(DamageVar))) continue;
            Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                ?? throw new InvalidOperationException("Expected native DamageVar async adapter.");
            yield return AccessTools.Method(state, "MoveNext");
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        FieldInfo variable = __originalMethod.DeclaringType.GetFields(Declared)
            .Single(field => field.FieldType == typeof(DamageVar));
        int matches = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(CreatureCmd) &&
                method.Name == nameof(CreatureCmd.Damage) &&
                method.GetParameters().Any(parameter => parameter.ParameterType == typeof(decimal)))
            {
                CodeInstruction loadState = new(OpCodes.Ldarg_0);
                loadState.labels.AddRange(instruction.labels);
                loadState.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear();
                instruction.blocks.Clear();
                yield return loadState;
                yield return new CodeInstruction(OpCodes.Ldfld, variable);
                instruction.opcode = OpCodes.Call;
                instruction.operand = CreateWrapper(method, typeof(DamageVar));
                matches++;
            }
            yield return instruction;
        }
        if (matches != 1)
            throw new InvalidOperationException("Expected one native DamageVar decimal command.");
    }

    internal static MethodInfo CreateWrapper(MethodInfo method, Type sourceType)
    {
        ParameterInfo[] parameters = method.GetParameters();
        ParameterExpression arguments = Expression.Parameter(typeof(object[]), "arguments");
        Expression[] values = parameters.Select((parameter, index) => Expression.Convert(
            Expression.ArrayIndex(arguments, Expression.Constant(index)), parameter.ParameterType)).ToArray();
        Command command = new()
        {
            Invoke = Expression.Lambda<Func<object[], Task<IEnumerable<DamageResult>>>>(
                Expression.Call(method, values), arguments).Compile(),
            Target = Array.FindIndex(parameters, parameter => parameter.Name == "target" &&
                parameter.ParameterType == typeof(Creature)),
            Dealer = Array.FindIndex(parameters, parameter => parameter.Name == "dealer" &&
                parameter.ParameterType == typeof(Creature)),
            Card = Array.FindIndex(parameters, parameter => parameter.ParameterType == typeof(CardModel)),
            Amount = Array.FindIndex(parameters, parameter => parameter.ParameterType == typeof(decimal)),
            Props = Array.FindIndex(parameters, parameter => parameter.ParameterType == typeof(ValueProp))
        };
        if (command.Amount < 0 || command.Props < 0)
            throw new InvalidOperationException("Native effect command has no damage amount or props.");
        int id = Commands.Count;
        Commands.Add(command);
        Type[] signature = parameters.Select(parameter => parameter.ParameterType)
            .Append(sourceType).ToArray();
        DynamicMethod wrapper = new("HbrEffectDamage_" + id, method.ReturnType, signature,
            typeof(HbrNativeEffectSkillDamagePatch), true);
        ILGenerator il = wrapper.GetILGenerator();
        il.Emit(OpCodes.Ldc_I4, id);
        il.Emit(OpCodes.Ldarg, (short)parameters.Length);
        il.Emit(OpCodes.Ldc_I4, parameters.Length);
        il.Emit(OpCodes.Newarr, typeof(object));
        for (int index = 0; index < parameters.Length; index++)
        {
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldc_I4, index);
            il.Emit(OpCodes.Ldarg, (short)index);
            if (parameters[index].ParameterType.IsValueType) il.Emit(OpCodes.Box, parameters[index].ParameterType);
            il.Emit(OpCodes.Stelem_Ref);
        }
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(HbrNativeEffectSkillDamagePatch), nameof(Run)));
        il.Emit(OpCodes.Ret);
        return wrapper;
    }

    private static async Task<IEnumerable<DamageResult>> Run(int id, object source, object[] arguments)
    {
        Command command = Commands[id];
        Creature statSource;
        if (source is DamageVar variable)
        {
            if (!HbrNativeEffectSkillHelper.IsSkillVariable(variable) ||
                !HbrBattleStatsHelper.IsEnabled(HbrNativeEffectSkillHelper.GetModeOwner(variable)))
                return await command.Invoke(arguments);
            statSource = null;
        }
        else if (source is MegaCrit.Sts2.Core.Models.Powers.ThornsPower thorns &&
                 thorns.IsMutable && HbrBattleStatsHelper.IsEnabled(thorns.Owner))
            statSource = thorns.Owner;
        else if (source is PotionModel potion && HbrNativeEffectSkillHelper.IsSkillModel(potion) &&
                 HbrNativeEffectSkillHelper.IsEnabled(potion))
            statSource = HbrNativeEffectSkillHelper.GetModeOwner(potion);
        else
            return await command.Invoke(arguments);
        Creature target = command.Target >= 0 ? (Creature)arguments[command.Target] : null;
        CardModel card = command.Card >= 0 ? (CardModel)arguments[command.Card] : null;
        Creature dealer = command.Dealer >= 0 ? (Creature)arguments[command.Dealer] :
            HbrNativeEffectSkillHelper.GetModeOwner(card);
        using var scope = HbrEffectSkillDamageContext.Begin(target, dealer, card,
            (decimal)arguments[command.Amount], (ValueProp)arguments[command.Props], statSource ?? dealer);
        return await command.Invoke(arguments);
    }
}

// These three native potions unwrap DamageVar before calling decimal Damage;
// PotionShapedRock already enters through the typed adapter above.
[HarmonyPatch]
internal static class HbrNativePotionEffectSkillDamagePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "FirePotion", "ExplosiveAmpoule", "FoulPotion" })
        {
            Type model = typeof(PotionModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Potions." + name, true);
            MethodInfo use = AccessTools.DeclaredMethod(model, "OnUse");
            Type state = use.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                ?? throw new InvalidOperationException("Expected native damage potion async use.");
            yield return AccessTools.Method(state, "MoveNext");
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        FieldInfo potion = AccessTools.Field(__originalMethod.DeclaringType, "<>4__this");
        int matches = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(CreatureCmd) &&
                method.Name == nameof(CreatureCmd.Damage) &&
                method.GetParameters().Any(parameter => parameter.ParameterType == typeof(decimal)))
            {
                CodeInstruction loadState = new(OpCodes.Ldarg_0);
                loadState.labels.AddRange(instruction.labels);
                loadState.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear();
                instruction.blocks.Clear();
                yield return loadState;
                yield return new CodeInstruction(OpCodes.Ldfld, potion);
                instruction.opcode = OpCodes.Call;
                instruction.operand = HbrNativeEffectSkillDamagePatch.CreateWrapper(method, typeof(PotionModel));
                matches++;
            }
            yield return instruction;
        }
        if (matches != 1)
            throw new InvalidOperationException("Expected one native damage potion decimal command.");
    }
}
