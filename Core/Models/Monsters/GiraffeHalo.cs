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

// HBR 长颈怪环, act-three elite: original GiraffeHaloDefault_01
// (MC04BExpDay01Kisokoma01_BOSS01). Its dp table opens with eight once-only
// steps, then alternates Attack Up and Judgment Ray
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class GiraffeHalo : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string NeckLashSkill = "GiraffeHalo_AttackSingle01";
    internal const string HaloShotsSkill = "GiraffeHalo_AttackAll01";
    internal const string JudgmentRaySkill = "GiraffeHalo_AttackAll02";
    internal const string AttackUpSkill = "GiraffeHalo_AttackUp";
    internal const string HealSkill = "GiraffeHalo_Heal";

    internal const string NeckLashMoveId = "NECK_LASH_MOVE";
    internal const string HaloShotsMoveId = "HALO_SHOTS_MOVE";
    internal const string JudgmentRayMoveId = "JUDGMENT_RAY_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 100;
    internal const int HaloShotsHits = 2;
    internal const int JudgmentRayHits = 5;
    internal const int IllusionLayers = 2;

    // Original HealDp 15000 over the original maximum DP 7500: a full heal.
    internal const decimal HealDpRatio = 15000m / 7500m;

    internal static readonly HbrgoMoveTables Tables = new(
        "GiraffeHaloDefault_01",
        Table("GiraffeHaloDefault_01",
            PickOnce((NeckLashSkill, 40), (HaloShotsSkill, 60)),
            PickOnce((NeckLashSkill, 40), (AttackUpSkill, 60)),
            OnceStep(JudgmentRaySkill),
            PickOnce((NeckLashSkill, 30), (HaloShotsSkill, 70)),
            OnceStep(AttackUpSkill),
            OnceStep(JudgmentRaySkill),
            PickOnce((NeckLashSkill, 40), (AttackUpSkill, 60)),
            OnceStep(HaloShotsSkill),
            Step(AttackUpSkill),
            Step(JudgmentRaySkill)),
        Table("GiraffeHaloDefault_01_break",
            Pick((HaloShotsSkill, 50), (JudgmentRaySkill, 50)),
            Pick((NeckLashSkill, 30), (AttackUpSkill, 70)),
            Step(JudgmentRaySkill),
            Pick((HaloShotsSkill, 50), (AttackUpSkill, 50)),
            Step(JudgmentRaySkill),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(NeckLashSkill, NeckLashMoveId, HbrgoMoveTarget.Single),
        new(HaloShotsSkill, HaloShotsMoveId, HbrgoMoveTarget.All),
        new(JudgmentRaySkill, JudgmentRayMoveId, HbrgoMoveTarget.All),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            256,
            240);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            266,
            250);

    private int NeckLashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            15,
            13);

    private int HaloShotsDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    private int JudgmentRayDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            3);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            NeckLashMoveId,
            NeckLashMove,
            new SingleAttackIntent(() => NeckLashDamage));
        yield return new MoveState(
            HaloShotsMoveId,
            HaloShotsMove,
            new MultiAttackIntent(HaloShotsDamage, () => HaloShotsHits));
        yield return new MoveState(
            JudgmentRayMoveId,
            JudgmentRayMove,
            new MultiAttackIntent(JudgmentRayDamage, () => JudgmentRayHits),
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

    private Task NeckLashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(NeckLashDamage);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task HaloShotsMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(HaloShotsDamage, HaloShotsHits, HbrgoAttackTargets.AllUnits);
    }

    // Original all-unit attack whose illusion always lands (chance 100%, not a
    // chance-based status), so every player in range takes it, blocked or not.
    private async Task JudgmentRayMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttackOn(JudgmentRayDamage, JudgmentRayHits, HbrgoAttackTargets.AllUnits);
        await HbrEnemyEffects.ApplyIllusion(
            UnitsInRange(targets, HbrgoAttackTargets.AllUnits), IllusionLayers, Creature);
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
        return move?.Id == JudgmentRayMoveId
            ? HbrEnemyEffects.ApplyIllusion(attackedTargets, IllusionLayers, Creature)
            : Task.CompletedTask;
    }
}
