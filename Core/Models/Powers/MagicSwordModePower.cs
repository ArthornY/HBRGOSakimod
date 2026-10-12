using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MagicSwordModePower : PowerModel
{
    public const int RollBonus = 77;

    private const decimal _dpRecoverThreshold = 0.75m;
    private const decimal _dpRecoverPercent = 0.25m;

    private bool _pendingDpBreak;
    private bool _isExiting;
    private bool _isGrantingClaritySwordheart;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ConsumedSwordheartAmount { get; private set; }

    public override MegaCrit.Sts2.Core.Localization.LocString Description =>
        HbrgoSakiEnergyIconHelper.Add31OPrefix(base.Description);

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    internal bool IsGrantingClaritySwordheart =>
        _isGrantingClaritySwordheart;

    public void MarkDpBrokenByDamage()
    {
        if (!IsMutable)
        {
            return;
        }

        AssertMutable();
        _pendingDpBreak = true;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner || !_pendingDpBreak)
        {
            return;
        }

        _pendingDpBreak = false;
        await SetSwordheartToZero(choiceContext);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        DeflectorProtection deflectorProtection = Owner.Player?.GetRelic<DeflectorProtection>();
        if (DisabledPower.IsAtMax(Owner))
        {
            return;
        }

        if (deflectorProtection != null && deflectorProtection.IsDpBelowPercent(_dpRecoverThreshold))
        {
            int restoredDp = deflectorProtection.RestoreDpByMaxPercent(_dpRecoverPercent);
            if (restoredDp > 0)
            {
                Flash();
            }

            await PowerCmd.Apply<DisabledPower>(choiceContext, Owner, 1m, Owner, null);
            await SpendSwordheart(choiceContext, 1m, null);
        }
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        if (power is SwordheartPower && power.Owner == Owner && power.Amount <= 0)
        {
            await ExitAndLuckySeven(choiceContext);
        }
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature != Owner)
        {
            return;
        }

        if (!MagicSwordEnergyPaymentHelper.TryTakePendingSwordheartCost(cardPlay.Card, out int swordheartCost))
        {
            return;
        }

        SwordheartPower swordheart = Owner.GetPower<SwordheartPower>();
        if (swordheart == null || swordheart.Amount <= 0)
        {
            await ExitAndLuckySeven(new ThrowingPlayerChoiceContext());
            return;
        }

        Flash();
        await ConsumeSwordheart(new ThrowingPlayerChoiceContext(), swordheartCost, cardPlay.Card);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await RemoveFeintCards(oldOwner);
        await ModeTransitionHelper.EnterDualSwordsAfterModeExit(oldOwner);
        await PlayMagicSwordLuckySevenOpening(
            new ThrowingPlayerChoiceContext(),
            oldOwner,
            ConsumedSwordheartAmount);
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player || player.PlayerCombatState == null)
        {
            return;
        }

        Flash();
        player.PlayerCombatState.LoseEnergy(1);
        await Task.CompletedTask;
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel clonedBy)
    {
        if (card is ChihayaClassmateHbrgoSaki &&
            card.Owner?.Creature == Owner &&
            card.Pile?.IsCombatPile == true &&
            card.Pile.Type != PileType.Play)
        {
            await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
        }

        await ModeTransitionHelper.HandleIncompatibleCardEnteredHand(
            Owner,
            card,
            oldPileType);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingDpBreak = false;
        _isExiting = false;
        _isGrantingClaritySwordheart = false;
    }

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        await PowerCmd.Remove(Owner.GetPower<NormalStatePower>());
        await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
        int discardedCardCount =
            await ModeTransitionHelper.DiscardIncompatibleCardsFromHandAndRedraw(Owner);
        await AddFeintCardToCombat();
        if (discardedCardCount > 0)
        {
            Flash();
        }
    }

    public async Task AddFeintCardToCombat()
    {
        Player player = Owner.Player;
        if (player?.PlayerCombatState == null || Owner.CombatState == null)
        {
            return;
        }

        bool alreadyHasFeint = player.PlayerCombatState.AllCards
            .Any(card => card is FeintHbrgoSaki);
        if (alreadyHasFeint)
        {
            return;
        }

        CardModel feint = Owner.CombatState.CreateCard<FeintHbrgoSaki>(player);
        await CardPileCmd.AddGeneratedCardToCombat(feint, PileType.Hand, player);
    }

    public async Task SpendSwordheart(PlayerChoiceContext choiceContext, decimal amount, CardModel cardSource)
    {
        SwordheartPower swordheart = Owner.GetPower<SwordheartPower>();
        if (swordheart == null || swordheart.Amount <= 0)
        {
            await ExitAndLuckySeven(choiceContext);
            return;
        }

        await ConsumeSwordheart(choiceContext, amount, cardSource);
    }

    internal async Task GrantSwordheartFromClarity(
        PlayerChoiceContext choiceContext,
        CardModel cardSource)
    {
        SwordheartPower swordheart = Owner.GetPower<SwordheartPower>();
        if (swordheart == null || swordheart.Amount <= 0) return;

        await GrantSwordheartIgnoringMagicSwordRestriction(
            choiceContext,
            (decimal)swordheart.Amount,
            cardSource);
    }

    internal async Task GrantSwordheartIgnoringMagicSwordRestriction(
        PlayerChoiceContext choiceContext,
        decimal amount,
        CardModel cardSource)
    {
        if (amount <= 0m)
        {
            return;
        }

        _isGrantingClaritySwordheart = true;
        try
        {
            await PowerCmd.Apply<SwordheartPower>(
                choiceContext,
                Owner,
                amount,
                Owner,
                cardSource);
        }
        finally
        {
            _isGrantingClaritySwordheart = false;
        }
    }

    private async Task SetSwordheartToZero(PlayerChoiceContext choiceContext)
    {
        SwordheartPower swordheart = Owner.GetPower<SwordheartPower>();
        if (swordheart == null || swordheart.Amount <= 0)
        {
            await ExitAndLuckySeven(choiceContext);
            return;
        }

        await ConsumeSwordheart(choiceContext, swordheart.Amount, null);
    }

    private async Task ConsumeSwordheart(PlayerChoiceContext choiceContext, decimal amount, CardModel cardSource)
    {
        SwordheartPower swordheart = Owner.GetPower<SwordheartPower>();
        if (swordheart == null || swordheart.Amount <= 0)
        {
            await ExitAndLuckySeven(choiceContext);
            return;
        }

        decimal amountToConsume = SwordheartClarityPower.GetSwordheartConsumptionAmount(Owner, amount);
        decimal consumedAmount = Math.Min(swordheart.Amount, amountToConsume);
        if (consumedAmount <= 0)
        {
            return;
        }

        AddConsumedSwordheartAmount(consumedAmount);
        await PowerCmd.ModifyAmount(choiceContext, swordheart, -consumedAmount, Owner, cardSource);
    }

    private void AddConsumedSwordheartAmount(decimal amount)
    {
        AssertMutable();
        ConsumedSwordheartAmount += (int)amount;
    }

    private async Task ExitAndLuckySeven(PlayerChoiceContext choiceContext)
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        Creature owner = Owner;
        int hitCount = ConsumedSwordheartAmount;
        Flash();
        await RemoveForModeTransition();
        await PlayMagicSwordLuckySeven(choiceContext, owner, hitCount);
    }

    internal Task RemoveForModeTransition()
    {
        return PowerCmd.Remove(this);
    }

    private static async Task PlayMagicSwordLuckySevenOpening(
        PlayerChoiceContext choiceContext,
        Creature owner,
        int consumedSwordheartAmount)
    {
        Player player = owner.Player;
        if (player == null ||
            owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        MagicSwordLuckySevenOpeningHbrgoSaki deckCard = player.Deck.Cards
            .OfType<MagicSwordLuckySevenOpeningHbrgoSaki>()
            .FirstOrDefault();
        if (deckCard == null)
        {
            return;
        }

        MagicSwordLuckySevenOpeningHbrgoSaki opening =
            (MagicSwordLuckySevenOpeningHbrgoSaki)
            owner.CombatState.CloneCard(deckCard);
        opening.DeckVersion = deckCard;
        SyncUpgradeLevelFromDeck(opening, deckCard);
        opening.SetConsumedSwordheartAmount(
            consumedSwordheartAmount);
        await CardCmd.AutoPlay(choiceContext, opening, null);
        await CombatFlowHelper.FinishAutoPlayedCard(opening);
    }

    private static async Task PlayMagicSwordLuckySeven(PlayerChoiceContext choiceContext, Creature owner, int hitCount)
    {
        Player player = owner.Player;
        if (player == null ||
            owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        MagicSwordLuckySevenHbrgoSaki deckCard = player.Deck.Cards
            .OfType<MagicSwordLuckySevenHbrgoSaki>()
            .FirstOrDefault();
        MagicSwordLuckySevenHbrgoSaki luckySeven = deckCard != null
            ? (MagicSwordLuckySevenHbrgoSaki)owner.CombatState.CloneCard(deckCard)
            : owner.CombatState.CreateCard<MagicSwordLuckySevenHbrgoSaki>(player);
        if (deckCard != null)
        {
            luckySeven.DeckVersion = deckCard;
            SyncUpgradeLevelFromDeck(luckySeven, deckCard);
        }

        luckySeven.SetMagicSwordModeHitCount(hitCount);
        await CardCmd.AutoPlay(choiceContext, luckySeven, null);
        await CombatFlowHelper.FinishAutoPlayedCard(luckySeven);
    }

    private static void SyncUpgradeLevelFromDeck(CardModel card, CardModel deckCard)
    {
        while (card.CurrentUpgradeLevel < deckCard.CurrentUpgradeLevel && card.IsUpgradable)
        {
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
        }
    }

    private static async Task RemoveFeintCards(Creature oldOwner)
    {
        Player player = oldOwner.Player;
        if (player?.PlayerCombatState == null)
        {
            return;
        }

        List<CardModel> feints = player.PlayerCombatState.AllCards
            .Where(card => card is FeintHbrgoSaki)
            .ToList();
        if (feints.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(feints);
        }
    }
}
