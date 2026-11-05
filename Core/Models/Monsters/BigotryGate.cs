using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
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

// HBR 异议之门 (BigotryGateSmall). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class BigotryGate : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string EchoMoveId = "ECHO_MOVE";

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

    private int DoorGuardBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            17,
            14);

    private int RamDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            20,
            18);

    private int EchoDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    private int EchoHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            2,
            2);

    internal const int EchoDefenseDown = 1;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState doorGuard = new(
            "DOOR_GUARD_MOVE",
            DoorGuardMove,
            new DefendIntent());
        MoveState ram = new(
            "RAM_MOVE",
            RamMove,
            new SingleAttackIntent(() => RamDamage));
        MoveState echo = new(
            EchoMoveId,
            EchoMove,
            new MultiAttackIntent(EchoDamage, () => EchoHits),
            new DebuffIntent());
        return ModMonsterMoveStateMachines.Cycle(
            doorGuard,
            ram,
            echo);
    }

    private async Task DoorGuardMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await CreatureCmd.GainBlock(
            Creature,
            DoorGuardBlock,
            ValueProp.Move,
            null);
    }

    private Task RamMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(RamDamage);
    }

    private async Task EchoMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(EchoDamage, EchoHits);
        await ApplyEcho(targets);
    }

    private Task ApplyEcho(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            EchoDefenseDown,
            Creature);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id switch
        {
            EchoMoveId => ApplyEcho(targets),
            _ => Task.CompletedTask
        };
    }
}
