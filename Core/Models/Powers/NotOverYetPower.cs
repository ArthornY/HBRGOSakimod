using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NotOverYetPower : PowerModel
{
    public const int RollBonus = 20;

    private const int _firstRollThreshold = 50;
    private const int _rollThresholdStep = 10;
    private const int _maxGeneratedAttacks = 5;
    private const int _allTargetAttackStartIndex = 4;
    private const decimal _finisherMaxHpPercent = 0.3m;

    private static readonly FieldInfo SingleTargetField = AccessTools.Field(typeof(AttackCommand), "_singleTarget");
    private static readonly FieldInfo CombatStateField = AccessTools.Field(typeof(AttackCommand), "_combatState");
    private static readonly FieldInfo IsRandomlyTargetedField = AccessTools.Field(typeof(AttackCommand), "<IsRandomlyTargeted>k__BackingField");
    private static readonly object AllTargetGeneratedCardsLock = new();
    private static readonly HashSet<CardModel> AllTargetGeneratedCards = new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool TriggeredThisTurn { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("D8A184");

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            SetTriggeredThisTurn(false);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (TriggeredThisTurn ||
            Owner.CombatState?.CurrentSide != Owner.Side ||
            cardPlay.Card.Owner?.Creature != Owner ||
            cardPlay.Card.Type != CardType.Attack ||
            cardPlay.IsAutoPlay ||
            !cardPlay.IsFirstInSeries)
        {
            return;
        }

        SetTriggeredThisTurn(true);
        if (CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Creature primaryTarget = GetPrimaryTarget(cardPlay);
        if (primaryTarget == null)
        {
            return;
        }

        SoulOfAnchor soulOfAnchor = Owner.Player?.GetRelic<SoulOfAnchor>();
        if (soulOfAnchor == null)
        {
            return;
        }

        await ResolveExtraRollChain(choiceContext, soulOfAnchor, cardPlay.Card, primaryTarget);
    }

    public override Task BeforeAttack(AttackCommand command)
    {
        if (command.Attacker != Owner ||
            command.ModelSource is not CardModel card ||
            !IsAllTargetGeneratedAttack(card) ||
            command.IsMultiTargeted && !command.IsRandomlyTargeted)
        {
            return Task.CompletedTask;
        }

        RetargetToAllOpponents(command, Owner.CombatState);
        return Task.CompletedTask;
    }

    private async Task ResolveExtraRollChain(
        PlayerChoiceContext choiceContext,
        SoulOfAnchor soulOfAnchor,
        CardModel triggeringCard,
        Creature primaryTarget)
    {
        for (int attackIndex = 1; attackIndex <= _maxGeneratedAttacks; attackIndex++)
        {
            if (CombatManager.Instance.IsOverOrEnding)
            {
                return;
            }

            int roll = await soulOfAnchor.RollExtraD100(choiceContext, triggeringCard);
            int threshold = _firstRollThreshold + (attackIndex - 1) * _rollThresholdStep;
            if (roll < threshold)
            {
                return;
            }

            CardRarity rarity = RollGeneratedCardRarity();
            CardModel generatedCard = CreateGeneratedAttack(rarity);
            Creature target = GetCardTarget(generatedCard, primaryTarget);
            if (generatedCard.TargetType == TargetType.AnyEnemy && target == null)
            {
                await RemoveGeneratedCardFromCombat(generatedCard);
                return;
            }

            Flash();
            await PlayGeneratedAttack(
                choiceContext,
                generatedCard,
                target,
                attackIndex >= _allTargetAttackStartIndex);

            if (attackIndex == _maxGeneratedAttacks &&
                !CombatManager.Instance.IsOverOrEnding &&
                primaryTarget.IsHittable)
            {
                await DealFinisherDamage(choiceContext, primaryTarget, triggeringCard);
            }
        }
    }

    private CardModel CreateGeneratedAttack(CardRarity rarity)
    {
        List<CardModel> candidates = Owner.Player.Deck.Cards
            .Where(card => card.Type == CardType.Attack)
            .Where(card => card.Rarity == rarity)
            .Where(card => card.ShouldPlay(card, AutoPlayType.Default))
            .ToList();

        if (candidates.Count > 0)
        {
            int selectedIndex = Owner.CombatState.RunState.Rng.Niche.NextInt(0, candidates.Count);
            CardModel deckCard = candidates[selectedIndex];
            CardModel copy = Owner.CombatState.CloneCard(deckCard);
            copy.DeckVersion = deckCard;
            SyncUpgradeLevelFromDeck(copy, deckCard);
            return copy;
        }

        StrikeHbrgoSaki strike = Owner.CombatState.CreateCard<StrikeHbrgoSaki>(Owner.Player);
        strike.DynamicVars.Damage.BaseValue *= GetFallbackStrikeMultiplier(rarity);
        return strike;
    }

    private CardRarity RollGeneratedCardRarity()
    {
        int roll = Owner.CombatState.RunState.Rng.Niche.NextInt(1, 101);
        if (roll <= 60)
        {
            return CardRarity.Common;
        }

        return roll <= 90 ? CardRarity.Uncommon : CardRarity.Rare;
    }

    private static decimal GetFallbackStrikeMultiplier(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Uncommon => 2m,
            CardRarity.Rare => 4m,
            _ => 1m
        };
    }

    private async Task PlayGeneratedAttack(
        PlayerChoiceContext choiceContext,
        CardModel card,
        Creature target,
        bool isAllTarget)
    {
        if (isAllTarget)
        {
            AddAllTargetGeneratedCard(card);
        }

        bool costsX = card.EnergyCost.CostsX;
        if (costsX)
        {
            int xEnergy = card.Owner.PlayerCombatState?.Energy ?? 0;
            DualSwordsCopyHelper.RecordXEnergySpent(card, xEnergy);
        }

        try
        {
            await CardCmd.AutoPlay(
                choiceContext,
                card,
                target,
                AutoPlayType.Default,
                skipCardPileVisuals: true);
            await CombatFlowHelper.FinishAutoPlayedCard(card);
        }
        finally
        {
            DualSwordsCopyHelper.Clear(card);
            RemoveAllTargetGeneratedCard(card);
            await RemoveGeneratedCardFromCombat(card);
        }
    }

    private static async Task RemoveGeneratedCardFromCombat(CardModel card)
    {
        if (card == null || card.HasBeenRemovedFromState)
        {
            return;
        }

        if (card.Pile?.IsCombatPile == true)
        {
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
            return;
        }

        card.RemoveFromState();
    }

    private async Task DealFinisherDamage(
        PlayerChoiceContext choiceContext,
        Creature primaryTarget,
        CardModel cardSource)
    {
        decimal damage = Math.Max(1m, Math.Floor(primaryTarget.MaxHp * _finisherMaxHpPercent));
        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            primaryTarget,
            damage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner,
            cardSource);
    }

    private Creature GetPrimaryTarget(CardPlay cardPlay)
    {
        if (cardPlay.Target != null && cardPlay.Target.Side != Owner.Side)
        {
            return cardPlay.Target;
        }

        return GetHittableOpponent();
    }

    private Creature GetCardTarget(CardModel card, Creature primaryTarget)
    {
        return card.TargetType switch
        {
            TargetType.AnyEnemy => primaryTarget.IsHittable ? primaryTarget : GetHittableOpponent(),
            TargetType.Self or TargetType.AnyAlly => Owner,
            _ => null
        };
    }

    private Creature GetHittableOpponent()
    {
        return Owner.CombatState
            .GetOpponentsOf(Owner)
            .FirstOrDefault(creature => creature.IsHittable);
    }

    private void SetTriggeredThisTurn(bool value)
    {
        AssertMutable();
        TriggeredThisTurn = value;
    }

    private static void SyncUpgradeLevelFromDeck(CardModel card, CardModel deckCard)
    {
        while (card.CurrentUpgradeLevel < deckCard.CurrentUpgradeLevel && card.IsUpgradable)
        {
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
        }
    }

    internal static bool IsAllTargetGeneratedAttack(CardModel card)
    {
        lock (AllTargetGeneratedCardsLock)
        {
            return AllTargetGeneratedCards.Contains(card);
        }
    }

    private static void AddAllTargetGeneratedCard(CardModel card)
    {
        lock (AllTargetGeneratedCardsLock)
        {
            AllTargetGeneratedCards.Add(card);
        }
    }

    private static void RemoveAllTargetGeneratedCard(CardModel card)
    {
        lock (AllTargetGeneratedCardsLock)
        {
            AllTargetGeneratedCards.Remove(card);
        }
    }

    private static void RetargetToAllOpponents(AttackCommand command, ICombatState combatState)
    {
        SingleTargetField.SetValue(command, null);
        CombatStateField.SetValue(command, null);
        IsRandomlyTargetedField.SetValue(command, false);
        command.TargetingAllOpponents(combatState);
    }
}
