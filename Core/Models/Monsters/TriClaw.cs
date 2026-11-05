using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 三爪镖 (TriClawSmall). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class TriClaw : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            62,
            58);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            66,
            62);

    private int TripleClawDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int TripleClawHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            3);

    private int DartDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            18,
            16);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState tripleClaw = new(
            "TRIPLE_CLAW_MOVE",
            TripleClawMove,
            new MultiAttackIntent(TripleClawDamage, () => TripleClawHits));
        MoveState dart = new(
            "DART_MOVE",
            DartMove,
            new SingleAttackIntent(() => DartDamage));
        return ModMonsterMoveStateMachines.Cycle(
            tripleClaw,
            dart);
    }

    private Task TripleClawMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(TripleClawDamage, TripleClawHits);
    }

    private Task DartMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(DartDamage);
    }
}
