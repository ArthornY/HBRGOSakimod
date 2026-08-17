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

public sealed class MagicSwordModeHbrgoSaki : CardModel
{
    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/magic_sword_mode_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override int MaxUpgradeLevel => 0;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Retain,
        CardKeyword.Exhaust
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<MagicSwordModePower>()
    };

    public MagicSwordModeHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<MagicSwordModePower>())
        {
            return;
        }

        await PowerCmd.Apply<MagicSwordModePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }
}
