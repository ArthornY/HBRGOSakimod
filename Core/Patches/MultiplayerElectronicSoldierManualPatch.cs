using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class MultiplayerElectronicSoldierManualPatch
{
    private static void Prefix(RunState state)
    {
        if (state?.Players == null ||
            state.Players.Count() <= 1 ||
            !state.Players.Any(player => player.Character is HbrgoSaki))
        {
            return;
        }

        foreach (Player player in state.Players)
        {
            if (player.GetRelic<ElectronicSoldierManual>() != null)
            {
                continue;
            }

            ElectronicSoldierManual manual =
                (ElectronicSoldierManual)ModelDb.Relic<ElectronicSoldierManual>().ToMutable();
            manual.FloorAddedToDeck = 1;
            player.AddRelicInternal(manual);
        }
    }
}
