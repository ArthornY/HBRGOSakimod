using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class NoSelfInDreamHbrgoSaki : CardModel
{
    private const int _blockPerTemporaryStrength = 3;
    private const decimal _currentDpLossPercent = 0.5m;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/no_self_in_dream_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<NoSelfInDreamPower>(),
        HoverTipFactory.FromPower<NoSelfInDreamTemporaryStrengthPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block)
    };

    public NoSelfInDreamHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int startingBlock = Owner.Creature.Block;
        int temporaryStrength =
            startingBlock / (HbrBattleStatsHelper.IsEnabled(Owner)
                ? _blockPerTemporaryStrength * 100 : _blockPerTemporaryStrength);
        if (startingBlock > 0)
        {
            await CreatureCommandCompatibility.LoseBlock(
                choiceContext,
                Owner.Creature,
                startingBlock,
                Owner.Creature);
        }

        if (temporaryStrength > 0)
        {
            await PowerCmd.Apply<NoSelfInDreamTemporaryStrengthPower>(
                choiceContext,
                Owner.Creature,
                temporaryStrength,
                Owner.Creature,
                this);
        }

        DeflectorProtection protection =
            Owner.GetRelic<DeflectorProtection>();
        protection?.LoseDpByCurrentPercent(
            _currentDpLossPercent);
        if (protection != null)
        {
            await protection.ResolveMentalStrengthDpLossRewards(
                choiceContext,
                this);
        }

        await PowerCmd.Apply<NoSelfInDreamPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
