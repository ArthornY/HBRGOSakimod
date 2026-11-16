using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class QuickSwitchHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/quick_switch_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new CardsVar(1)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<ReptureDualSwordsPower>(),
            HoverTipFactory.FromPower<ReptureBowPower>()
        };

    public QuickSwitchHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override void AddExtraArgsToDescription(
        LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("TargetMode", GetTargetModeCardText());
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<ReptureDualSwordsPower>())
        {
            await PowerCmd.Apply<ReptureBowPower>(
                choiceContext,
                Owner.Creature,
                1m,
                Owner.Creature,
                this);
        }
        else
        {
            await PowerCmd.Apply<ReptureDualSwordsPower>(
                choiceContext,
                Owner.Creature,
                1m,
                Owner.Creature,
                this);
        }

        await CardPileCmd.Draw(
            choiceContext,
            DynamicVars.Cards.BaseValue,
            Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }

    private string GetTargetModeCardText()
    {
        if (!IsMutable || Owner?.Creature == null)
        {
            return "[gold]Rapture-双剑[/gold]/[gold]Rapture-弓[/gold]";
        }

        return Owner.Creature.HasPower<ReptureDualSwordsPower>()
            ? "[gold]Rapture-弓[/gold]"
            : "[gold]Rapture-双剑[/gold]";
    }
}
