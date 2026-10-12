using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Relics;

// The permanent deck is the source of truth. Never persist a second counter
// that could drift after a removal, transformation, copy, or save/load.
public abstract class TeamCollectionRelic : RelicModel
{
    private int _lastDisplayedAmount = -1;

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool ShowCounter => IsMutable;
    public override int DisplayAmount => IsMutable ? CountCards(Owner) : 0;
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;

    protected abstract int CountCards(Player player);

    internal virtual void RefreshCounter()
    {
        AssertMutable();
        int amount = DisplayAmount;
        if (amount == _lastDisplayedAmount) return;
        _lastDisplayedAmount = amount;
        InvokeDisplayAmountChanged();
    }
}

public sealed class ThirtyACollection : TeamCollectionRelic
{
    public override string PackedIconPath => ImageHelper.GetImagePath("relics/thirty_a.png");
    protected override int CountCards(Player player) => ThirtyAKeywordRules.GetPermanentDistinctCount(player);
}

public sealed class CryChicCollection : TeamCollectionRelic
{
    // Currently four CRYCHIC cards exist; all four unlock the next rest-site story.
    internal const int RequiredKinds = 4;
    public override string PackedIconPath => ImageHelper.GetImagePath("relics/crychic.png");
    protected override int CountCards(Player player) => CryChicKeywordRules.GetPermanentDistinctCount(player);
}
