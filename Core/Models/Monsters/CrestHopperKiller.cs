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

// HBR 杀手跳虫, act two: original CrestHopperKiller2 (the Mt. Kinpu fights; the
// training fights' CrestHopperKillerVirtual has the same tables)
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class CrestHopperKiller : HbrgoMoveTableMonster
{
    internal const string AttackSkill = "CrestHopper_DefaultAttack";
    internal const string ComboSkill = "CrestHopper_SkillAttack";
    internal const string RepairSkill = "CrestHopper_Heal1";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string ComboMoveId = "COMBO_MOVE";
    internal const string RepairMoveId = "REPAIR_MOVE";

    internal const int ComboHits = 8;

    // Original HealDp 600 over the original maximum DP 2400.
    internal const decimal RepairDpRatio = 600m / 2400m;

    internal static readonly HbrgoMoveTables Tables = new(
        "CrestHopperKiller2",
        Table("CrestHopperKiller",
            Step(AttackSkill),
            Step(ComboSkill),
            Pick((AttackSkill, 50), (ComboSkill, 50)),
            Step(ComboSkill),
            Step(AttackSkill),
            Step(ComboSkill),
            Step(ComboSkill)),
        Table("CrestHopperKiller_break",
            Step(ComboSkill),
            Pick((AttackSkill, 40), (ComboSkill, 60)),
            Step(ComboSkill),
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
            51,
            48);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            55,
            52);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            6);

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
