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
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 触手巨蛇 (SlimySerpentDefault), elite. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class SlimySerpent : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string ConstrictMoveId = "CONSTRICT_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            266,
            250);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            276,
            260);

    private int ConstrictDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            12);

    internal const int ConstrictAttackDown = 2;

    private int TailSlamDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            29,
            26);

    internal const int ShedStrength = 2;

    private int ShedBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            22,
            18);

    private int FangFlurryDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int FangFlurryHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState constrict = new(
            ConstrictMoveId,
            ConstrictMove,
            new SingleAttackIntent(() => ConstrictDamage),
            new DebuffIntent());
        MoveState tailSlam = new(
            "TAIL_SLAM_MOVE",
            TailSlamMove,
            new SingleAttackIntent(() => TailSlamDamage));
        MoveState shed = new(
            "SHED_MOVE",
            ShedMove,
            new BuffIntent(),
            new DefendIntent());
        MoveState fangFlurry = new(
            "FANG_FLURRY_MOVE",
            FangFlurryMove,
            new MultiAttackIntent(FangFlurryDamage, () => FangFlurryHits));
        return ModMonsterMoveStateMachines.Cycle(
            constrict,
            tailSlam,
            shed,
            fangFlurry);
    }

    private async Task ConstrictMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(ConstrictDamage);
        await ApplyConstrict(targets);
    }

    private Task ApplyConstrict(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyAttackDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            ConstrictAttackDown,
            Creature);
    }

    private Task TailSlamMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(TailSlamDamage);
    }

    private async Task ShedMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            ShedStrength,
            Creature,
            null);
        await CreatureCmd.GainBlock(
            Creature,
            ShedBlock,
            ValueProp.Move,
            null);
    }

    private Task FangFlurryMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(FangFlurryDamage, FangFlurryHits);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id switch
        {
            ConstrictMoveId => ApplyConstrict(targets),
            _ => Task.CompletedTask
        };
    }
}
