using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

// The magic sword switch (design v3, section 3; user: 「我们让魔剑做成小开关怎么样？就像hbr
// 的鬼神化那样」). It opens from the HUD button during the owner's own turn with at least one
// swordheart and costs no card or energy. Its crack is settled after the finisher,
// or when combat ends with the mode still open.
internal static class HbrMagicSwordToggle
{
    internal const int MinimumSwordheart = 1;

    internal static bool Applies(Player player) =>
        player?.Character is HbrgoSaki31O && player.Creature?.IsAlive == true;

    internal static bool IsOpen(Player player) => player?.Creature?.HasPower<MagicSwordModePower>() == true;

    internal static int Swordheart(Player player) => player?.Creature?.GetPower<SwordheartPower>()?.Amount ?? 0;

    internal static bool IsOwnPlayPhase(Player player) =>
        CombatManager.Instance?.IsInProgress == true && CombatManager.Instance.PlayerActionsDisabled != true &&
        player?.PlayerCombatState?.Phase == PlayerTurnPhase.Play;

    internal static bool CanOpen(Player player) =>
        Applies(player) && IsOwnPlayPhase(player) && !IsOpen(player) && Swordheart(player) >= MinimumSwordheart;

    internal static bool CanClose(Player player) => Applies(player) && IsOwnPlayPhase(player) && IsOpen(player);

    // Runs inside the synchronized action, on every peer.
    internal static async Task Open(PlayerChoiceContext choiceContext, Player player)
    {
        if (!Applies(player) || IsOpen(player) || Swordheart(player) < MinimumSwordheart) return;
        await MagicSwordModePower.ApplyForNewOpening(choiceContext, player.Creature, null);
    }

    // Closing by hand plays Magic Sword Lucky Seven with the swordheart this opening spent.
    internal static async Task Close(PlayerChoiceContext choiceContext, Player player)
    {
        MagicSwordModePower mode = player?.Creature?.GetPower<MagicSwordModePower>();
        if (mode != null) await mode.ExitByToggle(choiceContext);
    }
}
