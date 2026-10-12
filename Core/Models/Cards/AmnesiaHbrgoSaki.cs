using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class AmnesiaHbrgoSaki : CardModel
{
    public override int MaxUpgradeLevel => 0;

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            "packed/card_portraits/hbrgo_saki/amnesia_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<AmnesiaDrawReductionPower>(1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<AmnesiaDrawReductionPower>()
        };

    public AmnesiaHbrgoSaki()
        : base(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
    {
    }

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (card != this)
        {
            return;
        }

        await Cmd.Wait(0.25f);
        await PowerCmd.Apply<AmnesiaDrawReductionPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["AmnesiaDrawReductionPower"].BaseValue,
            Owner.Creature,
            this);
    }
}
