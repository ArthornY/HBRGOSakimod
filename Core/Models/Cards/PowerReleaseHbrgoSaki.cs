using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PowerReleaseHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/power_release_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PowerReleasePower>()
        };

    public PowerReleaseHbrgoSaki()
        : base(
            2,
            CardType.Power,
            CardRarity.Uncommon,
            TargetType.Self)
    {
    }

    protected override void AddExtraArgsToDescription(
        LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "UpgradeEffect",
            IsUpgraded || DeckVersion?.IsUpgraded == true
                ? new LocString(
                        "cards",
                        "POWER_RELEASE_HBRGO_SAKI.upgradeEffect")
                    .GetFormattedText()
                : string.Empty);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<PowerReleasePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        if (IsUpgraded || DeckVersion?.IsUpgraded == true)
        {
            Owner.GetRelic<RestorationMoment>()?.GainCounter(5);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
