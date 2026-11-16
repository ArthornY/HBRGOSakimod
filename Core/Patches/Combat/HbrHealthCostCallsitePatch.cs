using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Fixed, audited native cost bodies, not a global stack walk or a general
// Power/Damage hook heuristic. Burn/Beckon/BadLuck/Regret and hostile reactions
// are damaging effects and intentionally absent from the payment list.
[HarmonyPatch]
internal static class HbrHealthCostCallsitePatch
{
    private sealed record Site(MethodInfo Body, int Count);
    private sealed record Command(MethodInfo Method, int Dealer, int Card);
    private static readonly List<Command> Commands = new();
    private static readonly Lazy<Site[]> Sites = new(BuildSites);
    private static IEnumerable<MethodBase> TargetMethods() => Sites.Value.Select(site => site.Body);
    private static Site[] BuildSites()
    {
        List<Site> sites = new();
        var names = new[] { "Cards.Bloodletting", "Cards.BloodWall", "Cards.Brand", "Cards.Breakthrough",
            "Cards.DemonicShield", "Cards.Hemokinesis", "Cards.Offering", "Powers.CrimsonMantlePower" };
        foreach (string name in names)
        {
            Type model = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models." + name);
            if (model == null) throw new TypeLoadException(name);
            string entry = name.StartsWith("Cards.", StringComparison.Ordinal) ? "OnPlay" : "AfterPlayerTurnStart";
            Add(AccessTools.DeclaredMethod(model, entry));
        }
        // Inferno and TheHangedMan already have an audited damage-profile
        // adapter, which records their cost there without a second transpiler.
        // Reducing a maximum may cause an internal HP adjustment through
        // Damage; it is structural, irrespective of the initiating card.
        Add(AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp)));
        return sites.ToArray();

        void Add(MethodInfo entry)
        {
            Type state = entry?.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
            MethodInfo body = state == null ? entry : AccessTools.DeclaredMethod(state, "MoveNext");
            if (body == null) throw new MissingMethodException("Native health cost body changed.");
            int count = PatchProcessor.GetOriginalInstructions(body).Count(instruction => IsDamage(instruction.operand as MethodInfo));
            if (count != 1) throw new InvalidOperationException("Expected one health-payment damage in " + body + "; found " + count);
            sites.Add(new(body, count));
        }
    }
    private static bool IsDamage(MethodInfo method) => method?.DeclaringType == typeof(CreatureCmd) &&
        method.Name == nameof(CreatureCmd.Damage) && method.ReturnType == typeof(Task<IEnumerable<DamageResult>>);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        Site site = Sites.Value.Single(candidate => candidate.Body == __originalMethod);
        int count = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is not MethodInfo method || !IsDamage(method)) { yield return instruction; continue; }
            yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = Adapter(method) };
            count++;
        }
        if (count != site.Count) throw new InvalidOperationException("Health cost callsite count changed in " + __originalMethod);
    }
    private static MethodInfo Adapter(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length < 5 || parameters[1].ParameterType != typeof(Creature) ||
            parameters[2].ParameterType != typeof(decimal) || parameters[3].ParameterType != typeof(ValueProp))
            throw new InvalidOperationException("Unsupported audited health cost signature: " + method);
        int dealer = Array.FindIndex(parameters, parameter => parameter.Name == "dealer");
        int card = Array.FindIndex(parameters, parameter => parameter.ParameterType == typeof(CardModel));
        int id = Commands.Count;
        Commands.Add(new(method, dealer, card));
        DynamicMethod wrapper = new("HbrHealthCost_" + id, method.ReturnType,
            parameters.Select(parameter => parameter.ParameterType).ToArray(), typeof(HbrHealthCostCallsitePatch), true);
        ILGenerator il = wrapper.GetILGenerator();
        il.Emit(OpCodes.Ldc_I4, id); il.Emit(OpCodes.Ldc_I4, parameters.Length); il.Emit(OpCodes.Newarr, typeof(object));
        for (int index = 0; index < parameters.Length; index++)
        {
            il.Emit(OpCodes.Dup); il.Emit(OpCodes.Ldc_I4, index); il.Emit(OpCodes.Ldarg, (short)index);
            if (parameters[index].ParameterType.IsValueType) il.Emit(OpCodes.Box, parameters[index].ParameterType);
            il.Emit(OpCodes.Stelem_Ref);
        }
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(HbrHealthCostCallsitePatch), nameof(Run))); il.Emit(OpCodes.Ret);
        return wrapper;
    }
    private static async Task<IEnumerable<DamageResult>> Run(int id, object[] args)
    {
        Command command = Commands[id];
        CardModel card = command.Card >= 0 ? (CardModel)args[command.Card] : null;
        Creature dealer = command.Dealer >= 0 ? (Creature)args[command.Dealer] : card?.Owner?.Creature;
        using var cost = HbrHealthCostContext.Begin((Creature)args[1], (decimal)args[2], (ValueProp)args[3], dealer, card);
        Task<IEnumerable<DamageResult>> original;
        try { original = (Task<IEnumerable<DamageResult>>)command.Method.Invoke(null, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        return await original;
    }
}
