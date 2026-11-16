using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 黏土重锤, act-one elite partner: original DarkKnockerDefaultVirtual
// (MC02_12_Battle01-03, MC02_18_Battle01, MC02_19_Battle01; DarkKnockerDefault
// has the same tables). The original has no charge; the 11-05 Strength charge
// and heavy attack are gone (docs/hbr-act-rosters.md, fourth batch).
public sealed class ClayKnocker : HbrgoMoveTableMonster
{
    internal const string AttackSkill = "DarkKnocker_Attack01";
    internal const string MultiAttackSkill = "DarkKnocker_Attack02";
    internal const string AllAttackSkill = "DarkKnocker_AttackAll01";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string MultiAttackMoveId = "MULTI_ATTACK_MOVE";
    internal const string AllAttackMoveId = "ALL_ATTACK_MOVE";

    internal const int MultiAttackHits = 2;

    internal static readonly HbrgoMoveTables Tables = new(
        "DarkKnockerDefaultVirtual",
        Table("DarkKnockerDefault",
            Step(AttackSkill),
            Step(AllAttackSkill),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
            Step(AllAttackSkill),
            Step(AllAttackSkill),
            Step(AllAttackSkill),
            Step(AllAttackSkill)),
        Table("DarkKnockerDefault_break",
            Step(MultiAttackSkill),
            Step(AllAttackSkill),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
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
            80,
            76);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            84,
            80);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int MultiAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            3);

    private int AllAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

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
