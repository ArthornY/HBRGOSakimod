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

// HBR BarrelWalker (BarrelWalkerDefault), act-two boss. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class BarrelWalker : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            380,
            360);

    public override int MaxInitialHp => MinInitialHp;

    private int CannonDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16,
            14);

    private int VolleyDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int VolleyHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            4);

    internal const int ReloadStrength = 3;

    private int ReloadBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            18,
            15);

    private int HeavyCannonDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            34,
            30);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState cannon = new(
            "CANNON_MOVE",
            CannonMove,
            new SingleAttackIntent(() => CannonDamage));
        MoveState volley = new(
            "VOLLEY_MOVE",
            VolleyMove,
            new MultiAttackIntent(VolleyDamage, () => VolleyHits));
        MoveState reload = new(
            "RELOAD_MOVE",
            ReloadMove,
            new BuffIntent(),
            new DefendIntent());
        MoveState heavyCannon = new(
            "HEAVY_CANNON_MOVE",
            HeavyCannonMove,
            new SingleAttackIntent(() => HeavyCannonDamage));
        return ModMonsterMoveStateMachines.Cycle(
            cannon,
            volley,
            reload,
            heavyCannon);
    }

    private Task CannonMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(CannonDamage);
    }

    private Task VolleyMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(VolleyDamage, VolleyHits);
    }

    private async Task ReloadMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            ReloadStrength,
            Creature,
            null);
        await CreatureCmd.GainBlock(
            Creature,
            ReloadBlock,
            ValueProp.Move,
            null);
    }

    private Task HeavyCannonMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(HeavyCannonDamage);
    }
}
