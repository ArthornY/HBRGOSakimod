using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class SakuraRainHbrgoSaki : CardModel
{
    internal bool HasRetaliationUpgrade => IsUpgraded || DeckVersion?.IsUpgraded == true;

    public override string Title => HasRetaliationUpgrade
        ? new LocString("cards", "SAKURA_RAIN_HBRGO_SAKI.upgradedTitle").GetFormattedText()
        : base.Title;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/sakura_rain_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<SakuraRainPower>()
    };

    public SakuraRainHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "UpgradeEffect",
            HasRetaliationUpgrade
                ? new LocString(
                        "cards",
                        "SAKURA_RAIN_HBRGO_SAKI.upgradeEffect")
                    .GetFormattedText()
                : string.Empty);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SakuraRainPower previous = Owner.Creature.GetPower<SakuraRainPower>();
        await PowerCmd.Apply<SakuraRainPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        if (previous != null && Owner.Creature.GetPower<SakuraRainPower>() == previous)
            previous.AddCopy();

        if (HasRetaliationUpgrade)
        {
            Owner.Creature.GetPower<SakuraRainPower>()?.EnableRetaliation();
        }
    }
}
