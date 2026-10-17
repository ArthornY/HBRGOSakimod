using System;
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

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_SEE_YOU_AGAIN")]
public sealed class HotaruSeeYouAgain : HotaruCannonCard
{
    private const string LocPrefix = "HOTARU_SEE_YOU_AGAIN";

    public HotaruSeeYouAgain() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    private HotaruCannonRetirementChoice RetirementChoice => IsMutable
        ? Owner?.GetRelic<Millsage>()?.GetCannonRetirementChoice(HotaruCannonKind.Longing)
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

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new IntVar("RecallLevel", 4m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        RetirementChoice == HotaruCannonRetirementChoice.None
            ? new IHoverTip[] { HoverTipFactory.FromPower<HotaruSeeYouAgainPower>() }
            : Array.Empty<IHoverTip>();

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string suffix = RetirementChoice switch
        {
            HotaruCannonRetirementChoice.Sacrifice => IsUpgraded
                ? ".sacrificeUpgradedDescription" : ".sacrificeDescription",
            HotaruCannonRetirementChoice.Redemption => ".redemptionDescription",
            _ => ".normalDescription"
        };
        LocString effect = new("cards", LocPrefix + suffix);
        DynamicVars.AddTo(effect);
        description.Add("SeeYouAgainDescription", effect);
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        switch (RetirementChoice)
        {
            case HotaruCannonRetirementChoice.Sacrifice:
                await HotaruSeeYouAgainCardChoice.SelectAndGenerate(context, this, IsUpgraded);
                break;
            case HotaruCannonRetirementChoice.Redemption:
                if (Owner.GetRelic<Millsage>() is { } relic)
                    await relic.RecallRandomRetiredCannon(context, DynamicVars["RecallLevel"].IntValue, this);
                break;
            default:
                await HotaruSeeYouAgainPower.Apply(context, Owner.Creature, this);
                break;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["RecallLevel"].UpgradeValueBy(2m);
        AddKeyword(CardKeyword.Retain);
    }
}
