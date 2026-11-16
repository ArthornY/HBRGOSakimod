using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

// These abilities explicitly count inserted turns. Do not replay every native
// positive-power turn callback, or disturb the suspended ordinary energy pool.
internal static class HbrInsertedTurnStartHelper
{
    internal static async Task Start(PlayerChoiceContext context, Player player)
    {
        if (player?.Creature.IsAlive != true) return;
        await ExtraServingCombatHelper.AfterEnergyReset(player);
        await PhantomsOfBygoneDaysThirtyA.AfterInsertedEnergyReset(player);
        await ThirtyOneXDelayedTurnHelper.AfterEnergyReset(context, player);
        MatsuokaBlowAwayCombatHelper.TurnStart(player);
        await MariaRepentanceCombatHelper.OnOwnTurnStart(context, player);
        await VritikaCalculationCombatHelper.OnOwnTurnStart(context, player);
        await ThirtyGBondHelper.TurnStart(context, player);
        if (player.GetRelic<ThirtyOneDCollection>() is { } intelligence)
            await ThirtyOneDBondHelper.TurnStart(context, intelligence);
        if (player.Creature.GetPower<MuuaIWillNotFallPower>() is { } faith)
            await MuuaFaithCombatHelper.TurnStart(context, faith);
        await KiryuExorcisingStringCombatHelper.StartInsertedTurn(context, player);
    }
}
