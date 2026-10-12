using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MoonlightReplayPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("D9B4FF");

    public string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/moonlight_replay_power.tres";

    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override int ModifyCardPlayCount(
        CardModel card,
        Creature target,
        int playCount)
    {
        if (card?.Owner?.Creature != Owner || Amount <= 0 ||
            CardPlayContextHelper.IsAutoPlay(card))
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
