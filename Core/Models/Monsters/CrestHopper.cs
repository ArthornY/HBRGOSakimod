using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class CrestHopper : HbrgoStaticMonster
{
    private bool _isSwarmMember;

    // Set by CrestHopperSwarmNormal before the creature is created, so the
    // native HP roll reads the lower range. Three full CrestHoppers would far
    // exceed an ordinary act-one fight's total HP.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsSwarmMember
    {
        get => _isSwarmMember;
        internal set
        {
            AssertMutable();
            _isSwarmMember = value;
        }
    }

    public override int MinInitialHp => IsSwarmMember
        ? AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            28,
            26)
        : AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            48,
            46);

    public override int MaxInitialHp => IsSwarmMember
        ? AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            32,
            30)
        : AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            52,
            50);

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int ComboHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            5);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState attack = new(
            "ATTACK_MOVE",
            AttackMove,
            new SingleAttackIntent(() => AttackDamage));
        MoveState combo = new(
            "COMBO_MOVE",
            ComboMove,
            new MultiAttackIntent(2, () => ComboHits));
        return ModMonsterMoveStateMachines.Cycle(attack, combo);
    }

    private Task AttackMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage);
    }

    private Task ComboMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(2, ComboHits);
    }
}
