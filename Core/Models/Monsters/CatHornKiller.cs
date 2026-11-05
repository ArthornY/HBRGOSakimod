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

// HBR 猫角兽γ (CatHornKiller). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class CatHornKiller : HbrgoStaticMonster
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

    private int HornRushDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int HornRushHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            2,
            2);

    private int SweepDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            15,
            14);

    internal const int RoarStrength = 3;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState hornRush = new(
            "HORN_RUSH_MOVE",
            HornRushMove,
            new MultiAttackIntent(HornRushDamage, () => HornRushHits));
        MoveState sweep = new(
            "SWEEP_MOVE",
            SweepMove,
            new SingleAttackIntent(() => SweepDamage));
        MoveState roar = new(
            "ROAR_MOVE",
            RoarMove,
            new BuffIntent());
        return ModMonsterMoveStateMachines.Cycle(
            hornRush,
            sweep,
            roar);
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
