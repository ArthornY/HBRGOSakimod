using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

public sealed class HealHopper : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            15,
            14);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            19,
            18);

    private int RepairAmount =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState repair = new(
            "REPAIR_BETA_MOVE",
            RepairBetaMove,
            new HealIntent(),
            new DefendIntent());
        return ModMonsterMoveStateMachines.SingleMoveLoop(repair);
    }

    private async Task RepairBetaMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        foreach (Creature ally in CombatState
                     .GetTeammatesOf(Creature)
                     .Where(ally => ally.IsAlive))
        {
            if (!ally.HasPower<BreakPower>())
            {
                ally.GetPower<HighDimensionalShellPower>()?
                    .Restore(RepairAmount);
            }

            await CreatureCmd.GainBlock(
                ally,
                RepairAmount,
                ValueProp.Move,
                null);
        }
    }
}
