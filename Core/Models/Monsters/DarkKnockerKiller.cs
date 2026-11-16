using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 杀手重锤, act two: original DarkKnockerKillerVirtual (the training fights;
// the Mt. Kinpu fight's DarkKnockerKillerFire2 has the same tables). The
// original has no heal and no charge; the 11-05 charge and heavy attack are
// gone (docs/hbr-monster-mechanics-proposal.md).
public sealed class DarkKnockerKiller : HbrgoMoveTableMonster
{
    internal const string AttackSkill = "DarkKnocker_Attack01";
    internal const string MultiAttackSkill = "DarkKnocker_Attack02";
    internal const string AllAttackSkill = "DarkKnocker_AttackAll01";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string MultiAttackMoveId = "MULTI_ATTACK_MOVE";
    internal const string AllAttackMoveId = "ALL_ATTACK_MOVE";

    internal const int MultiAttackHits = 2;

    internal static readonly HbrgoMoveTables Tables = new(
        "DarkKnockerKillerVirtual",
        Table("DarkKnockerKiller",
            Step(AttackSkill),
            Step(AllAttackSkill),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
            Step(AllAttackSkill),
            Step(MultiAttackSkill),
            Step(AttackSkill),
            Step(AllAttackSkill),
            Step(AllAttackSkill)),
        Table("DarkKnockerKiller_break",
            Step(AllAttackSkill),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
            Step(AllAttackSkill),
            Step(MultiAttackSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(MultiAttackSkill, MultiAttackMoveId, HbrgoMoveTarget.Single),
        new(AllAttackSkill, AllAttackMoveId, HbrgoMoveTarget.All),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            68,
            64);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            72,
            68);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            8);

    private int MultiAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            5);

    private int AllAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            12,
            11);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            AttackMoveId,
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        yield return new MoveState(
            MultiAttackMoveId,
            MultiAttackMove,
            new MultiAttackIntent(MultiAttackDamage, () => MultiAttackHits));
        yield return new MoveState(
            AllAttackMoveId,
            AllAttackMove,
            new SingleAttackIntent(() => AllAttackDamage));
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task MultiAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(MultiAttackDamage, MultiAttackHits);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task AllAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(AllAttackDamage, 1, HbrgoAttackTargets.AllUnits);
    }
}
