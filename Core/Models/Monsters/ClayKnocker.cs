using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class ClayKnocker : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            80,
            76);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            84,
            80);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int MultiAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int HeavyAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16,
            14);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState attack = new(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        MoveState multiAttack = new(
            "MULTI_ATTACK_MOVE",
            MultiAttackMove,
            new MultiAttackIntent(MultiAttackDamage, 2));
        MoveState charge = new(
            "CHARGE_MOVE",
            ChargeMove,
            new BuffIntent());
        MoveState heavyAttack = new(
            "HEAVY_ATTACK_MOVE",
            HeavyAttackMove,
            new SingleAttackIntent(() => HeavyAttackDamage));
        return ModMonsterMoveStateMachines.Cycle(
            attack,
            multiAttack,
            charge,
            heavyAttack);
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task MultiAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(MultiAttackDamage, 2);
    }

    private async Task ChargeMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            2,
            Creature,
            null);
    }

    private Task HeavyAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(HeavyAttackDamage);
    }
}
