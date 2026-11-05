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
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 巨肌魔臂 (MolArmsMys). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class MolArms : HbrgoStaticMonster
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

    private int HeavyPunchDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            19,
            17);

    private int DoubleSmashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

    private int DoubleSmashHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            2,
            2);

    internal const int RoarStrength = 3;

    private int RoarBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            12);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState heavyPunch = new(
            "HEAVY_PUNCH_MOVE",
            HeavyPunchMove,
            new SingleAttackIntent(() => HeavyPunchDamage));
        MoveState doubleSmash = new(
            "DOUBLE_SMASH_MOVE",
            DoubleSmashMove,
            new MultiAttackIntent(DoubleSmashDamage, () => DoubleSmashHits));
        MoveState roar = new(
            "ROAR_MOVE",
            RoarMove,
            new BuffIntent(),
            new DefendIntent());
        return ModMonsterMoveStateMachines.Cycle(
            heavyPunch,
            doubleSmash,
            roar);
    }

    private Task HeavyPunchMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(HeavyPunchDamage);
    }

    private Task DoubleSmashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(DoubleSmashDamage, DoubleSmashHits);
    }

    private async Task RoarMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            RoarStrength,
            Creature,
            null);
        await CreatureCmd.GainBlock(
            Creature,
            RoarBlock,
            ValueProp.Move,
            null);
    }
}
