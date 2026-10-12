using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class NowIsTheTimeToReignHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/now_is_the_time_to_reign_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantoms(Owner).Any();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>(),
            HoverTipFactory.FromPower<PhantomTemporaryLevelPower>()
        };

    public NowIsTheTimeToReignHbrgoSaki()
        : base(6, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        foreach (var phantom in PhantomHelper
                     .GetLivingPhantoms(Owner)
                     .ToArray())
        {
            await PowerCmd.Apply<PhantomTemporaryLevelPower>(
                choiceContext,
                phantom,
                1m,
                Owner.Creature,
                null);
        }
    }

    internal void AfterPhantomActed(Creature phantom)
    {
        if (!IsMutable ||
            !PhantomHelper.IsOwnedMainPhantom(phantom, Owner) ||
            Pile?.Type != PileType.Hand)
        {
            return;
        }

        int currentCost = EnergyCost.GetWithModifiers(
            CostModifiers.All);
        if (currentCost > 0)
        {
            EnergyCost.AddThisCombat(-1);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
