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
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PuttingOnAirsHbrgoSaki : CardModel
{
    internal const int UpgradedBonusBlock = 6;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantoms(Owner).Any();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.Static(StaticHoverTip.Block),
            HoverTipFactory.FromPower<PhantomPower>(),
            HoverTipFactory.FromPower<PhantomAttackToBlockPower>()
        };

    public PuttingOnAirsHbrgoSaki()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
            await PowerCmd.Apply<PhantomAttackToBlockPower>(
                choiceContext,
                phantom,
                1m,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
    }

    public override bool GainsBlock => true;
}
