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

// HBR 猫角兽, act one: original CatHornDefaultVirtual (β, MC02_13_Battle01).
// 成对的猫角兽 (CatHornsNormal, MC01_05_Battle01) copies α,
// CatHornSmallVirtual_first. Creepy Ball's seal is chance-based in the
// original, but HBR enemy-side application is guaranteed even through block
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class CatHorn : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults, IHbrAfterDeathListener
{
    internal const int DefaultVariant = 0;
    internal const int FirstVariant = 1;

    internal const string HornRushSkill = "CatHorn_Attack01";
    internal const string CreepyBallSkill = "CatHorn_Attack02";
    internal const string SweepSkill = "CatHorn_AttackAll01";
    internal const string HealSkill = "CatHorn_Heal01";

    internal const string HornRushMoveId = "HORN_RUSH_MOVE";
    internal const string CreepyBallMoveId = "CREEPY_BALL_MOVE";
    internal const string SweepMoveId = "SWEEP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int SweepHits = 2;

    // Original HealDp 700 over each variant's original maximum DP.
    internal const decimal DefaultHealDpRatio = 700m / 5400m;
    internal const decimal FirstHealDpRatio = 700m / 3000m;

    private static HbrgoMoveStep EitherSingle(int rush, int ball) =>
        Pick((HornRushSkill, rush), (CreepyBallSkill, ball));

    internal static readonly HbrgoMoveTables DefaultTables = new(
        "CatHornDefaultVirtual",
        Table("CatHornDefault",
            EitherSingle(50, 50),
            Step(SweepSkill),
            EitherSingle(50, 50),
            EitherSingle(50, 50),
            Step(SweepSkill),
            Step(HealSkill)),
        Table("CatHornDefault_break",
            Step(SweepSkill),
            EitherSingle(50, 50),
            Step(CreepyBallSkill),
            Step(SweepSkill),
            EitherSingle(50, 50),
            Step(HealSkill)));

    internal static readonly HbrgoMoveTables FirstTables = new(
        "CatHornSmallVirtual_first",
        Table("CatHornSmall",
            Step(HornRushSkill),
            EitherSingle(60, 40),
            Step(SweepSkill),
            EitherSingle(60, 40),
            Step(SweepSkill),
            Step(HealSkill)),
        Table("CatHornSmall_break",
            EitherSingle(60, 40),
            Step(SweepSkill),
            EitherSingle(60, 40),
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
            32,
            30);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            36,
            34);

    private int HornRushDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int CreepyBallDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

    private int SweepDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            3);

    internal override int MoveTableVariantCount => 2;

    internal override HbrgoMoveTables GetMoveTables(int variant) =>
        variant == FirstVariant ? FirstTables : DefaultTables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    private decimal HealDpRatio =>
        MoveTableVariant == FirstVariant ? FirstHealDpRatio : DefaultHealDpRatio;

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
