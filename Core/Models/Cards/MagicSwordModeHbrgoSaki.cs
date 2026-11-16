using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
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

    protected override bool IsPlayable =>
        Owner?.Character is HbrgoSaki31O &&
        Owner.Creature?.HasPower<MagicSwordModePower>() != true &&
        HbrMagicSwordToggle.Swordheart(Owner) >= HbrMagicSwordToggle.MinimumSwordheart;

    public MagicSwordModeHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false)
    {
    }

    // No longer generated (the magic sword opens from the HUD button since 2026-11-09). A copy
    // left in an older save opens it like the button, deferring the crack until exit.
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Character is not HbrgoSaki31O ||
            Owner.Creature.HasPower<MagicSwordModePower>() ||
            HbrMagicSwordToggle.Swordheart(Owner) < HbrMagicSwordToggle.MinimumSwordheart)
        {
            return;
        }

        await MagicSwordModePower.ApplyForNewOpening(choiceContext, Owner.Creature, this);
    }
}
