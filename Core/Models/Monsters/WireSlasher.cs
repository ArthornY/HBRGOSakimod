using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 旋轮 (WireSlasherDefault). A spinning wheel that alternates a single
// slash with a burst of launched wires.
public sealed class WireSlasher : HbrgoStaticMonster
{
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

    private int SpinSlashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int WireLaunchHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            3);

    internal const int WireLaunchDamage = 2;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState spinSlash = new(
            "SPIN_SLASH_MOVE",
            SpinSlashMove,
            new SingleAttackIntent(() => SpinSlashDamage));
        MoveState wireLaunch = new(
            "WIRE_LAUNCH_MOVE",
            WireLaunchMove,
            new MultiAttackIntent(WireLaunchDamage, () => WireLaunchHits));
        return ModMonsterMoveStateMachines.Cycle(spinSlash, wireLaunch);
    }

    private Task SpinSlashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(SpinSlashDamage);
    }

    private Task WireLaunchMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(WireLaunchDamage, WireLaunchHits);
    }
}
