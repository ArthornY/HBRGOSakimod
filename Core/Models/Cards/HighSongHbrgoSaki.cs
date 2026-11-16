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

public sealed class HighSongHbrgoSaki : CardModel
{
    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/high_song_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override int MaxUpgradeLevel => 0;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Retain
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<HighSongPower>()
    };

    public HighSongHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // A replay (Moonlight's, say) runs OnPlay again after the first run has already
        // replaced this card with a song and removed it from combat. There is nothing left
        // to replace: a second song would duplicate it, and removing the card again throws.
        if (Pile?.IsCombatPile != true)
        {
            return;
        }

        CardModel[] choices =
        {
            Owner.Creature.CombatState.CreateCard<ShadowDanceSongHbrgoSaki>(Owner),
            Owner.Creature.CombatState.CreateCard<BlueSkySongHbrgoSaki>(Owner),
            Owner.Creature.CombatState.CreateCard<LostDaysSongHbrgoSaki>(Owner)
        };

        CardModel selected = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            choices,
            Owner);
        if (selected == null)
        {
            return;
        }

        await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, Owner);
        await CardPileCmd.RemoveFromCombat(this);
    }
}
