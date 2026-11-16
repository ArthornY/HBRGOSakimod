using System.Collections.Generic;
using System.Linq;
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

// HBR 深渊重锤S, act-two elite: original AbyssKnockerKillerVirtual_MC03
// (MC03_18_Battle01). Rage Wave is the original all-unit skill; the 11-05
// bonus damage against targets with DP is gone, as the original's DP rate is
// 1.0 (docs/hbr-monster-mechanics-proposal.md). Act one's AbyssKnocker copies
// AbyssKnockerDefaultVirtual_MC02 (fourth batch).
public sealed class AbyssKnockerKiller : HbrgoMoveTableMonster
{
    internal const string AttackSkill = "AbyssKnocker_Attack01";
    internal const string MultiAttackSkill = "AbyssKnocker_Attack02";
    internal const string RageWaveSkill = "AbyssKnocker_AttackAll01";
    internal const string RecoverySkill = "AbyssKnocker_Buff01";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string MultiAttackMoveId = "MULTI_ATTACK_MOVE";
    internal const string RageWaveMoveId = "RAGE_WAVE_MOVE";
    internal const string RecoveryMoveId = "RECOVERY_MOVE";

    internal const int MultiAttackHits = 2;

    // Original HealDp 3000 over the original maximum DP 11000.
    internal const decimal RecoveryDpRatio = 3000m / 11000m;

    internal static readonly HbrgoMoveTables Tables = new(
        "AbyssKnockerKillerVirtual_MC03",
        Table("AbyssKnockerKiller",
            Step(RageWaveSkill),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
            Step(RageWaveSkill),
            Step(RageWaveSkill),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
            Step(RecoverySkill)),
        Table("AbyssKnockerKiller_break",
            Step(RageWaveSkill),
            Step(RageWaveSkill),
            Pick((AttackSkill, 60), (MultiAttackSkill, 40)),
            Step(RageWaveSkill),
            Pick((AttackSkill, 40), (MultiAttackSkill, 60)),
            Step(RecoverySkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(MultiAttackSkill, MultiAttackMoveId, HbrgoMoveTarget.Single),
        new(RageWaveSkill, RageWaveMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(RecoverySkill, RecoveryMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            160,
            150);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            166,
            156);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            15,
            13);

    private int MultiAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

    private int RageWaveDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            17,
            15);
    private int DisasterRoarCalamity => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 30, 20);
    private bool HasDisasterRoar => BoundCreature is { } creature &&
        EnemyStatScalingHelper.IsEnemy(creature) && HbrBattleStatsHelper.IsEnabled(creature);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            AttackMoveId,
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        yield return new MoveState(
            MultiAttackMoveId,
            MultiAttackMove,
            new MultiAttackIntent(MultiAttackDamage, () => MultiAttackHits));
        yield return new MoveState(
            RageWaveMoveId,
            RageWaveMove,
            // Unbound machines enumerate every potential asset for preloading.
            (BoundCreature == null || HasDisasterRoar) ? new AbstractIntent[] { new SingleAttackIntent(() => RageWaveDamage), new DebuffIntent() }
                : new AbstractIntent[] { new SingleAttackIntent(() => RageWaveDamage) });
        yield return new MoveState(
            RecoveryMoveId,
            RecoveryMove,
            new HealIntent());
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task MultiAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(MultiAttackDamage, MultiAttackHits);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private async Task RageWaveMove(IReadOnlyList<Creature> targets)
    {
        if (!HasDisasterRoar)
        { await PerformAttackOn(RageWaveDamage, 1, HbrgoAttackTargets.AllUnits); return; }
        HbrgoAttackHits hits = await PerformTrackedAttack(RageWaveDamage, 1, targets, HbrgoAttackTargets.AllUnits);
        foreach (Creature target in hits.Units.Where(target => HbrBattleStatsHelper.IsEnabled(target)))
            await PowerCmd.Apply<HbrCalamityPower>(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
                target, DisasterRoarCalamity, Creature, null);
    }

    private async Task RecoveryMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(RecoveryDpRatio);
    }
}
