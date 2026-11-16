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

// HBR 死亡玩偶, act two: original MarionetteKiller2 (the Mt. Kinpu fights; the
// training fights' MarionetteKillerVirtual has the same tables). In 门与诅咒玩偶
// (MC04BExpDay01Kisokoma01_R03) the encounter sets the cursed doll,
// MarionetteKillerCurse, with its own skills. The 11-05 curse pull (attack
// down) is not in the original (docs/hbr-monster-mechanics-proposal.md).
public sealed class MarionetteKiller : HbrgoMoveTableMonster, IHbrgoPhantomAttackResults
{
    internal const int DefaultVariant = 0;
    internal const int CurseVariant = 1;

    internal const string AttackSkill = "Marionette_AttackSingle01";
    internal const string StrongAttackSkill = "Marionette_AttackSingle02";
    internal const string RecoverySkill = "Marionette_Buff01";
    internal const string CurseAttackSkill = "MarionetteKillerCurse_AttackSingle01";
    internal const string CurseStampSkill = "MarionetteKillerCurse_AttackSingle02";
    internal const string CurseAttackUpSkill = "MarionetteKillerCurse_AttackUp";
    internal const string CurseHealSkill = "MarionetteKillerCurse_Heal";

    internal const string AttackMoveId = "ATTACK_MOVE";
    internal const string StrongAttackMoveId = "STRONG_ATTACK_MOVE";
    internal const string RecoveryMoveId = "RECOVERY_MOVE";
    internal const string CurseAttackMoveId = "CURSE_ATTACK_MOVE";
    internal const string CurseStampMoveId = "CURSE_STAMP_MOVE";
    internal const string AttackUpMoveId = "ATTACK_UP_MOVE";
    internal const string HealMoveId = "HEAL_MOVE";

    internal const int AttackUpPercent = 100;

    // Original HealDp over each variant's original maximum DP; the cursed
    // doll's 5000 over 2600 is a full heal.
    internal const decimal RecoveryDpRatio = 600m / 1700m;
    internal const decimal CurseHealDpRatio = 5000m / 2600m;

    internal static readonly HbrgoMoveTables DefaultTables = new(
        "MarionetteKiller2",
        Table("MarionetteKiller",
            Step(AttackSkill),
            Step(RecoverySkill),
            Step(StrongAttackSkill),
            Pick((AttackSkill, 50), (StrongAttackSkill, 50)),
            Step(RecoverySkill),
            // The original lists AttackSingle02 twice in this step.
            Pick((StrongAttackSkill, 100), (StrongAttackSkill, 100))),
        Table("MarionetteKiller_break",
            Step(StrongAttackSkill),
            Pick((AttackSkill, 40), (StrongAttackSkill, 60)),
            Step(AttackSkill),
            Step(RecoverySkill)));

    internal static readonly HbrgoMoveTables CurseTables = new(
        "MarionetteKillerCurse",
        Table("MarionetteKillerCurse",
            OnceStep(CurseStampSkill),
            PickOnce((CurseAttackUpSkill, 50), (CurseAttackSkill, 50)),
            PickOnce((CurseAttackSkill, 40), (CurseStampSkill, 60)),
            OnceStep(CurseAttackUpSkill),
            PickOnce((CurseAttackSkill, 30), (CurseStampSkill, 70)),
            Step(CurseAttackUpSkill),
            Pick((CurseAttackSkill, 30), (CurseStampSkill, 70)),
            Step(CurseAttackUpSkill),
            Step(CurseStampSkill)),
        Table("MarionetteKillerCurse_break",
            Step(CurseStampSkill),
            Pick((CurseAttackUpSkill, 50), (CurseAttackSkill, 50)),
            Pick((CurseAttackSkill, 30), (CurseStampSkill, 70)),
            Step(CurseHealSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, AttackMoveId, HbrgoMoveTarget.Single),
        new(StrongAttackSkill, StrongAttackMoveId, HbrgoMoveTarget.Single),
        new(RecoverySkill, RecoveryMoveId, HbrgoMoveTarget.Self),
        new(CurseAttackSkill, CurseAttackMoveId, HbrgoMoveTarget.Single),
        new(CurseStampSkill, CurseStampMoveId, HbrgoMoveTarget.Single),
        new(CurseAttackUpSkill, AttackUpMoveId, HbrgoMoveTarget.Self),
        new(CurseHealSkill, HealMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            47,
            44);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            51,
            48);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            13,
            11);

    private int StrongAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            13);

    private int CurseAttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            8);

    private int CurseStampDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            9);

    // Original Misfortune for 2 turns (3 on hard), as BigotryGate's Disaster
    // Shot: the mod's Calamity lasts 2 turns, so hard adds layers.
    private int CurseStampCalamity =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            30,
            20);

    internal override int MoveTableVariantCount => 2;

    internal override HbrgoMoveTables GetMoveTables(int variant) =>
        variant == CurseVariant ? CurseTables : DefaultTables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            AttackMoveId,
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        yield return new MoveState(
            StrongAttackMoveId,
            StrongAttackMove,
            new SingleAttackIntent(() => StrongAttackDamage));
        yield return new MoveState(
            RecoveryMoveId,
            RecoveryMove,
            new HealIntent());
        yield return new MoveState(
            CurseAttackMoveId,
            CurseAttackMove,
            new SingleAttackIntent(() => CurseAttackDamage));
        yield return new MoveState(
            CurseStampMoveId,
            CurseStampMove,
            new SingleAttackIntent(() => CurseStampDamage),
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

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task StrongAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(StrongAttackDamage);
    }

    private async Task RecoveryMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(RecoveryDpRatio);
    }

    private Task CurseAttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(CurseAttackDamage);
    }

    private async Task CurseStampMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(CurseStampDamage);
        await ApplyCurseStamp(targets);
    }

    // Calamity only lowers HBR attributes and enemy E, so it is applied where
    // HBR numerics run.
    private async Task ApplyCurseStamp(IEnumerable<Creature> targets)
    {
        foreach (Creature target in targets.Where(target =>
                     target.IsAlive && HbrBattleStatsHelper.IsEnabled(target)))
        {
            await PowerCmd.Apply<HbrCalamityPower>(
                new ThrowingPlayerChoiceContext(),
                target,
                CurseStampCalamity,
                Creature,
                null);
        }
    }

    private async Task AttackUpMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await HbrEnemyEffects.ApplyTimedBuff<HbrAttackUpPower>(Creature, AttackUpPercent);
    }

    private async Task HealMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(CurseHealDpRatio);
    }

    Task IHbrgoPhantomAttackResults.AfterPhantomAttack(
        MoveState move,
        IReadOnlyList<Creature> attackedTargets,
        IReadOnlyList<Creature> passedTargets,
        decimal passedDamage)
    {
        return move?.Id == CurseStampMoveId
            ? ApplyCurseStamp(attackedTargets)
            : Task.CompletedTask;
    }
}
