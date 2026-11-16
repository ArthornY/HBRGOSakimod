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
using static HBRGOSakiMod.Core.Models.Monsters.HbrgoMoveTableBuilder;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 阿拉克尼巨蛛, act-three boss: original ArachneLineDefault
// (MC04ExpPatrol02_BOSS01). The original summons nothing
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class ArachneLine : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string EightSlashSkill = "ArachneLine_AttackSingle01";
    internal const string AbsorbWhipSkill = "ArachneLine_AttackSingle02";
    internal const string PiercingFlashSkill = "ArachneLine_AttackAll01";
    internal const string AttackUpSkill = "ArachneLine_AttackUp";
    internal const string HealSkill = "ArachneLine_Heal";

    internal const string EightSlashMoveId = "EIGHT_SLASH_MOVE";
    internal const string AbsorbWhipMoveId = "ABSORB_WHIP_MOVE";
    internal const string PiercingFlashMoveId = "PIERCING_FLASH_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 20;
    internal const int EightSlashHits = 2;
    internal const int AbsorbWhipHits = 2;
    internal const int PiercingFlashHits = 8;

    // Original HealDp 15000 over the original maximum DP 41000.
    internal const decimal HealDpRatio = 15000m / 41000m;

    internal static readonly HbrgoMoveTables Tables = new(
        "ArachneLineDefault",
        Table("ArachneLineDefault",
            Step(EightSlashSkill),
            Pick((PiercingFlashSkill, 60), (AbsorbWhipSkill, 40)),
            Pick((PiercingFlashSkill, 40), (AbsorbWhipSkill, 60)),
            Pick((EightSlashSkill, 40), (AttackUpSkill, 60)),
            Step(PiercingFlashSkill),
            Step(AttackUpSkill),
            Step(AbsorbWhipSkill),
            Step(HealSkill)),
        Table("ArachneLineDefault_break",
            Step(PiercingFlashSkill),
            Pick((EightSlashSkill, 70), (AttackUpSkill, 30)),
            Step(PiercingFlashSkill),
            Pick((EightSlashSkill, 60), (AttackUpSkill, 40)),
            Step(AbsorbWhipSkill),
            Pick((EightSlashSkill, 40), (AttackUpSkill, 60)),
            Step(AbsorbWhipSkill),
            Pick((AttackUpSkill, 100), (HealSkill, 100)),
            Step(AbsorbWhipSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(EightSlashSkill, EightSlashMoveId, HbrgoMoveTarget.Single),
        new(AbsorbWhipSkill, AbsorbWhipMoveId, HbrgoMoveTarget.Single),
        new(PiercingFlashSkill, PiercingFlashMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            450,
            420);

    public override int MaxInitialHp => MinInitialHp;

    private int EightSlashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            15,
            13);

    private int AbsorbWhipDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            18,
            16);

    private int PiercingFlashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            4);

    // Original DefenseDown 20% (30% on hard): 2 modifier layers, 3 on hard.
    private int PiercingFlashDefenseDown =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            2);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            EightSlashMoveId,
            EightSlashMove,
            new MultiAttackIntent(EightSlashDamage, () => EightSlashHits));
        yield return new MoveState(
            AbsorbWhipMoveId,
            AbsorbWhipMove,
            new MultiAttackIntent(AbsorbWhipDamage, () => AbsorbWhipHits),
            new HealIntent());
        yield return new MoveState(
            PiercingFlashMoveId,
            PiercingFlashMove,
            new MultiAttackIntent(PiercingFlashDamage, () => PiercingFlashHits),
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

    private Task EightSlashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(EightSlashDamage, EightSlashHits);
    }

    // Original HealDpByDamage: DP back from the DP and HP the hits took.
    private async Task AbsorbWhipMove(IReadOnlyList<Creature> targets)
    {
        HbrgoAttackHits hits = await PerformTrackedAttack(AbsorbWhipDamage, AbsorbWhipHits, targets);
        HbrEnemyEffects.AbsorbDp(Creature, hits.PassedDamage);
    }

    // Original all-unit attack: every player-side unit, summons included, and
    // each takes defense down.
    private async Task PiercingFlashMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttackOn(PiercingFlashDamage, PiercingFlashHits, HbrgoAttackTargets.AllUnits);
        await ApplyPiercingFlash(UnitsInRange(targets, HbrgoAttackTargets.AllUnits));
    }

    private Task ApplyPiercingFlash(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            PiercingFlashDefenseDown,
            Creature);
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
        switch (move?.Id)
        {
            case AbsorbWhipMoveId:
                HbrEnemyEffects.AbsorbDp(Creature, passedDamage);
                return Task.CompletedTask;
            case PiercingFlashMoveId:
                return ApplyPiercingFlash(attackedTargets);
            default:
                return Task.CompletedTask;
        }
    }
}
