using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MagicSwordEnhancementHbrgoSaki : CardModel
{
    private bool HasSwordheartUpgrade => IsUpgraded || DeckVersion?.IsUpgraded == true;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/magic_sword_enhancement_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<MagicSwordEnhancementPower>(),
        HoverTipFactory.FromPower<MagicSwordModePower>(),
        HoverTipFactory.FromPower<SwordheartPower>()
    };

    public MagicSwordEnhancementHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<MagicSwordEnhancementPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        if (HasSwordheartUpgrade)
        {
            await PowerCmd.Apply<SwordheartPower>(
                choiceContext,
                Owner.Creature,
                1m,
                Owner.Creature,
                this);
        }
    }
}
