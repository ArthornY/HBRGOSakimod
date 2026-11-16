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

// HBR 治愈跳虫: act one copies CrestHopperHealVirtual (MC02_10_Battle01/02,
// MC02_11_Battle01, MC02_12_Battle02), whose Repair β heals every ally; the
// act-two training fights (MC03ExpTraining01Virtual_S01, _R02, _R05) set
// CrestHopperHealVirtual_MC03, which attacks while its shell holds and casts
// Repair Ω in Break. The 11-05 block is gone. It did not attack before, so
// its attacks take SmallHopper's numbers: the same original skills on a
// hopper of the same HP class (docs/hbr-act-rosters.md, fourth batch).
public sealed class HealHopper : HbrgoMoveTableMonster
{
    internal const int DefaultVariant = 0;
    internal const int OmegaVariant = 1;

    internal const string AttackSkill = "CrestHopper_DefaultAttack";
    internal const string ComboSkill = "CrestHopper_SkillAttack";
    internal const string RepairBetaSkill = "CrestHopper_HealAll1";
    internal const string RepairOmegaSkill = "CrestHopper_HealAll2";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string ComboMoveId = "COMBO_MOVE";
    internal const string RepairBetaMoveId = "REPAIR_BETA_MOVE";
    internal const string RepairOmegaMoveId = "REPAIR_OMEGA_MOVE";

    internal const int ComboHits = 8;

    // Original HealDp to every ally over the healer's original maximum DP:
    // Repair β 400 of 4000, Repair Ω 10000 of 2000 (a full heal).
    internal const decimal RepairBetaDpRatio = 400m / 4000m;
    internal const decimal RepairOmegaDpRatio = 10000m / 2000m;

    internal static readonly HbrgoMoveTables DefaultTables = new(
        "CrestHopperHealVirtual",
        Table("CrestHopperHeal",
            Step(AttackSkill),
            Pick((AttackSkill, 50), (ComboSkill, 50)),
            Step(RepairBetaSkill)),
        Table("CrestHopperHeal_break",
            Pick((AttackSkill, 40), (ComboSkill, 60)),
            Step(RepairBetaSkill)));

    internal static readonly HbrgoMoveTables OmegaTables = new(
        "CrestHopperHealVirtual_MC03",
        Table("CrestHopperHeal2",
            Step(AttackSkill),
            Pick((AttackSkill, 50), (ComboSkill, 50))),
        Table("CrestHopperHeal2_break",
            Step(RepairOmegaSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(ComboSkill, ComboMoveId, HbrgoMoveTarget.Single),
        new(RepairBetaSkill, RepairBetaMoveId, HbrgoMoveTarget.AllyAll),
        new(RepairOmegaSkill, RepairOmegaMoveId, HbrgoMoveTarget.AllyAll),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            15,
            14);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            19,
            18);

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

    internal override int MoveTableVariantCount => 2;

    internal override HbrgoMoveTables GetMoveTables(int variant) =>
        variant == OmegaVariant ? OmegaTables : DefaultTables;

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
            RepairBetaMoveId,
            RepairBetaMove,
            new HealIntent());
        yield return new MoveState(
            RepairOmegaMoveId,
            RepairOmegaMove,
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

    private async Task RepairBetaMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreAlliesDpRatio(RepairBetaDpRatio);
    }

    private async Task RepairOmegaMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreAlliesDpRatio(RepairOmegaDpRatio);
    }
}
