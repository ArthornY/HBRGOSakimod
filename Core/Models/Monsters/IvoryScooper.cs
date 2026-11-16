using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 象牙异蛛, act-one boss: original IvoryScooperDefaultVirtual
// (MC02_21_Battle02). It has no heal skill: Energy Drain restores DP from the
// damage it deals, which Break (no shell) blocks
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class IvoryScooper : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string ScoopSkill = "IvoryScooper_Attack01";
    internal const string EnergyDrainSkill = "IvoryScooper_Attack02";
    internal const string DestroyBashSkill = "IvoryScooper_AttackAll01";

    internal const string ScoopMoveId = "SCOOP_MOVE";
    internal const string EnergyDrainMoveId = "ENERGY_DRAIN_MOVE";
    internal const string DestroyBashMoveId = "DESTROY_BASH_MOVE";

    internal const int EnergyDrainHits = 3;

    private static HbrgoMoveStep ScoopOrBash() =>
        Pick((ScoopSkill, 50), (DestroyBashSkill, 50));

    internal static readonly HbrgoMoveTables Tables = new(
        "IvoryScooperDefaultVirtual",
        Table("IvoryScooperDefault",
            Step(ScoopSkill),
            Step(EnergyDrainSkill),
            ScoopOrBash(),
            ScoopOrBash(),
            Step(EnergyDrainSkill),
            Step(DestroyBashSkill)),
        Table("IvoryScooperDefault_break",
            Step(DestroyBashSkill),
            ScoopOrBash(),
            Pick((EnergyDrainSkill, 50), (DestroyBashSkill, 50)),
            ScoopOrBash(),
            Step(EnergyDrainSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(ScoopSkill, ScoopMoveId, HbrgoMoveTarget.Single),
        new(EnergyDrainSkill, EnergyDrainMoveId, HbrgoMoveTarget.Single),
        new(DestroyBashSkill, DestroyBashMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            192,
            180);

    public override int MaxInitialHp => MinInitialHp;

    private int ScoopDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            7);

    private int EnergyDrainDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            3);

    private int DestroyBashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            9);
    private int PoisonBurstPercent => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 15, 10);
    private bool HasPoisonBurst => BoundCreature is { } creature &&
        EnemyStatScalingHelper.IsEnemy(creature) && HbrBattleStatsHelper.IsEnabled(creature);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            ScoopMoveId,
            ScoopMove,
            new SingleAttackIntent(() => ScoopDamage));
        yield return new MoveState(
            EnergyDrainMoveId,
            EnergyDrainMove,
            new MultiAttackIntent(EnergyDrainDamage, () => EnergyDrainHits),
            new HealIntent());
        yield return new MoveState(
            DestroyBashMoveId,
            DestroyBashMove,
            // Unbound machines enumerate every potential asset for preloading.
            (BoundCreature == null || HasPoisonBurst) ? new AbstractIntent[] { new SingleAttackIntent(() => DestroyBashDamage), new DebuffIntent() }
                : new AbstractIntent[] { new SingleAttackIntent(() => DestroyBashDamage) });
    }

    private Task ScoopMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(ScoopDamage);
    }

    // Original HealDpByDamage: DP back from the DP and HP the hits took.
    private async Task EnergyDrainMove(IReadOnlyList<Creature> targets)
    {
        HbrgoAttackHits hits = await PerformTrackedAttack(EnergyDrainDamage, EnergyDrainHits, targets);
        HbrEnemyEffects.AbsorbDp(Creature, hits.PassedDamage);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private async Task DestroyBashMove(IReadOnlyList<Creature> targets)
    {
        if (!HasPoisonBurst)
        { await PerformAttackOn(DestroyBashDamage, 1, HbrgoAttackTargets.AllUnits); return; }
        HbrgoAttackHits hits = await PerformTrackedAttack(DestroyBashDamage, 1, targets, HbrgoAttackTargets.AllUnits);
        await HbrEnemyEffects.ApplyContinuousDamage(hits.Units, Creature, PoisonBurstPercent, 4);
    }

    Task IHbrgoPhantomAttackResults.AfterPhantomAttack(
        MoveState move,
        IReadOnlyList<Creature> attackedTargets,
        IReadOnlyList<Creature> passedTargets,
        decimal passedDamage)
    {
        if (move?.Id == EnergyDrainMoveId)
        {
            HbrEnemyEffects.AbsorbDp(Creature, passedDamage);
        }

        return Task.CompletedTask;
    }
}
