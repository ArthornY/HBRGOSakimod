namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class FabricatedSoulOfAnchor : SoulOfAnchor
{
    protected override int FirstTierUpperBound => 85;

    protected override int SecondTierLowerBound => 86;

    protected override int SecondTierUpperBound => 95;

    protected override int ThirdTierLowerBound => 96;
}
