using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class Millsage : RelicModel
{
    private HotaruCannonCombatState _combat = new();
    private object _lastOverclockStartToken;
    private object _lastOverclockEndToken;

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Layers;
    public override string PackedIconPath => "res://images/relics/millsage.webp";
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;

    [SavedProperty]
    public string CannonStateData
    {
        get => _combat.ToJson();
        private set
        {
            AssertMutable();
            _combat = HotaruCannonCombatState.FromJson(value);
            ResetOverclockPhaseGuards();
        }
    }

    public HotaruCannonState StateFor(HotaruCannonKind kind) => _combat.For(kind);
    internal HotaruRareCannonState RareCannonState => _combat.Rare;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _combat = HotaruCannonCombatState.FromJson(_combat.ToJson());
        ResetOverclockPhaseGuards();
        ResetCriticalCannonRewards();
    }

    public override async Task BeforeCombatStart()
    {
        _combat = new HotaruCannonCombatState();
        ResetOverclockPhaseGuards();
        ResetCriticalCannonRewards();
        if (Owner?.Creature?.CombatState is not { } combatState) return;
        _combat.EndRewardPending = true;
        if (HasCannonUpgrade(HotaruCannonKind.Happiness, 1))
            StateFor(HotaruCannonKind.Happiness).InitializeHappinessIncrease(3m);
        MonsterModel[] models =
        {
            ModelDb.Monster<HotaruHappinessCannon>(),
            ModelDb.Monster<HotaruCoffinCannon>(),
            ModelDb.Monster<HotaruContestCannon>(),
            ModelDb.Monster<HotaruLongingCannon>(),
            ModelDb.Monster<HotaruAbsolutionCannon>()
        };
        // Relic charge counts acquisitions; all five cannons are available
        // independently of that counter, including at its initial one charge.
        foreach (MonsterModel canonical in models)
        {
            HotaruCannonKind kind = ((HotaruCannonModel)canonical).Kind;
            if (WasCannonRetired(kind)) continue;
            if (GetCannon(kind) != null) continue;
            Creature cannon = combatState.CreateCreature((MonsterModel)canonical.ToMutable(), Owner.Creature.Side, null);
            await PlayerCmd.AddPet(cannon, Owner);
            cannon.PrepareForNextTurn(combatState.Enemies);
        }
        await RefreshEndgameCannon();
        if (PermanentSacrificeDamagePercent > 0m)
            await PowerCmd.Apply<HotaruDamageAmplificationPower>(new BlockingPlayerChoiceContext(),
                Owner.Creature, PermanentSacrificeDamagePercent, Owner.Creature, null);
        if (HasCannonUpgrade(HotaruCannonKind.Contest, 3))
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(new BlockingPlayerChoiceContext(),
                Owner.Creature, 2m, Owner.Creature, null);
        await HotaruCannonSupportUpgradeHelper.OnCombatStart(this);
        foreach (HotaruCannonKind kind in Enum.GetValues<HotaruCannonKind>())
            if (GetCannon(kind) is { } cannon) await RefreshIntent(cannon);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ApplyCombatWear(room);
        bool reward = _combat.EndRewardPending;
        if (reward) HotaruAbsolutionUpgradeHelper.AfterCombatEnd(this);
        _combat = new HotaruCannonCombatState();
        ResetOverclockPhaseGuards();
        ResetCriticalCannonRewards();
        return Task.CompletedTask;
    }

    public Creature GetCannon(HotaruCannonKind kind) => Owner?.Creature?.CombatState?.Allies
        .FirstOrDefault(creature => IsOwnedCannon(creature, Owner) &&
            creature.Monster is HotaruCannonModel model && model.Kind == kind);

    public static bool IsOwnedCannon(Creature creature, Player player) =>
        player != null && creature?.IsAlive == true && creature.PetOwner == player &&
        creature.Monster is HotaruCannonModel model && creature.CombatState == player.Creature.CombatState &&
        player.GetRelic<Millsage>()?.IsCannonAvailableInCombat(model.Kind) != false;

    public static bool IsEnemy(Creature creature, Player player) =>
        player?.Creature?.CombatState is { } combat && creature?.IsAlive == true &&
        creature.IsHittable && creature.Side != player.Creature.Side && combat.Enemies.Contains(creature);

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player) =>
        player == Owner
            ? RunTurnStart(choiceContext, OverclockImmediateTurnCoordinator.GetTurnToken(player))
            : Task.CompletedTask;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play?.Card?.Owner == Owner)
            {
            HotaruCommonCannonHelper.RecordManualCannonTarget(play);
            HotaruUncommonCardHelper.BeforeCardPlayed(play);
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel source)
    {
        await HotaruUncommonCardHelper.OnDebuffApplied(context, Owner, power, amount, applier, source);
        await HotaruRareDebuffHelper.OnDebuffApplied(context, Owner, power, amount, applier, source);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play) =>
        play?.Card?.Owner == Owner ? HotaruUncommonCardHelper.AfterCardPlayed(context, play) : Task.CompletedTask;

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState) =>
        Owner?.Creature is { } owner && owner.Side == side && participants.Contains(owner)
            ? HotaruEndgameEffectsHelper.ClearTurnDamage(Owner) : Task.CompletedTask;

    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants) =>
        Owner?.Creature is { } creature && side == creature.Side && participants.Contains(creature)
            ? RunTurnEnd(choiceContext, OverclockImmediateTurnCoordinator.GetTurnToken(Owner))
            : Task.CompletedTask;

    public override Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants) =>
        Owner?.Creature is { } creature && side == creature.Side && participants.Contains(creature) &&
        OverclockImmediateTurnCoordinator.GetTurnToken(Owner) == null
            ? HotaruRareCannonHelper.OnTurnEnd(context, Owner, null) : Task.CompletedTask;

    internal Task TriggerOverclockTurnStart(PlayerChoiceContext context, object turnToken) =>
        IsCurrentOverclockTurn(turnToken) ? RunTurnStart(context, turnToken) : Task.CompletedTask;

    internal Task TriggerOverclockTurnEnd(PlayerChoiceContext context, object turnToken) =>
        IsCurrentOverclockTurn(turnToken) ? RunTurnEnd(context, turnToken) : Task.CompletedTask;

    private bool IsCurrentOverclockTurn(object turnToken) =>
        turnToken != null && ReferenceEquals(turnToken, OverclockImmediateTurnCoordinator.GetTurnToken(Owner));

    private void ResetOverclockPhaseGuards()
    {
        _lastOverclockStartToken = null;
        _lastOverclockEndToken = null;
    }

    private bool TryBeginTurnPhase(HotaruCannonPhase phase, object overclockToken)
    {
        if (!CanAct()) return false;
        if (overclockToken != null)
        {
            if (!IsCurrentOverclockTurn(overclockToken)) return false;
            if (phase == HotaruCannonPhase.Start)
            {
                if (ReferenceEquals(_lastOverclockStartToken, overclockToken)) return false;
                _lastOverclockStartToken = overclockToken;
            }
            else
            {
                if (ReferenceEquals(_lastOverclockEndToken, overclockToken)) return false;
                _lastOverclockEndToken = overclockToken;
            }
            return true;
        }

        // Native turn numbers remain saved independently. An inserted OD turn
        // cannot consume the suspended ordinary turn's start/end receipts.
        int turn = Owner.PlayerCombatState.TurnNumber;
        if (phase == HotaruCannonPhase.Start)
        {
            if (_combat.LastNaturalStartTurn == turn) return false;
            _combat.LastNaturalStartTurn = turn;
        }
        else
        {
            if (_combat.LastNaturalEndTurn == turn) return false;
            _combat.LastNaturalEndTurn = turn;
        }
        return true;
    }

    private async Task RunTurnStart(PlayerChoiceContext context, object overclockToken)
    {
        if (!TryBeginTurnPhase(HotaruCannonPhase.Start, overclockToken)) return;
        using var turnPhase = HotaruUncommonCannonHelper.BeginTurnPhase();
        HotaruFirstManualCardHelper.ResetTurn(Owner, overclockToken);
        await HotaruRareCannonHelper.OnTurnStart(context, Owner, overclockToken);
        await HotaruRareRollHelper.OnTurnStart(context, Owner, overclockToken);
        await HotaruUncommonCardHelper.OnTurnStart(context, Owner, overclockToken);
        await HotaruUncommonCannonHelper.OnTurnStart(context, Owner, overclockToken);
        await HotaruCommonCannonHelper.OnTurnStart(context, Owner, overclockToken);
        await HotaruEchoLocationPower.TurnStart(context, Owner, overclockToken);
        await HotaruSeeYouAgainHelper.OnTurnStart(context, Owner, overclockToken);
        if (overclockToken != null) await HotaruEndgameEffectsHelper.ClearTurnDamage(Owner);
        await HotaruAbsolutionUpgradeHelper.GainTurnEnergy(this, Owner);
        using var vigorBatch = HotaruCannonVigorHelper.BeginBatch(this,
            new[] { HotaruCannonKind.Absolution, HotaruCannonKind.Endgame }
                .Select(kind => (Cannon: GetCannon(kind), Phase: HotaruCannonPhase.Start)));
        await ExecutePhaseBatch(context, HotaruCannonPhase.Start, Enum.GetValues<HotaruCannonKind>(), natural: true);
    }

    private async Task RunTurnEnd(PlayerChoiceContext context, object overclockToken)
    {
        if (!TryBeginTurnPhase(HotaruCannonPhase.End, overclockToken)) return;
        using var turnPhase = HotaruUncommonCannonHelper.BeginTurnPhase();
        using var vigorBatch = HotaruCannonVigorHelper.BeginBatch(this,
            new[] { HotaruCannonKind.Contest, HotaruCannonKind.Longing, HotaruCannonKind.Absolution, HotaruCannonKind.Endgame }
                .Select(kind => (Cannon: GetCannon(kind), Phase: HotaruCannonPhase.End)));
        await ExecutePhaseBatch(context, HotaruCannonPhase.End, Enum.GetValues<HotaruCannonKind>(), natural: true);
        await ResolveDefensiveEchoes(context, overclockToken != null);
    }

    private async Task ExecutePhaseBatch(PlayerChoiceContext context, HotaruCannonPhase phase,
        IEnumerable<HotaruCannonKind> kinds, decimal baseValueBonus = 0m, bool natural = false)
    {
        if (!CanAct()) return;
        var actions = new List<(Creature Cannon, int Count)>();
        foreach (HotaruCannonKind kind in kinds.Distinct())
            if (HasPhaseEffect(kind, phase) && GetCannon(kind) is { } cannon)
                actions.Add((cannon, TakeCannonActionCount(kind)));
        int rounds = actions.Count == 0 ? 0 : actions.Max(action => action.Count);
        // Each phase round is simultaneous for action streaks. Charges earned
        // inside it belong to a later action, never another cannon's first round.
        for (int round = 0; round < rounds && CanAct(); round++)
        {
            var completed = new List<Creature>();
            foreach (var action in actions)
            {
                if (!CanAct()) break;
                if (round >= action.Count || !IsOwnedCannon(action.Cannon, Owner)) continue;
                using var uncommon = HotaruUncommonCannonHelper.BeginAction(action.Cannon, isExtraAction: !natural || round > 0);
                await HotaruUncommonCannonHelper.BeforeAction(context, uncommon);
                await ExecutePhase(action.Cannon, phase, context, null, baseValueBonus: baseValueBonus);
                await HotaruUncommonCannonHelper.AfterAction(context, uncommon);
                completed.Add(action.Cannon);
            }
            await NotifyCannonActionsCompleted(context, completed);
        }
    }

    public async Task TriggerAllEffects(Creature cannon, PlayerChoiceContext choiceContext,
        CardModel cardSource = null, CardPlay cardPlay = null, decimal baseValueBonus = 0m,
        bool reuseVigorBatch = false, bool suppressCritical = false, HotaruCannonActionModifier modifier = null)
    {
        if (!CanAct() || !IsOwnedCannon(cannon, Owner)) return;
        HotaruCannonKind kind = ((HotaruCannonModel)cannon.Monster).Kind;
        baseValueBonus = Math.Clamp(baseValueBonus, 0m, HotaruCannonState.MaximumStoredValue);
        var attacks = new List<(Creature Cannon, HotaruCannonPhase Phase)>();
        if (kind == HotaruCannonKind.Absolution) attacks.Add((cannon, HotaruCannonPhase.Start));
        if (kind == HotaruCannonKind.Endgame && EndgameStartHitCount > 0) attacks.Add((cannon, HotaruCannonPhase.Start));
        if (kind is HotaruCannonKind.Contest or HotaruCannonKind.Longing or HotaruCannonKind.Absolution)
            attacks.Add((cannon, HotaruCannonPhase.End));
        if (kind == HotaruCannonKind.Endgame && EndgameEndHitCount > 0) attacks.Add((cannon, HotaruCannonPhase.End));
        using var vigorBatch = reuseVigorBatch ? null : HotaruCannonVigorHelper.BeginBatch(this, attacks);
        int actionCount = TakeCannonActionCount(kind);
        bool hasStart = HasPhaseEffect(kind, HotaruCannonPhase.Start);
        bool hasEnd = HasPhaseEffect(kind, HotaruCannonPhase.End);
        HotaruCannonPhase attackPhase = attacks.Count > 0 ? attacks[0].Phase
            : hasStart ? HotaruCannonPhase.Start : HotaruCannonPhase.End;
        for (int action = 0; action < actionCount && CanAct() && IsOwnedCannon(cannon, Owner); action++)
        {
            using var uncommon = HotaruUncommonCannonHelper.BeginAction(cannon, cardSource, cardPlay,
                isActivation: true, modifier: action == 0 ? modifier : null, claimInherited: action == 0,
                isExtraAction: true);
            await HotaruUncommonCannonHelper.BeforeAction(choiceContext, uncommon);
            var actionVigor = HotaruCannonVigorHelper.BeginCompleteAction(cannon, attackPhase);
            try
            {
                // A complete manual action includes START and END. Only this
                // first full action can claim the batch's Vigor allowance.
                if (hasStart)
                    await ExecutePhase(cannon, HotaruCannonPhase.Start, choiceContext, cardSource,
                        cardPlay, baseValueBonus, actionVigor, completeVigor: !hasEnd, suppressCritical: suppressCritical);
                if (CanAct() && hasEnd)
                    await ExecutePhase(cannon, HotaruCannonPhase.End, choiceContext, cardSource,
                        cardPlay, baseValueBonus, actionVigor, completeVigor: true, suppressCritical: suppressCritical);
            }
            finally { await actionVigor.Complete(choiceContext); }
            await NotifyCannonActionsCompleted(choiceContext, new[] { cannon });
            await HotaruUncommonCannonHelper.AfterAction(choiceContext, uncommon);
        }
    }

    public async Task FireHappiness(Creature target, PlayerChoiceContext choiceContext,
        CardModel cardSource = null, CardPlay cardPlay = null, bool growBeforeDamage = false,
        bool reuseVigorBatch = false)
    {
        Creature cannon = GetCannon(HotaruCannonKind.Happiness);
        if (!CanAct() || cannon == null || !IsEnemy(target, Owner)) return;
        HotaruCannonState state = StateFor(HotaruCannonKind.Happiness);
        bool all = HasCannonUpgrade(HotaruCannonKind.Happiness, 7);
        int hits = HotaruCannonVigorHelper.GetPlannedHitCount(this, HotaruCannonKind.Happiness, HotaruCannonPhase.End);
        using var vigorBatch = reuseVigorBatch ? null : HotaruCannonVigorHelper.BeginBatch(this,
            new[] { (Cannon: cannon, Phase: HotaruCannonPhase.End) });
        int actionCount = state.TakeActionCount();
        for (int action = 0; action < actionCount && CanAct() && IsOwnedCannon(cannon, Owner); action++)
        {
            if (all ? !Owner.Creature.CombatState.Enemies.Any(enemy => IsEnemy(enemy, Owner)) : !IsEnemy(target, Owner)) break;
            using var uncommon = HotaruUncommonCannonHelper.BeginAction(cannon, cardSource, cardPlay, isExtraAction: true);
            await HotaruUncommonCannonHelper.BeforeAction(choiceContext, uncommon);
            await HotaruCannonSupportUpgradeHelper.PreparePhase(choiceContext, cannon, HotaruCannonPhase.End);
            if (!CanAct() || !IsOwnedCannon(cannon, Owner)) break;
            using var phaseScope = HotaruCannonSupportUpgradeHelper.BeginPhase(cannon, HotaruCannonPhase.End);
            HotaruUncommonCannonHelper.MarkExecuted(cannon);
            if (growBeforeDamage) await GrowHappiness(choiceContext, cannon, 0m, cardSource, cardPlay);
            decimal damage = state.HappinessDamage;
            var vigorAttack = HotaruCannonVigorHelper.BeginAttack(cannon, HotaruCannonPhase.End);
            try
            {
                for (int hit = 0; hit < hits && CanAct(); hit++)
                {
                    Creature[] targets = all ? Owner.Creature.CombatState.Enemies.Where(enemy => IsEnemy(enemy, Owner)).ToArray()
                        : new[] { target };
                    foreach (Creature enemy in targets)
                    {
                        if (!CanAct()) break;
                        if (IsEnemy(enemy, Owner)) await DealDamage(cannon, enemy, damage, choiceContext, cardSource, cardPlay);
                    }
                }
            }
            finally { await vigorAttack.Complete(choiceContext); }
            await NotifyCannonActionsCompleted(choiceContext, new[] { cannon });
            await HotaruUncommonCannonHelper.AfterAction(choiceContext, uncommon);
            state.HalveHappinessDamage();
            if (CanAct()) await HotaruCannonSupportUpgradeHelper.AfterPhase(choiceContext, this,
                HotaruCannonKind.Happiness, HotaruCannonPhase.End, cardPlay);
            await ResolveCriticalCannonRewards(choiceContext);
            await RefreshIntent(cannon);
        }
    }

    internal void ApplyHappinessHitGrowth()
    {
        HotaruCannonState state = StateFor(HotaruCannonKind.Happiness);
        state.HalveHappinessIncreaseAfterDamage();
        if (HasCannonUpgrade(HotaruCannonKind.Happiness, 2)) state.AddHappinessIncrease(2m);
    }

    public decimal ModifyDamageAdditive(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource) =>
        HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, cardSource, props, out Creature cannon) &&
        IsOwnedCannon(cannon, Owner) ? HotaruAbsolutionUpgradeHelper.GetDoomFlatDamageBonus(cannon, target) : 0m;

    public void Charge(Creature cannon, int extraTriggers = 1)
    {
        if (!CanAct() || !IsOwnedCannon(cannon, Owner)) return;
        HotaruCannonKind kind = ((HotaruCannonModel)cannon.Monster).Kind;
        StateFor(kind).Charge(kind, extraTriggers);
        MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(RefreshIntent(cannon));
    }

    public async Task ChargeFromCard(PlayerChoiceContext context, Creature cannon, int extraTriggers,
        CardModel source)
    {
        if (!CanAct() || !IsOwnedCannon(cannon, Owner) || extraTriggers <= 0) return;
        var state = StateFor(((HotaruCannonModel)cannon.Monster).Kind);
        int before = state.PendingActionRepeats;
        Charge(cannon, extraTriggers);
        if (state.PendingActionRepeats > before)
            await HotaruUncommonCardHelper.OnCannonExtraActionsGranted(context, Owner, source);
    }

    // Only the Contest cannon doubles destruction gained by its own completed hit.
    // Every extra complete action earns its own destruction from actual damage.
    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target,
        decimal amount, Creature applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (canonicalPower is not DestructionRatePower || amount <= 0m ||
            !(IsOwnedCannon(applier, Owner) && applier.Monster is HotaruContestCannon ||
              HotaruCannonStatsHelper.IsContestSource(target, applier, this)) ||
            target?.Side == Owner.Creature.Side) return false;
        modifiedAmount = amount * 2m;
        return true;
    }

    private bool CanAct() => Owner?.Creature?.IsAlive == true && Owner.Creature.CombatState != null &&
        !CombatManager.Instance.IsOverOrEnding;

    private async Task ExecutePhase(Creature cannon, HotaruCannonPhase phase,
        PlayerChoiceContext context, CardModel cardSource, CardPlay cardPlay = null, decimal baseValueBonus = 0m,
        HotaruCannonVigorHelper.AttackScope actionVigor = null, bool completeVigor = true, Creature fixedTarget = null,
        bool suppressCritical = false)
    {
        if (!CanAct() || !IsOwnedCannon(cannon, Owner)) return;
        HotaruCannonKind kind = ((HotaruCannonModel)cannon.Monster).Kind;
        if (!HasPhaseEffect(kind, phase)) return;
        if (kind == HotaruCannonKind.Endgame && phase == HotaruCannonPhase.Start)
            await HotaruEndgameEffectsHelper.OnTurnStart(this, context);
        await HotaruCannonSupportUpgradeHelper.PreparePhase(context, cannon, phase);
        if (!CanAct() || !IsOwnedCannon(cannon, Owner)) return;
        using var phaseScope = HotaruCannonSupportUpgradeHelper.BeginPhase(cannon, phase, suppressCritical);
        HotaruUncommonCannonHelper.MarkExecuted(cannon);
        HotaruCannonState state = StateFor(kind);
        var vigorAttack = actionVigor ?? HotaruCannonVigorHelper.BeginAttack(cannon, phase);
        int hitCount = HotaruCannonVigorHelper.GetPlannedHitCount(this, kind, phase);
        try
        {
            if (kind == HotaruCannonKind.Endgame)
            {
                for (int hit = 0; hit < hitCount && CanAct(); hit++)
                    await DealPhaseDamage(cannon, fixedTarget, EndgameBaseDamage + baseValueBonus, context, cardSource, cardPlay);
            }
            else if (phase == HotaruCannonPhase.Start)
            {
                switch (kind)
                {
                    case HotaruCannonKind.Coffin:
                        decimal block = await HotaruCannonBlockHelper.Gain(
                            Owner.Creature, 4m + baseValueBonus, cardPlay, cannon);
                        if (Owner.Creature.GetPower<HotaruMyCoffinPower>() is { } coffin)
                            await coffin.OnCoffinBlock(context, cannon, block, cardSource, cardPlay);
                        break;
                    case HotaruCannonKind.Longing:
                        await HotaruCannonBlockHelper.Gain(Owner.Creature, 3m + baseValueBonus, cardPlay, cannon);
                        break;
                    case HotaruCannonKind.Absolution:
                        for (int hit = 0; hit < hitCount && CanAct(); hit++)
                            await DealPhaseDamage(cannon, fixedTarget, 1m + baseValueBonus, context, cardSource, cardPlay);
                        break;
                }
            }
            else
            {
                switch (kind)
                {
                    case HotaruCannonKind.Happiness:
                        await GrowHappiness(context, cannon, baseValueBonus, cardSource, cardPlay);
                        break;
                    case HotaruCannonKind.Contest:
                        for (int hit = 0; hit < hitCount && CanAct(); hit++)
                        {
                            Creature target = fixedTarget ?? RandomEnemy();
                            if (target != null)
                                await DealDamage(cannon, target,
                                    2m + SixSenseCombatHelper.TargetResistanceBreakCount(target) + baseValueBonus,
                                    context, cardSource, cardPlay);
                        }
                        break;
                    case HotaruCannonKind.Longing:
                        for (int hit = 0; hit < hitCount && CanAct(); hit++)
                            await DealPhaseDamage(cannon, fixedTarget, 3m + baseValueBonus, context, cardSource, cardPlay);
                        break;
                    case HotaruCannonKind.Absolution:
                        using (HighHitDamageVisualHelper.BeginDirectDamageBatch(
                            hitCount, Owner.Creature,
                            cardSource, ValueProp.Move))
                            for (int hit = 0; hit < hitCount && CanAct(); hit++)
                                await DealPhaseDamage(cannon, fixedTarget, 2m + baseValueBonus, context, cardSource, cardPlay);
                        break;
                }
            }
        }
        finally
        {
            if (completeVigor) await vigorAttack.Complete(context);
        }
        if (CanAct()) await HotaruCannonSupportUpgradeHelper.AfterPhase(context, this, kind, phase, cardPlay);
        await ResolveCriticalCannonRewards(context);
        await RefreshIntent(cannon);
    }

    private Creature RandomEnemy()
    {
        Creature[] enemies = Owner.Creature.CombatState.Enemies.Where(enemy => IsEnemy(enemy, Owner)).ToArray();
        // Rebuild the living target pool for every segment; no draw when no enemy is left.
        return enemies.Length == 0 ? null : enemies[Owner.RunState.Rng.Niche.NextInt(enemies.Length)];
    }

    private async Task DealRandomDamage(Creature cannon, decimal damage, PlayerChoiceContext context,
        CardModel cardSource, CardPlay cardPlay = null, int phaseMultiplier = 1)
    {
        Creature target = RandomEnemy();
        if (target != null) await DealDamage(cannon, target, damage, context, cardSource, cardPlay, phaseMultiplier);
    }

    private static Task<bool> DealDamage(Creature cannon, Creature target, decimal damage,
        PlayerChoiceContext context, CardModel cardSource, CardPlay cardPlay = null, int phaseMultiplier = 1) =>
        HotaruCannonStatsHelper.Damage(cannon, target, damage, context, cardSource, cardPlay, phaseMultiplier);

    private static Task RefreshIntent(Creature cannon) =>
        MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance?.GetCreatureNode(cannon)?.RefreshIntents() ?? Task.CompletedTask;
}
