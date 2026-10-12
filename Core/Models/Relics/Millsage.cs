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
    public const int InitialLayers = 5;
    public const int MaximumLayers = 6;
    private int _layers = InitialLayers;
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
    public int Layers
    {
        get => _layers;
        private set
        {
            AssertMutable();
            _layers = Math.Clamp(value, 0, MaximumLayers);
            InvokeDisplayAmountChanged();
        }
    }

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

    public void SetLayers(int amount) => Layers = amount;
    public HotaruCannonState StateFor(HotaruCannonKind kind) => _combat.For(kind);

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
        // The sixth Millsage slot is reserved; no unrequested sixth cannon is created.
        foreach (MonsterModel canonical in models.Take(Math.Min(5, Layers)))
        {
            HotaruCannonKind kind = ((HotaruCannonModel)canonical).Kind;
            if (GetCannon(kind) != null) continue;
            Creature cannon = combatState.CreateCreature((MonsterModel)canonical.ToMutable(), Owner.Creature.Side, null);
            await PlayerCmd.AddPet(cannon, Owner);
            cannon.PrepareForNextTurn(combatState.Enemies);
        }
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
        _combat = new HotaruCannonCombatState();
        ResetOverclockPhaseGuards();
        ResetCriticalCannonRewards();
        if (reward) HotaruAbsolutionUpgradeHelper.AfterCombatEnd(this);
        return Task.CompletedTask;
    }

    public Creature GetCannon(HotaruCannonKind kind) => Owner?.Creature?.CombatState?.Allies
        .FirstOrDefault(creature => IsOwnedCannon(creature, Owner) &&
            creature.Monster is HotaruCannonModel model && model.Kind == kind);

    public static bool IsOwnedCannon(Creature creature, Player player) =>
        player != null && creature?.IsAlive == true && creature.PetOwner == player &&
        creature.Monster is HotaruCannonModel && creature.CombatState == player.Creature.CombatState;

    public static bool IsEnemy(Creature creature, Player player) =>
        player?.Creature?.CombatState is { } combat && creature?.IsAlive == true &&
        creature.IsHittable && creature.Side != player.Creature.Side && combat.Enemies.Contains(creature);

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player) =>
        player == Owner
            ? RunTurnStart(choiceContext, OverclockImmediateTurnCoordinator.GetTurnToken(player))
            : Task.CompletedTask;

    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants) =>
        Owner?.Creature is { } creature && side == creature.Side && participants.Contains(creature)
            ? RunTurnEnd(choiceContext, OverclockImmediateTurnCoordinator.GetTurnToken(Owner))
            : Task.CompletedTask;

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
        await HotaruAbsolutionUpgradeHelper.GainTurnEnergy(this, Owner);
        using var vigorBatch = HotaruCannonVigorHelper.BeginBatch(this,
            new[] { (Cannon: GetCannon(HotaruCannonKind.Absolution), Phase: HotaruCannonPhase.Start) });
        foreach (HotaruCannonKind kind in Enum.GetValues<HotaruCannonKind>())
        {
            if (!CanAct()) break;
            if (!HotaruCannonState.HasEffect(kind, HotaruCannonPhase.Start)) continue;
            Creature cannon = GetCannon(kind);
            if (cannon != null) await ExecuteNaturalPhase(cannon, HotaruCannonPhase.Start, context);
        }
    }

    private async Task RunTurnEnd(PlayerChoiceContext context, object overclockToken)
    {
        if (!TryBeginTurnPhase(HotaruCannonPhase.End, overclockToken)) return;
        using var vigorBatch = HotaruCannonVigorHelper.BeginBatch(this,
            new[] { HotaruCannonKind.Contest, HotaruCannonKind.Longing, HotaruCannonKind.Absolution }
                .Select(kind => (Cannon: GetCannon(kind), Phase: HotaruCannonPhase.End)));
        foreach (HotaruCannonKind kind in Enum.GetValues<HotaruCannonKind>())
        {
            if (!CanAct()) break;
            if (!HotaruCannonState.HasEffect(kind, HotaruCannonPhase.End)) continue;
            Creature cannon = GetCannon(kind);
            if (cannon != null) await ExecuteNaturalPhase(cannon, HotaruCannonPhase.End, context);
        }
    }

    private async Task ExecuteNaturalPhase(Creature cannon, HotaruCannonPhase phase,
        PlayerChoiceContext context)
    {
        if (!CanAct() || !IsOwnedCannon(cannon, Owner)) return;
        HotaruCannonKind kind = ((HotaruCannonModel)cannon.Monster).Kind;
        if (!HotaruCannonState.HasEffect(kind, phase)) return;
        int actionCount = StateFor(kind).TakeActionCount();
        for (int action = 0; action < actionCount && CanAct() && IsOwnedCannon(cannon, Owner); action++)
            await ExecutePhase(cannon, phase, context, null);
    }

    public async Task TriggerAllEffects(Creature cannon, PlayerChoiceContext choiceContext,
        CardModel cardSource = null, CardPlay cardPlay = null, decimal baseValueBonus = 0m,
        bool reuseVigorBatch = false)
    {
        if (!CanAct() || !IsOwnedCannon(cannon, Owner)) return;
        HotaruCannonKind kind = ((HotaruCannonModel)cannon.Monster).Kind;
        baseValueBonus = Math.Clamp(baseValueBonus, 0m, HotaruCannonState.MaximumStoredValue);
        var attacks = new List<(Creature Cannon, HotaruCannonPhase Phase)>();
        if (kind == HotaruCannonKind.Absolution) attacks.Add((cannon, HotaruCannonPhase.Start));
        if (kind is HotaruCannonKind.Contest or HotaruCannonKind.Longing or HotaruCannonKind.Absolution)
            attacks.Add((cannon, HotaruCannonPhase.End));
        using var vigorBatch = reuseVigorBatch ? null : HotaruCannonVigorHelper.BeginBatch(this, attacks);
        int actionCount = StateFor(kind).TakeActionCount();
        bool hasStart = HotaruCannonState.HasEffect(kind, HotaruCannonPhase.Start);
        bool hasEnd = HotaruCannonState.HasEffect(kind, HotaruCannonPhase.End);
        HotaruCannonPhase attackPhase = attacks.Count > 0 ? attacks[0].Phase
            : hasStart ? HotaruCannonPhase.Start : HotaruCannonPhase.End;
        for (int action = 0; action < actionCount && CanAct() && IsOwnedCannon(cannon, Owner); action++)
        {
            var actionVigor = HotaruCannonVigorHelper.BeginCompleteAction(cannon, attackPhase);
            try
            {
                // A complete manual action includes START and END. Only this
                // first full action can claim the batch's Vigor allowance.
                if (hasStart)
                    await ExecutePhase(cannon, HotaruCannonPhase.Start, choiceContext, cardSource,
                        cardPlay, baseValueBonus, actionVigor, completeVigor: !hasEnd);
                if (CanAct() && hasEnd)
                    await ExecutePhase(cannon, HotaruCannonPhase.End, choiceContext, cardSource,
                        cardPlay, baseValueBonus, actionVigor, completeVigor: true);
            }
            finally { await actionVigor.Complete(choiceContext); }
        }
    }

    public async Task FireHappiness(Creature target, PlayerChoiceContext choiceContext,
        CardModel cardSource = null, CardPlay cardPlay = null, bool growBeforeDamage = false)
    {
        Creature cannon = GetCannon(HotaruCannonKind.Happiness);
        if (!CanAct() || cannon == null || !IsEnemy(target, Owner)) return;
        HotaruCannonState state = StateFor(HotaruCannonKind.Happiness);
        bool all = HasCannonUpgrade(HotaruCannonKind.Happiness, 7);
        int hits = all ? 2 : 1;
        using var vigorBatch = HotaruCannonVigorHelper.BeginBatch(this,
            new[] { (Cannon: cannon, Phase: HotaruCannonPhase.End) });
        int actionCount = state.TakeActionCount();
        for (int action = 0; action < actionCount && CanAct() && IsOwnedCannon(cannon, Owner); action++)
        {
            if (all ? !Owner.Creature.CombatState.Enemies.Any(enemy => IsEnemy(enemy, Owner)) : !IsEnemy(target, Owner)) break;
            await HotaruCannonSupportUpgradeHelper.PreparePhase(choiceContext, cannon, HotaruCannonPhase.End);
            if (!CanAct() || !IsOwnedCannon(cannon, Owner)) break;
            using var phaseScope = HotaruCannonSupportUpgradeHelper.BeginPhase(cannon, HotaruCannonPhase.End);
            if (growBeforeDamage) state.GrowHappiness();
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
        HotaruCannonVigorHelper.AttackScope actionVigor = null, bool completeVigor = true)
    {
        if (!CanAct() || !IsOwnedCannon(cannon, Owner)) return;
        HotaruCannonKind kind = ((HotaruCannonModel)cannon.Monster).Kind;
        if (!HotaruCannonState.HasEffect(kind, phase)) return;
        await HotaruCannonSupportUpgradeHelper.PreparePhase(context, cannon, phase);
        if (!CanAct() || !IsOwnedCannon(cannon, Owner)) return;
        using var phaseScope = HotaruCannonSupportUpgradeHelper.BeginPhase(cannon, phase);
        HotaruCannonState state = StateFor(kind);
        var vigorAttack = actionVigor ?? HotaruCannonVigorHelper.BeginAttack(cannon, phase);
        try
        {
            if (phase == HotaruCannonPhase.Start)
            {
                switch (kind)
                {
                    case HotaruCannonKind.Coffin:
                        await HotaruCannonBlockHelper.Gain(Owner.Creature, 4m + baseValueBonus, cardPlay, cannon);
                        break;
                    case HotaruCannonKind.Longing:
                        await HotaruCannonBlockHelper.Gain(Owner.Creature, 3m + baseValueBonus, cardPlay, cannon);
                        break;
                    case HotaruCannonKind.Absolution:
                        for (int hit = 0; hit < 3 && CanAct(); hit++)
                            await DealRandomDamage(cannon, 1m + baseValueBonus, context, cardSource, cardPlay);
                        break;
                }
            }
            else
            {
                switch (kind)
                {
                    case HotaruCannonKind.Happiness:
                        state.GrowHappiness(baseValueBonus);
                        break;
                    case HotaruCannonKind.Contest:
                        for (int hit = 0; hit < (HasCannonUpgrade(HotaruCannonKind.Contest, 7) ? 2 : 1) && CanAct(); hit++)
                        {
                            Creature target = RandomEnemy();
                            if (target != null)
                                await DealDamage(cannon, target,
                                    2m + SixSenseCombatHelper.TargetResistanceBreakCount(target) + baseValueBonus,
                                    context, cardSource, cardPlay);
                        }
                        break;
                    case HotaruCannonKind.Longing:
                        await DealRandomDamage(cannon, 3m + baseValueBonus, context, cardSource, cardPlay);
                        break;
                    case HotaruCannonKind.Absolution:
                        using (HighHitDamageVisualHelper.BeginDirectDamageBatch(
                            HotaruAbsolutionUpgradeHelper.GetEndHitCount(this), Owner.Creature,
                            cardSource, ValueProp.Move))
                            for (int hit = 0; hit < HotaruAbsolutionUpgradeHelper.GetEndHitCount(this) && CanAct(); hit++)
                                await DealRandomDamage(cannon, 2m + baseValueBonus, context, cardSource, cardPlay);
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
