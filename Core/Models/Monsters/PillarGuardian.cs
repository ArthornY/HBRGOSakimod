using System.Linq;
using System.Collections.Generic;
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

// HBR 三柱守卫, act-three boss: original PillarGuardianDefault
// (MC04BWilDay04MilitaryFacility01_BOSS01). Its dp table opens with seven
// once-only steps, then loops attack, Attack Up, Paralysis Burst
// (docs/hbr-monster-mechanics-proposal.md).
public sealed class PillarGuardian : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string PillarStrikeSkill = "PillarGuardian_AttackSingle01";
    internal const string DestructionSpearSkill = "PillarGuardian_AttackSingle02";
    internal const string ParalysisBurstSkill = "PillarGuardian_AttackAll01";
    internal const string AttackUpSkill = "PillarGuardian_AttackUp";
    internal const string HealSkill = "PillarGuardian_Heal";

    internal const string PillarStrikeMoveId = "PILLAR_STRIKE_MOVE";
    internal const string DestructionSpearMoveId = "DESTRUCTION_SPEAR_MOVE";
    internal const string ParalysisBurstMoveId = "PARALYSIS_BURST_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 100;
    internal const int PillarStrikeHits = 2;
    internal const int DestructionSpearHits = 4;
    internal const int ParalysisBurstHits = 5;

    // Original HealDp 15000 over the original maximum DP 21000.
    internal const decimal HealDpRatio = 15000m / 21000m;

    internal static readonly HbrgoMoveTables Tables = new(
        "PillarGuardianDefault",
        Table("PillarGuardianDefault",
            PickOnce((PillarStrikeSkill, 60), (DestructionSpearSkill, 40)),
            PickOnce((PillarStrikeSkill, 70), (DestructionSpearSkill, 30)),
            PickOnce((PillarStrikeSkill, 50), (AttackUpSkill, 50)),
            OnceStep(ParalysisBurstSkill),
            PickOnce((PillarStrikeSkill, 80), (DestructionSpearSkill, 20)),
            OnceStep(AttackUpSkill),
            OnceStep(ParalysisBurstSkill),
            Pick((PillarStrikeSkill, 80), (DestructionSpearSkill, 20)),
            Step(AttackUpSkill),
            Step(ParalysisBurstSkill)),
        Table("PillarGuardianDefault_break",
            Pick((PillarStrikeSkill, 30), (DestructionSpearSkill, 70)),
            Pick((PillarStrikeSkill, 20), (DestructionSpearSkill, 80)),
            Pick((DestructionSpearSkill, 50), (AttackUpSkill, 50)),
            Step(ParalysisBurstSkill),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(PillarStrikeSkill, PillarStrikeMoveId, HbrgoMoveTarget.Single),
        new(DestructionSpearSkill, DestructionSpearMoveId, HbrgoMoveTarget.Single),
        new(ParalysisBurstSkill, ParalysisBurstMoveId, HbrgoMoveTarget.All),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            490,
            460);

    public override int MaxInitialHp => MinInitialHp;

    private int PillarStrikeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            8);

    private int DestructionSpearDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            4);

    private int ParalysisBurstDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            4);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal const string DeathsDoorMoveId = "DEATHS_DOOR_MOVE";
    private int DeathsDoorDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 125, 100);
    internal override bool HasFlavorMechanics => true;
    private protected override IEnumerable<MoveState> CreateFlavorMoveStates()
    { yield return new MoveState(DeathsDoorMoveId, DeathsDoorMove, new SingleAttackIntent(() => DeathsDoorDamage)); }
    private protected override HbrEnemyFlavorChoice ChooseFlavorMove(string tableMoveId) =>
        Creature.HasPower<PillarOverdriveDetectionPower>() && CombatState.Players.Any(player =>
            player.Creature.IsAlive && player.GetRelic<HBRGOSakiMod.Core.Models.Relics.Overclock>()?.IsMaxLevel == true)
            ? new(DeathsDoorMoveId) : null;
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (!FlavorEnabled || Creature.HasPower<PillarOverdriveDetectionPower>()) return;
        await PowerCmd.Apply<PillarOverdriveDetectionPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
        HbrEnemyFlavorSchedulingHelper.RefreshOwner(Creature);
    }
    private Task DeathsDoorMove(IReadOnlyList<Creature> _) => FlavorEnabled
        ? PerformAttackOn(DeathsDoorDamage, 1, HbrgoAttackTargets.AllUnits) : Task.CompletedTask;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            PillarStrikeMoveId,
            PillarStrikeMove,
            new MultiAttackIntent(PillarStrikeDamage, () => PillarStrikeHits),
            new DebuffIntent());
        yield return new MoveState(
            DestructionSpearMoveId,
            DestructionSpearMove,
            new MultiAttackIntent(DestructionSpearDamage, () => DestructionSpearHits));
        yield return new MoveState(
            ParalysisBurstMoveId,
            ParalysisBurstMove,
            new MultiAttackIntent(ParalysisBurstDamage, () => ParalysisBurstHits),
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

    private async Task PillarStrikeMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(PillarStrikeDamage, PillarStrikeHits);
        await HbrEnemyEffects.ApplyContinuousDpDamage(targets, Creature);
    }

    private Task DestructionSpearMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(DestructionSpearDamage, DestructionSpearHits);
    }

    // Original all-unit attack: every player-side unit, summons included.
    // The stun in HBR attaches on contact; legacy still requires passing block.
    private async Task ParalysisBurstMove(IReadOnlyList<Creature> targets)
    {
        HbrgoAttackHits hits = await PerformTrackedAttack(
            ParalysisBurstDamage, ParalysisBurstHits, targets, HbrgoAttackTargets.AllUnits);
        await HbrEnemyEffects.ApplyActionLock<HbrStunPower>(HbrEnemyEffects.ControlTargets(hits, Creature), Creature);
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
        return move?.Id switch
        {
            PillarStrikeMoveId => HbrEnemyEffects.ApplyContinuousDpDamage(attackedTargets, Creature),
            ParalysisBurstMoveId => HbrEnemyEffects.ApplyActionLock<HbrStunPower>(passedTargets, Creature),
            _ => Task.CompletedTask
        };
    }
}
