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

// HBR 线圈巨轮 (TargetWireFrameSmall). Locks on behind its coils, then fires.
public sealed class TargetWireFrame : HbrgoStaticMonster
{
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

    private int LockOnBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            6);

    private int CoilShotDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            12,
            11);

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
        return ModMonsterMoveStateMachines.Cycle(lockOn, coilShot);
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
}
