using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

public static class SixSenseChoiceHelper
{
    public static async Task<SixSenseAttribute?> Choose(
        Player player,
        ElectronicSoldierManual manual,
        IReadOnlyList<SixSenseAttribute> options,
        bool canExceedMaximum = false)
    {
        if (options.Count == 0)
        {
            return null;
        }

        uint choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(player);
        SixSenseAttribute selectedAttribute;
        if (ShouldSelectLocal(player))
        {
            int selectedIndex = TestMode.IsOn
                ? 0
                : await NSixSenseChoiceScreen.ShowScreen(
                    manual,
                    options,
                    canExceedMaximum).ChoiceSelected();
            if (selectedIndex < 0 || selectedIndex >= options.Count)
            {
                return null;
            }

            selectedAttribute = options[selectedIndex];
            RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex((int)selectedAttribute));
        }
        else
        {
            int selectedAttributeValue = (await RunManager.Instance.PlayerChoiceSynchronizer.WaitForRemoteChoice(player, choiceId)).AsIndex();
            if (!System.Enum.IsDefined(typeof(SixSenseAttribute), selectedAttributeValue))
            {
                return null;
            }

            selectedAttribute = (SixSenseAttribute)selectedAttributeValue;
        }

        return canExceedMaximum || manual.CanIncreaseSense(selectedAttribute)
            ? selectedAttribute
            : null;
    }

    private static bool ShouldSelectLocal(Player player)
    {
        return LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay;
    }
}
