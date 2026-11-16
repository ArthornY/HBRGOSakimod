using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 旋轮, act one: original WireSlasherDefaultDark (MC02_25). The fights of
// MC02_25 field dark, light, thunder, fire and ice wheels whose skills and
// tables differ only by element; the mod has no elements, so one class
// copies the dark one (docs/hbr-monster-mechanics-proposal.md).
public sealed class WireSlasher : HbrgoMoveTableMonster
{
    internal const string AttackSkill = "WireSlasher_AttackSingle01_Dark";
    internal const string ComboSkill = "WireSlasher_AttackSingle02_Dark";
    internal const string AllAttackSkill = "WireSlasher_AttackAll01_Dark";
    internal const string RecoverySkill = "WireSlasher_Buff01";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string ComboMoveId = "COMBO_MOVE";
    internal const string AllAttackMoveId = "ALL_ATTACK_MOVE";
    internal const string RecoveryMoveId = "RECOVERY_MOVE";

    internal const int ComboHits = 7;

    // Original HealDp 700 over the original maximum DP 7600.
    internal const decimal RecoveryDpRatio = 700m / 7600m;

    internal static readonly HbrgoMoveTables Tables = new(
        "WireSlasherDefaultDark",
        Table("WireSlasherDefault_Dark",
            Step(AttackSkill),
            Step(AllAttackSkill),
            Pick((AttackSkill, 50), (ComboSkill, 50)),
            Step(ComboSkill),
            Step(RecoverySkill),
            Step(AllAttackSkill),
            Step(AllAttackSkill)),
        Table("WireSlasherDefault_Dark_break",
            Step(AllAttackSkill),
            Pick((AttackSkill, 40), (ComboSkill, 60)),
            Step(RecoverySkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(ComboSkill, ComboMoveId, HbrgoMoveTarget.Single),
        new(AllAttackSkill, AllAttackMoveId, HbrgoMoveTarget.All),
        new(RecoverySkill, RecoveryMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            22,
            20);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            26,
            24);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int ComboDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            1,
            1);

    private int AllAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
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
            ComboMoveId,
            ComboMove,
            new MultiAttackIntent(ComboDamage, () => ComboHits));
        yield return new MoveState(
            AllAttackMoveId,
            AllAttackMove,
            new SingleAttackIntent(() => AllAttackDamage));
        yield return new MoveState(
            RecoveryMoveId,
            RecoveryMove,
            new HealIntent());
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task ComboMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(ComboDamage, ComboHits);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task AllAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(AllAttackDamage, 1, HbrgoAttackTargets.AllUnits);
    }

    private async Task RecoveryMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(RecoveryDpRatio);
    }
}
