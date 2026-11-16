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

// HBR 针嘴鸟, an elite: original NeedleBirdSmallVirtual_MC02 (MC02_20_Battle01).
// Moves follow its original dp/hp tables (docs/hbr-monster-mechanics-proposal.md):
// Charge, then Ruin Bullet empties the target's DP while still charged. A stun
// or DP break interrupts the charge, without turning DP break into a stun.
public sealed class NeedleBird : HbrgoMoveTableMonster
{
    internal const string AttackSkill = "NeedleBird_AttackSingle01";
    internal const string MurderBladeSkill = "NeedleBird_AttackAll01";
    internal const string ChargeSkill = "NeedleBird_Charge01";
    internal const string RuinBulletSkill = "NeedleBird_AttackSingle02_Braek";
    internal const string RecoverSkill = "NeedleBird_Buff01";

    internal const string PeckMoveId = "PECK_MOVE";
    internal const string MurderBladeMoveId = "MURDER_BLADE_MOVE";
    internal const string ChargeMoveId = "CHARGE_MOVE";
    internal const string RuinBulletMoveId = "RUIN_BULLET_MOVE";
    internal const string RecoverMoveId = "RECOVER_MOVE";

    // Original HealDp 700 over the original maximum DP 27800.
    internal const decimal RecoverDpRatio = 700m / 27800m;

    internal static readonly HbrgoMoveTables Tables = new(
        "NeedleBirdSmallVirtual_MC02",
        Table("NeedleBirdSmall",
            Step(AttackSkill),
            Step(ChargeSkill),
            Step(RuinBulletSkill, EnemyChargeCondition),
            Pick((AttackSkill, 60), (MurderBladeSkill, 40)),
            Pick((AttackSkill, 40), (MurderBladeSkill, 60))),
        Table("NeedleBirdSmall_break",
            Pick((AttackSkill, 40), (MurderBladeSkill, 60)),
            Step(ChargeSkill),
            Step(RuinBulletSkill, EnemyChargeCondition),
            Pick((AttackSkill, 60), (MurderBladeSkill, 40)),
            Step(ChargeSkill),
            Step(RuinBulletSkill, EnemyChargeCondition),
            Step(RecoverSkill)));

    private static readonly HbrgoTableMove[] Moves =
    {
        new(AttackSkill, PeckMoveId, HbrgoMoveTarget.Single),
        new(MurderBladeSkill, MurderBladeMoveId, HbrgoMoveTarget.All, SpiritDefense: true),
        new(ChargeSkill, ChargeMoveId, HbrgoMoveTarget.Self),
        new(RuinBulletSkill, RuinBulletMoveId, HbrgoMoveTarget.Single),
        new(RecoverSkill, RecoverMoveId, HbrgoMoveTarget.Self),
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            86,
            82);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            90,
            86);

    private int PeckDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            17,
            14);

    private int MurderBladeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            19,
            16);

    internal override HbrgoMoveTables GetMoveTables(int variant) => Tables;

    internal override IReadOnlyList<HbrgoTableMove> TableMoves => Moves;

    internal override IEnumerable<MoveState> CreateTableMoveStates()
    {
        yield return new MoveState(
            PeckMoveId,
            PeckMove,
            new SingleAttackIntent(() => PeckDamage));
        yield return new MoveState(
            MurderBladeMoveId,
            MurderBladeMove,
            new SingleAttackIntent(() => MurderBladeDamage));
        yield return new MoveState(
            ChargeMoveId,
            ChargeMove,
            new BuffIntent());
        yield return new MoveState(
            RuinBulletMoveId,
            RuinBulletMove,
            new DebuffIntent(strong: true));
        yield return new MoveState(
            RecoverMoveId,
            RecoverMove,
            new HealIntent());
    }

    private Task PeckMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(PeckDamage);
    }

    // Original all-unit attack: every player-side unit, summons included.
    private Task MurderBladeMove(IReadOnlyList<Creature> _)
    {
        return PerformAttackOn(MurderBladeDamage, 1, HbrgoAttackTargets.AllUnits);
    }

    private async Task ChargeMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await HbrEnemyEffects.ApplyCharge(Creature);
    }

    // No damage: the target's DP is emptied (a phantom halves an enemy's).
    private async Task RuinBulletMove(IReadOnlyList<Creature> targets)
    {
        // A DP break or stun can remove Charge after this move was selected.
        // Preserve the saved AI cursor/follow-up; do not execute a stale charge.
        if (!Creature.HasPower<HbrEnemyChargePower>()) return;
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        foreach (Creature target in HbrEnemyEffects.SingleTargets(Creature, targets))
        {
            await HbrEnemyEffects.BreakDp(new ThrowingPlayerChoiceContext(), target, Creature);
        }

        await HbrEnemyEffects.RemoveCharge(Creature);
    }

    private async Task RecoverMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        RestoreDpRatio(RecoverDpRatio);
    }
}
