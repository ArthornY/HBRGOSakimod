using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class NotAsItShouldBeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/not_as_it_should_be_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[]
        {
            CardKeyword.Exhaust
        };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new CardsVar(3),
            new PowerVar<NextCriticalSuccessPower>(1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[]
        {
            HoverTipFactory.FromPower<NextCriticalSuccessPower>()
        };

    public NotAsItShouldBeHbrgoSaki()
        : base(
            1,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        List<CardModel> cardsToExhaust = Owner.PlayerCombatState
            .DrawPile.Cards
            .Take(DynamicVars.Cards.IntValue)
            .ToList();
        foreach (CardModel card in cardsToExhaust)
        {
            await CardExhaustRuntimeCompatibility.Exhaust(choiceContext, card);
        }

        await PowerCmd.Apply<NextCriticalSuccessPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["NextCriticalSuccessPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
