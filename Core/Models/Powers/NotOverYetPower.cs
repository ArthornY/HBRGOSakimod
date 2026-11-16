using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NotOverYetPower : PowerModel
{
    public const int RollBonus = 5;

    private const decimal FirstRollThreshold = 50m;
    private const decimal RollThresholdMultiplier = 1.5m;
    private const int _allTargetAttackStartIndex = 4;
    private const decimal _finisherMaxHpPercent = 0.3m;

    private static readonly FieldInfo SingleTargetField =
        PrivateAccess.Field<AttackCommand>("_singleTarget");
    private static readonly FieldInfo CombatStateField =
        PrivateAccess.Field<AttackCommand>("_combatState");
    private static readonly FieldInfo IsRandomlyTargetedField =
        PrivateAccess.Field<AttackCommand>("<IsRandomlyTargeted>k__BackingField");
    private static readonly object AllTargetGeneratedCardsLock = new();
    private static readonly HashSet<CardModel> AllTargetGeneratedCards = new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool TriggeredThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TriggersUsedThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasFourthAttack { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasFifthAttack { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool EnhancedRollBonus { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasAdvantageRolls { get; private set; }

    public int MaxGeneratedAttacks => HasFifthAttack ? 5 : HasFourthAttack ? 4 : 3;
    public int AttackRollBonus => RollBonus + (EnhancedRollBonus ? 10 : 0);

    internal void EnableUpgrades(bool fourthAttack, bool fifthAttack, bool enhancedRollBonus, bool advantageRolls)
    {
        AssertMutable();
        HasFourthAttack |= fourthAttack || fifthAttack;
        HasFifthAttack |= fifthAttack;
        EnhancedRollBonus |= enhancedRollBonus;
        HasAdvantageRolls |= advantageRolls;
        Owner?.InvokePowerModified(this, 0, true);
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("D8A184");

    public override LocString Description => IsMutable
        ? CreateDescription(false, AttackRollBonus, MaxGeneratedAttacks, HasAdvantageRolls,
            Owner?.Player?.Character is HbrgoSaki31O)
        : base.Description;

    internal LocString CompactDescription => CreateDescription(true, AttackRollBonus,
        MaxGeneratedAttacks, HasAdvantageRolls, false);

    internal static LocString GetCardCompactDescription(SoyoAidHbrgoSaki card)
    {
        NotOverYetPower active = card.IsMutable ? card.Owner?.Creature?.GetPower<NotOverYetPower>() : null;
        int maxRolls = card.HasUpgradeEffect(1) ? 5 : card.HasUpgradeEffect(0) ? 4 : 3;
        int bonus = card.HasUpgradeEffect(5) ? 15 : RollBonus;
        return CreateDescription(true, Math.Max(bonus, active?.AttackRollBonus ?? RollBonus),
            Math.Max(maxRolls, active?.MaxGeneratedAttacks ?? 3),
            card.HasUpgradeEffect(6) || active?.HasAdvantageRolls == true, false);
    }

    private static LocString CreateDescription(bool compact, int bonus, int maxRolls,
        bool advantage, bool swordheart)
    {
        LocString text = new("powers", "NOT_OVER_YET_POWER." +
            (compact ? "activeCompactDescription" : "activeDescription"));
        text.Add("RollBonus", bonus);
        text.Add("MaxRolls", maxRolls);
        text.Add("ChainUpgradeDescription", maxRolls >= 5 ? RuleText("fifthAttackDescription") :
            maxRolls >= 4 ? RuleText("fourthAttackDescription") : string.Empty);
        text.Add("AdvantageDescription", advantage ? RuleText("advantageDescription") : string.Empty);
        text.Add("SwordheartDescription", swordheart ? RuleText("swordheartDescription") : string.Empty);
        return text;
    }

    private static string RuleText(string suffix) =>
        "\n" + new LocString("powers", "NOT_OVER_YET_POWER." + suffix).GetFormattedText();

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            SetTriggersUsedThisTurn(0);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (GetTriggersUsedThisTurn() >= GetTriggerLimit() ||
            Owner.CombatState?.CurrentSide != Owner.Side ||
            cardPlay.Card.Owner?.Creature != Owner ||
            cardPlay.Card.Type != CardType.Attack ||
            cardPlay.IsAutoPlay ||
            !cardPlay.IsFirstInSeries)
        {
            return;
        }

        SetTriggersUsedThisTurn(GetTriggersUsedThisTurn() + 1);
        if (CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Creature primaryTarget = GetPrimaryTarget(cardPlay);
        if (primaryTarget == null)
        {
            return;
        }

        RollGlossaryCapability rollCapability =
            cardPlay.Card.Capability<RollGlossaryCapability>();
        if (rollCapability == null)
        {
            return;
        }

        await ResolveExtraRollChain(
            choiceContext,
            rollCapability,
            cardPlay.Card,
            primaryTarget);
    }

    internal async Task TriggerAdditionalChains(
        PlayerChoiceContext choiceContext,
        CardModel triggeringCard,
        Creature primaryTarget,
        int count)
    {
        if (count <= 0 ||
            triggeringCard?.Owner?.Creature != Owner ||
            Owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        RollGlossaryCapability rollCapability =
            triggeringCard.Capability<RollGlossaryCapability>();
        if (rollCapability == null)
        {
            return;
        }

        for (int index = 0; index < count; index++)
        {
            Creature target = primaryTarget?.IsHittable == true
                ? primaryTarget
                : GetHittableOpponent();
            if (target == null ||
                CombatManager.Instance.IsOverOrEnding)
            {
                return;
            }

            await ResolveExtraRollChain(
                choiceContext,
                rollCapability,
                triggeringCard,
                target);
        }
    }

    internal static async Task TriggerAdditionalChainsForCard(
        PlayerChoiceContext choiceContext,
        Creature owner,
        CardModel triggeringCard,
        Creature primaryTarget,
        int count)
    {
        NotOverYetPower activePower =
            owner?.GetPower<NotOverYetPower>();
        if (activePower != null)
        {
            await activePower.TriggerAdditionalChains(
                choiceContext,
                triggeringCard,
                primaryTarget,
                count);
            return;
        }

        if (owner?.CombatState == null ||
            count <= 0 ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        NotOverYetPower transientPower =
            (NotOverYetPower)ModelDb
                .Power<NotOverYetPower>()
                .ToMutable();
        transientPower.ApplyInternal(owner, 1m, silent: true);
        try
        {
            await transientPower.TriggerAdditionalChains(
                choiceContext,
                triggeringCard,
                primaryTarget,
                count);
        }
        finally
        {
            if (ReferenceEquals(
                    owner.GetPower<NotOverYetPower>(),
                    transientPower))
            {
                transientPower.RemoveInternal();
            }
        }
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
        RollGlossaryCapability rollCapability,
        CardModel triggeringCard,
        Creature primaryTarget)
    {
        // Keep fractional precision between successes: floor only for comparison,
        // giving 50 / 75 / 112 / 168 / 253 rather than compounding rounded values.
        decimal successThreshold = FirstRollThreshold;
        for (int attackIndex = 1; attackIndex <= MaxGeneratedAttacks; attackIndex++)
        {
            if (CombatManager.Instance.IsOverOrEnding)
            {
                return;
            }

            int roll = await rollCapability.RollExtraD100(
                choiceContext,
                triggeringCard,
                HasAdvantageRolls);
            decimal threshold = decimal.Floor(successThreshold);
            if ((!rollCapability.LastExtraRollWasCritical && roll < threshold) ||
                CombatManager.Instance.IsOverOrEnding || GetHittableOpponent() == null)
            {
                return;
            }

            CardRarity rarity = RollGeneratedCardRarity();
            CardModel generatedCard = CreateGeneratedAttack(rarity);
            Creature target = GetCardTarget(generatedCard);
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
                attackIndex >= _allTargetAttackStartIndex && (HasFourthAttack || HasFifthAttack));

            if (HasFifthAttack && attackIndex == 5 &&
                !CombatManager.Instance.IsOverOrEnding &&
                primaryTarget?.IsHittable == true)
            {
                await DealFinisherDamage(choiceContext, primaryTarget, triggeringCard);
            }
            successThreshold *= RollThresholdMultiplier;
        }
    }

    private CardModel CreateGeneratedAttack(CardRarity rarity)
    {
        List<CardModel> candidates = Owner.Player.Deck.Cards
            .Where(MagicSwordCombatTemplateHelper.CanGenerateFromDeck)
            .Where(card => card.Type == CardType.Attack)
            .Where(card => card.Rarity == rarity)
            .Where(card =>
                ModeTransitionHelper.IsCardCompatibleWithCurrentMode(
                    Owner,
                    card))
            .Where(card => card.ShouldPlay(card, AutoPlayType.Default))
            .ToList();

        if (candidates.Count > 0)
        {
            int selectedIndex = Owner.CombatState.RunState.Rng.Niche.NextInt(0, candidates.Count);
            CardModel deckCard = candidates[selectedIndex];
            CardModel copy = Owner.CombatState.CloneCard(deckCard);
            copy.DeckVersion = deckCard;
            CardCopyContextHelper.RecordCopy(copy);
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
            CardCopyContextHelper.RecordXEnergySpent(card, xEnergy);
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
            CardCopyContextHelper.Clear(card);
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
        decimal damage = Math.Max(1m, LargeCreatureHealthHelper.GetMaxHp(primaryTarget) * _finisherMaxHpPercent);
        Flash();
        await CreatureCommandCompatibility.Damage(
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

    private Creature GetCardTarget(CardModel card)
    {
        return card.TargetType switch
        {
            TargetType.AnyEnemy => GetRandomHittableOpponent(),
            TargetType.Self or TargetType.AnyAlly => Owner,
            _ => null
        };
    }

    private Creature GetRandomHittableOpponent()
    {
        Creature[] targets = Owner.CombatState.GetOpponentsOf(Owner)
            .Where(creature => creature.IsHittable).ToArray();
        return targets.Length == 0 ? null :
            targets[Owner.CombatState.RunState.Rng.Niche.NextInt(0, targets.Length)];
    }

    private Creature GetHittableOpponent()
    {
        return Owner.CombatState
            .GetOpponentsOf(Owner)
            .FirstOrDefault(creature => creature.IsHittable);
    }

    private int GetTriggerLimit()
    {
        return Math.Max(0, Amount);
    }

    private int GetTriggersUsedThisTurn()
    {
        return Math.Max(
            TriggersUsedThisTurn,
            TriggeredThisTurn ? 1 : 0);
    }

    private void SetTriggersUsedThisTurn(int value)
    {
        AssertMutable();
        TriggersUsedThisTurn = Math.Max(0, value);
        TriggeredThisTurn = TriggersUsedThisTurn > 0;
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
