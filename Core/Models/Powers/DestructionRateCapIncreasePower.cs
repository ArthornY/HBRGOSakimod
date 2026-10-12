using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Powers;

// Kept on the target independently of Break and DestructionRatePower so the
// increased cap survives subsequent shell/break cycles in this combat.
public sealed class DestructionRateCapIncreasePower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
    public override LocString Description => !IsMutable || Amount <= 0
        ? LocString.GetIfExists("powers", Id.Entry + ".previewDescription") ?? base.Description
        : base.Description;
    public override Color AmountLabelColor => new("FF9C6E");
    public string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/destruction_rate_power.tres";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
}
