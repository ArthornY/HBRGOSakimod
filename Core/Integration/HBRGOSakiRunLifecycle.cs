using System;
using System.Linq;
using System.Reflection;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Patches;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib;

namespace HBRGOSakiMod.Core.Integration;

internal static class HBRGOSakiRunLifecycle
{
    private static IDisposable _runStartedSubscription;
    private static IDisposable _runLoadedSubscription;

    public static void Register()
    {
        _runStartedSubscription ??=
            RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(
                OnRunStarted,
                replayCurrentState: false);
        _runLoadedSubscription ??=
            RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(
                OnRunLoaded,
                replayCurrentState: false);
    }

    private static void OnRunStarted(RunStartedEvent evt)
    {
        EnsureRollInfrastructure(evt.RunState, evt.IsMultiplayer);
        // A shared enemy cannot use HBR health while native teammates retain
        // STS-scale stats. Their existing multiplayer manual stores this same
        // fresh-run profile; loading a prior run never enters this path.
        if (evt.RunState.Players.Any(player => player.Character is HbrgoSaki or HbrgoHotaru))
            foreach (Player player in evt.RunState.Players)
                player.GetRelic<ElectronicSoldierManual>()?.InitializeHbrBattleNumerics();
        foreach (Player player in evt.RunState.Players.Where(player => player.Character is HbrgoHotaru))
            player.GetRelic<HotaruElectronicSoldierManual>()?.InitializeStartingHealth();
    }

    private static void OnRunLoaded(RunLoadedEvent evt)
    {
        foreach (Player player in evt.RunState.Players.Where(player => player.Character is HbrgoHotaru))
            player.GetRelic<HotaruElectronicSoldierManual>()?.PreserveLoadedHealth();
        EnsureRollInfrastructure(evt.RunState, evt.IsMultiplayer);
    }

    private static void EnsureRollInfrastructure(
        RunState runState,
        bool isMultiplayer)
    {
        if (runState?.Players == null ||
            !runState.Players.Any(player =>
                player.Character is HbrgoSaki or HbrgoHotaru))
        {
            return;
        }

        // Profile unlocks belong to this client, not to a teammate's variant.
        Player localPlayer = LocalContext.GetMe(runState.Players) ??
            (runState.Players.Count == 1 ? runState.Players[0] : null);
        HbrgoSakiVariantPreference.EnsureSkullFeatherLinkedUnlocks(localPlayer?.Character);

        foreach (Player player in runState.Players)
        {
            if (player.Character is HbrgoHotaru)
                MigrateLegacyHotaruManual(player);
            HbrMemoryGardenDampen.RestorePending(player);
            HbrMemoryGardenProgression.Synchronize(player);
            TeamCollectionRelics.Restore(player);
            if (player.Character is HbrgoSaki or HbrgoHotaru)
            {
                if (!player.Relics.OfType<SoulOfAnchor>().Any())
                {
                    SoulOfAnchor history =
                        (SoulOfAnchor)ModelDb
                            .Relic<SoulOfAnchor>()
                            .ToMutable();
                    history.FloorAddedToDeck = 1;
                    player.AddRelicInternal(history);
                }

                if (player.Character is HbrgoSaki30A)
                {
                    player.GetRelic<MomentOfReign>()?
                        .EnsureSummonableMonsterRoster();
                }

                if (player.Character is HbrgoHotaru)
                {
                    if (player.GetRelic<Overclock>() == null)
                    {
                        Overclock overclock = (Overclock)ModelDb.Relic<Overclock>().ToMutable();
                        overclock.FloorAddedToDeck = 1;
                        player.AddRelicInternal(overclock);
                    }
                    // Load migration uses the player's saved RNG and persisted
                    // once-only marker. It never runs the HP initialization hook.
                    if (!SixSenseStartingSeedState.IsAwaitingRunSeed(player))
                        player.GetRelic<ElectronicSoldierManual>()?.GrantStartingSixSense();
                }

                continue;
            }

            if (!isMultiplayer)
            {
                continue;
            }

            if (player.GetRelic<ElectronicSoldierManual>() == null)
            {
                ElectronicSoldierManual manual =
                    (ElectronicSoldierManual)ModelDb
                        .Relic<ElectronicSoldierManual>()
                        .ToMutable();
                manual.FloorAddedToDeck = 1;
                player.AddRelicInternal(manual);
                TeamCollectionRelics.Restore(player);
            }

            if (!player.Relics.OfType<SoulOfAnchor>().Any())
            {
                FabricatedSoulOfAnchor soul =
                    (FabricatedSoulOfAnchor)ModelDb
                        .Relic<FabricatedSoulOfAnchor>()
                        .ToMutable();
                soul.FloorAddedToDeck = 1;
                player.AddRelicInternal(soul);
            }
        }
    }

    private static void MigrateLegacyHotaruManual(Player player)
    {
        HotaruElectronicSoldierManual hotaru = player.GetRelic<HotaruElectronicSoldierManual>();
        if (hotaru == null) return;
        // The former multiplayer lifecycle attached this exact base type next
        // to Hotaru's starter book. Its common progress belongs to that book.
        // Do not reinterpret another character's upgraded/specialized relic.
        ElectronicSoldierManual[] legacy = player.Relics.OfType<ElectronicSoldierManual>()
            .Where(manual => manual.GetType() == typeof(ElectronicSoldierManual))
            .OrderByDescending(MemoryGardenUpgradePointsHelper.GetTotalSixSensePoints)
            .ThenByDescending(manual => manual.SixSenseAllocationRevision).ToArray();
        if (legacy.Length == 0) return;
        ElectronicSoldierManual source = legacy[0];
        foreach (PropertyInfo property in typeof(ElectronicSoldierManual).GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (property.DeclaringType != typeof(ElectronicSoldierManual) ||
                property.GetCustomAttribute<SavedPropertyAttribute>() == null || property.SetMethod == null)
                continue;
            // Native save annotations define the complete common state. Copy
            // zero/default values as well, without changing Hotaru's own ID,
            // HP initialization guard, owner, or current HP.
            object value = property.GetValue(source);
            property.SetValue(hotaru, value is Array array ? array.Clone() : value);
        }
        foreach (ElectronicSoldierManual duplicate in legacy)
            player.RemoveRelicInternal(duplicate);
    }
}
