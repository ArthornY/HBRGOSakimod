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

// HBR 章鱼尾, act one: original OctopusTailDefaultVirtual (MC02_10_Battle02,
// MC02_14_Battle01). Erosion Slice's virus is chance-based in the original,
// but HBR enemy-side application is guaranteed even through block. The original numbers the hp
// table from step 2; its three rows become steps 1-3
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class OctopusTail : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string TailWhipSkill = "OctopusTail_Attack01";
    internal const string ErosionSliceSkill = "OctopusTail_Attack02";
    internal const string AllAttackSkill = "OctopusTail_AttackAll01";
    internal const string HealSkill = "OctopusTail_Heal01";

    internal const string TailWhipMoveId = "TAIL_WHIP_MOVE";
    internal const string ErosionSliceMoveId = "EROSION_SLICE_MOVE";
    internal const string AllAttackMoveId = "ALL_ATTACK_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    // Original HealDp 700 over the original maximum DP 8400.
    internal const decimal HealDpRatio = 700m / 8400m;

    private static HbrgoMoveStep EitherSingle() =>
        Pick((TailWhipSkill, 50), (ErosionSliceSkill, 50));

    internal static readonly HbrgoMoveTables Tables = new(
        "OctopusTailDefaultVirtual",
        Table("OctopusTailDefault",
            EitherSingle(),
            Step(AllAttackSkill),
            EitherSingle(),
            EitherSingle(),
            Step(AllAttackSkill),
            Step(AllAttackSkill)),
        Table("OctopusTailDefault_break",
            EitherSingle(),
            Pick((AllAttackSkill, 100), (TailWhipSkill, 50), (ErosionSliceSkill, 50)),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(TailWhipSkill, TailWhipMoveId, HbrgoMoveTarget.Single),
        new(ErosionSliceSkill, ErosionSliceMoveId, HbrgoMoveTarget.Single),
        new(AllAttackSkill, AllAttackMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            28,
            26);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            32,
            30);

    private int TailWhipDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            6);

    private int ErosionSliceDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            7);

    private int AllAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            TailWhipMoveId,
            TailWhipMove,
            new SingleAttackIntent(() => TailWhipDamage));
        yield return new MoveState(
            ErosionSliceMoveId,
            ErosionSliceMove,
            new SingleAttackIntent(() => ErosionSliceDamage),
            new DebuffIntent());
        yield return new MoveState(
            AllAttackMoveId,
            AllAttackMove,
            new SingleAttackIntent(() => AllAttackDamage));
        yield return new MoveState(
            HealMoveId,
            HealMove,
            new HealIntent());
    }

    private Task TailWhipMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(TailWhipDamage);
    }

    // Virus in HBR attaches on contact; legacy still requires passing block.
    private async Task ErosionSliceMove(IReadOnlyList<Creature> targets)
    {
        HbrgoAttackHits hits = await PerformTrackedAttack(ErosionSliceDamage, 1, targets);
        await HbrEnemyEffects.ApplyVirus(HbrEnemyEffects.ControlTargets(hits, Creature), Creature);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task AllAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(AllAttackDamage, 1, HbrgoAttackTargets.AllUnits);
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
        return move?.Id == ErosionSliceMoveId
            ? HbrEnemyEffects.ApplyVirus(passedTargets, Creature)
            : Task.CompletedTask;
    }
}
