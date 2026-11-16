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

// HBR 章鱼尾γ, act two: original OctopusTailKillerVirtual (MC03_17_Battle01).
// Same skills as 章鱼尾 except its own all-unit attack; Erosion Slice's virus
// attaches on contact regardless of block in HBR. The original numbers the hp table from step 2
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class OctopusTailKiller : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string TailWhipSkill = "OctopusTail_Attack01";
    internal const string ErosionSliceSkill = "OctopusTail_Attack02";
    internal const string AllAttackSkill = "OctopusTail_AttackAll01_Killer";
    internal const string HealSkill = "OctopusTail_Heal01";

    internal const string TailWhipMoveId = "TAIL_WHIP_MOVE";
    internal const string ErosionSliceMoveId = "EROSION_SLICE_MOVE";
    internal const string AllAttackMoveId = "ALL_ATTACK_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    // Original HealDp 700 over the original maximum DP 6200.
    internal const decimal HealDpRatio = 700m / 6200m;

    private static HbrgoMoveStep EitherSingle(int whip, int slice) =>
        Pick((TailWhipSkill, whip), (ErosionSliceSkill, slice));

    internal static readonly HbrgoMoveTables Tables = new(
        "OctopusTailKillerVirtual",
        Table("OctopusTailKiller",
            Step(AllAttackSkill),
            EitherSingle(40, 60),
            Step(AllAttackSkill),
            EitherSingle(40, 60),
            EitherSingle(40, 60),
            Step(AllAttackSkill)),
        Table("OctopusTailKiller_break",
            EitherSingle(50, 50),
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
            49,
            46);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            53,
            50);

    private int TailWhipDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

    private int ErosionSliceDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            12,
            11);

    private int AllAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            9);

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
