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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 三柱守卫 (PillarGuardianDefault), act-three boss. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class PillarGuardian : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            490,
            460);

    public override int MaxInitialHp => MinInitialHp;

    private int PillarStrikeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            23,
            20);

    internal const int ResonanceStrength = 4;
    internal const int ResonanceDefenseDown = 2;

    private int QuakeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

    private int QuakeHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            4);

    private int GuardBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            36,
            30);

    // Fixed DP recovery in native units; the HBR resource boundary scales it.
    internal const decimal GuardDp = 50m;

    private int CollapseDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            45,
            40);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState pillarStrike = new(
            "PILLAR_STRIKE_MOVE",
            PillarStrikeMove,
            new SingleAttackIntent(() => PillarStrikeDamage));
        MoveState resonance = new(
            "RESONANCE_MOVE",
            ResonanceMove,
            new BuffIntent(),
            new DebuffIntent());
        MoveState quake = new(
            "QUAKE_MOVE",
            QuakeMove,
            new MultiAttackIntent(QuakeDamage, () => QuakeHits));
        MoveState guard = new(
            "GUARD_MOVE",
            GuardMove,
            new DefendIntent(),
            new HealIntent());
        MoveState collapse = new(
            "COLLAPSE_MOVE",
            CollapseMove,
            new SingleAttackIntent(() => CollapseDamage));
        return ModMonsterMoveStateMachines.Cycle(
            pillarStrike,
            resonance,
            quake,
            guard,
            collapse);
    }

    private Task PillarStrikeMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(PillarStrikeDamage);
    }

    private async Task ResonanceMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            ResonanceStrength,
            Creature,
            null);
        await HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            ResonanceDefenseDown,
            Creature);
    }

    private Task QuakeMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(QuakeDamage, QuakeHits);
    }

    private async Task GuardMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        Creature.GetPower<HighDimensionalShellPower>()?
            .Restore(HbrResourceValueHelper.Fixed(Creature, GuardDp));
        await CreatureCmd.GainBlock(
            Creature,
            GuardBlock,
            ValueProp.Move,
            null);
    }

    private Task CollapseMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(CollapseDamage);
    }
}
