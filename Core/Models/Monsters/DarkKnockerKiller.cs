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

// HBR 杀手重锤 (DarkKnockerKiller). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class DarkKnockerKiller : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            68,
            64);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            72,
            68);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            11,
            10);

    internal const int ChargeStrength = 3;

    private int HeavyAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            20,
            18);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState attack = new(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
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
            charge,
            heavyAttack);
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private async Task ChargeMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            ChargeStrength,
            Creature,
            null);
    }

    private Task HeavyAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(HeavyAttackDamage);
    }
}
