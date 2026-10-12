using System;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Defeat skips the native AfterCombatEnd model hooks. Settle only the saved
// wear APIs before the native pending-loss handler publishes CombatEnded.
[HarmonyPatch]
internal static class HotaruCombatWearLossPatch
{
    private static readonly MethodInfo ProcessLoss = ResolveProcessLoss();
    private static readonly FieldInfo PendingLossField = ProcessLoss.GetParameters().Length == 0
        ? AccessTools.DeclaredField(typeof(CombatManager), "_pendingLoss")
            ?? throw new MissingFieldException(typeof(CombatManager).FullName, "_pendingLoss")
        : null;
    private static readonly MethodInfo PendingLossGetter = PendingLossField == null
        ? AccessTools.PropertyGetter(ProcessLoss.GetParameters()[0].ParameterType, "PendingLoss")
            ?? throw new MissingMemberException(ProcessLoss.GetParameters()[0].ParameterType.FullName, "PendingLoss")
        : null;
    private static readonly MethodInfo RoomGetter = ResolveRoomGetter();

    private static MethodInfo ResolveProcessLoss()
    {
        // Stable keeps pending loss on CombatManager; beta carries it on the
        // CombatTurnState argument. Do not inject a field absent from beta.
        MethodInfo method = AccessTools.DeclaredMethod(typeof(CombatManager), "ProcessPendingLoss", Type.EmptyTypes);
        if (method == null)
        {
            Type turnState = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.CombatTurnState");
            if (turnState != null)
                method = AccessTools.DeclaredMethod(typeof(CombatManager), "ProcessPendingLoss", new[] { turnState });
        }
        if (method == null || method.IsStatic || method.ReturnType != typeof(void))
            throw new MissingMethodException(typeof(CombatManager).FullName, "ProcessPendingLoss stable/beta signature");
        return method;
    }

    private static MethodInfo ResolveRoomGetter()
    {
        Type pendingType = PendingLossField?.FieldType ?? PendingLossGetter.ReturnType;
        MethodInfo getter = AccessTools.PropertyGetter(pendingType, "Room");
        if (getter == null || getter.IsStatic || getter.ReturnType != typeof(CombatRoom))
            throw new MissingMemberException(pendingType.FullName, "Room");
        return getter;
    }

    private static MethodBase TargetMethod() => ProcessLoss;

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(CombatManager __instance, object[] __args)
    {
        object pendingLoss = PendingLossField != null
            ? PendingLossField.GetValue(__instance)
            : __args.Length > 0 && __args[0] != null
                ? PendingLossGetter.Invoke(__args[0], null)
                : null;
        if (pendingLoss == null || RoomGetter.Invoke(pendingLoss, null) is not CombatRoom room)
            return;

        // Both APIs own their persisted room stamp and player eligibility.
        // Do not replay victory rewards, OD restoration or other end hooks.
        foreach (var player in room.CombatState.Players)
        {
            player.GetRelic<Millsage>()?.ApplyCombatWear(room);
            player.GetRelic<DeflectorProtection>()?.ApplyHotaruCombatWear(room);
        }
    }
}
