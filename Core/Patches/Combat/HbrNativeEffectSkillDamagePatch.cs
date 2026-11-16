using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
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
        internal int Targets;
        internal bool Canonical;
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
            Props = Array.FindIndex(parameters, parameter => parameter.ParameterType == typeof(ValueProp)),
            Targets = Array.FindIndex(parameters, parameter =>
                typeof(IEnumerable<Creature>).IsAssignableFrom(parameter.ParameterType)),
            Canonical = method == NativeDamageApiCompatibility.Command
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
        Creature target = command.Target >= 0 ? (Creature)arguments[command.Target] : null;
        CardModel card = command.Card >= 0 ? (CardModel)arguments[command.Card] : null;
        Creature dealer = command.Dealer >= 0 ? (Creature)arguments[command.Dealer] :
            HbrNativeEffectSkillHelper.GetModeOwner(card);
        AbstractModel model = source as AbstractModel ??
            (source is DamageVar value ? HbrNativeEffectSkillHelper.GetVariableSource(value) : null);
        HbrNativeDamageProfiles.TryGet(model, out HbrNativeDamageProfile profile);
        // Inferno has both a resource cost and an outgoing skill. Preserve
        // the native self-cost's already-scaled HP, not a second skill curve.
        if (profile?.Basis == HbrNativeDamageBasis.FixedCost ||
            profile?.OwnerSelfIsFixedCost == true && model is PowerModel self && ReferenceEquals(target, self.Owner))
        {
            // This body already owns the declared Inferno/TheHangedMan cost.
            // Record it here instead of adding a competing transpiler at the
            // same native instruction. Nested commands claim separate scopes.
            using var cost = HbrHealthCostContext.Begin(target, (decimal)arguments[command.Amount],
                (ValueProp)arguments[command.Props], dealer, card);
            return await InvokePrepared(command, arguments);
        }
        Creature statSource;
        if (source is DamageVar variable)
        {
            if (!HbrNativeEffectSkillHelper.IsSkillVariable(variable) ||
                !HbrBattleStatsHelper.IsEnabled(HbrNativeEffectSkillHelper.GetModeOwner(variable)))
                return await InvokePrepared(command, arguments);
            if (HbrNativeEffectSkillHelper.IsEnvironmentalSelfDamageVariable(variable) &&
                ReferenceEquals(target, HbrNativeEffectSkillHelper.GetModeOwner(variable)))
            {
                using var environmental = HbrEffectSkillDamageContext.BeginEnvironmental(target, dealer, card,
                    (decimal)arguments[command.Amount], (ValueProp)arguments[command.Props]);
                return await InvokePrepared(command, arguments);
            }
            statSource = profile == null ? null : HbrNativeDamageProfiles.ResolveSource(model);
        }
        // Thorns and Galvanic deal their stacks as the power owner's skill.
        else if (source is PowerModel { IsMutable: true } power &&
                 power is MegaCrit.Sts2.Core.Models.Powers.ThornsPower or MegaCrit.Sts2.Core.Models.Powers.GalvanicPower &&
                 HbrBattleStatsHelper.IsEnabled(power.Owner))
            statSource = power.Owner;
        // Damage over time deals its stacks as its applier's skill, or as a fixed loss.
        else if (source is PowerModel dot &&
                 HbrNativeEffectSkillHelper.TryGetDamageOverTimeSource(dot, out Creature applier, out bool fixedLoss))
        {
            if (fixedLoss)
            {
                arguments[command.Amount] = HbrResourceValueHelper.Fixed(dot.Owner, (decimal)arguments[command.Amount]);
                return await InvokePrepared(command, arguments);
            }
            statSource = applier;
        }
        else if (model != null && profile?.Basis is HbrNativeDamageBasis.SkillRaw or HbrNativeDamageBasis.WorldAmount &&
                 (HbrBattleStatsHelper.IsEnabled(HbrNativeDamageProfiles.ResolveSource(model)) ||
                    model is PowerModel missingApplier && HbrBattleStatsHelper.IsEnabled(missingApplier.Owner)))
            statSource = HbrNativeDamageProfiles.ResolveSource(model);
        else if (source is PotionModel or RelicModel && HbrNativeEffectSkillHelper.IsSkillModel((AbstractModel)source) &&
                 HbrNativeEffectSkillHelper.IsEnabled((AbstractModel)source))
            statSource = HbrNativeEffectSkillHelper.GetModeOwner((AbstractModel)source);
        else
            return await InvokePrepared(command, arguments);
        using var scope = profile?.Basis == HbrNativeDamageBasis.WorldAmount
            ? HbrEffectSkillDamageContext.BeginWorld(target, dealer, card,
                (decimal)arguments[command.Amount], (ValueProp)arguments[command.Props], statSource ?? dealer)
            : HbrEffectSkillDamageContext.Begin(target, dealer, card,
                (decimal)arguments[command.Amount], (ValueProp)arguments[command.Props], statSource ?? dealer);
        return await InvokePrepared(command, arguments);
    }

    // These callers already have an audited skill-scope wrapper, so the Aoi
    // callsite adapter deliberately leaves them owned by this wrapper. Flush
    // pending attached effects after declaring that scope and before the one
    // real native command; never recurse through an outer Harmony prefix.
    private static Task<IEnumerable<DamageResult>> InvokePrepared(Command command, object[] arguments) =>
        ThirtyOneBEnemyActionHelper.HasPending ? PrepareThenInvoke(command, arguments) : command.Invoke(arguments);

    private static async Task<IEnumerable<DamageResult>> PrepareThenInvoke(Command command, object[] arguments)
    {
        if (command.Canonical) await ThirtyOneBEnemyActionHelper.PrepareDamage(arguments);
        else
        {
            Creature dealer = command.Dealer >= 0 ? arguments[command.Dealer] as Creature : null;
            ValueProp props = (ValueProp)arguments[command.Props];
            if (command.Target >= 0)
            {
                await ThirtyOneBEnemyActionHelper.PrepareDamageTargets(
                    new[] { (Creature)arguments[command.Target] }, dealer, props);
            }
            else if (command.Targets >= 0)
            {
                Creature[] snapshot = ((IEnumerable<Creature>)arguments[command.Targets]).ToArray();
                arguments[command.Targets] = snapshot;
                await ThirtyOneBEnemyActionHelper.PrepareDamageTargets(
                    snapshot, dealer, props);
            }
        }
        return await command.Invoke(arguments);
    }
}

// New audited sources use the same command wrapper as the original native
// adapters. Inspect their declared managed bodies once at patch installation,
// including synchronous helpers such as RollingBoulder's DoDamage and every
// separate passive/evoke body of a damaging orb.
[HarmonyPatch]
internal static class HbrNativeProfiledDamagePatch
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    internal static IEnumerable<MethodBase> TargetMethods()
    {
        HashSet<MethodBase> seen = new();
        foreach (var profile in HbrNativeDamageProfiles.All)
        {
            if (profile.Basis is not (HbrNativeDamageBasis.SkillRaw or HbrNativeDamageBasis.WorldAmount or HbrNativeDamageBasis.FixedCost)) continue;
            foreach (MethodInfo method in profile.ModelType.GetMethods(Declared))
            {
                if (method.IsStatic || method.IsAbstract || method.ContainsGenericParameters) continue;
                Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
                MethodInfo body = state == null ? method : AccessTools.Method(state, "MoveNext");
                if (body == null || body.GetMethodBody() == null || !seen.Add(body)) continue;
                if (PatchProcessor.GetOriginalInstructions(body).Any(HbrNativeEffectSkillModelCommand.IsDecimalDamage))
                    yield return body;
            }
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        FieldInfo owner = AccessTools.Field(__originalMethod.DeclaringType, "<>4__this");
        int count = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (HbrNativeEffectSkillModelCommand.IsDecimalDamage(instruction))
            {
                CodeInstruction load = new(OpCodes.Ldarg_0);
                load.labels.AddRange(instruction.labels); load.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear(); instruction.blocks.Clear();
                yield return load;
                if (owner != null) yield return new CodeInstruction(OpCodes.Ldfld, owner);
                instruction.opcode = OpCodes.Call;
                instruction.operand = HbrNativeEffectSkillDamagePatch.CreateWrapper((MethodInfo)instruction.operand, typeof(AbstractModel));
                count++;
            }
            yield return instruction;
        }
        if (count == 0) throw new InvalidOperationException("Audited native effect body lost its decimal damage command: " + __originalMethod);
    }
}

// Native models whose async hook passes a plain decimal to Damage. The body's
// single command gets a wrapper that also receives the model it belongs to.
internal static class HbrNativeEffectSkillModelCommand
{
    internal static bool IsDecimalDamage(CodeInstruction instruction) =>
        instruction.operand is MethodInfo method && method.DeclaringType == typeof(CreatureCmd) &&
        method.Name == nameof(CreatureCmd.Damage) &&
        method.GetParameters().Any(parameter => parameter.ParameterType == typeof(decimal));

    // The async body holding the model's one decimal command. Runtimes move it
    // between hooks (v0.111.0 Poison ticks in a new Trigger method).
    internal static MethodInfo Body(Type model)
    {
        MethodInfo[] bodies = model.GetNestedTypes(BindingFlags.NonPublic)
            .Where(type => typeof(IAsyncStateMachine).IsAssignableFrom(type))
            .Select(type => AccessTools.Method(type, "MoveNext"))
            .Where(body => PatchProcessor.GetOriginalInstructions(body).Any(IsDecimalDamage))
            .ToArray();
        return bodies.Length == 1 ? bodies[0]
            : throw new InvalidOperationException("Expected one native decimal damage body in " + model.FullName);
    }

    internal static IEnumerable<CodeInstruction> Wrap(IEnumerable<CodeInstruction> instructions,
        MethodBase body, Type sourceType)
    {
        FieldInfo model = AccessTools.Field(body.DeclaringType, "<>4__this");
        int matches = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (IsDecimalDamage(instruction))
            {
                CodeInstruction loadState = new(OpCodes.Ldarg_0);
                loadState.labels.AddRange(instruction.labels);
                loadState.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear();
                instruction.blocks.Clear();
                yield return loadState;
                yield return new CodeInstruction(OpCodes.Ldfld, model);
                instruction.opcode = OpCodes.Call;
                instruction.operand = HbrNativeEffectSkillDamagePatch.CreateWrapper((MethodInfo)instruction.operand, sourceType);
                matches++;
            }
            yield return instruction;
        }
        if (matches != 1)
            throw new InvalidOperationException("Expected one native decimal damage command in " +
                body.DeclaringType?.FullName);
    }
}

// Lost Wisp reads its DamageVar's value and Mr. Struggles deals the turn
// number; both pass a decimal rather than the typed adapter above.
[HarmonyPatch]
internal static class HbrNativeRelicEffectSkillDamagePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return HbrNativeEffectSkillModelCommand.Body(typeof(MegaCrit.Sts2.Core.Models.Relics.LostWisp));
        yield return HbrNativeEffectSkillModelCommand.Body(typeof(MegaCrit.Sts2.Core.Models.Relics.MrStruggles));
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
        HbrNativeEffectSkillModelCommand.Wrap(instructions, __originalMethod, typeof(RelicModel));
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
