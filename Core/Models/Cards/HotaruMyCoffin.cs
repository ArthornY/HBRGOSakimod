using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_MY_COFFIN")]
public sealed class HotaruMyCoffin : HotaruCannonCard
{
    private const string LocPrefix = "HOTARU_MY_COFFIN";

    public HotaruMyCoffin() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    private HotaruCannonRetirementChoice RetirementChoice => IsMutable
        ? Owner?.GetRelic<Millsage>()?.GetCannonRetirementChoice(HotaruCannonKind.Coffin)
            ?? HotaruCannonRetirementChoice.None
        : HotaruCannonRetirementChoice.None;

    public override string Title
    {
        get
        {
            string suffix = RetirementChoice switch
            {
                HotaruCannonRetirementChoice.Sacrifice => ".sacrificeTitle",
                HotaruCannonRetirementChoice.Redemption => ".redemptionTitle",
                _ => ".title"
            };
            string title = new LocString("cards", LocPrefix + suffix).GetFormattedText();
            return IsUpgraded ? title + "+" : title;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new IntVar("SacrificePercent", 100m),
        new IntVar("RedemptionPercent", 50m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<DoomPower>()
    };

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string suffix = RetirementChoice switch
        {
            HotaruCannonRetirementChoice.Sacrifice => ".sacrificeDescription",
            HotaruCannonRetirementChoice.Redemption => ".redemptionDescription",
            _ => IsUpgraded ? ".normalUpgradedDescription" : ".normalDescription"
        };
        LocString effect = new("cards", LocPrefix + suffix);
        DynamicVars.AddTo(effect);
        description.Add("CoffinDescription", effect);
    }

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruMyCoffinPower.Apply(context, Owner.Creature, IsUpgraded,
            DynamicVars["SacrificePercent"].BaseValue,
            DynamicVars["RedemptionPercent"].BaseValue, this);

    protected override void OnUpgrade()
    {
        DynamicVars["SacrificePercent"].UpgradeValueBy(100m);
        DynamicVars["RedemptionPercent"].UpgradeValueBy(50m);
    }
}
