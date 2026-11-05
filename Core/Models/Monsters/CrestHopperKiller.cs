using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 杀手跳虫 (CrestHopperKiller). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class CrestHopperKiller : HbrgoStaticMonster
{
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
            7);

    private int ComboDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            3);

    private int ComboHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            3);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState attack = new(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        MoveState combo = new(
            "COMBO_MOVE",
            ComboMove,
            new MultiAttackIntent(ComboDamage, () => ComboHits));
        return ModMonsterMoveStateMachines.Cycle(
            attack,
            combo);
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task ComboMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(ComboDamage, ComboHits);
    }
}
