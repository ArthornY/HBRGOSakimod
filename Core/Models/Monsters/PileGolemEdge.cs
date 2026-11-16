using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 石柱像, act-two elite: original PileGolemEdgeSmall
// (MC04ExpMtKumotori01_S01). Its signature is Attack Up, then Confusion
// Spike at double damage (docs/hbr-monster-mechanics-proposal.md).
public sealed class PileGolemEdge : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string AvalancheSkill = "PileGolemEdge_AttackAll01";
    internal const string TremorSkill = "PileGolemEdge_AttackAll02";
    internal const string ConfusionSpikeSkill = "PileGolemEdge_AttackSingle01";
    internal const string AttackUpSkill = "PileGolemEdge_AttackUp";
    internal const string HealSkill = "PileGolemEdge_Heal";

    internal const string AvalancheMoveId = "AVALANCHE_MOVE";
    internal const string TremorMoveId = "TREMOR_MOVE";
    internal const string ConfusionSpikeMoveId = "CONFUSION_SPIKE_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 100;

    // Original HealDp 15000 over the original maximum DP 14000: a full heal.
    internal const decimal HealDpRatio = 15000m / 14000m;

    private static HbrgoMoveStep EitherAll() =>
        Pick((AvalancheSkill, 50), (TremorSkill, 50));

    internal static readonly HbrgoMoveTables Tables = new(
        "PileGolemEdgeSmall",
        Table("PileGolemEdgesmall",
            Pick((ConfusionSpikeSkill, 50), (AvalancheSkill, 50)),
            Step(AttackUpSkill),
            Step(ConfusionSpikeSkill),
            EitherAll()),
        Table("PileGolemEdgesmall_break",
            Step(AttackUpSkill),
            Step(ConfusionSpikeSkill),
            EitherAll(),
            Step(AttackUpSkill),
            Step(ConfusionSpikeSkill),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AvalancheSkill, AvalancheMoveId, HbrgoMoveTarget.All),
        new(TremorSkill, TremorMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(ConfusionSpikeSkill, ConfusionSpikeMoveId, HbrgoMoveTarget.Single),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            180,
            170);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            186,
            176);

    private int AvalancheDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            11,
            10);

    private int TremorDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            13,
            11);

    private int ConfusionSpikeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            12);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            AvalancheMoveId,
            AvalancheMove,
            new SingleAttackIntent(() => AvalancheDamage));
        yield return new MoveState(
            TremorMoveId,
            TremorMove,
            new SingleAttackIntent(() => TremorDamage));
        yield return new MoveState(
            ConfusionSpikeMoveId,
            ConfusionSpikeMove,
            new SingleAttackIntent(() => ConfusionSpikeDamage),
            new DebuffIntent());
        yield return new MoveState(
            AttackUpMoveId,
            AttackUpMove,
            new BuffIntent());
        yield return new MoveState(
            HealMoveId,
            HealMove,
            new HealIntent());
    }

    // Original all-unit attacks: every player-side unit, summons included.
    private Task AvalancheMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(AvalancheDamage, 1, HbrgoAttackTargets.AllUnits);
    }

    private Task TremorMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(TremorDamage, 1, HbrgoAttackTargets.AllUnits);
    }

    // Confusion in HBR attaches on contact; legacy still requires passing block.
    private async Task ConfusionSpikeMove(IReadOnlyList<Creature> targets)
    {
        HbrgoAttackHits hits = await PerformTrackedAttack(ConfusionSpikeDamage, 1, targets);
        await HbrEnemyEffects.ApplyConfusion(HbrEnemyEffects.ControlTargets(hits, Creature), Creature);
    }

    private async Task AttackUpMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await HbrEnemyEffects.ApplyTimedBuff<HbrAttackUpPower>(Creature, AttackUpPercent);
    }

    private async Task HealMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(HealDpRatio);
    }

    Task IHbrgoPhantomAttackResults.AfterPhantomAttack(
        MoveState move,
        IReadOnlyList<Creature> attackedTargets,
        IReadOnlyList<Creature> passedTargets,
        decimal passedDamage)
    {
        return move?.Id == ConfusionSpikeMoveId
            ? HbrEnemyEffects.ApplyConfusion(passedTargets, Creature)
            : Task.CompletedTask;
    }
}
