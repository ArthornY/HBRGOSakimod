using System.Globalization;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

// Amount is only a native presence marker. The exact integral values use the
// native SavedString representation, so neither Int32 nor double is involved.
public sealed class LargeHealthPower : PowerModel
{
    [SavedProperty]
    public string CurrentHpData { get; set; } = "0";

    [SavedProperty]
    public string MaxHpData { get; set; } = "0";

    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;
    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    internal decimal CurrentHp => Parse(CurrentHpData);
    internal decimal MaxHp => Parse(MaxHpData);

    internal void Store(decimal current, decimal maximum)
    {
        AssertMutable();
        CurrentHpData = current.ToString(CultureInfo.InvariantCulture);
        MaxHpData = maximum.ToString(CultureInfo.InvariantCulture);
    }

    private static decimal Parse(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture,
            out decimal parsed) ? parsed : 0m;
}
