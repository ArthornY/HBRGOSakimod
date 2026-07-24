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
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ChihayaClassmateHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/chihaya_classmate_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<NormalStatePower>(),
        HoverTipFactory.FromPower<ReptureBowPower>(),
        HoverTipFactory.FromPower<ReptureDualSwordsPower>()
    };

    public ChihayaClassmateHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel selectedMode = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            new CardModel[]
            {
                CombatState.CreateCard<ReptureBowChoiceHbrgoSaki>(Owner),
                CombatState.CreateCard<ReptureDualSwordsChoiceHbrgoSaki>(Owner)
            },
            Owner);

        if (selectedMode == null)
        {
            return;
        }

        await EnterSelectedRepture(choiceContext, selectedMode);
        await DrawModeRestrictedCard(choiceContext, selectedMode);
        await ReplaceWithSwitchInHand(choiceContext);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }

    private async Task ReplaceWithSwitchInHand(PlayerChoiceContext choiceContext)
    {
        CardModel switchCard = CombatState.CreateCard<SwitchHbrgoSaki>(Owner);
        SyncUpgradeLevel(switchCard);

        await CardPileCmd.AddGeneratedCardToCombat(switchCard, PileType.Hand, Owner);
        await CardPileCmd.RemoveFromCombat(this);
    }

    private async Task EnterSelectedRepture(PlayerChoiceContext choiceContext, CardModel selectedMode)
    {
        using (ModeTransitionHelper.SuppressDualSwordsOnModeExit())
        {
            if (Owner.Creature.HasPower<NormalStatePower>())
            {
                await PowerCmd.Remove(Owner.Creature.GetPower<NormalStatePower>());
            }

            if (Owner.Creature.HasPower<ReptureBowPower>())
            {
                await PowerCmd.Remove(Owner.Creature.GetPower<ReptureBowPower>());
            }

            if (Owner.Creature.HasPower<ReptureDualSwordsPower>())
            {
                await PowerCmd.Remove(Owner.Creature.GetPower<ReptureDualSwordsPower>());
            }
        }

        if (selectedMode is ReptureDualSwordsChoiceHbrgoSaki)
        {
            await PowerCmd.Apply<ReptureDualSwordsPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            return;
        }

        await PowerCmd.Apply<ReptureBowPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    private async Task DrawModeRestrictedCard(PlayerChoiceContext choiceContext, CardModel selectedMode)
    {
        CardPile drawPile = Owner.PlayerCombatState.DrawPile;
        CardModel restrictedCard = drawPile.Cards.FirstOrDefault(card =>
            IsRestrictedToSelectedMode(card, selectedMode));
        if (restrictedCard == null)
        {
            return;
        }

        await CardPileCmd.Add(
            restrictedCard,
            drawPile,
            CardPilePosition.Top,
            this,
            skipVisuals: true);
        await CardPileCmd.Draw(choiceContext, Owner);
    }

    private static bool IsRestrictedToSelectedMode(CardModel card, CardModel selectedMode)
    {
        return selectedMode switch
        {
            ReptureBowChoiceHbrgoSaki =>
                card is ArrowRainHbrgoSaki or SlideStepHbrgoSaki or StrongShotHbrgoSaki,
            ReptureDualSwordsChoiceHbrgoSaki =>
                card is CrossSlashHbrgoSaki
                    or SwallowReversalHbrgoSaki
                    or BladeDanceHbrgoSaki
                    or MimicDreamBubbleHbrgoSaki,
            _ => false
        };
    }

    private void SyncUpgradeLevel(CardModel card)
    {
        while (card.CurrentUpgradeLevel < CurrentUpgradeLevel && card.IsUpgradable)
        {
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
        }
    }
}
