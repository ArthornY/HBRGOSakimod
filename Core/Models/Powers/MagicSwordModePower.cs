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
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

// Since 2026-11-09 the magic sword is a switch (HbrMagicSwordToggle) laid over the bow or
// dual swords, which keep working; it no longer forces dual swords, removes 千早同学？ or
// carries the two survival rules (design v3, section 3).
public sealed class MagicSwordModePower : PowerModel
{
    public const int RollBonus = 77;
    internal const int ExtraEnergyPerTurn = 1;
    internal const decimal DeckBonusPerCard = 0.02m;

    private bool _isExiting;
    private bool _isEmergencyRetreat;
    private bool _playedFinisher;
    private static bool _crackLifecycleRegistered;

    // Missing in an old mid-combat save: that opening already paid its crack.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CrackTimingVersion { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasSettledExitCrack { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ConsumedSwordheartAmount { get; private set; }

    public override MegaCrit.Sts2.Core.Localization.LocString Description =>
        HbrgoSakiEnergyIconHelper.Add31OPrefix(base.Description);

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    internal static async Task ApplyForNewOpening(PlayerChoiceContext context, Creature owner, CardModel source)
    {
        if (owner == null || owner.HasPower<MagicSwordModePower>()) return;
        var power = (MagicSwordModePower)ModelDb.Power<MagicSwordModePower>().ToMutable();
        power.CrackTimingVersion = 1;
        power.HasSettledExitCrack = false;
        await PowerCmd.Apply(context, power, owner, 1m, owner, source);
    }

    private void SettleExitCrack(Creature owner)
    {
        if (CrackTimingVersion < 1 || HasSettledExitCrack || _isEmergencyRetreat) return;
        AssertMutable();
        HasSettledExitCrack = true;
        owner?.Player?.GetRelic<ElectronicSoldierManual>()?.AddFateDiceCrack();
    }

    private static void EnsureCrackLifecycle()
    {
        if (_crackLifecycleRegistered) return;
        _crackLifecycleRegistered = true;
        // A loss skips native AfterCombatEnd model hooks. Its final event still
        // has the room's live actors, so settle each holder independently here.
        CombatManager.Instance.CombatEnded += room =>
        {
            if (room?.CombatState == null) return;
            foreach (Player player in room.CombatState.Players)
                player.Creature.GetPower<MagicSwordModePower>()?.SettleExitCrack(player.Creature);
        };
    }

    internal static decimal GetDeckBonusMultiplier(Creature dealer) =>
        dealer?.HasPower<MagicSwordModePower>() == true && dealer.Player != null
            ? 1m + dealer.Player.Deck.Cards.Count * DeckBonusPerCard
            : 1m;

    internal static int GetTurnEnergyBonus(Player player) =>
        player?.Creature?.HasPower<MagicSwordModePower>() == true ? ExtraEnergyPerTurn : 0;

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (dealer != Owner || target?.Side == Owner.Side ||
            amount <= 0m || !props.IsPoweredAttack())
        {
            return 1m;
        }

        return HbrgoSakiDamageMultiplierHelper.AddNonTemporary(
            GetDeckBonusMultiplier(dealer), this);
    }

    public override decimal ModifyPowerAmountGivenMultiplicative(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature target,
        CardModel cardSource)
    {
        return power is DestructionRatePower && giver == Owner &&
               target != null && target.Side != Owner.Side && amount > 0m
            ? GetDeckBonusMultiplier(giver)
            : 1m;
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        if (power is SwordheartPower && power.Owner == Owner && power.Amount <= 0 &&
            !_isEmergencyRetreat && cardSource is not TeleportHbrgoSaki)
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
        if (_isEmergencyRetreat) return;
        _playedFinisher |= await PlayMagicSwordLuckySevenOpening(
            new ThrowingPlayerChoiceContext(),
            oldOwner,
            ConsumedSwordheartAmount);
        if (!_isExiting)
        {
            if (_playedFinisher || CombatManager.Instance.IsOverOrEnding) SettleExitCrack(oldOwner);
            MagicSwordTurnEndHelper.Request(oldOwner);
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        SettleExitCrack(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player || player.PlayerCombatState == null)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(ExtraEnergyPerTurn, player);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _isExiting = false;
        _isEmergencyRetreat = false;
        _playedFinisher = false;
    }

    // Opening keeps the current form (常态, bow or dual swords) and 千早同学？.
    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        EnsureCrackLifecycle();
        await AddFeintCardToCombat();
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

    // Pressing the button again while open (「再按一次按钮手动关掉」) ends it the same
    // way as running out of swordheart.
    internal Task ExitByToggle(PlayerChoiceContext choiceContext) => ExitAndLuckySeven(choiceContext);

    internal async Task ExitForEmergencyRetreat(PlayerChoiceContext context, CardModel source)
    {
        if (_isExiting) return;
        AssertMutable();
        _isExiting = true;
        _isEmergencyRetreat = true;
        HasSettledExitCrack = true;
        ConsumedSwordheartAmount = 0;
        Creature owner = Owner;
        SwordheartPower swordheart = owner.GetPower<SwordheartPower>();
        if (swordheart?.Amount > 0)
            await PowerCmd.ModifyAmount(context, swordheart, -(decimal)swordheart.Amount, owner, source);
        await RemoveForModeTransition();
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
        _playedFinisher |= await PlayMagicSwordLuckySeven(choiceContext, owner, hitCount);
        if (_playedFinisher || CombatManager.Instance.IsOverOrEnding) SettleExitCrack(owner);
        MagicSwordTurnEndHelper.Request(owner);
    }

    internal Task RemoveForModeTransition()
    {
        return PowerCmd.Remove(this);
    }

    private static async Task<bool> PlayMagicSwordLuckySevenOpening(
        PlayerChoiceContext choiceContext,
        Creature owner,
        int consumedSwordheartAmount)
    {
        Player player = owner.Player;
        if (player == null ||
            owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return false;
        }

        MagicSwordLuckySevenOpeningHbrgoSaki deckCard = player.Deck.Cards
            .OfType<MagicSwordLuckySevenOpeningHbrgoSaki>()
            .FirstOrDefault();
        if (deckCard == null)
        {
            return false;
        }

        MagicSwordLuckySevenOpeningHbrgoSaki opening =
            (MagicSwordLuckySevenOpeningHbrgoSaki)
            owner.CombatState.CloneCard(deckCard);
        opening.DeckVersion = deckCard;
        SyncUpgradeLevelFromDeck(opening, deckCard);
        opening.SetConsumedSwordheartAmount(
            consumedSwordheartAmount);
        opening.PrepareForFinisherAttempt();
        await CardCmd.AutoPlay(choiceContext, opening, null);
        await CombatFlowHelper.FinishAutoPlayedCard(opening);
        return opening.HasExecutedForFinisher;
    }

    private static async Task<bool> PlayMagicSwordLuckySeven(PlayerChoiceContext choiceContext, Creature owner, int hitCount)
    {
        Player player = owner.Player;
        if (player == null ||
            owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return false;
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
        luckySeven.PrepareForFinisherAttempt();
        // The finisher show (design v3, section 6) only waits around the autoplay: the card, its hits,
        // targets and order resolve exactly as before, and each peer picks its own show.
        using HbrLuckySevenFinisher show = HbrLuckySevenFinisher.Begin(owner, luckySeven, hitCount);
        await show.BeforeHits();
        await CardCmd.AutoPlay(choiceContext, luckySeven, null);
        await show.AfterHits();
        await CombatFlowHelper.FinishAutoPlayedCard(luckySeven);
        return luckySeven.HasExecutedForFinisher;
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
