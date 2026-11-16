using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 嵴冠跳虫, act one: original CrestHopperDefaultVirtual (MC02_10_Battle02,
// MC02_11_Battle01, MC02_14_Battle02). The swarm fight's CrestHopperDefault_MC01
// (MC01_12_Battle01event) has the same tables with DP 4400; one set is used
// (docs/hbr-act-rosters.md, fourth batch).
public sealed class CrestHopper : HbrgoMoveTableMonster
{
    internal const string AttackSkill = "CrestHopper_DefaultAttack";
    internal const string ComboSkill = "CrestHopper_SkillAttack";
    internal const string RepairSkill = "CrestHopper_Heal1";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string ComboMoveId = "COMBO_MOVE";
    internal const string RepairMoveId = "REPAIR_MOVE";

    internal const int ComboHits = 8;

    // Original HealDp 600 over the original maximum DP 4000.
    internal const decimal RepairDpRatio = 600m / 4000m;

    internal static readonly HbrgoMoveTables Tables = new(
        "CrestHopperDefaultVirtual",
        Table("CrestHopperDefault",
            Step(AttackSkill),
            Step(ComboSkill),
            Pick((AttackSkill, 50), (ComboSkill, 50)),
            Step(AttackSkill),
            Step(ComboSkill),
            Step(ComboSkill)),
        Table("CrestHopperDefault_break",
            Step(AttackSkill),
            Pick((AttackSkill, 40), (ComboSkill, 60)),
            Step(ComboSkill),
            Step(RepairSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(ComboSkill, ComboMoveId, HbrgoMoveTarget.Single),
        new(RepairSkill, RepairMoveId, HbrgoMoveTarget.Self),
    };

    private bool _isSwarmMember;

    // Set by CrestHopperSwarmNormal before the creature is created, so the
    // native HP roll reads the lower range. Three full CrestHoppers would far
    // exceed an ordinary act-one fight's total HP.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsSwarmMember
    {
        get => _isSwarmMember;
        internal set
        {
            AssertMutable();
            _isSwarmMember = value;
        }
    }

    public override int MinInitialHp => IsSwarmMember
        ? AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            28,
            26)
        : AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            48,
            46);

    public override int MaxInitialHp => IsSwarmMember
        ? AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            32,
            30)
        : AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            52,
            50);

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
