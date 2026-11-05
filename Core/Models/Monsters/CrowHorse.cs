using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
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

// HBR 鸦头马 (CrowHorseDefault), an act-one boss. Tramples, sends its crow
// flock, neighs to strengthen itself and expose the party, then stampedes.
public sealed class CrowHorse : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            160,
            150);

    public override int MaxInitialHp => MinInitialHp;

    private int TrampleDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            15,
            13);

    private int CrowFlockDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            3);

    private int StampedeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            27,
            24);

    internal const int CrowFlockHits = 5;
    internal const int NeighStrength = 3;
    internal const int NeighDefenseDown = 2;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState trample = new(
            "TRAMPLE_MOVE",
            TrampleMove,
            new SingleAttackIntent(() => TrampleDamage));
        MoveState crowFlock = new(
            "CROW_FLOCK_MOVE",
            CrowFlockMove,
            new MultiAttackIntent(CrowFlockDamage, CrowFlockHits));
        MoveState neigh = new(
            "NEIGH_MOVE",
            NeighMove,
            new BuffIntent(),
            new DebuffIntent());
        MoveState stampede = new(
            "STAMPEDE_MOVE",
            StampedeMove,
            new SingleAttackIntent(() => StampedeDamage));
        return ModMonsterMoveStateMachines.Cycle(
            trample,
            crowFlock,
            neigh,
            stampede);
    }

    private Task TrampleMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(TrampleDamage);
    }

    private Task CrowFlockMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(CrowFlockDamage, CrowFlockHits);
    }

    private async Task NeighMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            NeighStrength,
            Creature,
            null);
        await HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            NeighDefenseDown,
            Creature);
    }

    private Task StampedeMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(StampedeDamage);
    }
}
