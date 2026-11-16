using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class SkullFeather :
    HbrgoStaticMonster,
    IHbrgoRevivingStaticMonster
{
    private MoveState _reviveState;
    private int _phase = 1;
    private bool _isReviving;
    private int _retainedStrength;
    private int _highDpMoveIndex;
    private int _lowDpMoveIndex;

    public override int MinInitialHp => ToughValue(700, 600);

    public override int MaxInitialHp => MinInitialHp;

    public bool HasRemainingRevives => Phase == 1;

    public bool ShouldSuppressDeathFade => HasRemainingRevives;

    private int RevivedHp => ToughValue(1000, 900);

    private int FrozenBeamHitCount => DeadlyValue(6, 5);

    private int DeathCannonHitCount => DeadlyValue(5, 4);

    private int NormalAttackDamage => DeadlyValue(21, 18);

    private int DoomThornDamage => DeadlyValue(9, 7);

    private int FrenzyStrikeDamage => DeadlyValue(12, 9);

    private decimal AngerStrikeDpRatio => DeadlyValue(50, 40) / 100m;

    private int FreezingHellDamage => DeadlyValue(12, 9);

    private int TentaclesScissorDamage => DeadlyValue(15, 12);

    private int AbsoluteTemperatureHitCount => DeadlyValue(9, 8);

    protected override string VisualsPath =>
        SceneHelper.GetScenePath("creature_visuals/skull_feather");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Phase
    {
        get => _phase;
        private set
        {
            AssertMutable();
            _phase = Math.Clamp(value, 1, 2);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsReviving
    {
        get => _isReviving;
        private set
        {
            AssertMutable();
            _isReviving = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RetainedStrength
    {
        get => _retainedStrength;
        private set
        {
            AssertMutable();
            _retainedStrength = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HighDpMoveIndex
    {
        get => _highDpMoveIndex;
        private set
        {
            AssertMutable();
            _highDpMoveIndex = Math.Clamp(value, 0, 3);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LowDpMoveIndex
    {
        get => _lowDpMoveIndex;
        private set
        {
            AssertMutable();
            _lowDpMoveIndex = Math.Clamp(value, 0, 5);
        }
    }

    private MoveState ReviveState
    {
        get => _reviveState;
        set
        {
            AssertMutable();
            _reviveState = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (!Creature.HasPower<SkullFeatherPower>())
        {
            await PowerCmd.Apply<SkullFeatherPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                1m,
                Creature,
                null);
        }

        if (HasRemainingRevives &&
            !Creature.HasPower<SkullFeatherRevivalPower>())
        {
            await PowerCmd.Apply<SkullFeatherRevivalPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                1m,
                Creature,
                null);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();
        var phaseOneRouter = new ConditionalBranchState(
            "PHASE_ONE_DP_ROUTER");

        MoveState highAttackBoost = Move(
            "P1_HIGH_ATTACK_BOOST_MOVE",
            PhaseOneHighAttackBoostMove,
            new BuffIntent());
        MoveState frozenBeam = Move(
            "P1_FROZEN_BEAM_MOVE",
            FrozenBeamMove,
            new MultiAttackIntent(1, FrozenBeamHitCount),
            new DebuffIntent());
        MoveState deathCannon = Move(
            "P1_DEATH_CANNON_MOVE",
            DeathCannonMove,
            new MultiAttackIntent(3, DeathCannonHitCount),
            new DebuffIntent());
        MoveState freezingBurst = Move(
            "P1_FREEZING_BURST_MOVE",
            FreezingBurstMove,
            new MultiAttackIntent(5, 5));

        MoveState defenseBoost = Move(
            "P1_DEFENSE_BOOST_MOVE",
            DefenseBoostMove,
            new DefendIntent());
        MoveState angerStrike = Move(
            "P1_ANGER_STRIKE_MOVE",
            AngerStrikeMove,
            new SingleAttackIntent(() => GetAngerStrikeIntentDamage()),
            new DebuffIntent());
        MoveState frenzyStrike = Move(
            "P1_FRENZY_STRIKE_MOVE",
            FrenzyStrikeMove,
            new MultiAttackIntent(FrenzyStrikeDamage, 2),
            new DebuffIntent());
        MoveState normalAttack = Move(
            "P1_NORMAL_ATTACK_MOVE",
            NormalAttackMove,
            new SingleAttackIntent(NormalAttackDamage));
        MoveState lowAttackBoost = Move(
            "P1_LOW_ATTACK_BOOST_MOVE",
            PhaseOneLowAttackBoostMove,
            new BuffIntent());
        MoveState doomThorn = Move(
            "P1_DOOM_THORN_MOVE",
            DoomThornMove,
            new MultiAttackIntent(DoomThornDamage, 3),
            new DebuffIntent());

        MoveState[] phaseOneMoves =
        {
            highAttackBoost,
            frozenBeam,
            deathCannon,
            freezingBurst,
            defenseBoost,
            angerStrike,
            frenzyStrike,
            normalAttack,
            lowAttackBoost,
            doomThorn
        };
        foreach (MoveState move in phaseOneMoves)
        {
            move.FollowUpState = phaseOneRouter;
        }

        phaseOneRouter.AddState(
            highAttackBoost,
            () => UsesHighDpCycle() && HighDpMoveIndex == 0);
        phaseOneRouter.AddState(
            frozenBeam,
            () => UsesHighDpCycle() && HighDpMoveIndex == 1);
        phaseOneRouter.AddState(
            deathCannon,
            () => UsesHighDpCycle() && HighDpMoveIndex == 2);
        phaseOneRouter.AddState(
            freezingBurst,
            () => UsesHighDpCycle() && HighDpMoveIndex == 3);
        phaseOneRouter.AddState(
            defenseBoost,
            () => !UsesHighDpCycle() && LowDpMoveIndex == 0);
        phaseOneRouter.AddState(
            angerStrike,
            () => !UsesHighDpCycle() && LowDpMoveIndex == 1);
        phaseOneRouter.AddState(
            frenzyStrike,
            () => !UsesHighDpCycle() && LowDpMoveIndex == 2);
        phaseOneRouter.AddState(
            normalAttack,
            () => !UsesHighDpCycle() && LowDpMoveIndex == 3);
        phaseOneRouter.AddState(
            lowAttackBoost,
            () => !UsesHighDpCycle() && LowDpMoveIndex == 4);
        phaseOneRouter.AddState(
            doomThorn,
            () => !UsesHighDpCycle() && LowDpMoveIndex == 5);

        MoveState spatialDomination = Move(
            "P2_SPATIAL_DOMINATION_MOVE",
            SpatialDominationMove,
            new DefendIntent(),
            new BuffIntent());
        MoveState absoluteTemperature = Move(
            "P2_ABSOLUTE_TEMPERATURE_MOVE",
            AbsoluteTemperatureMove,
            new MultiAttackIntent(-2, AbsoluteTemperatureHitCount),
            new DebuffIntent());
        MoveState freezingHell = Move(
            "P2_FREEZING_HELL_MOVE",
            FreezingHellMove,
            new MultiAttackIntent(FreezingHellDamage, 3),
            new DebuffIntent(strong: true));
        MoveState tentaclesScissor = Move(
            "P2_TENTACLES_SCISSOR_MOVE",
            TentaclesScissorMove,
            new MultiAttackIntent(TentaclesScissorDamage, 2),
            new DebuffIntent());
        MoveState attackUp = Move(
            "P2_ATTACK_UP_MOVE",
            AttackUpMove,
            new BuffIntent());
        MoveState heal = Move(
            "P2_HEAL_MOVE",
            HealMove,
            new DefendIntent(),
            new HealIntent());

        spatialDomination.FollowUpState = absoluteTemperature;
        absoluteTemperature.FollowUpState = freezingHell;
        freezingHell.FollowUpState = tentaclesScissor;
        tentaclesScissor.FollowUpState = attackUp;
        attackUp.FollowUpState = heal;
        heal.FollowUpState = spatialDomination;

        ReviveState = Move(
            "RESPAWN_MOVE",
            RespawnMove,
            new HealIntent(),
            new BuffIntent());
        ReviveState.MustPerformOnceBeforeTransitioning = true;
        ReviveState.FollowUpState = spatialDomination;

        states.Add(phaseOneRouter);
        states.AddRange(phaseOneMoves);
        states.AddRange(new MonsterState[]
        {
            spatialDomination,
            absoluteTemperature,
            freezingHell,
            tentaclesScissor,
            attackUp,
            heal,
            ReviveState
        });

        MonsterState initialState = Phase == 2
            ? spatialDomination
            : phaseOneRouter;
        return new MonsterMoveStateMachine(states, initialState);
    }

    public async Task TriggerReviveState()
    {
        await CreatureCmd.TriggerAnim(Creature, "Dead", 0f);
        SetMoveImmediate(ReviveState, forceTransition: true);
    }

    internal void BeginRevive(int retainedStrength)
    {
        RetainedStrength = retainedStrength;
        IsReviving = true;
    }

    internal void FinishRevive()
    {
        IsReviving = false;
    }

    private static MoveState Move(
        string id,
        Func<IReadOnlyList<Creature>, Task> action,
        params AbstractIntent[] intents)
    {
        return new MoveState(id, action, intents);
    }

    private int ToughValue(int ascended, int normal)
    {
        return AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            ascended,
            normal);
    }

    private int DeadlyValue(int ascended, int normal)
    {
        return AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            ascended,
            normal);
    }

    private bool UsesHighDpCycle()
    {
        HighDimensionalShellPower shell =
            Creature.GetPower<HighDimensionalShellPower>();
        if (shell == null)
        {
            return !Creature.HasPower<BreakPower>();
        }

        decimal maximum = HighDimensionalShellPower.CalculateAmount(Creature);
        return shell.CurrentDp > maximum * 0.5m;
    }

    private int GetAngerStrikeIntentDamage()
    {
        DeflectorProtection protection = CombatState?.Players
            .Where(player => player.Creature.IsAlive)
            .Select(player => player.GetRelic<DeflectorProtection>())
            .FirstOrDefault(relic => relic != null);
        return protection == null
            ? 0
            : Math.Max(0, (int)Math.Ceiling(
                protection.MaxDp * AngerStrikeDpRatio));
    }

    private void AdvanceHighDpCycle()
    {
        HighDpMoveIndex = (HighDpMoveIndex + 1) % 4;
    }

    private void AdvanceLowDpCycle()
    {
        LowDpMoveIndex = (LowDpMoveIndex + 1) % 6;
    }

    private async Task PhaseOneHighAttackBoostMove(
        IReadOnlyList<Creature> targets)
    {
        await AttackBoostMove(targets);
        AdvanceHighDpCycle();
    }

    internal Task BeforePhantomIntentPerformed(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id == "P1_ANGER_STRIKE_MOVE"
            ? ApplyAngerStrikeDpLoss(GetPhantomMoveTargets(targets))
            : Task.CompletedTask;
    }

    internal async Task AfterPhantomIntentPerformed(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        Creature[] opponents = GetPhantomMoveTargets(targets);
        switch (move?.Id)
        {
            case "P1_FROZEN_BEAM_MOVE":
                await ApplyToLivingTargets<FrailPower>(opponents, 4m);
                AdvanceHighDpCycle();
                break;
            case "P1_DEATH_CANNON_MOVE":
                await ApplyToLivingTargets<StrengthPower>(opponents, -2m);
                await ApplyToLivingTargets<DexterityPower>(opponents, -2m);
                AdvanceHighDpCycle();
                break;
            case "P1_FREEZING_BURST_MOVE":
                AdvanceHighDpCycle();
                break;
            case "P1_ANGER_STRIKE_MOVE":
            case "P1_FRENZY_STRIKE_MOVE":
                if (opponents.Length > 0)
                {
                    PhantomPlayerBenefitContext.QueueTurnBenefit(1);
                }
                AdvanceLowDpCycle();
                break;
            case "P1_DOOM_THORN_MOVE":
                await ApplyToLivingTargets<WeakPower>(opponents, 4m);
                AdvanceLowDpCycle();
                break;
            case "P1_NORMAL_ATTACK_MOVE":
                AdvanceLowDpCycle();
                break;
            case "P2_ABSOLUTE_TEMPERATURE_MOVE":
                await ApplyToLivingTargets<PoisonPower>(opponents, 20m);
                break;
            case "P2_FREEZING_HELL_MOVE":
                if (opponents.Length > 0)
                {
                    PhantomPlayerBenefitContext.QueueTurnBenefit(3);
                }
                break;
            case "P2_TENTACLES_SCISSOR_MOVE":
                await ApplyToLivingTargets<FrailPower>(opponents, 5m);
                break;
        }
    }

    private Creature[] GetPhantomMoveTargets(IReadOnlyList<Creature> targets)
    {
        return targets?.Where(target =>
            CombatState != null &&
            PhantomHelper.IsValidPhantomTarget(target) &&
            target.Side != Creature.Side &&
            ReferenceEquals(target.CombatState, CombatState))
            .ToArray() ?? Array.Empty<Creature>();
    }

    private async Task FrozenBeamMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(1, FrozenBeamHitCount);
        await ApplyToLivingTargets<FrailPower>(targets, 4m);
        AdvanceHighDpCycle();
    }

    private async Task DeathCannonMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(3, DeathCannonHitCount);
        await ApplyToLivingTargets<StrengthPower>(targets, -2m);
        await ApplyToLivingTargets<DexterityPower>(targets, -2m);
        AdvanceHighDpCycle();
    }

    private async Task FreezingBurstMove(IReadOnlyList<Creature> _)
    {
        await PerformAttack(5, 5);
        AdvanceHighDpCycle();
    }

    private async Task DefenseBoostMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await CreatureCmd.GainBlock(
            Creature,
            50m,
            ValueProp.Move,
            null);
        AdvanceLowDpCycle();
    }

    private async Task AngerStrikeMove(IReadOnlyList<Creature> targets)
    {
        await ApplyAngerStrikeDpLoss(targets);
        await ApplyTurnPenalty(targets, 1);
        AdvanceLowDpCycle();
    }

    private async Task ApplyAngerStrikeDpLoss(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.IsAlive))
        {
            DeflectorProtection protection =
                target.Player?.GetRelic<DeflectorProtection>();
            if (protection != null)
            {
                int dpDamage = Math.Max(
                    0,
                    (int)Math.Ceiling(
                        protection.MaxDp * AngerStrikeDpRatio));
                protection.LoseDp(dpDamage);
                await protection.ResolveMentalStrengthDpLossRewards(
                    new ThrowingPlayerChoiceContext());
            }
        }
    }

    private async Task FrenzyStrikeMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(FrenzyStrikeDamage, 2);
        await ApplyTurnPenalty(targets, 1);
        AdvanceLowDpCycle();
    }

    private async Task NormalAttackMove(IReadOnlyList<Creature> _)
    {
        await PerformAttack(NormalAttackDamage);
        AdvanceLowDpCycle();
    }

    private async Task PhaseOneLowAttackBoostMove(
        IReadOnlyList<Creature> targets)
    {
        await AttackBoostMove(targets);
        AdvanceLowDpCycle();
    }

    private async Task AttackBoostMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            2m,
            Creature,
            null);
    }

    private async Task DoomThornMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(DoomThornDamage, 3);
        await ApplyToLivingTargets<WeakPower>(targets, 4m);
        AdvanceLowDpCycle();
    }

    private async Task SpatialDominationMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await CreatureCmd.GainBlock(
            Creature,
            30m,
            ValueProp.Move,
            null);
        await PowerCmd.Apply<VigorPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            5m,
            Creature,
            null);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            1m,
            Creature,
            null);
    }

    private async Task AbsoluteTemperatureMove(
        IReadOnlyList<Creature> targets)
    {
        await PerformAttack(-2, AbsoluteTemperatureHitCount);
        await ApplyToLivingTargets<PoisonPower>(targets, 20m);
    }

    private async Task FreezingHellMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(FreezingHellDamage, 3);
        await ApplyTurnPenalty(targets, 3);
    }

    private async Task TentaclesScissorMove(
        IReadOnlyList<Creature> targets)
    {
        await PerformAttack(TentaclesScissorDamage, 2);
        await ApplyToLivingTargets<FrailPower>(targets, 5m);
    }

    private Task AttackUpMove(IReadOnlyList<Creature> targets)
    {
        return AttackBoostMove(targets);
    }

    private async Task HealMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await CreatureCmd.GainBlock(
            Creature,
            50m,
            ValueProp.Move,
            null);
        Creature.GetPower<HighDimensionalShellPower>()?.Restore(HbrResourceValueHelper.Fixed(Creature, 100m));
    }

    private async Task RespawnMove(IReadOnlyList<Creature> _)
    {
        Phase = 2;
        SkullFeatherRevivalPower revival =
            Creature.GetPower<SkullFeatherRevivalPower>();
        int retainedStrength = IsReviving
            ? RetainedStrength
            : Creature.GetPowerAmount<StrengthPower>();
        decimal scaledHp = Creature.ScaleHpForMultiplayer(
            RevivedHp,
            CombatState.Encounter,
            CombatState.Players.Count,
            CombatState.RunState.CurrentActIndex);
        scaledHp = EnemyHealthScalingHelper.ScaleReplacementHealth(scaledHp, Creature);

        FinishRevive();
        revival?.FinishRevive();
        await CreatureCmd.SetMaxHp(Creature, scaledHp);
        await SaviorGrandEntranceCombatHelper.HealFromPhaseTransition(Creature, scaledHp);

        SkullFeatherPower phaseOnePower =
            Creature.GetPower<SkullFeatherPower>();
        if (phaseOnePower != null)
        {
            await PowerCmd.Remove(phaseOnePower);
        }

        await HighDimensionalShellCombatStartPatch.GrantShell(
            Creature,
            replaceExisting: true);

        int currentStrength = Creature.GetPowerAmount<StrengthPower>();
        int strengthDifference = retainedStrength - currentStrength;
        if (strengthDifference != 0)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                strengthDifference,
                Creature,
                null);
        }

        if (revival != null)
        {
            await PowerCmd.Remove(revival);
        }
    }

    private Task ApplyTurnPenalty(
        IReadOnlyList<Creature> targets,
        int turns)
    {
        return PowerCmd.Apply<SkullFeatherTurnPenaltyPower>(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            turns,
            Creature,
            null);
    }

    private Task ApplyToLivingTargets<TPower>(
        IReadOnlyList<Creature> targets,
        decimal amount)
        where TPower : PowerModel, new()
    {
        if (typeof(TPower) == typeof(WeakPower))
            return HbrBossDebuffApplication.ApplyAttackDown(
                new ThrowingPlayerChoiceContext(),
                targets.Where(target => target.IsAlive).ToArray(),
                amount,
                Creature,
                null);
        return HBRGOSakiMod.Core.Patches.Combat.ThirtyOneAYukiEnumerableDebuffPatch.ApplyEnumerable<TPower>(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            amount,
            Creature,
            null);
    }
}
