using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class HastePower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("6ED7FF");
    public string CustomPackedIconPath => ModelDb.Power<DexterityPower>().IconPath;
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal Task OnResistancesBroken(PlayerChoiceContext context, CardModel card, int count) =>
        count > 0 && Amount > 0 && Owner.IsAlive
            ? PowerCmd.Apply<DexterityPower>(context, Owner, (decimal)Amount * count, Owner, card)
            : Task.CompletedTask;
}
