using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class BreakInsightPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("6ED7FF");
    public string CustomPackedIconPath => ModelDb.Power<DrawCardsNextTurnPower>().IconPath;
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal Task OnResistancesBroken(PlayerChoiceContext context, int count) =>
        count > 0 && Amount > 0 && Owner?.IsAlive == true && Owner.Player != null
            ? CardPileCmd.Draw(context, (decimal)Amount * count, Owner.Player)
            : Task.CompletedTask;
}
