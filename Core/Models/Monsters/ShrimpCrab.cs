using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 磐石异蟹 (ShrimpCrabDefault), elite pair. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class ShrimpCrab : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            128,
            120);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            134,
            126);

    private int PincerDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            18,
            16);

    private int HardShellBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            20,
            16);

    // Fixed DP recovery in native units; the HBR resource boundary scales it.
    internal const decimal HardShellDp = 20m;

    private int CrushDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    private int CrushHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            3);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState pincer = new(
            "PINCER_MOVE",
            PincerMove,
            new SingleAttackIntent(() => PincerDamage));
        MoveState hardShell = new(
            "HARD_SHELL_MOVE",
            HardShellMove,
            new DefendIntent(),
            new HealIntent());
        MoveState crush = new(
            "CRUSH_MOVE",
            CrushMove,
            new MultiAttackIntent(CrushDamage, () => CrushHits));
        return ModMonsterMoveStateMachines.Cycle(
            pincer,
            hardShell,
            crush);
    }

    private Task PincerMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(PincerDamage);
    }

    private async Task HardShellMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        Creature.GetPower<HighDimensionalShellPower>()?
            .Restore(HbrResourceValueHelper.Fixed(Creature, HardShellDp));
        await CreatureCmd.GainBlock(
            Creature,
            HardShellBlock,
            ValueProp.Move,
            null);
    }

    private Task CrushMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(CrushDamage, CrushHits);
    }
}
