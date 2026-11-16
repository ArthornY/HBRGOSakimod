using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Poison, Constrict, Disintegration, Strangle and Demise deal their stacks with
// no dealer, or as their own owner. In HBR those stacks are the applier's skill
// units, like an enemy's Thorns or Galvanic: an enemy applier resolves through
// the incoming curve, a player-side one through its own attributes. Stacks a
// holder applied to itself are a fixed HP loss instead. Stacks, decay, removal
// and saves stay native; only each tick's calculation changes.
[HarmonyPatch]
internal static class HbrNativeDamageOverTimePatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        HbrNativeEffectSkillHelper.DamageOverTimeTypes.Select(HbrNativeEffectSkillModelCommand.Body);

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
        HbrNativeEffectSkillModelCommand.Wrap(instructions, __originalMethod, typeof(PowerModel));
}

// Health bars and the top bar read Poison's next-turn total here. Each tick
// opens the same declared skill scope as the real command, so the preview
// resolves through the identical calculation, without a critical sample.
[HarmonyPatch(typeof(PoisonPower), nameof(PoisonPower.CalculateTotalDamageNextTurn))]
internal static class HbrNativePoisonPreviewPatch
{
    private const ValueProp TickProps = ValueProp.Unblockable | ValueProp.Unpowered;
    private static readonly Func<PoisonPower, int> TriggerCount =
        AccessTools.MethodDelegate<Func<PoisonPower, int>>(AccessTools.PropertyGetter(typeof(PoisonPower), "TriggerCount"));
    private static readonly Func<IRunState, ICombatState, Creature, decimal, decimal> ModifyDamage = BuildModifyDamage();

    // The native preview's own Hook.ModifyDamage overload; v0.111.0 adds a CardPlay.
    private static Func<IRunState, ICombatState, Creature, decimal, decimal> BuildModifyDamage()
    {
        MethodInfo modify = PatchProcessor.GetOriginalInstructions(
                AccessTools.Method(typeof(PoisonPower), nameof(PoisonPower.CalculateTotalDamageNextTurn)))
            .Select(instruction => instruction.operand).OfType<MethodInfo>()
            .Single(method => method.DeclaringType == typeof(Hook) && method.Name == nameof(Hook.ModifyDamage));
        ParameterExpression run = Expression.Parameter(typeof(IRunState));
        ParameterExpression combat = Expression.Parameter(typeof(ICombatState));
        ParameterExpression target = Expression.Parameter(typeof(Creature));
        ParameterExpression damage = Expression.Parameter(typeof(decimal));
        List<ParameterExpression> locals = new();
        Expression[] arguments = modify.GetParameters().Select(parameter =>
        {
            Type type = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
            if (parameter.ParameterType.IsByRef)
            {
                ParameterExpression local = Expression.Variable(type);
                locals.Add(local);
                return local;
            }
            return type == typeof(IRunState) ? run : type == typeof(ICombatState) ? combat
                : type == typeof(Creature) && parameter.Name == "target" ? target
                : type == typeof(decimal) ? damage
                : type == typeof(ValueProp) ? Expression.Constant(TickProps)
                : type == typeof(ModifyDamageHookType) ? Expression.Constant(ModifyDamageHookType.All)
                : type == typeof(CardPreviewMode) ? Expression.Constant(CardPreviewMode.None)
                : (Expression)Expression.Default(type);
        }).ToArray();
        return Expression.Lambda<Func<IRunState, ICombatState, Creature, decimal, decimal>>(
            Expression.Block(typeof(decimal), locals, Expression.Call(modify, arguments)),
            run, combat, target, damage).Compile();
    }

    private static bool Prefix(PoisonPower __instance, ref int __result)
    {
        if (!HbrNativeEffectSkillHelper.TryGetDamageOverTimeSource(__instance, out Creature source, out bool fixedLoss))
            return true;
        Creature owner = __instance.Owner;
        int ticks = Math.Min(__instance.Amount, TriggerCount(__instance));
        decimal total = 0m;
        for (int tick = 0; tick < ticks; tick++)
        {
            decimal raw = __instance.Amount - tick;
            if (fixedLoss)
            {
                total += ModifyDamage(owner.CombatState.RunState, owner.CombatState, owner,
                    HbrResourceValueHelper.Fixed(owner, raw));
                continue;
            }
            using var scope = HbrEffectSkillDamageContext.Begin(owner, null, null, raw, TickProps, source);
            total += ModifyDamage(owner.CombatState.RunState, owner.CombatState, owner, raw);
        }
        __result = (int)Math.Clamp(total, 0m, int.MaxValue);
        return false;
    }
}
