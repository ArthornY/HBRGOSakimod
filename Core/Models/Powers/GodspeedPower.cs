using Godot;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

// Godspeed (神速) cut one more point from each six-sense resistance. The
// resistances were removed on 2026-11-09 and the card retired; the power stays
// for saved runs that still hold the card and changes nothing.
public sealed class GodspeedPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("6ED7FF");
}
