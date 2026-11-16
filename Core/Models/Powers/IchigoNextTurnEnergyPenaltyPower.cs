using System.Threading.Tasks;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
namespace HBRGOSakiMod.Core.Models.Powers;
public sealed class IchigoNextTurnEnergyPenaltyPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            string prefix = EnergyIconHelper.GetPrefix(this);
            text.Add("EnergyIcons", string.Concat(Enumerable.Repeat($"[img]res://images/packed/sprite_fonts/{prefix}_energy_icon.png[/img]", System.Math.Max(0, Amount))));
            return text;
        }
    }
    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player || Amount <= 0) return;
        int loss = Amount;
        await PowerCmd.Remove(this);
        await PlayerCmd.LoseEnergy(loss, player);
    }
}
