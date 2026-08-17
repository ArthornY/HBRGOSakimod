using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;

namespace HBRGOSakiMod.Core.Integration;

internal static class HBRGOSakiRunLifecycle
{
    private static IDisposable _runStartedSubscription;

    public static void Register()
    {
        _runStartedSubscription ??=
            RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(
                OnRunStarted,
                replayCurrentState: false);
    }

    private static void OnRunStarted(RunStartedEvent evt)
    {
        if (!evt.IsMultiplayer ||
            evt.RunState?.Players == null ||
            evt.RunState.Players.Count() <= 1 ||
            !evt.RunState.Players.Any(player => player.Character is HbrgoSaki))
        {
            return;
        }

        foreach (Player player in evt.RunState.Players)
        {
            if (player.Character is HbrgoSaki)
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

            if (player.GetRelic<FabricatedSoulOfAnchor>() == null)
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
