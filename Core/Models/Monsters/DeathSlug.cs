using System;
using HBRGOSakiMod.Core.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class DeathSlug :
    HbrgoStaticMonster,
    IHbrgoRevivingStaticMonster
{
    private MoveState _reviveState;
    private int _phase = 1;
    private bool _isReviving;
    private int _retainedStrength;

    public override int MinInitialHp => ToughValue(220, 200);

    public override int MaxInitialHp => MinInitialHp;

    public bool HasRemainingRevives => Phase == 1;

    public bool ShouldSuppressDeathFade => HasRemainingRevives;

    private int RevivedHp => ToughValue(330, 300);

    private int AttackDamage => DeadlyValue(14, 12);

    private int MultiAttackDamage => DeadlyValue(10, 8);

    private int ShootingHellHitCount => DeadlyValue(7, 6);

    private int DeathBurstHitCount => DeadlyValue(10, 9);

    protected override string VisualsPath =>
        SceneHelper.GetScenePath("creature_visuals/death_slug");

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
        foreach (Creature player in CombatState.PlayerCreatures)
        {
            DeathSlugRespawnPower legacyRespawn =
                player.GetPower<DeathSlugRespawnPower>();
            if (legacyRespawn != null)
            {
                await PowerCmd.Remove(legacyRespawn);
            }
        }

        if (HasRemainingRevives &&
            !Creature.HasPower<DeathSlugRevivalPower>())
        {
            await PowerCmd.Apply<DeathSlugRevivalPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                1m,
                Creature,
                null);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState phaseOneAttack = Move(
            "P1_NORMAL_ATTACK_MOVE",
            NormalAttackMove,
            new SingleAttackIntent(AttackDamage));
        var phaseOneRandom = new RandomBranchState("PHASE_ONE_RANDOM");
        MoveState phaseOneRandomAttack = Move(
            "P1_RANDOM_NORMAL_ATTACK_MOVE",
            NormalAttackMove,
            new SingleAttackIntent(AttackDamage));
        MoveState phaseOneRandomMultiAttack = Move(
            "P1_RANDOM_MULTI_ATTACK_MOVE",
            MultiAttackMove,
            new MultiAttackIntent(MultiAttackDamage, 2));
        MoveState phaseOneCharge = Move(
            "P1_CHARGE_MOVE",
            ChargeMove,
            new BuffIntent());
        MoveState phaseOneShootingHell = Move(
            "P1_SHOOTING_HELL_MOVE",
            ShootingHellMove,
            new MultiAttackIntent(5, ShootingHellHitCount));
        MoveState recovery = Move(
            "RECOVERY_MOVE",
            RecoveryMove,
            new HealIntent());

        phaseOneAttack.FollowUpState = phaseOneRandom;
        phaseOneRandom.AddBranch(
            phaseOneRandomAttack,
            MoveRepeatType.CanRepeatForever,
            0.5f);
        phaseOneRandom.AddBranch(
            phaseOneRandomMultiAttack,
            MoveRepeatType.CanRepeatForever,
            0.5f);
        phaseOneRandomAttack.FollowUpState = phaseOneCharge;
        phaseOneRandomMultiAttack.FollowUpState = phaseOneCharge;
        phaseOneCharge.FollowUpState = phaseOneShootingHell;
        phaseOneShootingHell.FollowUpState = recovery;
        recovery.FollowUpState = phaseOneAttack;

        MoveState phaseTwoAttack = Move(
            "P2_NORMAL_ATTACK_MOVE",
            NormalAttackMove,
            new SingleAttackIntent(AttackDamage));
        MoveState phaseTwoMultiAttack = Move(
            "P2_MULTI_ATTACK_MOVE",
            MultiAttackMove,
            new MultiAttackIntent(MultiAttackDamage, 2));
        var phaseTwoRandom = new RandomBranchState("PHASE_TWO_RANDOM");
        MoveState phaseTwoRandomAttack = Move(
            "P2_RANDOM_NORMAL_ATTACK_MOVE",
            NormalAttackMove,
            new SingleAttackIntent(AttackDamage));
        MoveState phaseTwoRandomMultiAttack = Move(
            "P2_RANDOM_MULTI_ATTACK_MOVE",
            MultiAttackMove,
            new MultiAttackIntent(MultiAttackDamage, 2));
        MoveState phaseTwoDeathBurst = Move(
            "P2_DEATH_BURST_MOVE",
            DeathBurstMove,
            new BuffIntent(),
            new MultiAttackIntent(4, DeathBurstHitCount));

        phaseTwoAttack.FollowUpState = phaseTwoMultiAttack;
        phaseTwoMultiAttack.FollowUpState = phaseTwoRandom;
        phaseTwoRandom.AddBranch(
            phaseTwoRandomAttack,
            MoveRepeatType.CanRepeatForever,
            0.5f);
        phaseTwoRandom.AddBranch(
            phaseTwoRandomMultiAttack,
            MoveRepeatType.CanRepeatForever,
            0.5f);
        phaseTwoRandomAttack.FollowUpState = phaseTwoDeathBurst;
        phaseTwoRandomMultiAttack.FollowUpState = phaseTwoDeathBurst;
        phaseTwoDeathBurst.FollowUpState = phaseTwoAttack;

        ReviveState = Move(
            "RESPAWN_MOVE",
            RespawnMove,
            new HealIntent(),
            new BuffIntent());
        ReviveState.MustPerformOnceBeforeTransitioning = true;
        var reviveBranch = new ConditionalBranchState("REVIVE_BRANCH");
        ReviveState.FollowUpState = reviveBranch;
        reviveBranch.AddState(
            phaseOneAttack,
            () => Phase == 1);
        reviveBranch.AddState(
            phaseTwoAttack,
            () => Phase == 2);

        return new MonsterMoveStateMachine(
            new MonsterState[]
            {
                phaseOneAttack,
                phaseOneRandom,
                phaseOneRandomAttack,
                phaseOneRandomMultiAttack,
                phaseOneCharge,
                phaseOneShootingHell,
                recovery,
                phaseTwoAttack,
                phaseTwoMultiAttack,
                phaseTwoRandom,
                phaseTwoRandomAttack,
                phaseTwoRandomMultiAttack,
                phaseTwoDeathBurst,
                ReviveState,
                reviveBranch
            },
            Phase == 2 ? phaseTwoAttack : phaseOneAttack);
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

    private Task NormalAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task MultiAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(MultiAttackDamage, 2);
    }

    private Task ShootingHellMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(5, ShootingHellHitCount);
    }

    private async Task ChargeMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            3m,
            Creature,
            null);
    }

    private async Task RecoveryMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        Creature.GetPower<HighDimensionalShellPower>()?.Restore(HbrResourceValueHelper.Fixed(Creature, 30m));
    }

    private async Task DeathBurstMove(IReadOnlyList<Creature> _)
    {
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            2m,
            Creature,
            null);
        await PerformAttack(2, DeathBurstHitCount);
    }

    private async Task RespawnMove(IReadOnlyList<Creature> _)
    {
        Phase = 2;

        DeathSlugRevivalPower revival =
            Creature.GetPower<DeathSlugRevivalPower>();
        DeathSlugRespawnPower legacyRespawn =
            Creature.GetPower<DeathSlugRespawnPower>();
        List<DeathSlugRespawnPower> playerRespawnPowers =
            CombatState.PlayerCreatures
                .Select(player => player.GetPower<DeathSlugRespawnPower>())
                .Where(power => power != null)
                .ToList();
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
        await CreatureCmd.SetMaxHp(Creature, scaledHp);
        await SaviorGrandEntranceCombatHelper.HealFromPhaseTransition(Creature, scaledHp);
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

        if (legacyRespawn != null)
        {
            await PowerCmd.Remove(legacyRespawn);
        }

        foreach (DeathSlugRespawnPower respawnPower in playerRespawnPowers)
        {
            await PowerCmd.Remove(respawnPower);
        }

        if (Phase == 2 && revival != null)
        {
            await PowerCmd.Remove(revival);
        }
    }
}
