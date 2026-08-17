using System.Threading.Tasks;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

public static class JoiningCeremonyChoiceHelper
{
    public const int ChoiceCount = 9;

    public static async Task<int?> Choose(Player player)
    {
        uint choiceId = RunManager.Instance.PlayerChoiceSynchronizer
            .ReserveChoiceId(player);
        int selectedChoice;
        if (ShouldSelectLocal(player))
        {
            selectedChoice = TestMode.IsOn
                ? 0
                : await NJoiningCeremonyChoiceScreen.ShowScreen()
                    .ChoiceSelected();
            if (selectedChoice < 0 || selectedChoice >= ChoiceCount)
            {
                return null;
            }

            RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                player,
                choiceId,
                PlayerChoiceResult.FromIndex(selectedChoice));
        }
        else
        {
            selectedChoice = (await RunManager.Instance
                    .PlayerChoiceSynchronizer
                    .WaitForRemoteChoice(player, choiceId))
                .AsIndex();
        }

        return selectedChoice >= 0 && selectedChoice < ChoiceCount
            ? selectedChoice
            : null;
    }

    private static bool ShouldSelectLocal(Player player)
    {
        return LocalContext.IsMe(player) &&
               RunManager.Instance.NetService.Type != NetGameType.Replay;
    }
}
