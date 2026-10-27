using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace HBRGOSakiMod.Core.Patches;

internal static class SixSenseStartingSeedState
{
    private static readonly ConditionalWeakTable<Player, object> PendingPlayers = new();

    internal static void AwaitRunSeed(Player player) =>
        PendingPlayers.GetValue(player, static _ => new object());

    internal static bool IsAwaitingRunSeed(Player player) =>
        player != null && PendingPlayers.TryGetValue(player, out _);

    internal static void CompleteRunSeed(Player player) => PendingPlayers.Remove(player);
}

[HarmonyPatch(typeof(Player), nameof(Player.CreateForNewRun),
    new[] { typeof(CharacterModel), typeof(UnlockState), typeof(ulong) })]
internal static class SixSenseNewPlayerSeedPatch
{
    private static void Postfix(Player __result) => SixSenseStartingSeedState.AwaitRunSeed(__result);
}

[HarmonyPatch]
internal static class SixSenseCardAttributePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type scope in new[] { typeof(RunState), typeof(CombatState) })
        {
            // Both creation and cloning reach the owner-ready, one-argument overload.
            yield return AccessTools.DeclaredMethod(scope, nameof(RunState.AddCard),
                new[] { typeof(CardModel) })
                ?? throw new MissingMethodException(scope.FullName, "AddCard(CardModel)");
        }
    }

    private static void Postfix(CardModel __0)
    {
        // New players already have a seed-0 placeholder RNG when CreateShared adds
        // their starting deck. Wait for InitializeSeed to supply the actual run seed.
        if (SixSenseStartingSeedState.IsAwaitingRunSeed(__0.Owner)) return;

        // Ritsu copies capabilities before a clone reaches AddCard. Persisted and
        // inherited attributes are retained without another random draw.
        SixSenseCardCapability.AssignIfMissing(__0, __0.Owner);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.InitializeSeed))]
internal static class SixSenseStartingInventoryPatch
{
    private static void Postfix(Player __instance)
    {
        bool isNewPlayer = SixSenseStartingSeedState.IsAwaitingRunSeed(__instance);
        SixSenseStartingSeedState.CompleteRunSeed(__instance);
        if (isNewPlayer)
        {
            if (__instance.Character is HBRGOSakiMod.Core.Models.Characters.HbrgoSaki or
                HBRGOSakiMod.Core.Models.Characters.HbrgoHotaru)
                __instance.GetRelic<ElectronicSoldierManual>()?.InitializeHbrBattleNumerics();
            __instance.GetRelic<HotaruElectronicSoldierManual>()?.InitializeStartingHealth();
        }
        // Starting cards enter RunState before its real player RNG is initialized.
        __instance.GetRelic<ElectronicSoldierManual>()?.GrantStartingSixSense();
        if (__instance.Deck == null) return;
        foreach (CardModel card in __instance.Deck.Cards)
        {
            SixSenseCardCapability.AssignIfMissing(card, __instance);
        }
    }
}
