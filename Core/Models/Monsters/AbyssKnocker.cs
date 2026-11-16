using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 深渊重锤, act-one elite: original AbyssKnockerDefaultVirtual_MC02 (the
// chapter-two training forest's boss; AbyssKnockerDefault and the chapter-one
// cave's AbyssKnockerDefaultMC01 have the same tables). Rage Wave is the
// original all-unit, spirit-defended skill; its DP rate is 1.0, so the 11-05
// bonus against targets with DP is gone, as is the recovery's block
// (docs/hbr-act-rosters.md, fourth batch).
public sealed class AbyssKnocker : HbrgoMoveTableMonster
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

    // Original HealDp 3000 over the original maximum DP 44200.
    internal const decimal RecoveryDpRatio = 3000m / 44200m;

    internal static readonly HbrgoMoveTables Tables = new(
        "AbyssKnockerDefaultVirtual_MC02",
        Table("AbyssKnockerDefault",
            Step(AttackSkill),
            Step(RageWaveSkill),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
            Step(RageWaveSkill)),
        Table("AbyssKnockerDefault_break",
            Step(RageWaveSkill),
            Pick((AttackSkill, 50), (MultiAttackSkill, 50)),
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
            94,
            90);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            98,
            94);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    private int MultiAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            4);

    private int RageWaveDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

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
            new SingleAttackIntent(() => RageWaveDamage));
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
    private Task RageWaveMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(RageWaveDamage, 1, HbrgoAttackTargets.AllUnits);
    }

    private async Task RecoveryMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(RecoveryDpRatio);
    }
}
