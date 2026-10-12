using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class CrushYouLikeABugReplayPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("D9B4FF");
    public string CustomPackedIconPath => ImageHelper.GetImagePath("powers/tiny_moments_power.png");
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    private bool CanReplay(CardModel card) => Amount > 0 &&
        card?.Owner?.Creature == Owner && !card.IsDupe && !DualSwordsCopyHelper.IsDualSwordsCopy(card);

    public override int ModifyCardPlayCount(CardModel card, Creature target, int playCount) =>
        CanReplay(card) ? (int)Math.Min(int.MaxValue, (long)playCount + 1) : playCount;

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (!CanReplay(card)) return Task.CompletedTask;
        Flash();
        // This native hook occurs before any OnPlay in the selected series.
        // Newly granted layers during that series have no end-of-card consumer.
        return PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1m, null, card);
    }
}
