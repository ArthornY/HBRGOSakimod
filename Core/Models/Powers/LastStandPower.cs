using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class LastStandPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F05A67");

    public override async Task AfterEnergyReset(Player player)
    {
        if (player == Owner.Player)
        {
            await PlayerCmd.GainEnergy(1m, player);
        }
    }

    public override decimal ModifyHandDrawLate(
        Player player,
        decimal count)
    {
        return player == Owner.Player
            ? count + 2m
            : count;
    }
}
