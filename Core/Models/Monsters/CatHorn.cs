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

// HBR 猫角兽 (CatHornSmall/Default). Two-horned rushes, a sweeping blow and
// a roar that strengthens the following round.
public sealed class CatHorn : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            32,
            30);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            36,
            34);

    private int HornRushDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            4);

    private int SweepDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            9);

    internal const int HornRushHits = 2;
    internal const int RoarStrength = 2;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState hornRush = new(
            "HORN_RUSH_MOVE",
            HornRushMove,
            new MultiAttackIntent(HornRushDamage, HornRushHits));
        MoveState sweep = new(
            "SWEEP_MOVE",
            SweepMove,
            new SingleAttackIntent(() => SweepDamage));
        MoveState roar = new(
            "ROAR_MOVE",
            RoarMove,
            new BuffIntent());
        return ModMonsterMoveStateMachines.Cycle(hornRush, sweep, roar);
    }

    private Task HornRushMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(HornRushDamage, HornRushHits);
    }

    private Task SweepMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(SweepDamage);
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
    }
}
