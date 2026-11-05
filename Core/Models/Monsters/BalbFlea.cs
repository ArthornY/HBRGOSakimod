using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

// HBR 灯胆跳虫 (BalbFleaDefault). Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class BalbFlea : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string FlashMoveId = "FLASH_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            56,
            52);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            60,
            56);

    private int FlashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            9);

    internal const int FlashAttackDown = 1;

    private int RamDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            15,
            14);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState flash = new(
            FlashMoveId,
            FlashMove,
            new SingleAttackIntent(() => FlashDamage),
            new DebuffIntent());
        MoveState ram = new(
            "RAM_MOVE",
            RamMove,
            new SingleAttackIntent(() => RamDamage));
        return ModMonsterMoveStateMachines.Cycle(
            flash,
            ram);
    }

    private async Task FlashMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(FlashDamage);
        await ApplyFlash(targets);
    }

    private Task ApplyFlash(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyAttackDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            FlashAttackDown,
            Creature);
    }

    private Task RamMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(RamDamage);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id switch
        {
            FlashMoveId => ApplyFlash(targets),
            _ => Task.CompletedTask
        };
    }
}
