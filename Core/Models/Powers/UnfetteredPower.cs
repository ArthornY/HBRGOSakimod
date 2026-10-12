using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class UnfetteredPower : PowerModel
{
    private bool _isPlayingGeneratedAttack;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (_isPlayingGeneratedAttack ||
            cardPlay?.Card?.Owner?.Creature != Owner ||
            cardPlay.Card.Type != CardType.Attack ||
            RollKeywordRules.IsCopiedCard(cardPlay.Card) ||
            !cardPlay.IsFirstInSeries ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        ReptureCardMode targetMode = GetOtherMode();
        if (targetMode == ReptureCardMode.None)
        {
            return;
        }

        CardModel generatedAttack = CreateRandomAttack(targetMode);
        if (generatedAttack == null)
        {
            return;
        }

        _isPlayingGeneratedAttack = true;
        try
        {
            Flash();
            using (ModeTransitionHelper.SuppressModeTransition(
                       generatedAttack))
            {
                await CardCmd.AutoPlay(
                    choiceContext,
                    generatedAttack,
                    null,
                    AutoPlayType.Default);
                await CombatFlowHelper.FinishAutoPlayedCard(
                    generatedAttack);
            }
        }
        finally
        {
            try
            {
                await RemoveGeneratedCardFromCombat(generatedAttack);
            }
            finally
            {
                _isPlayingGeneratedAttack = false;
            }
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _isPlayingGeneratedAttack = false;
    }

    private ReptureCardMode GetOtherMode()
    {
        if (Owner.HasPower<ReptureDualSwordsPower>())
        {
            return ReptureCardMode.Bow;
        }

        return Owner.HasPower<ReptureBowPower>()
            ? ReptureCardMode.DualSwords
            : ReptureCardMode.None;
    }

    private CardModel CreateRandomAttack(ReptureCardMode targetMode)
    {
        List<CardModel> candidates = Owner.Player.Deck.Cards
            .Where(card => card.Type == CardType.Attack)
            .Where(card =>
                ModeTransitionHelper.GetCardMode(card) == targetMode)
            .Where(card =>
                !card.Keywords.Contains(CardKeyword.Exhaust))
            .ToList();

        if (candidates.Count == 0)
        {
            StrikeHbrgoSaki strike = Owner.CombatState
                .CreateCard<StrikeHbrgoSaki>(Owner.Player);
            strike.UpgradeInternal();
            strike.FinalizeUpgradeInternal();
            return strike;
        }

        CardModel deckCard = Owner.CombatState.RunState.Rng
            .CombatCardGeneration.NextItem(candidates);
        CardModel generatedAttack = Owner.CombatState
            .CloneCard(deckCard);
        generatedAttack.DeckVersion = deckCard;
        SyncUpgradeLevelFromDeck(generatedAttack, deckCard);
        return generatedAttack;
    }

    private static void SyncUpgradeLevelFromDeck(
        CardModel card,
        CardModel deckCard)
    {
        while (card.CurrentUpgradeLevel <
                   deckCard.CurrentUpgradeLevel &&
               card.IsUpgradable)
        {
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
        }
    }

    private static async Task RemoveGeneratedCardFromCombat(
        CardModel card)
    {
        if (card == null || card.HasBeenRemovedFromState)
        {
            return;
        }

        if (card.Pile?.IsCombatPile == true)
        {
            await CardPileCmd.RemoveFromCombat(
                card,
                skipVisuals: true);
            return;
        }

        card.RemoveFromState();
    }
}
