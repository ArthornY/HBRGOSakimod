using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
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
    }

    private static void OnRunLoaded(RunLoadedEvent evt)
    {
        EnsureRollInfrastructure(evt.RunState, evt.IsMultiplayer);
    }

    private static void EnsureRollInfrastructure(
        RunState runState,
        bool isMultiplayer)
    {
        if (runState?.Players == null ||
            !runState.Players.Any(player =>
                player.Character is HbrgoSaki))
        {
            return;
        }

        foreach (Player player in runState.Players)
        {
            if (player.Character is HbrgoSaki)
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
}
