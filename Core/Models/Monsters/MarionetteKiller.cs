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

// HBR 死亡玩偶 (MarionetteKiller). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class MarionetteKiller : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string CursePullMoveId = "CURSE_PULL_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            47,
            44);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            51,
            48);

    private int CursePullDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    internal const int CursePullAttackDown = 1;

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            11,
            10);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState cursePull = new(
            CursePullMoveId,
            CursePullMove,
            new SingleAttackIntent(() => CursePullDamage),
            new DebuffIntent());
        MoveState attack = new(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        return ModMonsterMoveStateMachines.Cycle(
            cursePull,
            attack);
    }

    private async Task CursePullMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(CursePullDamage);
        await ApplyCursePull(targets);
    }

    private Task ApplyCursePull(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyAttackDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            CursePullAttackDown,
            Creature);
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id switch
        {
            CursePullMoveId => ApplyCursePull(targets),
            _ => Task.CompletedTask
        };
    }
}
