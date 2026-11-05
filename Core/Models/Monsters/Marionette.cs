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

// HBR 提线木偶 (MarionetteDefault). A frail puppet; pulling a target's
// strings lowers its attack.
public sealed class Marionette : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string StringPullMoveId = "STRING_PULL_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            26,
            24);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            30,
            28);

    private int StringPullDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    internal const int StringPullAttackDown = 1;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState stringPull = new(
            StringPullMoveId,
            StringPullMove,
            new SingleAttackIntent(() => StringPullDamage),
            new DebuffIntent());
        MoveState attack = new(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        return ModMonsterMoveStateMachines.Cycle(stringPull, attack);
    }

    private async Task StringPullMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(StringPullDamage);
        await ApplyStringPull(targets);
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task ApplyStringPull(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyAttackDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            StringPullAttackDown,
            Creature);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id == StringPullMoveId
            ? ApplyStringPull(targets)
            : Task.CompletedTask;
    }
}
