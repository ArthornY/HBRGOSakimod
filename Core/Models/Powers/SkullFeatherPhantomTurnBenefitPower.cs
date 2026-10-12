using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SkullFeatherPhantomTurnBenefitPower : PowerModel, ICustomPower
{
    private const int DrawBonus = 2;
    private const int EnergyBonus = 1;

    public string CustomPackedIconPath => ModelDb.Power<DrawCardsNextTurnPower>().IconPath;
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (Amount > 0 && player == Owner.Player)
        {
            Flash();
            await PlayerCmd.GainEnergy(EnergyBonus, player);
        }
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        return Amount > 0 && player == Owner.Player
            ? count + DrawBonus
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
