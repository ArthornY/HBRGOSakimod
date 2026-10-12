using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class HowRidiculousHbrgoSaki : CardModel
{
    private const string TargetStrengthLossVar = "TargetStrengthLoss";
    private const string PhantomStrengthVar = "PhantomStrength";

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantoms(Owner).Any();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(TargetStrengthLossVar, 3m),
            new IntVar(PhantomStrengthVar, 1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>(),
            HoverTipFactory.FromPower<StrengthPower>()
        };

    public HowRidiculousHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            cardPlay.Target,
            -DynamicVars[TargetStrengthLossVar].BaseValue,
            Owner.Creature,
            this);
        foreach (var phantom in PhantomHelper
                     .GetLivingPhantoms(Owner)
                     .ToArray())
        {
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                phantom,
                DynamicVars[PhantomStrengthVar].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[TargetStrengthLossVar].UpgradeValueBy(1m);
        DynamicVars[PhantomStrengthVar].UpgradeValueBy(1m);
    }
}
