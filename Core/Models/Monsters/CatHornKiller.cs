using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Compatibility;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 猫角兽γ, act two: original CatHornKillerVirtual (training fights). Same
// skills as 猫角兽; Creepy Ball's seal attaches on contact regardless of block in HBR
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class CatHornKiller : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults, IHbrAfterDeathListener
{
    internal const string HornRushSkill = "CatHorn_Attack01";
    internal const string CreepyBallSkill = "CatHorn_Attack02";
    internal const string SweepSkill = "CatHorn_AttackAll01";
    internal const string HealSkill = "CatHorn_Heal01";

    internal const string HornRushMoveId = "HORN_RUSH_MOVE";
    internal const string CreepyBallMoveId = "CREEPY_BALL_MOVE";
    internal const string SweepMoveId = "SWEEP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int SweepHits = 2;

    // Original HealDp 700 over the original maximum DP 4000.
    internal const decimal HealDpRatio = 700m / 4000m;

    private static HbrgoMoveStep EitherSingle() =>
        Pick((HornRushSkill, 40), (CreepyBallSkill, 60));

    internal static readonly HbrgoMoveTables Tables = new(
        "CatHornKillerVirtual",
        Table("CatHornKiller",
            Step(SweepSkill),
            EitherSingle(),
            Step(SweepSkill),
            Step(SweepSkill),
            EitherSingle(),
            Step(HealSkill)),
        Table("CatHornKiller_break",
            EitherSingle(),
            Step(CreepyBallSkill),
            Step(SweepSkill),
            EitherSingle(),
            Step(SweepSkill),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(HornRushSkill, HornRushMoveId, HbrgoMoveTarget.Single),
        new(CreepyBallSkill, CreepyBallMoveId, HbrgoMoveTarget.Single),
        new(SweepSkill, SweepMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            62,
            58);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            66,
            62);

    private int HornRushDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            9);

    private int CreepyBallDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            13,
            12);

    private int SweepDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal const string PowerGeneratorMoveId = "POWER_GENERATOR_MOVE";
    internal override bool HasFlavorMechanics => true;
    private protected override IEnumerable<MoveState> CreateFlavorMoveStates()
    { yield return new MoveState(PowerGeneratorMoveId, PowerGeneratorMove, new BuffIntent()); }
    private protected override HbrEnemyFlavorChoice ChooseFlavorMove(string tableMoveId) =>
        tableMoveId == HealMoveId && CombatState.GetTeammatesOf(Creature).Count(unit => unit.IsAlive) >= 3
            ? new(PowerGeneratorMoveId) : null;
    private async Task PowerGeneratorMove(IReadOnlyList<Creature> _)
    {
        if (!FlavorEnabled) return;
        await CreatureCmd.TriggerAnim(Creature, "Cast", .15f);
        foreach (Creature ally in CombatState.GetTeammatesOf(Creature).Where(unit => unit.IsAlive).ToArray())
            await HbrEffectApplication.Apply<HbrAttackUpPower>(new ThrowingPlayerChoiceContext(), ally, 200m, Creature,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
    }
    public Task AfterDeath(PlayerChoiceContext context, Creature target, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented && FlavorEnabled && Creature.IsAlive && target.Side == Creature.Side)
            HbrEnemyFlavorSchedulingHelper.RefreshOwner(Creature);
        return Task.CompletedTask;
    }

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            HornRushMoveId,
            HornRushMove,
            new SingleAttackIntent(() => HornRushDamage));
        yield return new MoveState(
            CreepyBallMoveId,
            CreepyBallMove,
            new SingleAttackIntent(() => CreepyBallDamage),
            new DebuffIntent());
        yield return new MoveState(
            SweepMoveId,
            SweepMove,
            new MultiAttackIntent(SweepDamage, () => SweepHits));
        yield return new MoveState(
            HealMoveId,
            HealMove,
            new HealIntent());
    }

    private Task HornRushMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(HornRushDamage);
    }

    // Seal in HBR attaches on contact; legacy still requires passing block.
    private async Task CreepyBallMove(IReadOnlyList<Creature> targets)
    {
        HbrgoAttackHits hits = await PerformTrackedAttack(CreepyBallDamage, 1, targets);
        await HbrEnemyEffects.ApplyImprison(HbrEnemyEffects.ControlTargets(hits, Creature), Creature);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task SweepMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(SweepDamage, SweepHits, HbrgoAttackTargets.AllUnits);
    }

    private async Task HealMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(HealDpRatio);
    }

    Task IHbrgoPhantomAttackResults.AfterPhantomAttack(
        MoveState move,
        IReadOnlyList<Creature> attackedTargets,
        IReadOnlyList<Creature> passedTargets,
        decimal passedDamage)
    {
        return move?.Id == CreepyBallMoveId
            ? HbrEnemyEffects.ApplyImprison(passedTargets, Creature)
            : Task.CompletedTask;
    }
}
