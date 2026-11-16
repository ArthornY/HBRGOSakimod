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

// HBR 提线木偶, act one: original MarionetteDefaultVirtual (MC02_10_Battle01,
// MC02_13_Battle01). The weak fight (MarionetteWeak) copies the tutorial's
// MarionetteSmallVirtual_first (MC01_01_Battle01), which has no hp table. The
// 11-05 string pull (attack down) is not in the original
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class Marionette : HbrgoMoveTableMonster
{
    internal const int DefaultVariant = 0;
    internal const int TutorialVariant = 1;

    internal const string AttackSkill = "Marionette_AttackSingle01";
    internal const string StrongAttackSkill = "Marionette_AttackSingle02";
    internal const string RecoverySkill = "Marionette_Buff01";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string StrongAttackMoveId = "STRONG_ATTACK_MOVE";
    internal const string RecoveryMoveId = "RECOVERY_MOVE";

    // Original HealDp 600 over each variant's original maximum DP.
    internal const decimal DefaultRecoveryDpRatio = 600m / 3600m;
    internal const decimal TutorialRecoveryDpRatio = 600m / 2000m;

    internal static readonly HbrgoMoveTables DefaultTables = new(
        "MarionetteDefaultVirtual",
        Table("MarionetteDefault",
            Step(AttackSkill),
            Step(StrongAttackSkill),
            Pick((AttackSkill, 50), (StrongAttackSkill, 50)),
            Step(RecoverySkill),
            Step(StrongAttackSkill),
            Step(StrongAttackSkill)),
        Table("MarionetteDefault_break",
            Step(AttackSkill),
            Pick((AttackSkill, 40), (StrongAttackSkill, 60)),
            Step(StrongAttackSkill),
            Step(RecoverySkill)));

    internal static readonly HbrgoMoveTables TutorialTables = new(
        "MarionetteSmallVirtual_first",
        Table("MarionetteSmallVirtual_first",
            Step(AttackSkill),
            Step(AttackSkill),
            Step(StrongAttackSkill),
            Step(RecoverySkill)),
        HbrgoMoveTable.None);

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(StrongAttackSkill, StrongAttackMoveId, HbrgoMoveTarget.Single),
        new(RecoverySkill, RecoveryMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            26,
            24);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            30,
            28);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int StrongAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

    internal override int MoveTableVariantCount => 2;

    internal override HbrgoMoveTables GetMoveTables(int variant) =>
        variant == TutorialVariant ? TutorialTables : DefaultTables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    private decimal RecoveryDpRatio =>
        MoveTableVariant == TutorialVariant ? TutorialRecoveryDpRatio : DefaultRecoveryDpRatio;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            AttackMoveId,
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        yield return new MoveState(
            StrongAttackMoveId,
            StrongAttackMove,
            new SingleAttackIntent(() => StrongAttackDamage));
        yield return new MoveState(
            RecoveryMoveId,
            RecoveryMove,
            new HealIntent());
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task StrongAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(StrongAttackDamage);
    }

    private async Task RecoveryMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(RecoveryDpRatio);
    }
}
