using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
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

public sealed class AbyssKnocker : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            94,
            90);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            98,
            94);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int RageWaveDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            12);

    private int RageWaveDpBonus =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            5);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState attack = new(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        MoveState multiAttack = new(
            "MULTI_ATTACK_MOVE",
            MultiAttackMove,
            new MultiAttackIntent(AttackDamage, 2));
        MoveState recovery = new(
            "RECOVERY_MOVE",
            RecoveryMove,
            new HealIntent(),
            new DefendIntent());
        MoveState rageWave = new(
            "RAGE_WAVE_MOVE",
            RageWaveMove,
            new SingleAttackIntent(
                () => RageWaveDamage + RageWaveDpBonus));
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
        return PerformAttack(AttackDamage, 2);
    }

    private async Task RecoveryMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        Creature.GetPower<HighDimensionalShellPower>()?.Restore(10);
        await CreatureCmd.GainBlock(
            Creature,
            10,
            ValueProp.Move,
            null);
    }

    private async Task RageWaveMove(IReadOnlyList<Creature> targets)
    {
        List<Creature> targetsWithDp = targets
            .Where(target => target.IsAlive &&
                HasNonZeroDp(target))
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

    private static bool HasNonZeroDp(Creature target)
    {
        HighDimensionalShellPower shell =
            target.GetPower<HighDimensionalShellPower>();
        if (shell != null && shell.CurrentDp != 0m)
        {
            return true;
        }

        DeflectorProtection protection =
            target.Player?.GetRelic<DeflectorProtection>();
        return protection != null && protection.Dp != 0;
    }
}
