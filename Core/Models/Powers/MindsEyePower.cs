using BaseLib.Abstracts;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MindsEyePower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F8D36A");
    public string CustomPackedIconPath => ImageHelper.GetImagePath("powers/tiny_moments_power.png");
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0) return;
        // This is stack consumption on every completed turn, including OD.
        // Positive-duration protection must not suppress the user's -1 rule.
        await PowerCmd.ModifyAmount(context, this, -1m, null, null);
    }
}
