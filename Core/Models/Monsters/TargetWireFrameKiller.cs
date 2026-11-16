using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 线圈巨轮γ, act-two elite. Its moves copy the original γ,
// TargetWireFrameKiller_Day2E01, which fights alone in MC04ExpMtKumotori01_E01.
// The lineup (TargetWireFrameKillerElite) stays MC03ExpMtKinpu02_E02's, whose
// original wheel is β with two killer hoppers; β's opening story move, which
// hits its own side, is not carried over (docs/hbr-act-rosters.md).
public sealed class TargetWireFrameKiller : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string CoilShotSkill = "TargetWireFrame_Attack01";
    internal const string IllusionSpinSkill = "TargetWireFrame_Attack02";
    internal const string ScatterSkill = "TargetWireFrame_AttackAll01";
    internal const string HealSkill = "TargetWireFrame_Heal02";

    internal const string CoilShotMoveId = "COIL_SHOT_MOVE";
    internal const string IllusionSpinMoveId = "ILLUSION_SPIN_MOVE";
    internal const string ScatterMoveId = "SCATTER_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int IllusionLayers = 1;

    // Original HealDp 5000 over the original maximum DP 7000.
    internal const decimal HealDpRatio = 5000m / 7000m;

    private static HbrgoMoveStep CoilOrSpin() =>
        Pick((CoilShotSkill, 40), (IllusionSpinSkill, 60));

    internal static readonly HbrgoMoveTables Tables = new(
        "TargetWireFrameKiller_Day2E01",
        Table("TargetWireFrameKiller",
            Step(ScatterSkill),
            CoilOrSpin(),
            CoilOrSpin(),
            Step(ScatterSkill),
            CoilOrSpin(),
            Step(HealSkill)),
        Table("TargetWireFrameKiller_break",
            Step(ScatterSkill),
            CoilOrSpin(),
            CoilOrSpin(),
            CoilOrSpin(),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(CoilShotSkill, CoilShotMoveId, HbrgoMoveTarget.Single),
        new(IllusionSpinSkill, IllusionSpinMoveId, HbrgoMoveTarget.Single),
        new(ScatterSkill, ScatterMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            96,
            90);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            100,
            94);

    private int CoilShotDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            13,
            12);

    private int IllusionSpinDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            18,
            16);

    private int ScatterDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16,
            14);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            CoilShotMoveId,
            CoilShotMove,
            new SingleAttackIntent(() => CoilShotDamage));
        yield return new MoveState(
            IllusionSpinMoveId,
            IllusionSpinMove,
            new SingleAttackIntent(() => IllusionSpinDamage),
            new DebuffIntent());
        yield return new MoveState(
            ScatterMoveId,
            ScatterMove,
            new SingleAttackIntent(() => ScatterDamage));
        yield return new MoveState(
            HealMoveId,
            HealMove,
            new HealIntent());
    }

    private Task CoilShotMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(CoilShotDamage);
    }

    // Illusion in HBR attaches on contact; legacy still requires passing block.
    private async Task IllusionSpinMove(IReadOnlyList<Creature> targets)
    {
        HbrgoAttackHits hits = await PerformTrackedAttack(IllusionSpinDamage, 1, targets);
        await HbrEnemyEffects.ApplyIllusion(HbrEnemyEffects.ControlTargets(hits, Creature), IllusionLayers, Creature);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task ScatterMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(ScatterDamage, 1, HbrgoAttackTargets.AllUnits);
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
        return move?.Id == IllusionSpinMoveId
            ? HbrEnemyEffects.ApplyIllusion(passedTargets, IllusionLayers, Creature)
            : Task.CompletedTask;
    }
}
