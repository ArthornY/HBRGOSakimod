using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 针嘴鸟 (NeedleBirdSmall), an elite. Pecks, charges, then pierces; the
// pierce strikes harder against a target that still has DP (its original
// "BreakAttack" effect).
public sealed class NeedleBird : HbrgoStaticMonster
{
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

    private int PeckHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            4);

    private int ChargeBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            12,
            10);

    private int BreakPierceDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            17,
            15);

    private int BreakPierceDpBonus =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            6);

    internal const int PeckDamage = 3;
    internal const int ChargeStrength = 3;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState peck = new(
            "PECK_MOVE",
            PeckMove,
            new MultiAttackIntent(PeckDamage, () => PeckHits));
        MoveState charge = new(
            "CHARGE_MOVE",
            ChargeMove,
            new BuffIntent(),
            new DefendIntent());
        MoveState breakPierce = new(
            "BREAK_PIERCE_MOVE",
            BreakPierceMove,
            new SingleAttackIntent(
                () => BreakPierceDamage + BreakPierceDpBonus));
        return ModMonsterMoveStateMachines.Cycle(peck, charge, breakPierce);
    }

    private Task PeckMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(PeckDamage, PeckHits);
    }

    private async Task ChargeMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            ChargeStrength,
            Creature,
            null);
        await CreatureCmd.GainBlock(
            Creature,
            ChargeBlock,
            ValueProp.Move,
            null);
    }

    private async Task BreakPierceMove(IReadOnlyList<Creature> targets)
    {
        List<Creature> targetsWithDp = targets
            .Where(target => target.IsAlive && HasActiveDp(target))
            .ToList();

        await PerformAttack(BreakPierceDamage);
        if (targetsWithDp.Count == 0)
        {
            return;
        }

        await CreatureCommandCompatibility.Damage(
            new ThrowingPlayerChoiceContext(),
            targetsWithDp.Where(target => target.IsAlive),
            BreakPierceDpBonus,
            ValueProp.Move,
            Creature);
    }
}
