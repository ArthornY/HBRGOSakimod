using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_MY_INDULGENCE")]
public sealed class HotaruMyIndulgence : HotaruCannonCard
{
    private const string LocPrefix = "HOTARU_MY_INDULGENCE";

    public HotaruMyIndulgence() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    private HotaruCannonRetirementChoice RetirementChoice => IsMutable
        ? Owner?.GetRelic<Millsage>()?.GetCannonRetirementChoice(HotaruCannonKind.Absolution)
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

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string suffix = RetirementChoice switch
        {
            HotaruCannonRetirementChoice.Sacrifice => IsUpgraded
                ? ".sacrificeUpgradedDescription" : ".sacrificeDescription",
            HotaruCannonRetirementChoice.Redemption => IsUpgraded
                ? ".redemptionUpgradedDescription" : ".redemptionDescription",
            _ => IsUpgraded ? ".normalUpgradedDescription" : ".normalDescription"
        };
        description.Add("IndulgenceDescription", new LocString("cards", LocPrefix + suffix));
    }

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruMyIndulgencePower.Apply(context, Owner.Creature, IsUpgraded, this);

    protected override void OnUpgrade() { }
}
