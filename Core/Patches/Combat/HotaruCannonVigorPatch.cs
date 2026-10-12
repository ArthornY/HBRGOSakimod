using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HotaruCannonVigorPatch
{
    private sealed class PowerReservations
    {
        internal readonly HashSet<AttackCommand> Commands = new();
    }
    private sealed class CommandReservations
    {
        internal readonly HashSet<VigorPower> Powers = new();
    }

    private static readonly object ReservationLock = new();
    private static readonly ConditionalWeakTable<VigorPower, PowerReservations> ByPower = new();
    private static readonly ConditionalWeakTable<AttackCommand, CommandReservations> ByCommand = new();
    private static readonly FieldInfo InternalData = AccessTools.DeclaredField(typeof(PowerModel), "_internalData")
        ?? throw new MissingFieldException(typeof(PowerModel).FullName, "_internalData");
    private static readonly Type DataType = typeof(VigorPower).GetNestedType("Data", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(VigorPower).FullName, "Data");
    private static readonly FieldInfo ReservedCommand = AccessTools.DeclaredField(DataType, "commandToModify")
        ?? throw new MissingFieldException(DataType.FullName, "commandToModify");

    private static MethodBase TargetMethod()
    {
        Type[] common = { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel) };
        MethodInfo method = AccessTools.DeclaredMethod(typeof(VigorPower), nameof(VigorPower.ModifyDamageAdditive), common);
        if (method == null)
            method = AccessTools.DeclaredMethod(typeof(VigorPower), nameof(VigorPower.ModifyDamageAdditive),
                new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay) });
        if (method == null || method.ReturnType != typeof(decimal))
            throw new MissingMethodException(typeof(VigorPower).FullName, "ModifyDamageAdditive exact stable/beta signature");
        return method;
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(VigorPower __instance, Creature target, ValueProp props,
        Creature dealer, CardModel cardSource, ref decimal __result)
    {
        if (!HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, cardSource, props, out Creature cannon))
            return;
        __result = HotaruCannonVigorHelper.ResolveBonus(__instance, cannon,
            HotaruCannonStatsHelper.CurrentPreviewPhase, HotaruCannonStatsHelper.IsDamagePreview,
            HotaruCannonStatsHelper.IsReactionDamage, __result);
    }

    internal static bool IsVigorReservedForNativeAttack(VigorPower power)
    {
        if (power == null) return false;
        lock (ReservationLock)
            return ByPower.TryGetValue(power, out PowerReservations reservations) && reservations.Commands.Count > 0;
    }

    private static AttackCommand ReadReservedCommand(VigorPower power)
    {
        object data = power == null ? null : InternalData.GetValue(power);
        return data != null && DataType.IsInstanceOfType(data) ? ReservedCommand.GetValue(data) as AttackCommand : null;
    }

    internal static bool CanObserveNewReservation(VigorPower power) =>
        power != null && ReadReservedCommand(power) == null;

    internal static async Task ObserveBefore(Task original, VigorPower power, AttackCommand command, bool mayCapture)
    {
        // Both native versions capture synchronously before returning their
        // Task. Protect that accepted slot before any decorated Task can await.
        // Never mark again after awaiting: AfterAttack may already have ended it.
        if (mayCapture && command != null && ReferenceEquals(ReadReservedCommand(power), command))
            lock (ReservationLock)
            {
                ByPower.GetValue(power, _ => new PowerReservations()).Commands.Add(command);
                ByCommand.GetValue(command, _ => new CommandReservations()).Powers.Add(power);
            }
        try { await original; }
        catch
        {
            ClearReservation(power, command);
            throw;
        }
    }

    internal static void ClearReservation(VigorPower power, AttackCommand command)
    {
        if (power == null || command == null) return;
        lock (ReservationLock)
        {
            if (ByPower.TryGetValue(power, out PowerReservations powers))
            {
                powers.Commands.Remove(command);
                if (powers.Commands.Count == 0) ByPower.Remove(power);
            }
            if (ByCommand.TryGetValue(command, out CommandReservations commands))
            {
                commands.Powers.Remove(power);
                if (commands.Powers.Count == 0) ByCommand.Remove(command);
            }
        }
    }

    internal static void ClearCommand(AttackCommand command)
    {
        if (command == null) return;
        lock (ReservationLock)
        {
            if (!ByCommand.TryGetValue(command, out CommandReservations commands)) return;
            foreach (VigorPower power in commands.Powers)
                if (ByPower.TryGetValue(power, out PowerReservations powers))
                {
                    powers.Commands.Remove(command);
                    if (powers.Commands.Count == 0) ByPower.Remove(power);
                }
            ByCommand.Remove(command);
        }
    }

    internal static async Task ObserveAfter(Task original, VigorPower power, AttackCommand command)
    {
        try { await original; }
        finally { ClearReservation(power, command); }
    }

    internal static async Task<AttackCommand> ObserveExecution(Task<AttackCommand> original, AttackCommand command)
    {
        // An error in native damage can skip AfterAttack entirely. This observer
        // releases only tracking state and preserves the original task/result.
        try { return await original; }
        finally { ClearCommand(command); }
    }
}

[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.BeforeAttack))]
internal static class HotaruCannonVigorBeforeAttackReservationPatch
{
    private static void Prefix(VigorPower __instance, out bool __state) =>
        __state = HotaruCannonVigorPatch.CanObserveNewReservation(__instance);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(VigorPower __instance, AttackCommand command, bool __state, ref Task __result) =>
        __result = HotaruCannonVigorPatch.ObserveBefore(__result, __instance, command, __state);

    private static Exception Finalizer(Exception __exception, VigorPower __instance, AttackCommand command)
    {
        if (__exception != null) HotaruCannonVigorPatch.ClearReservation(__instance, command);
        return __exception;
    }
}

[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.AfterAttack))]
internal static class HotaruCannonVigorAfterAttackReservationPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(VigorPower __instance, AttackCommand command, ref Task __result) =>
        __result = HotaruCannonVigorPatch.ObserveAfter(__result, __instance, command);

    private static Exception Finalizer(Exception __exception, VigorPower __instance, AttackCommand command)
    {
        if (__exception != null) HotaruCannonVigorPatch.ClearReservation(__instance, command);
        return __exception;
    }
}

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class HotaruCannonVigorNativeAttackCompletionPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(AttackCommand __instance, ref Task<AttackCommand> __result) =>
        __result = HotaruCannonVigorPatch.ObserveExecution(__result, __instance);

    private static Exception Finalizer(Exception __exception, AttackCommand __instance)
    {
        if (__exception != null) HotaruCannonVigorPatch.ClearCommand(__instance);
        return __exception;
    }
}
