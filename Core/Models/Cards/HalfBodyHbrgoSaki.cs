using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

/// <summary>A permanent garden memory whose effects work without playing it.</summary>
public sealed class HalfBodyHbrgoSaki : CardModel
{
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;
    private CardPlay _pendingAttackPlay;
    private CardPlay _pendingNonAttackPlay;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasTriggeredAttackThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasTriggeredNonAttackThisTurn { get; private set; }

    public override int MaxUpgradeLevel => UpgradeEffectCount;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/half_body_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Unplayable, CardKeyword.Innate, CardKeyword.Eternal, CardKeyword.Retain
    };

    protected override bool IsPlayable => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (HasUpgradeEffect(5)) yield return HoverTipFactory.FromPower<DexterityPower>();
            if (HasUpgradeEffect(6)) yield return HoverTipFactory.FromPower<StrengthPower>();
        }
    }

    public HalfBodyHbrgoSaki() : base(-1, CardType.Power, CardRarity.Rare, TargetType.None) { }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) => card != this;

    internal IReadOnlyList<int> GetAvailableUpgradeEffects() =>
        Enumerable.Range(0, UpgradeEffectCount).Where(id => !HasUpgradeEffect(id)).ToArray();

    internal bool HasUpgradeEffect(int effectId) =>
        UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(effectId);

    internal bool PrepareUpgradeEffect(int effectId)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(effectId)) return false;
        _preparedUpgradeEffect = effectId;
        return true;
    }

    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString()))
            .ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
    }

    public override Task BeforeCombatStart()
    {
        ResetTurnTriggers();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner?.Creature is { } creature && side == creature.Side && participants.Contains(creature))
            ResetTurnTriggers();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !IsActiveInHand()) return;
        // The normal turn energy reset and innate draw have already completed.
        bool gainGold = HasUpgradeEffect(0);
        bool gainEnergy = HasUpgradeEffect(3);
        if (gainGold) await PlayerCmd.GainGold(5m, player);
        if (gainEnergy && CanResolve()) await PlayerCmd.GainEnergy(1m, player);
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay?.Card?.Owner != Owner || !IsActiveInHand()) return Task.CompletedTask;
        // Reserve the first play before its effects can create another play.
        // Each physical copy has independent per-turn reservations.
        if (cardPlay.Card.Type == CardType.Attack)
        {
            if (HasUpgradeEffect(5) && !HasTriggeredAttackThisTurn)
            {
                HasTriggeredAttackThisTurn = true;
                _pendingAttackPlay = cardPlay;
            }
        }
        else if (HasUpgradeEffect(6) && !HasTriggeredNonAttackThisTurn)
        {
            HasTriggeredNonAttackThisTurn = true;
            _pendingNonAttackPlay = cardPlay;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay == null) return;
        bool gainDexterity = ReferenceEquals(_pendingAttackPlay, cardPlay);
        bool gainStrength = ReferenceEquals(_pendingNonAttackPlay, cardPlay);
        // Clear before power hooks can recursively play or move cards. The
        // in-hand condition was captured when this play actually began.
        if (gainDexterity) _pendingAttackPlay = null;
        if (gainStrength) _pendingNonAttackPlay = null;
        if (!CanResolve()) return;
        if (gainDexterity)
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        if (gainStrength && CanResolve())
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!HasUpgradeEffect(4) || !IsActiveInHand() || side != Owner.Creature.Side ||
            !participants.Contains(Owner.Creature)) return;
        int damage = HbrHalfBodyEffects.GetRemovedCardCount(Owner);
        if (damage <= 0) return;
        Creature[] targets = Owner.Creature.CombatState.GetOpponentsOf(Owner.Creature)
            .Where(creature => creature.IsHittable).ToArray();
        if (targets.Length == 0) return;
        // Native OnTurnEndInHandWrapper moves even retained cards to discard.
        // This passive instead leaves the memory in hand for its other effects.
        await CreatureCommandCompatibility.Damage(choiceContext, targets, damage,
            ValueProp.Unpowered, Owner.Creature, this);
    }

    public override bool ShouldDieLate(Creature creature) =>
        !HbrHalfBodyEffects.CanPreventDeath(this, creature);

    public override Task AfterPreventingDeath(Creature creature) =>
        HbrHalfBodyEffects.PreventDeath(this, creature);

    public override Task AfterCombatEnd(CombatRoom room) =>
        HbrHalfBodyEffects.GrantRemovalReward(this, room);

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index >= UpgradeEffectCount) return;
        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        bool IsEligible(int id) => id >= 0 && id < UpgradeEffectCount && !applied.Contains(id);
        int effectId = index < UpgradeEffectOrder.Length && IsEligible(UpgradeEffectOrder[index])
            ? UpgradeEffectOrder[index]
            : _preparedUpgradeEffect is int prepared && IsEligible(prepared)
                ? prepared
                : Enumerable.Range(0, UpgradeEffectCount).First(IsEligible);
        if (index < UpgradeEffectOrder.Length) UpgradeEffectOrder[index] = effectId;
        else UpgradeEffectOrder = UpgradeEffectOrder.Append(effectId).ToArray();
        _preparedUpgradeEffect = null;
    }

    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        UpgradeEffectOrder = (UpgradeEffectOrder ?? Array.Empty<int>())
            .Where(id => id >= 0 && id < UpgradeEffectCount).Distinct().Take(UpgradeEffectCount).ToArray();
        _preparedUpgradeEffect = null;
        _pendingAttackPlay = null;
        _pendingNonAttackPlay = null;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        UpgradeEffectOrder = (int[])UpgradeEffectOrder.Clone();
        _preparedUpgradeEffect = null;
        ResetTurnTriggers();
    }

    protected override void AfterDowngraded()
    {
        base.AfterDowngraded();
        UpgradeEffectOrder = Array.Empty<int>();
        _preparedUpgradeEffect = null;
        if (!HbrMemoryGardenProgression.IsRebuilding) ResetTurnTriggers();
    }

    private bool CanResolve() => IsMutable && !HasBeenRemovedFromState &&
        Owner?.PlayerCombatState != null && Owner.Creature.IsAlive &&
        Owner.Creature.CombatState != null && !CombatManager.Instance.IsOverOrEnding;

    private bool IsActiveInHand() => Pile?.Type == PileType.Hand && CanResolve();

    private void ResetTurnTriggers()
    {
        HasTriggeredAttackThisTurn = false;
        HasTriggeredNonAttackThisTurn = false;
        _pendingAttackPlay = null;
        _pendingNonAttackPlay = null;
    }
}
