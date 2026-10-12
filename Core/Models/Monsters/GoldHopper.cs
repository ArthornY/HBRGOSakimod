using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class GoldHopper : HbrgoStaticMonster
{
    private MoveState _attackState;
    private MoveState _escapeState;
    private int _breakRecoveries;
    private bool _useBetaVisuals;
    private bool _bonusRewardGranted;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            56,
            54);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            60,
            58);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    protected override string VisualsPath =>
        SceneHelper.GetScenePath(
            UseBetaVisuals
                ? "creature_visuals/gold_hopper_beta"
                : "creature_visuals/gold_hopper_alpha");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BreakRecoveries
    {
        get => _breakRecoveries;
        private set
        {
            AssertMutable();
            _breakRecoveries = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool UseBetaVisuals
    {
        get => _useBetaVisuals;
        private set
        {
            AssertMutable();
            _useBetaVisuals = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BonusRewardGranted
    {
        get => _bonusRewardGranted;
        private set
        {
            AssertMutable();
            _bonusRewardGranted = value;
        }
    }

    public void ConfigureVisuals(bool useBetaVisuals)
    {
        UseBetaVisuals = useBetaVisuals;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        Creature.PowerRemoved += OnPowerRemoved;
    }

    public override void BeforeRemovedFromRoom()
    {
        Creature.PowerRemoved -= OnPowerRemoved;
        base.BeforeRemovedFromRoom();
    }

    public override Task AfterDeath(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext
            choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature ||
            wasRemovalPrevented ||
            BonusRewardGranted ||
            CombatState.RunState.CurrentRoom is not CombatRoom combatRoom)
        {
            return Task.CompletedTask;
        }

        BonusRewardGranted = true;
        foreach (var player in CombatState.Players)
        {
            combatRoom.AddExtraReward(
                player,
                new GoldReward(20, 25, player));
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _attackState = new MoveState(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        _escapeState = new MoveState(
            "ESCAPE_MOVE",
            EscapeMove,
            new EscapeIntent());
        _attackState.FollowUpState = _attackState;
        _escapeState.FollowUpState = _escapeState;
        return new MonsterMoveStateMachine(
            new MonsterState[] { _attackState, _escapeState },
            _attackState);
    }

    private async Task AttackMove(IReadOnlyList<Creature> _)
    {
        await PerformAttack(AttackDamage);
        if (BreakRecoveries >= 2 &&
            _attackState != null &&
            _escapeState != null)
        {
            _attackState.FollowUpState = _escapeState;
        }
    }

    private async Task EscapeMove(IReadOnlyList<Creature> _)
    {
        NCombatRoom.Instance?
            .GetCreatureNode(Creature)?
            .ToggleIsInteractable(false);
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0.15f);
        await CreatureCmd.Escape(Creature);
    }

    private void OnPowerRemoved(PowerModel power)
    {
        if (power is not BreakPower || !Creature.IsAlive)
        {
            return;
        }

        BreakRecoveries++;
    }
}
