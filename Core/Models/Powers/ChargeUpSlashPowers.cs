using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ChargeUpSlashNextTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<StrengthPower>()
        };

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player || Amount <= 0)
        {
            return;
        }

        decimal amount = Amount;
        var owner = Owner;
        await PowerCmd.Remove(this);
        await PowerCmd.Apply<ChargeUpSlashTemporaryStrengthPower>(
            new ThrowingPlayerChoiceContext(),
            owner,
            amount,
            owner,
            null);
    }
}

public sealed class ChargeUpSlashTemporaryStrengthPower :
    TemporaryStrengthPower
{
    public override AbstractModel OriginModel =>
        ModelDb.Card<ChargeUpSlashHbrgoSaki>();
}
