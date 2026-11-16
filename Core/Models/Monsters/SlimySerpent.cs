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

// HBR 触手巨蛇, act-three elite: original SlimySerpentDefault
// (MC04BExpDay04Desert01_S01). Its dp table opens with seven once-only steps,
// then loops Attack Up, Destruction Rush, Disease Plant
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class SlimySerpent : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string TailSlamSkill = "SlimySerpent_AttackAll01_MC04B";
    internal const string DestructionRushSkill = "SlimySerpent_AttackAll02_MC04B";
    internal const string DiseasePlantSkill = "SlimySerpent_AttackSingle01_Default_MC04B";
    internal const string AttackUpSkill = "SlimySerpent_AttackUp02";
    internal const string HealSkill = "SlimySerpent_Heal";

    internal const string TailSlamMoveId = "TAIL_SLAM_MOVE";
    internal const string DestructionRushMoveId = "DESTRUCTION_RUSH_MOVE";
    internal const string DiseasePlantMoveId = "DISEASE_PLANT_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 100;
    internal const int TailSlamHits = 2;

    // Original HealDp 15000 over the original maximum DP 76000.
    internal const decimal HealDpRatio = 15000m / 76000m;

    internal static readonly HbrgoMoveTables Tables = new(
        "SlimySerpentDefault",
        Table("SlimySerpentDefault",
            PickOnce((DiseasePlantSkill, 50), (TailSlamSkill, 50)),
            OnceStep(AttackUpSkill),
            OnceStep(DestructionRushSkill),
            OnceStep(DiseasePlantSkill),
            PickOnce((TailSlamSkill, 50), (DestructionRushSkill, 50)),
            PickOnce((DiseasePlantSkill, 40), (TailSlamSkill, 60)),
            PickOnce((TailSlamSkill, 30), (DestructionRushSkill, 70)),
            Step(AttackUpSkill),
            Step(DestructionRushSkill),
            Step(DiseasePlantSkill)),
        Table("SlimySerpentDefault_break",
            Step(AttackUpSkill),
            Step(DestructionRushSkill),
            Pick((DiseasePlantSkill, 50), (TailSlamSkill, 50)),
            Step(AttackUpSkill),
            Step(DestructionRushSkill),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(TailSlamSkill, TailSlamMoveId, HbrgoMoveTarget.All),
        new(DestructionRushSkill, DestructionRushMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(DiseasePlantSkill, DiseasePlantMoveId, HbrgoMoveTarget.Single),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            266,
            250);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            276,
            260);

    private int TailSlamDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    private int DestructionRushDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            19,
            17);

    // Original OverDrivePointDown: 5% of an OD level, 10% on hard.
    private int DestructionRushOverdriveDown =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            5);

    private int DiseasePlantDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            17,
            15);

    // Original AttackDown 20% (30% on hard): 2 modifier layers, 3 on hard.
    private int DiseasePlantAttackDown =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            2);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            TailSlamMoveId,
            TailSlamMove,
            new MultiAttackIntent(TailSlamDamage, () => TailSlamHits));
        yield return new MoveState(
            DestructionRushMoveId,
            DestructionRushMove,
            new SingleAttackIntent(() => DestructionRushDamage),
            new DebuffIntent());
        yield return new MoveState(
            DiseasePlantMoveId,
            DiseasePlantMove,
            new SingleAttackIntent(() => DiseasePlantDamage),
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

    // Original all-unit attack: every player-side unit, summons included.
    private Task TailSlamMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(TailSlamDamage, TailSlamHits, HbrgoAttackTargets.AllUnits);
    }

    // Original all-unit attack; every player's OD gauge drops.
    private async Task DestructionRushMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttackOn(DestructionRushDamage, 1, HbrgoAttackTargets.AllUnits);
        HbrEnemyEffects.LowerOverdrive(targets, DestructionRushOverdriveDown, Creature);
    }

    private async Task DiseasePlantMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(DiseasePlantDamage);
        await ApplyDiseasePlant(targets);
    }

    private Task ApplyDiseasePlant(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyAttackDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            DiseasePlantAttackDown,
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
            case DiseasePlantMoveId:
                return ApplyDiseasePlant(attackedTargets);
            case DestructionRushMoveId:
                HbrEnemyEffects.LowerOverdrive(attackedTargets, DestructionRushOverdriveDown, Creature);
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }
}
