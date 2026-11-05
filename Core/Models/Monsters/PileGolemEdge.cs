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

// HBR 石柱像 (PileGolemEdgeSmall), elite. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class PileGolemEdge : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            180,
            170);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            186,
            176);

    private int RockFistDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16,
            14);

    internal const int HardenStrength = 2;

    private int HardenBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            24,
            20);

    private int AvalancheDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    private int AvalancheHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            3);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState rockFist = new(
            "ROCK_FIST_MOVE",
            RockFistMove,
            new SingleAttackIntent(() => RockFistDamage));
        MoveState harden = new(
            "HARDEN_MOVE",
            HardenMove,
            new BuffIntent(),
            new DefendIntent());
        MoveState avalanche = new(
            "AVALANCHE_MOVE",
            AvalancheMove,
            new MultiAttackIntent(AvalancheDamage, () => AvalancheHits));
        return ModMonsterMoveStateMachines.Cycle(
            rockFist,
            harden,
            avalanche);
    }

    private Task RockFistMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(RockFistDamage);
    }

    private async Task HardenMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            HardenStrength,
            Creature,
            null);
        await CreatureCmd.GainBlock(
            Creature,
            HardenBlock,
            ValueProp.Move,
            null);
    }

    private Task AvalancheMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AvalancheDamage, AvalancheHits);
    }
}
