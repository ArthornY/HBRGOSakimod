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
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 象牙异蛛 (IvoryScooperDefault), an act-one boss. Scoops, entangles a
// target to lower its attack, breaks the ground, then hides behind its ivory
// plating, gaining block and restoring DP.
public sealed class IvoryScooper : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string EntangleMoveId = "ENTANGLE_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            192,
            180);

    public override int MaxInitialHp => MinInitialHp;

    private int ScoopDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            12);

    private int EntangleDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    private int GroundBreakDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int IvoryGuardBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            18,
            14);

    internal const int EntangleAttackDown = 2;
    internal const int GroundBreakHits = 3;
    // Fixed DP recovery in native units; the HBR resource boundary scales it.
    internal const decimal IvoryGuardDp = 25m;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState scoop = new(
            "SCOOP_MOVE",
            ScoopMove,
            new SingleAttackIntent(() => ScoopDamage));
        MoveState entangle = new(
            EntangleMoveId,
            EntangleMove,
            new SingleAttackIntent(() => EntangleDamage),
            new DebuffIntent());
        MoveState groundBreak = new(
            "GROUND_BREAK_MOVE",
            GroundBreakMove,
            new MultiAttackIntent(GroundBreakDamage, GroundBreakHits));
        MoveState ivoryGuard = new(
            "IVORY_GUARD_MOVE",
            IvoryGuardMove,
            new DefendIntent(),
            new HealIntent());
        return ModMonsterMoveStateMachines.Cycle(
            scoop,
            entangle,
            groundBreak,
            ivoryGuard);
    }

    private Task ScoopMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(ScoopDamage);
    }

    private async Task EntangleMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(EntangleDamage);
        await ApplyEntangle(targets);
    }

    private Task GroundBreakMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(GroundBreakDamage, GroundBreakHits);
    }

    private async Task IvoryGuardMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        Creature.GetPower<HighDimensionalShellPower>()?
            .Restore(HbrResourceValueHelper.Fixed(Creature, IvoryGuardDp));
        await CreatureCmd.GainBlock(
            Creature,
            IvoryGuardBlock,
            ValueProp.Move,
            null);
    }

    private Task ApplyEntangle(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyAttackDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            EntangleAttackDown,
            Creature);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id == EntangleMoveId
            ? ApplyEntangle(targets)
            : Task.CompletedTask;
    }
}
