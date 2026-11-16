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

// HBR 线圈巨轮, act one: original TargetWireFrameSmallVirtual (α,
// MC02_11_Battle01, MC02_14_Battle02). Illusion Spin's illusion is
// chance-based in the original, but HBR enemy-side application is guaranteed even through block
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class TargetWireFrame : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string CoilShotSkill = "TargetWireFrame_Attack01";
    internal const string IllusionSpinSkill = "TargetWireFrame_Attack02";
    internal const string ScatterSkill = "TargetWireFrame_AttackAll01";
    internal const string HealSkill = "TargetWireFrame_Heal01";

    internal const string CoilShotMoveId = "COIL_SHOT_MOVE";
    internal const string IllusionSpinMoveId = "ILLUSION_SPIN_MOVE";
    internal const string ScatterMoveId = "SCATTER_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int IllusionLayers = 1;

    // Original HealDp 700 over the original maximum DP 6200.
    internal const decimal HealDpRatio = 700m / 6200m;

    private static HbrgoMoveStep CoilOrSpin(int coil, int spin) =>
        Pick((CoilShotSkill, coil), (IllusionSpinSkill, spin));

    internal static readonly HbrgoMoveTables Tables = new(
        "TargetWireFrameSmallVirtual",
        Table("TargetWireFrameSmall",
            Step(CoilShotSkill),
            Step(ScatterSkill),
            CoilOrSpin(60, 40),
            CoilOrSpin(60, 40),
            Step(ScatterSkill),
            Step(HealSkill)),
        Table("TargetWireFrameSmall_break",
            CoilOrSpin(50, 50),
            CoilOrSpin(50, 50),
            Step(ScatterSkill),
            CoilOrSpin(50, 50),
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
            26,
            24);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            30,
            28);

    private int CoilShotDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            6);

    private int IllusionSpinDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

    private int ScatterDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

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
