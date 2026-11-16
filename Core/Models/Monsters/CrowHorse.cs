using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 鸦头马, act-one boss: original CrowHorseDefaultVirtual (MC02_15_Battle01).
// Its dp table has no heal; only the hp table in Break restores DP
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class CrowHorse : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string TrampleSkill = "CrowHorse_Attack01";
    internal const string DestructionBallSkill = "CrowHorse_Attack02";
    internal const string ExtinctionRaySkill = "CrowHorse_AttackAll01";
    internal const string HealSkill = "CrowHorse_Heal01";

    internal const string TrampleMoveId = "TRAMPLE_MOVE";
    internal const string DestructionBallMoveId = "DESTRUCTION_BALL_MOVE";
    internal const string ExtinctionRayMoveId = "EXTINCTION_RAY_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    // Original HealDp 3000 over the original maximum DP 21600.
    internal const decimal HealDpRatio = 3000m / 21600m;

    internal static readonly HbrgoMoveTables Tables = new(
        "CrowHorseDefaultVirtual",
        Table("CrowHorseDefault",
            Step(TrampleSkill),
            Pick((TrampleSkill, 50), (DestructionBallSkill, 50)),
            Step(ExtinctionRaySkill),
            Pick((TrampleSkill, 50), (DestructionBallSkill, 50)),
            Pick((TrampleSkill, 50), (ExtinctionRaySkill, 50)),
            Pick((DestructionBallSkill, 50), (ExtinctionRaySkill, 50))),
        Table("CrowHorseDefault_break",
            Step(DestructionBallSkill),
            Pick((TrampleSkill, 40), (ExtinctionRaySkill, 60)),
            Pick((DestructionBallSkill, 60), (ExtinctionRaySkill, 40)),
            Pick((TrampleSkill, 40), (DestructionBallSkill, 60)),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(TrampleSkill, TrampleMoveId, HbrgoMoveTarget.Single),
        new(DestructionBallSkill, DestructionBallMoveId, HbrgoMoveTarget.Single),
        new(ExtinctionRaySkill, ExtinctionRayMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            160,
            150);

    public override int MaxInitialHp => MinInitialHp;

    private int TrampleDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            12);

    private int DestructionBallDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            18,
            15);

    // Original OverDrivePointDown: 5% of an OD level, 10% on hard.
    private int DestructionBallOverdriveDown =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            5);

    private int ExtinctionRayDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16,
            13);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            TrampleMoveId,
            TrampleMove,
            new SingleAttackIntent(() => TrampleDamage));
        yield return new MoveState(
            DestructionBallMoveId,
            DestructionBallMove,
            new SingleAttackIntent(() => DestructionBallDamage),
            new DebuffIntent());
        yield return new MoveState(
            ExtinctionRayMoveId,
            ExtinctionRayMove,
            new SingleAttackIntent(() => ExtinctionRayDamage));
        yield return new MoveState(
            HealMoveId,
            HealMove,
            new HealIntent());
    }

    private Task TrampleMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(TrampleDamage);
    }

    private async Task DestructionBallMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(DestructionBallDamage);
        HbrEnemyEffects.LowerOverdrive(targets, DestructionBallOverdriveDown, Creature);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task ExtinctionRayMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(ExtinctionRayDamage, 1, HbrgoAttackTargets.AllUnits);
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
        if (move?.Id == DestructionBallMoveId)
        {
            HbrEnemyEffects.LowerOverdrive(attackedTargets, DestructionBallOverdriveDown, Creature);
        }

        return Task.CompletedTask;
    }
}
