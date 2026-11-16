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

// HBR 巨肌魔臂, act three: original MolArmsSmall (the two desert fights). Its
// dp table opens with five once-only steps, then loops. Deceive Shot's Wisdom
// Down is not chance-based in the original, so it always lands. With two in
// one fight the second starts one step ahead (MolArmsPairNormal), so their
// boosted all-unit attacks fall on different turns
// (docs/hbr-monster-mechanics-proposal.md, section 7).
public sealed class MolArms : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string DoubleSmashSkill = "MolArms_AttackSingle01";
    internal const string DeceiveShotSkill = "MolArms_AttackSingle02";
    internal const string AllAttackSkill = "MolArms_AttackAll01";
    internal const string AttackUpSkill = "MolArms_AttackUp";
    internal const string HealSkill = "MolArms_Heal";

    internal const string DoubleSmashMoveId = "DOUBLE_SMASH_MOVE";
    internal const string DeceiveShotMoveId = "DECEIVE_SHOT_MOVE";
    internal const string AllAttackMoveId = "ALL_ATTACK_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 100;
    internal const int DoubleSmashHits = 5;
    internal const int DeceiveShotHits = 3;
    internal const int PairOffsetStep = 1;

    // Original HealDp 5000 over the original maximum DP 6000.
    internal const decimal HealDpRatio = 5000m / 6000m;

    private static HbrgoMoveStep SmashOrShot(int smash, int shot) =>
        Pick((DoubleSmashSkill, smash), (DeceiveShotSkill, shot));

    internal static readonly HbrgoMoveTables Tables = new(
        "MolArmsSmall",
        Table("MolArmsSmall",
            PickOnce((DoubleSmashSkill, 40), (DeceiveShotSkill, 60)),
            PickOnce((DoubleSmashSkill, 50), (AllAttackSkill, 50)),
            PickOnce((DoubleSmashSkill, 40), (DeceiveShotSkill, 60)),
            OnceStep(AttackUpSkill),
            PickOnce((DeceiveShotSkill, 50), (AllAttackSkill, 50)),
            SmashOrShot(30, 70),
            Step(AttackUpSkill),
            Step(AllAttackSkill)),
        Table("MolArmsSmall_break",
            Step(AttackUpSkill),
            Step(AllAttackSkill),
            SmashOrShot(40, 60),
            SmashOrShot(30, 70),
            SmashOrShot(20, 80),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(DoubleSmashSkill, DoubleSmashMoveId, HbrgoMoveTarget.Single),
        new(DeceiveShotSkill, DeceiveShotMoveId, HbrgoMoveTarget.Single),
        new(AllAttackSkill, AllAttackMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            128,
            120);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            134,
            126);

    private int DoubleSmashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            2,
            2);

    private int DeceiveShotDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            4);

    private int AllAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            13,
            12);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            DoubleSmashMoveId,
            DoubleSmashMove,
            new MultiAttackIntent(DoubleSmashDamage, () => DoubleSmashHits));
        yield return new MoveState(
            DeceiveShotMoveId,
            DeceiveShotMove,
            new MultiAttackIntent(DeceiveShotDamage, () => DeceiveShotHits),
            new DebuffIntent());
        yield return new MoveState(
            AllAttackMoveId,
            AllAttackMove,
            new SingleAttackIntent(() => AllAttackDamage));
        yield return new MoveState(
            AttackUpMoveId,
            AttackUpMove,
            new BuffIntent());
        yield return new MoveState(
            HealMoveId,
            HealMove,
            new HealIntent());
    }

    private Task DoubleSmashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(DoubleSmashDamage, DoubleSmashHits);
    }

    // Original WisdomDownRate always lands on the attacked players.
    private async Task DeceiveShotMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(DeceiveShotDamage, DeceiveShotHits);
        await HbrEnemyEffects.ApplyStatRateDown<HbrWisdomDownPower>(targets, Creature);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task AllAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(AllAttackDamage, 1, HbrgoAttackTargets.AllUnits);
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
        return move?.Id == DeceiveShotMoveId
            ? HbrEnemyEffects.ApplyStatRateDown<HbrWisdomDownPower>(attackedTargets, Creature)
            : Task.CompletedTask;
    }
}
