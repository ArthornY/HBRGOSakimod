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

// HBR 章鱼尾 (OctopusTailDefault). Its tentacles wrap a target and lower
// its defense before the tail whips.
public sealed class OctopusTail : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string TentacleWrapMoveId = "TENTACLE_WRAP_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            28,
            26);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            32,
            30);

    private int TentacleWrapDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            4);

    private int TailWhipDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            9);

    internal const int TentacleWrapDefenseDown = 1;

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
        return ModMonsterMoveStateMachines.Cycle(tentacleWrap, tailWhip);
    }

    private async Task TentacleWrapMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(TentacleWrapDamage);
        await ApplyTentacleWrap(targets);
    }

    private Task TailWhipMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(TailWhipDamage);
    }

    private Task ApplyTentacleWrap(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            TentacleWrapDefenseDown,
            Creature);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id == TentacleWrapMoveId
            ? ApplyTentacleWrap(targets)
            : Task.CompletedTask;
    }
}
