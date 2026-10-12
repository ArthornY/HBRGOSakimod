using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using System.Collections.Generic;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ThirtyOneACollection : TeamCollectionRelic
{
    internal bool HasBlockConversion => !IsMelted && DisplayAmount >= 2;
    internal bool CountsDodgeAsBlock => !IsMelted && DisplayAmount >= 4;
    internal bool GrantsDodgeDamageBonus => !IsMelted;
    internal bool HasExpandedDodge => !IsMelted && DisplayAmount >= 6;

    internal override void RefreshCounter()
    {
        base.RefreshCounter();
        if (IsMutable) Owner?.Creature?.GetPower<DodgePower>()?.RefreshMaximum();
    }

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/thirty_one_a.png");
    protected override int CountCards(Player player) => ThirtyOneAKeywordRules.GetPermanentDistinctCount(player);
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<DodgePower>() };
}
