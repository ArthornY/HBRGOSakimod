using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_UNENDING_CONTEST")]
public sealed class HotaruUnendingContest : HotaruCannonCard
{
    private const string LocPrefix = "HOTARU_UNENDING_CONTEST";

    public HotaruUnendingContest() : base(0, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    private HotaruCannonRetirementChoice RetirementChoice => IsMutable
        ? Owner?.GetRelic<Millsage>()?.GetCannonRetirementChoice(HotaruCannonKind.Contest)
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

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new IntVar("Contest", 1m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        RetirementChoice switch
        {
            HotaruCannonRetirementChoice.Sacrifice => HoverTipFactory.FromPower<HotaruContestSacrificePower>(),
            HotaruCannonRetirementChoice.Redemption => HoverTipFactory.FromPower<HotaruContestRedemptionPower>(),
            _ => HoverTipFactory.FromPower<HotaruContestPower>()
        }
    };

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string suffix = RetirementChoice switch
        {
            HotaruCannonRetirementChoice.Sacrifice => ".sacrificeDescription",
            HotaruCannonRetirementChoice.Redemption => ".redemptionDescription",
            _ => ".normalDescription"
        };
        LocString effect = new("cards", LocPrefix + suffix);
        DynamicVars.AddTo(effect);
        description.Add("ContestDescription", effect);
    }

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        decimal amount = DynamicVars["Contest"].BaseValue;
        return RetirementChoice switch
        {
            HotaruCannonRetirementChoice.Sacrifice => PowerCmd.Apply<HotaruContestSacrificePower>(
                context, play.Target, amount, Owner.Creature, this),
            HotaruCannonRetirementChoice.Redemption => PowerCmd.Apply<HotaruContestRedemptionPower>(
                context, play.Target, amount, Owner.Creature, this),
            _ => PowerCmd.Apply<HotaruContestPower>(context, play.Target, amount, Owner.Creature, this)
        };
    }

    protected override void OnUpgrade() => DynamicVars["Contest"].UpgradeValueBy(1m);
}
