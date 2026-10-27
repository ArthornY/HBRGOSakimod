using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_MY_HAPPINESS")]
public sealed class HotaruMyHappiness : HotaruCannonCard
{
    private const string LocPrefix = "HOTARU_MY_HAPPINESS";

    public HotaruMyHappiness() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    private HotaruCannonRetirementChoice RetirementChoice => IsMutable
        ? Owner?.GetRelic<Millsage>()?.GetCannonRetirementChoice(HotaruCannonKind.Happiness)
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
        new DynamicVar[] { new IntVar("BaseValueGain", 5m) };

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string suffix = RetirementChoice switch
        {
            HotaruCannonRetirementChoice.Sacrifice => ".sacrificeDescription",
            HotaruCannonRetirementChoice.Redemption => ".redemptionDescription",
            _ => ".normalDescription"
        };
        string key = LocPrefix + suffix;
        if (HbrSkillCardDescription.IsEnabled(this) && new LocString("cards", key + ".hbr").Exists())
            key += ".hbr";
        LocString effect = new("cards", key);
        DynamicVars.AddTo(effect);
        description.Add("HappinessDescription", effect);
    }

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruMyHappinessPower.Apply(context, Owner.Creature,
            DynamicVars["BaseValueGain"].IntValue, this);

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["BaseValueGain"].UpgradeValueBy(2m);
    }
}
