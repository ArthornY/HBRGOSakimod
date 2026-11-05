using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 线圈巨轮γ (TargetWireFrameKiller), elite. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class TargetWireFrameKiller : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            96,
            90);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            100,
            94);

    private int LockOnBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            12);

    private int CoilShotDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            22,
            20);

    private int ScatterDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int ScatterHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            3);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState lockOn = new(
            "LOCK_ON_MOVE",
            LockOnMove,
            new DefendIntent());
        MoveState coilShot = new(
            "COIL_SHOT_MOVE",
            CoilShotMove,
            new SingleAttackIntent(() => CoilShotDamage));
        MoveState scatter = new(
            "SCATTER_MOVE",
            ScatterMove,
            new MultiAttackIntent(ScatterDamage, () => ScatterHits));
        return ModMonsterMoveStateMachines.Cycle(
            lockOn,
            coilShot,
            scatter);
    }

    private async Task LockOnMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await CreatureCmd.GainBlock(
            Creature,
            LockOnBlock,
            ValueProp.Move,
            null);
    }

    private Task CoilShotMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(CoilShotDamage);
    }

    private Task ScatterMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(ScatterDamage, ScatterHits);
    }
}
