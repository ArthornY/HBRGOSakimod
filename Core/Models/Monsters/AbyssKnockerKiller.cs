using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 深渊重锤S (AbyssKnockerKiller), elite. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class AbyssKnockerKiller : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            160,
            150);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            166,
            156);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            13,
            12);

    private int MultiAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

    private int MultiAttackHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            2,
            2);

    private int RecoveryBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16,
            14);

    // Fixed DP recovery in native units; the HBR resource boundary scales it.
    internal const decimal RecoveryDp = 20m;

    private int RageWaveDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            25,
            22);

    private int RageWaveDpBonus =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            8);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState attack = new(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        MoveState multiAttack = new(
            "MULTI_ATTACK_MOVE",
            MultiAttackMove,
            new MultiAttackIntent(MultiAttackDamage, () => MultiAttackHits));
        MoveState recovery = new(
            "RECOVERY_MOVE",
            RecoveryMove,
            new DefendIntent(),
            new HealIntent());
        MoveState rageWave = new(
            "RAGE_WAVE_MOVE",
            RageWaveMove,
            new SingleAttackIntent(() => RageWaveDamage + RageWaveDpBonus));
        return ModMonsterMoveStateMachines.Cycle(
            attack,
            multiAttack,
            recovery,
            rageWave);
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task MultiAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(MultiAttackDamage, MultiAttackHits);
    }

    private async Task RecoveryMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        Creature.GetPower<HighDimensionalShellPower>()?
            .Restore(HbrResourceValueHelper.Fixed(Creature, RecoveryDp));
        await CreatureCmd.GainBlock(
            Creature,
            RecoveryBlock,
            ValueProp.Move,
            null);
    }

    private async Task RageWaveMove(IReadOnlyList<Creature> targets)
    {
        List<Creature> targetsWithDp = targets
            .Where(target => target.IsAlive && HasActiveDp(target))
            .ToList();

        await PerformAttack(RageWaveDamage);
        if (targetsWithDp.Count == 0)
        {
            return;
        }

        await CreatureCommandCompatibility.Damage(
            new ThrowingPlayerChoiceContext(),
            targetsWithDp.Where(target => target.IsAlive),
            RageWaveDpBonus,
            ValueProp.Move,
            Creature);
    }
}
