using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class RedCrimson : MonsterModel
{
    public const int MaxActionGauge = 7;

    private int _summonLevel = 1;
    private int _intentCycleBonus;
    private int _actionGauge;
    private int _nextAttackBonus;
    private bool _nextAttackWaivesCounterCost;
    private bool _isActing;
    private bool _hasPaidDeathCounter;

    [SavedProperty]
    public int SummonLevel
    {
        get => _summonLevel;
        private set
        {
            AssertMutable();
            _summonLevel = Math.Clamp(value, 1, 6);
        }
    }

    public override int MinInitialHp => GetHpForLevel(SummonLevel);

    public override int MaxInitialHp => GetHpForLevel(SummonLevel);

    public override bool ShouldShowInCompendium => false;

    public override bool HasDeathSfx => false;

    protected override string VisualsPath =>
        SceneHelper.GetScenePath("creature_visuals/red_crimson");

    public void Configure(int level, int intentCycleBonus = 0)
    {
        SummonLevel = level;
        IntentCycleBonus = intentCycleBonus;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int IntentCycleBonus
    {
        get => _intentCycleBonus;
        private set
        {
            AssertMutable();
            _intentCycleBonus = Math.Clamp(value, 0, 1);
        }
    }

    public void SetIntentCycleBonus(int bonus)
    {
        int nextBonus = Math.Clamp(bonus, 0, 1);
        if (IntentCycleBonus == nextBonus)
        {
            return;
        }

        IntentCycleBonus = nextBonus;
        if (Creature?.CombatState == null || !Creature.IsAlive)
        {
            return;
        }

        ResetStateMachine();
        SetUpForCombat();
        Creature.PrepareForNextTurn(
            CombatState
                .GetOpponentsOf(Creature)
                .Where(target => target.IsHittable));
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ActionGauge
    {
        get => _actionGauge;
        private set
        {
            AssertMutable();
            _actionGauge = Math.Clamp(
                value,
                0,
                MaxActionGauge);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int NextAttackBonus
    {
        get => _nextAttackBonus;
        private set
        {
            AssertMutable();
            _nextAttackBonus = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool NextAttackWaivesCounterCost
    {
        get => _nextAttackWaivesCounterCost;
        private set
        {
            AssertMutable();
            _nextAttackWaivesCounterCost = value;
        }
    }

    public void EmpowerNextAttack(int bonusAttackCount)
    {
        if (bonusAttackCount <= 0 || !Creature.IsAlive)
        {
            return;
        }

        NextAttackBonus += bonusAttackCount;
        NextAttackWaivesCounterCost = true;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState strike = new(
            "STRIKE_MOVE",
            StrikeMove,
            new SingleAttackIntent(() => BaseIntentDamage));
        MoveState deltaScrape = new(
            "DELTA_SCRAPE_MOVE",
            DeltaScrapeMove,
            new SingleAttackIntent(() => BaseIntentDamage),
            new DebuffIntent());
        MoveState disappearRay = new(
            "DISAPPEAR_RAY_MOVE",
            DisappearRayMove,
            new SingleAttackIntent(() => BaseIntentDamage));
        MoveState bloodFall = new(
            "BLOOD_FALL_MOVE",
            BloodFallMove,
            new SingleAttackIntent(() => BaseIntentDamage),
            new DebuffIntent(strong: true));
        MoveState despairingStarlight = new(
            "DESPAIRING_STARLIGHT_MOVE",
            DespairingStarlightMove,
            new MultiAttackIntent(
                GetBaseIntentDamageForLevel(SummonLevel),
                10));
        MoveState worldsEndSuperNova = new(
            "WORLDS_END_SUPER_NOVA_MOVE",
            WorldsEndSuperNovaMove,
            new MultiAttackIntent(
                GetDpForLevel(SummonLevel),
                5));
        MoveState stunned = new(
            "STUN_MOVE",
            _ => Task.CompletedTask,
            new StunIntent());

        int cycleLevel = Math.Clamp(
            SummonLevel + IntentCycleBonus,
            1,
            6);
        return cycleLevel switch
        {
            1 => ModMonsterMoveStateMachines.SingleMoveLoop(strike),
            2 => ModMonsterMoveStateMachines.Cycle(deltaScrape, strike),
            3 => ModMonsterMoveStateMachines.Cycle(
                deltaScrape,
                disappearRay,
                strike),
            4 => ModMonsterMoveStateMachines.Cycle(
                bloodFall,
                deltaScrape,
                disappearRay,
                strike),
            5 => ModMonsterMoveStateMachines.Cycle(
                bloodFall,
                deltaScrape,
                disappearRay,
                despairingStarlight,
                strike),
            _ => ModMonsterMoveStateMachines.Cycle(
                worldsEndSuperNova,
                stunned,
                bloodFall,
                deltaScrape,
                disappearRay,
                despairingStarlight,
                strike),
        };
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Creature.Side ||
            !Creature.IsAlive ||
            CombatState == null ||
            !CombatState.IsLiveCombat() ||
            CombatManager.Instance.IsOverOrEnding ||
            CombatManager.Instance.PlayersTakingExtraTurn.Count > 0)
        {
            return;
        }

        await TakeAction(resetActionGauge: false);
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Creature.Side &&
            Creature.IsAlive &&
            combatState.IsLiveCombat())
        {
            SpendRelicCounter(2);
        }

        return Task.CompletedTask;
    }

    public async Task AddActionGauge(
        PlayerChoiceContext choiceContext,
        int amount)
    {
        if (amount <= 0 ||
            !Creature.IsAlive ||
            CombatState == null ||
            !CombatState.IsLiveCombat() ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        ActionGauge += amount;
        if (ActionGauge < MaxActionGauge)
        {
            return;
        }

        await TakeAction(resetActionGauge: true);
    }

    public Task ActImmediately()
    {
        return TakeAction(resetActionGauge: false);
    }

    internal void OnBeforeDeath()
    {
        if (_hasPaidDeathCounter)
        {
            return;
        }

        _hasPaidDeathCounter = true;
        SpendRelicCounter(10);
    }

    private int MaxDp => HighDimensionalShellPower.CalculateAmount(Creature);

    private int BaseIntentDamage => Math.Max(1, MaxDp / 2);

    private async Task StrikeMove(IReadOnlyList<Creature> _)
    {
        await AttackRandomTarget(
            BaseIntentDamage,
            weak: 0,
            counterCost: 0);
    }

    private async Task DeltaScrapeMove(IReadOnlyList<Creature> _)
    {
        await AttackRandomTarget(
            BaseIntentDamage,
            weak: 2,
            counterCost: 1);
    }

    private async Task DisappearRayMove(IReadOnlyList<Creature> _)
    {
        await AttackAllTargets(
            BaseIntentDamage,
            vulnerable: 0,
            counterCost: 1);
    }

    private async Task BloodFallMove(IReadOnlyList<Creature> _)
    {
        await AttackAllTargets(
            BaseIntentDamage,
            vulnerable: 3,
            counterCost: 2);
    }

    private async Task DespairingStarlightMove(
        IReadOnlyList<Creature> _)
    {
        if (GetHittableOpponents().Count == 0)
        {
            return;
        }

        AttackEnhancement enhancement =
            ConsumeNextAttackEnhancement();
        await CreatureCmd.TriggerAnim(
            Creature,
            "Attack",
            0.25f);
        PlayerChoiceContext choiceContext =
            new ThrowingPlayerChoiceContext();
        for (int hit = 0;
             hit < 10 + enhancement.BonusAttackCount;
             hit++)
        {
            List<Creature> targets = GetHittableOpponents();
            if (targets.Count == 0)
            {
                break;
            }

            Creature target =
                CombatState.RunState.Rng.CombatTargets.NextItem(targets);
            await CreatureCmd.Damage(
                choiceContext,
                target,
                BaseIntentDamage,
                ValueProp.Move,
                Creature);
        }

        SpendRelicCounter(
            3,
            enhancement.WaivesCounterCost);
    }

    private async Task WorldsEndSuperNovaMove(
        IReadOnlyList<Creature> _)
    {
        if (GetHittableOpponents().Count == 0)
        {
            return;
        }

        AttackEnhancement enhancement =
            ConsumeNextAttackEnhancement();
        await CreatureCmd.TriggerAnim(
            Creature,
            "Attack",
            0.25f);
        PlayerChoiceContext choiceContext =
            new ThrowingPlayerChoiceContext();
        for (int hit = 0;
             hit < 5 + enhancement.BonusAttackCount;
             hit++)
        {
            List<Creature> targets = GetHittableOpponents();
            if (targets.Count == 0)
            {
                break;
            }

            await CreatureCmd.Damage(
                choiceContext,
                targets,
                BaseIntentDamage * 2m,
                ValueProp.Move,
                Creature);
        }

        SpendRelicCounter(
            3,
            enhancement.WaivesCounterCost);
    }

    private async Task AttackRandomTarget(
        decimal damage,
        int weak,
        int counterCost)
    {
        List<Creature> targets = GetHittableOpponents();
        if (targets.Count == 0)
        {
            return;
        }

        AttackEnhancement enhancement =
            ConsumeNextAttackEnhancement();
        Creature target =
            CombatState.RunState.Rng.CombatTargets.NextItem(targets);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Attack",
            0.25f);
        PlayerChoiceContext choiceContext =
            new ThrowingPlayerChoiceContext();
        for (int hit = 0;
             hit < 1 + enhancement.BonusAttackCount &&
             target.IsHittable;
             hit++)
        {
            await CreatureCmd.Damage(
                choiceContext,
                target,
                damage,
                ValueProp.Move,
                Creature);
        }
        if (weak > 0 && target.IsAlive)
        {
            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                target,
                weak,
                Creature,
                null);
        }

        SpendRelicCounter(
            counterCost,
            enhancement.WaivesCounterCost);
    }

    private async Task AttackAllTargets(
        decimal damage,
        int vulnerable,
        int counterCost)
    {
        List<Creature> targets = GetHittableOpponents();
        if (targets.Count == 0)
        {
            return;
        }

        AttackEnhancement enhancement =
            ConsumeNextAttackEnhancement();
        await CreatureCmd.TriggerAnim(
            Creature,
            "Attack",
            0.25f);
        PlayerChoiceContext choiceContext =
            new ThrowingPlayerChoiceContext();
        for (int hit = 0;
             hit < 1 + enhancement.BonusAttackCount;
             hit++)
        {
            List<Creature> currentTargets =
                GetHittableOpponents();
            if (currentTargets.Count == 0)
            {
                break;
            }

            await CreatureCmd.Damage(
                choiceContext,
                currentTargets,
                damage,
                ValueProp.Move,
                Creature);
        }
        if (vulnerable > 0)
        {
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext,
                targets.Where(target => target.IsAlive),
                vulnerable,
                Creature,
                null);
        }

        SpendRelicCounter(
            counterCost,
            enhancement.WaivesCounterCost);
    }

    private List<Creature> GetHittableOpponents()
    {
        return CombatState
            .GetOpponentsOf(Creature)
            .Where(target => target.IsHittable)
            .ToList();
    }

    private void SpendRelicCounter(
        int amount,
        bool waived = false)
    {
        if (waived)
        {
            return;
        }

        Creature.PetOwner?
            .GetRelic<RestorationMoment>()?
            .SpendCounter(amount * 2);
    }

    private AttackEnhancement ConsumeNextAttackEnhancement()
    {
        AttackEnhancement enhancement = new(
            NextAttackBonus,
            NextAttackWaivesCounterCost);
        NextAttackBonus = 0;
        NextAttackWaivesCounterCost = false;
        return enhancement;
    }

    private async Task TakeAction(bool resetActionGauge)
    {
        if (_isActing ||
            !Creature.IsAlive ||
            CombatState == null ||
            !CombatState.IsLiveCombat() ||
            CombatManager.Instance.IsOverOrEnding)
        {
            if (resetActionGauge)
            {
                ActionGauge = 0;
            }

            return;
        }

        _isActing = true;
        try
        {
            if (Creature.GetCreatureNode() is { } creatureNode)
            {
                await creatureNode.PerformIntent();
            }

            await PerformMove();
            if (!Creature.IsAlive ||
                CombatManager.Instance.IsOverOrEnding ||
                !CombatState.IsLiveCombat())
            {
                return;
            }

            Creature.PrepareForNextTurn(
                CombatState
                    .GetOpponentsOf(Creature)
                    .Where(target => target.IsHittable));
        }
        finally
        {
            if (resetActionGauge)
            {
                ActionGauge = 0;
            }

            _isActing = false;
        }
    }

    private static int GetHpForLevel(int level)
    {
        return level switch
        {
            1 => 10,
            2 => 20,
            3 => 25,
            4 => 35,
            5 => 50,
            _ => 70
        };
    }

    private static int GetDpForLevel(int level)
    {
        return Math.Max(1, GetHpForLevel(level) / 5);
    }

    private static int GetBaseIntentDamageForLevel(int level)
    {
        return Math.Max(1, GetDpForLevel(level) / 2);
    }

    private readonly record struct AttackEnhancement(
        int BonusAttackCount,
        bool WaivesCounterCost);
}
