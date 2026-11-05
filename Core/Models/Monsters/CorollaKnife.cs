using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 花冠利刃 (CorollaKnifeSmall). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class CorollaKnife : HbrgoStaticMonster
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            83,
            78);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            87,
            82);

    private int SlashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            17,
            15);

    private int PetalDanceDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int PetalDanceHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            4,
            4);

    internal const int WhetStrength = 3;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState slash = new(
            "SLASH_MOVE",
            SlashMove,
            new SingleAttackIntent(() => SlashDamage));
        MoveState petalDance = new(
            "PETAL_DANCE_MOVE",
            PetalDanceMove,
            new MultiAttackIntent(PetalDanceDamage, () => PetalDanceHits));
        MoveState whet = new(
            "WHET_MOVE",
            WhetMove,
            new BuffIntent());
        return ModMonsterMoveStateMachines.Cycle(
            slash,
            petalDance,
            whet);
    }

    private Task SlashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(SlashDamage);
    }

    private Task PetalDanceMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(PetalDanceDamage, PetalDanceHits);
    }

    private async Task WhetMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            WhetStrength,
            Creature,
            null);
    }
}
