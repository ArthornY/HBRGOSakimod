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

// HBR 异议之门. Three original variants share the skills but not the table:
// BigotryGateSmall_01 (most encounters), BigotryGateSmall_02 (the second gate
// of "两扇异议之门") and BigotryGateDefault_01 ("门与诅咒玩偶"). None has an hp
// table, so Break keeps walking the dp table (docs/hbr-monster-mechanics-proposal.md).
public sealed class BigotryGate : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const int SmallVariant = 0;
    internal const int SecondSmallVariant = 1;
    internal const int DefaultVariant = 2;

    internal const string ImpactSkill = "BigotryGate_AttackSingle01";
    internal const string DisasterShotSkill = "BigotryGate_AttackSingle02";
    internal const string StrongImpulseSkill = "BigotryGate_AttackAll01";
    internal const string AttackUpSkill = "BigotryGate_AttackUp";
    internal const string HealSkill = "BigotryGate_Heal";

    internal const string ImpactMoveId = "IMPACT_MOVE";
    internal const string DisasterShotMoveId = "DISASTER_SHOT_MOVE";
    internal const string StrongImpulseMoveId = "STRONG_IMPULSE_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";
    internal const string FaintnessStormMoveId = "FAINTNESS_STORM_MOVE";

    internal const int AttackUpPercent = 20;
    internal const int ImpactHits = 3;

    // Original HealDp 5000 over each variant's original maximum DP.
    internal const decimal SmallHealDpRatio = 5000m / 5900m;
    internal const decimal DefaultHealDpRatio = 5000m / 7300m;

    private static HbrgoMoveStep EitherSingle() =>
        Pick((ImpactSkill, 50), (DisasterShotSkill, 50));

    internal static readonly HbrgoMoveTables SmallTables = new(
        "BigotryGateSmall_01",
        Table("BigotryGateSmall_01",
            Step(ImpactSkill),
            EitherSingle(),
            Step(AttackUpSkill),
            Step(StrongImpulseSkill),
            EitherSingle(),
            EitherSingle(),
            Step(AttackUpSkill),
            Step(StrongImpulseSkill),
            Step(HealSkill)),
        HbrgoMoveTable.None);

    internal static readonly HbrgoMoveTables SecondSmallTables = new(
        "BigotryGateSmall_02",
        Table("BigotryGateSmall_02",
            Step(AttackUpSkill),
            Step(StrongImpulseSkill),
            Step(ImpactSkill),
            EitherSingle(),
            Step(AttackUpSkill),
            Step(StrongImpulseSkill),
            EitherSingle(),
            EitherSingle(),
            Step(HealSkill)),
        HbrgoMoveTable.None);

    internal static readonly HbrgoMoveTables DefaultTables = new(
        "BigotryGateDefault_01",
        Table("BigotryGateDefault_01",
            Step(DisasterShotSkill),
            EitherSingle(),
            Step(AttackUpSkill),
            Step(StrongImpulseSkill),
            EitherSingle(),
            EitherSingle(),
            Step(AttackUpSkill),
            Step(StrongImpulseSkill),
            Step(HealSkill)),
        HbrgoMoveTable.None);

    private static readonly HbrgoTableMove[] Moves =
    {
        new(ImpactSkill, ImpactMoveId, HbrgoMoveTarget.Single),
        new(DisasterShotSkill, DisasterShotMoveId, HbrgoMoveTarget.Single),
        new(StrongImpulseSkill, StrongImpulseMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            106,
            100);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            112,
            106);

    private int ImpactDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            4);

    private int DisasterShotDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16,
            14);

    // Original Misfortune for 2 turns (3 on hard). The mod's Calamity lasts
    // 2 turns, so the hard version's extra turn becomes extra layers.
    private int DisasterShotCalamity =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            30,
            20);

    private int StrongImpulseDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            22,
            19);

    internal override int MoveTableVariantCount => 3;

    internal override HbrgoMoveTables GetMoveTables(int variant) => variant switch
    {
        SecondSmallVariant => SecondSmallTables,
        DefaultVariant => DefaultTables,
        _ => SmallTables
    };

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;
    internal override bool HasFlavorMechanics => true;
    private protected override IEnumerable<MoveState> CreateFlavorMoveStates()
    {
        yield return new MoveState(FaintnessStormMoveId, FaintnessStormMove, new DebuffIntent(), new BuffIntent());
    }
    private protected override HbrEnemyFlavorChoice ChooseFlavorMove(string tableMoveId) =>
        Creature.GetPower<FaintnessStormCountdownPower>()?.IsDue == true
            ? new HbrEnemyFlavorChoice(FaintnessStormMoveId, PauseTable: true) : null;
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (!EnemyStatScalingHelper.IsEnemy(Creature) || !HbrBattleStatsHelper.IsEnabled(Creature) ||
            Creature.HasPower<FaintnessStormCountdownPower>()) return;
        await PowerCmd.Apply<FaintnessStormCountdownPower>(new ThrowingPlayerChoiceContext(), Creature, 4m, Creature, null);
        HbrEnemyFlavorSchedulingHelper.RefreshOwner(Creature);
    }

    private async Task FaintnessStormMove(IReadOnlyList<Creature> _)
    {
        if (!EnemyStatScalingHelper.IsEnemy(Creature) || !HbrBattleStatsHelper.IsEnabled(Creature)) return;
        await CreatureCmd.TriggerAnim(Creature, "Cast", .15f);
        var players = CombatState.Players.Select(player => player.Creature)
            .Where(player => player?.IsAlive == true && player.Side != Creature.Side).ToArray();
        await HbrEnemyEffects.ApplyActionLock<HbrStunPower>(players, Creature);
        await HbrEnemyEffects.ApplyTimedBuff<HbrDefenseUpPower>(Creature, 50);
        Creature.GetPower<FaintnessStormCountdownPower>()?.ResetAfterStorm();
    }

    private decimal HealDpRatio =>
        MoveTableVariant == DefaultVariant ? DefaultHealDpRatio : SmallHealDpRatio;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            ImpactMoveId,
            ImpactMove,
            new MultiAttackIntent(ImpactDamage, () => ImpactHits));
        yield return new MoveState(
            DisasterShotMoveId,
            DisasterShotMove,
            new SingleAttackIntent(() => DisasterShotDamage),
            new DebuffIntent());
        yield return new MoveState(
            StrongImpulseMoveId,
            StrongImpulseMove,
            new SingleAttackIntent(() => StrongImpulseDamage),
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

    private Task ImpactMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(ImpactDamage, ImpactHits);
    }

    private async Task DisasterShotMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(DisasterShotDamage);
        await ApplyDisasterShot(targets);
    }

    // Calamity only lowers HBR attributes and enemy E, so it is applied where
    // HBR numerics run.
    private async Task ApplyDisasterShot(IEnumerable<Creature> targets)
    {
        foreach (Creature target in targets.Where(target =>
                     target.IsAlive && HbrBattleStatsHelper.IsEnabled(target)))
        {
            await PowerCmd.Apply<HbrCalamityPower>(
                new ThrowingPlayerChoiceContext(),
                target,
                DisasterShotCalamity,
                Creature,
                null);
        }
    }

    // Original all-unit attack: every player-side unit, summons included,
    // and each takes Spirit Down.
    private async Task StrongImpulseMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttackOn(StrongImpulseDamage, 1, HbrgoAttackTargets.AllUnits);
        await HbrEnemyEffects.ApplyStatRateDown<HbrSpiritDownPower>(
            UnitsInRange(targets, HbrgoAttackTargets.AllUnits), Creature);
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
            DisasterShotMoveId => ApplyDisasterShot(attackedTargets),
            StrongImpulseMoveId => HbrEnemyEffects.ApplyStatRateDown<HbrSpiritDownPower>(attackedTargets, Creature),
            _ => Task.CompletedTask
        };
    }
}
