using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SkullFeatherTurnPenaltyPower : PowerModel
{
    private const int DrawReduction = 2;
    private const int EnergyReduction = 1;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (Amount > 0 && player == Owner.Player)
        {
            Flash();
            await PlayerCmd.LoseEnergy(EnergyReduction, player);
        }
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        return Amount > 0 && player == Owner.Player
            ? Math.Max(0m, count - DrawReduction)
            : count;
    }

    public override async Task AfterModifyingHandDraw()
    {
        if (Amount <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.TickDownDuration(this);
    }
}
