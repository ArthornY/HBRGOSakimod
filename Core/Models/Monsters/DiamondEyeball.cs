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

// HBR 钻石眼魔, act-two boss: original DiamondEyeballDefaultVirtual_MC03_01
// (MC03ExpTraining02Virtual_BOSS01). While its DP holds it mostly fires the
// binding and defense-breaking single shots; in Break, Disaster Ray every
// other turn (docs/hbr-monster-mechanics-proposal.md).
public sealed class DiamondEyeball : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const string LaserBindSkill = "DiamondEyeball_AttackSingle01";
    internal const string PrismRaySkill = "DiamondEyeball_AttackSingle02";
    internal const string DisasterRaySkill = "DiamondEyeball_AttackAll01";
    internal const string FacetShieldSkill = "DiamondEyeball_Buff01";

    internal const string LaserBindMoveId = "LASER_BIND_MOVE";
    internal const string PrismRayMoveId = "PRISM_RAY_MOVE";
    internal const string DisasterRayMoveId = "DISASTER_RAY_MOVE";
    internal const string FacetShieldMoveId = "FACET_SHIELD_MOVE";

    // Original HealDp 700 over the original maximum DP 32800.
    internal const decimal FacetShieldDpRatio = 700m / 32800m;

    internal static readonly HbrgoMoveTables Tables = new(
        "DiamondEyeballDefaultVirtual_MC03_01",
        Table("DiamondEyeballDefault",
            Step(LaserBindSkill),
            Pick((LaserBindSkill, 60), (PrismRaySkill, 40)),
            Step(DisasterRaySkill),
            Pick((LaserBindSkill, 40), (PrismRaySkill, 60)),
            Pick((LaserBindSkill, 60), (PrismRaySkill, 40))),
        Table("DiamondEyeballDefault_break",
            Step(DisasterRaySkill),
            Pick((LaserBindSkill, 60), (PrismRaySkill, 40)),
            Step(DisasterRaySkill),
            Pick((LaserBindSkill, 60), (PrismRaySkill, 40)),
            Step(DisasterRaySkill),
            Pick((LaserBindSkill, 60), (PrismRaySkill, 40)),
            Step(FacetShieldSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(LaserBindSkill, LaserBindMoveId, HbrgoMoveTarget.Single),
        new(PrismRaySkill, PrismRayMoveId, HbrgoMoveTarget.Single),
        new(DisasterRaySkill, DisasterRayMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(FacetShieldSkill, FacetShieldMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            340,
            320);

    public override int MaxInitialHp => MinInitialHp;

    private int LaserBindDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            15,
            13);

    private int PrismRayDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            19,
            17);

    // Original DefenseDown 20% (30% on hard): 2 modifier layers, 3 on hard.
    private int PrismRayDefenseDown =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            2);

    private int DisasterRayDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            3);

    internal const int DisasterRayHits = 5;

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal const string DpCheckMoveId = "DP_REMAINING_CHECK_MOVE";
    internal const string DivideExplosionMoveId = "DIVIDE_EXPLOSION_MOVE";
    internal override bool HasFlavorMechanics => true;
    internal override bool FlavorReselectOnShellBreak => true;
    internal int DivideExplosionDamage
    {
        get
        {
            HighDimensionalShellPower shell = Creature.GetPower<HighDimensionalShellPower>();
            decimal max = shell?.MaxAmount ?? 0m, current = shell?.CurrentDp ?? 0m;
            if (max <= 0m) return 8;
            return current >= max * .75m ? 47 : current >= max * .5m ? 23 : current >= max * .25m ? 15 : 8;
        }
    }
    private protected override IEnumerable<MoveState> CreateFlavorMoveStates()
    {
        yield return new MoveState(DpCheckMoveId, DpCheckMove, new UnknownIntent());
        yield return new MoveState(DivideExplosionMoveId, DivideExplosionMove, new SingleAttackIntent(() => DivideExplosionDamage));
    }
    private protected override HbrEnemyFlavorChoice ChooseFlavorMove(string tableMoveId)
    {
        DiamondDpCheckPower check = Creature.GetPower<DiamondDpCheckPower>();
        if (check?.CanCheck != true) return null;
        return check.ExplosionPending ? new(DivideExplosionMoveId, true) : check.Amount <= 1 ? new(DpCheckMoveId, true) : null;
    }
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (!FlavorEnabled || Creature.HasPower<DiamondDpCheckPower>()) return;
        await PowerCmd.Apply<DiamondDpCheckPower>(new ThrowingPlayerChoiceContext(), Creature, 3m, Creature, null);
        HbrEnemyFlavorSchedulingHelper.RefreshOwner(Creature);
    }
    private async Task DpCheckMove(IReadOnlyList<Creature> _)
    {
        if (!FlavorEnabled) return;
        await CreatureCmd.TriggerAnim(Creature, "Cast", .15f);
        Creature.GetPower<DiamondDpCheckPower>()?.BeginExplosion();
    }
    private Task DivideExplosionMove(IReadOnlyList<Creature> _) => FlavorEnabled
        ? PerformAttackOn(DivideExplosionDamage, 1, HbrgoAttackTargets.AllUnits) : Task.CompletedTask;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            LaserBindMoveId,
            LaserBindMove,
            new SingleAttackIntent(() => LaserBindDamage),
            new DebuffIntent());
        yield return new MoveState(
            PrismRayMoveId,
            PrismRayMove,
            new SingleAttackIntent(() => PrismRayDamage),
            new DebuffIntent());
        yield return new MoveState(
            DisasterRayMoveId,
            DisasterRayMove,
            new MultiAttackIntent(DisasterRayDamage, () => DisasterRayHits));
        yield return new MoveState(
            FacetShieldMoveId,
            FacetShieldMove,
            new HealIntent());
    }

    // Bind in HBR attaches on contact; legacy still requires passing block.
    private async Task LaserBindMove(IReadOnlyList<Creature> targets)
    {
        HbrgoAttackHits hits = await PerformTrackedAttack(LaserBindDamage, 1, targets);
        await HbrEnemyEffects.ApplyActionLock<HbrBindPower>(HbrEnemyEffects.ControlTargets(hits, Creature), Creature);
    }

    private async Task PrismRayMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(PrismRayDamage);
        await ApplyPrismRay(targets);
    }

    private Task ApplyPrismRay(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            PrismRayDefenseDown,
            Creature);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task DisasterRayMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(DisasterRayDamage, DisasterRayHits, HbrgoAttackTargets.AllUnits);
    }

    private async Task FacetShieldMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(FacetShieldDpRatio);
    }

    Task IHbrgoPhantomAttackResults.AfterPhantomAttack(
        MoveState move,
        IReadOnlyList<Creature> attackedTargets,
        IReadOnlyList<Creature> passedTargets,
        decimal passedDamage)
    {
        return move?.Id switch
        {
            LaserBindMoveId => HbrEnemyEffects.ApplyActionLock<HbrBindPower>(passedTargets, Creature),
            PrismRayMoveId => ApplyPrismRay(attackedTargets),
            _ => Task.CompletedTask
        };
    }
}
