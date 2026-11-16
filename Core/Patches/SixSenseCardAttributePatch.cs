using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
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

// New players already have a seed-0 placeholder RNG when CreateShared builds
// them; the run seed arrives with InitializeSeed. Starting numerics and the
// fifteen starting six-sense points wait for it. Cards no longer receive a
// six-sense attribute (removed 2026-11-09).
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
        __instance.GetRelic<ElectronicSoldierManual>()?.GrantStartingSixSense();
    }
}
