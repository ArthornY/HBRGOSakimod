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

// HBR BarrelWalker (烈焰步行火炮), act-two boss: original
// BarrelWalkerDefaultFire_MC04_02 (MC04ExpMtKumotori01_BOSS01). Its dp table
// is a four-step loop ending in Attack Up, then the heavy cannon at the player
// with the least DP (docs/hbr-monster-mechanics-proposal.md).
public sealed class BarrelWalker : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string CannonSkill = "BarrelWalker_AttackSingle01_Fire";
    internal const string HeavyCannonSkill = "BarrelWalker_AttackSingle02_Fire";
    internal const string RageMagnumSkill = "BarrelWalker_AttackAll01_Fire";
    internal const string AttackUpSkill = "BarrelWalker_AttackUp";
    internal const string HealSkill = "BarrelWalker_Heal";

    internal const string CannonMoveId = "CANNON_MOVE";
    internal const string HeavyCannonMoveId = "HEAVY_CANNON_MOVE";
    internal const string RageMagnumMoveId = "RAGE_MAGNUM_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 100;
    internal const int RageMagnumHits = 8;

    // Original HealDp 15000 over the original maximum DP 31000.
    internal const decimal HealDpRatio = 15000m / 31000m;

    internal static readonly HbrgoMoveTables Tables = new(
        "BarrelWalkerDefaultFire_MC04_02",
        Table("BarrelWalkerDefaultFire",
            Step(CannonSkill),
            Pick((CannonSkill, 40), (RageMagnumSkill, 60)),
            Step(AttackUpSkill),
            Step(HeavyCannonSkill)),
        Table("BarrelWalkerDefaultFire_break",
            Pick((CannonSkill, 40), (RageMagnumSkill, 60)),
            Step(AttackUpSkill),
            Step(HeavyCannonSkill),
            Pick((CannonSkill, 30), (RageMagnumSkill, 70)),
            Step(HealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(CannonSkill, CannonMoveId, HbrgoMoveTarget.Single),
        new(HeavyCannonSkill, HeavyCannonMoveId, HbrgoMoveTarget.MinimumDp),
        new(RageMagnumSkill, RageMagnumMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(AttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(HealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            380,
            360);

    public override int MaxInitialHp => MinInitialHp;

    private int CannonDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16,
            14);

    private int HeavyCannonDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            20,
            17);

    private int RageMagnumDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            2,
            2);

    // Original DefenseDown 15% (25% on hard): 1 modifier layer, 2 on hard.
    private int RageMagnumDefenseDown =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            2,
            1);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal const string DemiseCannonMoveId = "DEMISE_CANNON_MOVE";
    internal override bool HasFlavorMechanics => true;
    internal override bool FlavorReselectOnShellBreak => true;
    private protected override IEnumerable<MoveState> CreateFlavorMoveStates()
    { yield return new MoveState(DemiseCannonMoveId, DemiseCannonMove, new SingleAttackIntent(() => 40)); }
    private protected override HbrEnemyFlavorChoice ChooseFlavorMove(string tableMoveId) =>
        Creature.GetPower<BarrelDemiseCountdownPower>()?.IsDue == true ? new(DemiseCannonMoveId, true) : null;
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (!FlavorEnabled || Creature.HasPower<BarrelDemiseCountdownPower>()) return;
        await PowerCmd.Apply<BarrelDemiseCountdownPower>(new ThrowingPlayerChoiceContext(), Creature, 4m, Creature, null);
        HbrEnemyFlavorSchedulingHelper.RefreshOwner(Creature);
    }
    private async Task DemiseCannonMove(IReadOnlyList<Creature> _)
    {
        if (!FlavorEnabled) return;
        await PerformAttackOn(40, 1, HbrgoAttackTargets.AllUnits);
        Creature.GetPower<BarrelDemiseCountdownPower>()?.ResetAfterCannon();
    }

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            CannonMoveId,
            CannonMove,
            new SingleAttackIntent(() => CannonDamage));
        yield return new MoveState(
            HeavyCannonMoveId,
            HeavyCannonMove,
            new SingleAttackIntent(() => HeavyCannonDamage));
        yield return new MoveState(
            RageMagnumMoveId,
            RageMagnumMove,
            new MultiAttackIntent(RageMagnumDamage, () => RageMagnumHits),
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

    private Task CannonMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(CannonDamage);
    }

    // Original MinimumDp skill: the living player with the least DP.
    private Task HeavyCannonMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(HeavyCannonDamage, 1, HbrgoAttackTargets.MinimumDp);
    }

    // Original all-unit attack: every player-side unit, summons included, and
    // each takes defense down.
    private async Task RageMagnumMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttackOn(RageMagnumDamage, RageMagnumHits, HbrgoAttackTargets.AllUnits);
        await ApplyRageMagnum(UnitsInRange(targets, HbrgoAttackTargets.AllUnits));
    }

    private Task ApplyRageMagnum(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            RageMagnumDefenseDown,
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
        return move?.Id == RageMagnumMoveId
            ? ApplyRageMagnum(attackedTargets)
            : Task.CompletedTask;
    }
}
