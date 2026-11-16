using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 重锤, act one: original DarkKnockerSmallVirtual (the chapter-two training
// forest; DarkKnockerSmall has the same tables and DP). Its fights,
// KnockersWithHealerNormal (MC02_12_Battle02, whose original has two clay
// knockers) and KnockersElite, have no original knocker of their own
// (docs/hbr-act-rosters.md, fourth batch).
public sealed class Knocker : HbrgoMoveTableMonster
{
    internal const string AttackSkill = "DarkKnocker_Attack01";
    internal const string MultiAttackSkill = "DarkKnocker_Attack02";
    internal const string AllAttackSkill = "DarkKnocker_AttackAll01";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string MultiAttackMoveId = "MULTI_ATTACK_MOVE";
    internal const string AllAttackMoveId = "ALL_ATTACK_MOVE";

    internal const int MultiAttackHits = 2;

    internal static readonly HbrgoMoveTables Tables = new(
        "DarkKnockerSmallVirtual",
        Table("DarkKnockerSmall",
            Step(AttackSkill),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
            Step(AllAttackSkill),
            Step(AttackSkill),
            Step(AllAttackSkill),
            Step(AllAttackSkill)),
        Table("DarkKnockerSmall_break",
            Step(AttackSkill),
            Pick((AttackSkill, 40), (MultiAttackSkill, 60)),
            Step(AllAttackSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(MultiAttackSkill, MultiAttackMoveId, HbrgoMoveTarget.Single),
        new(AllAttackSkill, AllAttackMoveId, HbrgoMoveTarget.All),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            40,
            38);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            44,
            42);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int MultiAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            3);

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
