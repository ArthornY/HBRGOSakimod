using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MoonlightReplayPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("D9B4FF");

    public override int ModifyCardPlayCount(
        CardModel card,
        Creature target,
        int playCount)
    {
        if (card?.Owner?.Creature != Owner || Amount <= 0)
        {
            return playCount;
        }

        Flash();
        return playCount + 1;
    }

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        await PowerCmd.Decrement(this);
    }
}
