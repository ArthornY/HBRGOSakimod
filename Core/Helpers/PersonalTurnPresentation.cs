using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PersonalTurnPresentation
{
    internal static void Show(Player player)
    {
        if (!LocalContext.IsMe(player) || NCombatRoom.Instance is not { } room) return;
        NPlayerTurnBanner banner = NPlayerTurnBanner.Create(player.PlayerCombatState.TurnNumber);
        if (banner == null) return;
        room.AddChild(banner);
        banner.GetNode<MegaLabel>("Label").SetTextAutoSize(new LocString("gameplay_ui", "PLAYER_TURN_EXTRA").GetFormattedText());
    }
}
