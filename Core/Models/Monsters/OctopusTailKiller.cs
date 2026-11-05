using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 章鱼尾γ (OctopusTailKiller). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class OctopusTailKiller : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string TentacleWrapMoveId = "TENTACLE_WRAP_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            49,
            46);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            53,
            50);

    private int TentacleWrapDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    internal const int TentacleWrapDefenseDown = 1;

    private int TailWhipDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            13);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState tentacleWrap = new(
            TentacleWrapMoveId,
            TentacleWrapMove,
            new SingleAttackIntent(() => TentacleWrapDamage),
            new DebuffIntent());
        MoveState tailWhip = new(
            "TAIL_WHIP_MOVE",
            TailWhipMove,
            new SingleAttackIntent(() => TailWhipDamage));
        return ModMonsterMoveStateMachines.Cycle(
            tentacleWrap,
            tailWhip);
    }

    private async Task TentacleWrapMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(TentacleWrapDamage);
        await ApplyTentacleWrap(targets);
    }

    private Task ApplyTentacleWrap(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            TentacleWrapDefenseDown,
            Creature);
    }

    private Task TailWhipMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(TailWhipDamage);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id switch
        {
            TentacleWrapMoveId => ApplyTentacleWrap(targets),
            _ => Task.CompletedTask
        };
    }
}
