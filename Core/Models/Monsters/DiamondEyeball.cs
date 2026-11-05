using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 钻石眼魔 (DiamondEyeballDefault), act-two boss. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class DiamondEyeball : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string GazeMoveId = "GAZE_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            340,
            320);

    public override int MaxInitialHp => MinInitialHp;

    private int GazeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            11,
            10);

    internal const int GazeDefenseDown = 2;

    private int PrismRayDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            27,
            24);

    private int FacetShieldBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            24,
            20);

    // Fixed DP recovery in native units; the HBR resource boundary scales it.
    internal const decimal FacetShieldDp = 30m;

    private int BeamVolleyDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int BeamVolleyHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState gaze = new(
            GazeMoveId,
            GazeMove,
            new SingleAttackIntent(() => GazeDamage),
            new DebuffIntent());
        MoveState prismRay = new(
            "PRISM_RAY_MOVE",
            PrismRayMove,
            new SingleAttackIntent(() => PrismRayDamage));
        MoveState facetShield = new(
            "FACET_SHIELD_MOVE",
            FacetShieldMove,
            new DefendIntent(),
            new HealIntent());
        MoveState beamVolley = new(
            "BEAM_VOLLEY_MOVE",
            BeamVolleyMove,
            new MultiAttackIntent(BeamVolleyDamage, () => BeamVolleyHits));
        return ModMonsterMoveStateMachines.Cycle(
            gaze,
            prismRay,
            facetShield,
            beamVolley);
    }

    private async Task GazeMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(GazeDamage);
        await ApplyGaze(targets);
    }

    private Task ApplyGaze(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            GazeDefenseDown,
            Creature);
    }

    private Task PrismRayMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(PrismRayDamage);
    }

    private async Task FacetShieldMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        Creature.GetPower<HighDimensionalShellPower>()?
            .Restore(HbrResourceValueHelper.Fixed(Creature, FacetShieldDp));
        await CreatureCmd.GainBlock(
            Creature,
            FacetShieldBlock,
            ValueProp.Move,
            null);
    }

    private Task BeamVolleyMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(BeamVolleyDamage, BeamVolleyHits);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id switch
        {
            GazeMoveId => ApplyGaze(targets),
            _ => Task.CompletedTask
        };
    }
}
