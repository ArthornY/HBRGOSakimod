using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
internal static class ThirtyOneBEnemyMoveScopePatch
{
    private static void Prefix(MonsterModel __instance, out ThirtyOneBEnemyActionHelper.Scope __state) => __state = ThirtyOneBEnemyActionHelper.Begin(__instance.Creature);
    private static void Postfix(ref Task __result, ThirtyOneBEnemyActionHelper.Scope __state)
    { if (__state == null) return; try { __result = ThirtyOneBEnemyActionHelper.Finish(__result, __state); } finally { ThirtyOneBEnemyActionHelper.RestoreCaller(__state); } }
    private static Exception Finalizer(Exception __exception, ThirtyOneBEnemyActionHelper.Scope __state)
    { if (__exception != null) ThirtyOneBEnemyActionHelper.Close(__state); return __exception; }
}
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class ThirtyOneBEnemyAttackScopePatch
{
    private static void Prefix(AttackCommand __instance, out ThirtyOneBEnemyActionHelper.Scope __state) => __state = ThirtyOneBEnemyActionHelper.Begin(__instance.Attacker);
    private static void Postfix(ref Task<AttackCommand> __result, ThirtyOneBEnemyActionHelper.Scope __state)
    { if (__state == null) return; try { __result = Finish(__result, __state); } finally { ThirtyOneBEnemyActionHelper.RestoreCaller(__state); } }
    private static async Task<AttackCommand> Finish(Task<AttackCommand> native, ThirtyOneBEnemyActionHelper.Scope scope)
    { await ThirtyOneBEnemyActionHelper.Finish(native, scope); return await native; }
    private static Exception Finalizer(Exception __exception, ThirtyOneBEnemyActionHelper.Scope __state)
    { if (__exception != null) ThirtyOneBEnemyActionHelper.Close(__state); return __exception; }
}
[HarmonyPatch]
internal static class ThirtyOneBEnemyActualTargetPatch
{
    private static MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(object[] __args, out ThirtyOneBEnemyActionHelper.Scope __state)
    {
        Creature dealer = (Creature)__args[4];
        __state = ThirtyOneBEnemyActionHelper.Begin(dealer);
        ThirtyOneBEnemyActionHelper.Capture(NativeDamageApiCompatibility.ReadTargets(__args), dealer, (ValueProp)__args[3]);
    }
    private static void Postfix(ref Task<IEnumerable<DamageResult>> __result, ThirtyOneBEnemyActionHelper.Scope __state)
    {
        if (__state == null) return;
        Task<IEnumerable<DamageResult>> native = __result;
        try { __result = Finish(native, __state); } finally { ThirtyOneBEnemyActionHelper.RestoreCaller(__state); }
    }
    private static async Task<IEnumerable<DamageResult>> Finish(Task<IEnumerable<DamageResult>> native, ThirtyOneBEnemyActionHelper.Scope scope)
    { await ThirtyOneBEnemyActionHelper.Finish(native, scope); return await native; }
    private static Exception Finalizer(Exception __exception, ThirtyOneBEnemyActionHelper.Scope __state)
    { if (__exception != null) ThirtyOneBEnemyActionHelper.Close(__state); return __exception; }
}
[HarmonyPatch]
internal static class ThirtyOneBAoiDamageNullificationPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => typeof(Hook).GetMethods().Where(method => method.Name == nameof(Hook.ModifyDamage) &&
        method.GetParameters().Any(parameter => parameter.ParameterType == typeof(CardPreviewMode)));
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Creature target, Creature dealer, ref decimal __result)
    { if (ThirtyOneBEnemyActionHelper.IsDamageNullified(target, dealer)) __result = 0m; }
}
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply), new[] { typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool) })]
internal static class ThirtyOneBAoiAttachedPowerPatch
{
    private static bool Prefix(PlayerChoiceContext choiceContext, PowerModel power, Creature target, decimal amount, Creature applier,
        CardModel cardSource, bool silent, ref Task __result)
    {
        if (ThirtyOneBEnemyActionHelper.TryDeferPower(power, target, amount, applier,
            () => PowerCmd.Apply(choiceContext, power, target, amount, applier, cardSource, silent)) ||
            ThirtyOneBEnemyActionHelper.BlocksPower(power, target, amount, applier))
        { __result = Task.CompletedTask; return false; }
        return true;
    }
}
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount))]
internal static class ThirtyOneBAoiAttachedPowerChangePatch
{
    private static bool Prefix(PlayerChoiceContext choiceContext, PowerModel power, decimal offset, Creature applier,
        CardModel cardSource, bool silent, ref Task<int> __result)
    {
        if (ThirtyOneBEnemyActionHelper.TryDeferPower(power, power.Owner, offset, applier,
            () => PowerCmd.ModifyAmount(choiceContext, power, offset, applier, cardSource, silent)) ||
            ThirtyOneBEnemyActionHelper.BlocksPower(power, power.Owner, offset, applier))
        { __result = Task.FromResult(power.Amount); return false; }
        return true;
    }
}
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Stun), new[] { typeof(Creature), typeof(Func<IReadOnlyList<Creature>, Task>), typeof(string) })]
internal static class ThirtyOneBAoiAttachedStunPatch
{
    private static bool Prefix(Creature creature, MethodBase __originalMethod, object[] __args, ref Task __result)
    { if (!ThirtyOneBEnemyActionHelper.TryDeferCommand(creature, null, __originalMethod, null, __args) &&
          !ThirtyOneBEnemyActionHelper.IsNullified(creature)) return true; __result = Task.CompletedTask; return false; }
}
[HarmonyPatch]
internal static class ThirtyOneBAoiAttachedStatusCardPatch
{
    private static MethodBase TargetMethod() => typeof(CardPileCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(CardPileCmd.Add) && method.GetParameters() is var p && p.Length >= 5 &&
            p[0].ParameterType == typeof(IEnumerable<CardModel>) && p[1].ParameterType == typeof(CardPile));
    private static void Prefix(ref IEnumerable<CardModel> cards, MethodBase __originalMethod, object[] __args)
    {
        List<CardModel> retained = new();
        foreach (CardModel card in cards)
        {
            if (card.Type is CardType.Status or CardType.Curse)
            {
                object[] deferred = __args; deferred[0] = new[] { card };
                if (ThirtyOneBEnemyActionHelper.TryDeferCommand(card.Owner?.Creature, null, __originalMethod, null, deferred) ||
                    ThirtyOneBEnemyActionHelper.IsNullified(card.Owner?.Creature)) continue;
            }
            retained.Add(card);
        }
        cards = retained;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetCurrentHp))]
internal static class ThirtyOneBAoiAttachedHpPatch
{
    private static bool Prefix(Creature creature, decimal amount, MethodBase __originalMethod, object[] __args, ref Task __result)
    { if (amount >= LargeCreatureHealthHelper.GetCurrentHp(creature) ||
          !ThirtyOneBEnemyActionHelper.TryDeferCommand(creature, null, __originalMethod, null, __args) && !ThirtyOneBEnemyActionHelper.IsNullified(creature)) return true; __result = Task.CompletedTask; return false; }
}
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp))]
internal static class ThirtyOneBAoiAttachedMaxHpLossPatch
{
    private static bool Prefix(Creature creature, decimal amount, MethodBase __originalMethod, object[] __args, ref Task __result)
    { if (amount <= 0m || !ThirtyOneBEnemyActionHelper.TryDeferCommand(creature, null, __originalMethod, null, __args) && !ThirtyOneBEnemyActionHelper.IsNullified(creature)) return true; __result = Task.CompletedTask; return false; }
}
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp))]
internal static class ThirtyOneBAoiAttachedMaxHpPatch
{
    private static bool Prefix(Creature creature, decimal amount, MethodBase __originalMethod, object[] __args, ref Task<decimal> __result)
    { if (amount >= LargeCreatureHealthHelper.GetMaxHp(creature) ||
          !ThirtyOneBEnemyActionHelper.TryDeferCommand(creature, null, __originalMethod, null, __args) && !ThirtyOneBEnemyActionHelper.IsNullified(creature)) return true; __result = Task.FromResult(0m); return false; }
}
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Remove), new[] { typeof(PowerModel) })]
internal static class ThirtyOneBAoiAttachedBuffRemovalPatch
{
    private static bool Prefix(PowerModel power, MethodBase __originalMethod, object[] __args, ref Task __result)
    {
      // Native Remove(null) is a successful no-op, including generic removals
      // of powers that are not present. Preserve that contract before routing.
      if (power == null) return true;
      if (power.Type != MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff ||
          !ThirtyOneBEnemyActionHelper.TryDeferCommand(power.Owner, null, __originalMethod, null, __args) && !ThirtyOneBEnemyActionHelper.IsNullified(power.Owner)) return true; __result = Task.CompletedTask; return false; }
}
[HarmonyPatch(typeof(HbrEnemyEffects), nameof(HbrEnemyEffects.LoseDpShare))]
internal static class ThirtyOneBAoiAttachedDpLossPatch
{
    private static bool Prefix(Creature owner, Creature dealer, MethodBase __originalMethod, object[] __args, ref Task<decimal> __result)
    { if (!ThirtyOneBEnemyActionHelper.TryDeferCommand(owner, dealer, __originalMethod, null, __args) && !ThirtyOneBEnemyActionHelper.IsNullified(owner)) return true; __result = Task.FromResult(0m); return false; }
}
[HarmonyPatch(typeof(HbrEnemyEffects), nameof(HbrEnemyEffects.BreakDp))]
internal static class ThirtyOneBAoiAttachedDpBreakPatch
{
    private static bool Prefix(Creature target, Creature dealer, MethodBase __originalMethod, object[] __args, ref Task __result)
    { if (!ThirtyOneBEnemyActionHelper.TryDeferCommand(target, dealer, __originalMethod, null, __args) && !ThirtyOneBEnemyActionHelper.IsNullified(target)) return true; __result = Task.CompletedTask; return false; }
}
[HarmonyPatch(typeof(HbrEnemyEffects), nameof(HbrEnemyEffects.LowerOverdrive))]
internal static class ThirtyOneBAoiAttachedOdLossPatch
{
    private static void Prefix(ref IEnumerable<Creature> targets, Creature applier, MethodBase __originalMethod, object[] __args)
    {
        List<Creature> retained = new();
        foreach (Creature target in targets)
        {
            object[] deferred = __args; deferred[0] = new[] { target };
            if (!ThirtyOneBEnemyActionHelper.TryDeferCommand(target, applier, __originalMethod, null, deferred) && !ThirtyOneBEnemyActionHelper.IsNullified(target)) retained.Add(target);
        }
        targets = retained;
    }
}

[HarmonyPatch]
internal static class ThirtyOneBAoiAttachedKillPatch
{
    private static MethodBase TargetMethod() => typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(method => method.Name == nameof(CreatureCmd.Kill) && method.GetParameters() is var p &&
            p.Length >= 2 && p[0].ParameterType == typeof(IReadOnlyCollection<Creature>) &&
            p.Any(parameter => parameter.ParameterType == typeof(bool)))
        .OrderByDescending(method => method.GetParameters().Length).First();
    private static bool Prefix(ref IReadOnlyCollection<Creature> creatures, MethodBase __originalMethod, object[] __args, ref Task __result)
    {
        List<Creature> retained = new();
        foreach (Creature target in creatures)
        {
            object[] deferred = __args; deferred[0] = new[] { target };
            if (!ThirtyOneBEnemyActionHelper.TryDeferCommand(target, null, __originalMethod, null, deferred) && !ThirtyOneBEnemyActionHelper.IsNullified(target)) retained.Add(target);
        }
        creatures = retained;
        if (creatures.Count > 0) return true;
        __result = Task.CompletedTask; return false;
    }
}
[HarmonyPatch(typeof(HBRGOSakiMod.Core.Models.Relics.DeflectorProtection), nameof(HBRGOSakiMod.Core.Models.Relics.DeflectorProtection.LoseDp), new[] { typeof(int) })]
internal static class ThirtyOneBAoiAttachedPlayerDpLossPatch
{
    private static bool Prefix(HBRGOSakiMod.Core.Models.Relics.DeflectorProtection __instance, int amount, MethodBase __originalMethod, object[] __args, ref int __result)
    { if (amount <= 0 || !ThirtyOneBEnemyActionHelper.TryDeferCommand(__instance.Owner?.Creature, null, __originalMethod, __instance, __args) && !ThirtyOneBEnemyActionHelper.IsNullified(__instance.Owner?.Creature)) return true; __result = 0; return false; }
}
[HarmonyPatch(typeof(HBRGOSakiMod.Core.Models.Powers.HighDimensionalShellPower), "BreakImmediately")]
internal static class ThirtyOneBAoiAttachedShellBreakPatch
{
    private static bool Prefix(HBRGOSakiMod.Core.Models.Powers.HighDimensionalShellPower __instance, MethodBase __originalMethod, object[] __args, ref Task __result)
    { if (!ThirtyOneBEnemyActionHelper.TryDeferCommand(__instance.Owner, null, __originalMethod, __instance, __args) && !ThirtyOneBEnemyActionHelper.IsNullified(__instance.Owner)) return true; __result = Task.CompletedTask; return false; }
}
[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseEnergy))]
internal static class ThirtyOneBAoiAttachedEnergyLossPatch
{
    private static bool Prefix(MegaCrit.Sts2.Core.Entities.Players.Player player, MethodBase __originalMethod, object[] __args, ref Task __result)
    { if (!ThirtyOneBEnemyActionHelper.TryDeferCommand(player?.Creature, null, __originalMethod, null, __args) && !ThirtyOneBEnemyActionHelper.IsNullified(player?.Creature)) return true; __result = Task.CompletedTask; return false; }
}
