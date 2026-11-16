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

// HBR 小型跳虫, act one: original CrestHopperSmall (MC01_12_Battle01b). The
// opening HP and DP baseline in HbrEnemyResourceAdapter is unchanged. Damage
// follows the 11-05 average of 4 (5) a turn; the combo's eight hits cannot go
// below 1 each (docs/hbr-act-rosters.md, fourth batch).
public sealed class SmallHopper : HbrgoMoveTableMonster
{
    internal const string AttackSkill = "CrestHopper_DefaultAttack";
    internal const string ComboSkill = "CrestHopper_SkillAttack";
    internal const string RepairSkill = "CrestHopper_Heal1";

    // HbrEnemySkillProfiles reads this id for the opening body-row curve.
    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string ComboMoveId = "COMBO_MOVE";
    internal const string RepairMoveId = "REPAIR_MOVE";

    internal const int ComboHits = 8;

    // Original HealDp 600 over the original maximum DP 2400.
    internal const decimal RepairDpRatio = 600m / 2400m;

    internal static readonly HbrgoMoveTables Tables = new(
        "CrestHopperSmall",
        Table("CrestHopperSmall",
            Step(AttackSkill),
            Pick((AttackSkill, 50), (ComboSkill, 50)),
            Step(AttackSkill),
            Step(ComboSkill),
            Step(ComboSkill)),
        Table("CrestHopperSmall_break",
            Step(AttackSkill),
            Pick((AttackSkill, 40), (ComboSkill, 60)),
            Step(AttackSkill),
            Step(RepairSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(ComboSkill, ComboMoveId, HbrgoMoveTarget.Single),
        new(RepairSkill, RepairMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            13,
            12);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            17,
            16);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            3);

    private int ComboDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            1,
            1);

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
            RepairMoveId,
            RepairMove,
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

    private async Task RepairMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(RepairDpRatio);
    }
}
