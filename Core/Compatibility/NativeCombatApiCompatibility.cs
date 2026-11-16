using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Compatibility;

internal static class NativeCombatApiCompatibility
{
    private static readonly MethodInfo UndoReady =
        typeof(CombatManager).GetMethod("TryUndoReadyToEndTurn", [typeof(Player)]) ??
        typeof(CombatManager).GetMethod("UndoReadyToEndTurn", [typeof(Player)]) ??
        throw new MissingMethodException(typeof(CombatManager).FullName, "TryUndoReadyToEndTurn / UndoReadyToEndTurn");
    private static readonly MethodInfo Populate = typeof(Player).GetMethods()
        .Single(method => method.Name == "PopulateCombatState" && method.GetParameters().Length == 2);
    private static readonly MethodInfo Proceed =
        typeof(RunManager).GetMethod("ProceedFromTerminalRewardsScreenAsync", Type.EmptyTypes) ??
        typeof(RunManager).GetMethods().Single(method => method.Name == "ProceedFromTerminalRewardsScreen" &&
            method.ReturnType == typeof(Task));

    internal static void UndoReadyToEndTurnCompat(this CombatManager manager, Player player) =>
        CompatibilityReflection.Invoke(UndoReady, manager, [player]);

    internal static void PopulateCombatStateCompat(this Player player, Rng rng, ICombatState combat) =>
        CompatibilityReflection.Invoke(Populate, player, [rng, combat]);

    internal static Task ProceedFromTerminalRewardsScreenCompat(this RunManager manager) =>
        (Task)CompatibilityReflection.Invoke(Proceed, manager, []);
}
