using Godot;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class QuickSwitchPower : PowerModel
{
    // Legacy save compatibility. Form changes now keep full card effects;
    // no new effect grants, reserves or consumes this obsolete counter.
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

}
