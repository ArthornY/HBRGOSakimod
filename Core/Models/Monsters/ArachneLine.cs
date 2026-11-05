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

// HBR 阿拉克尼巨蛛 (ArachneLineDefault), act-three boss. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class ArachneLine : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string SilkBindMoveId = "SILK_BIND_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            450,
            420);

    public override int MaxInitialHp => MinInitialHp;

    private int SilkBindDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14,
            12);

    internal const int SilkBindAttackDown = 2;

    private int EightSlashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    private int EightSlashHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            6);

    private int WebWeaveBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            30,
            25);

    // Fixed DP recovery in native units; the HBR resource boundary scales it.
    internal const decimal WebWeaveDp = 40m;

    private int SeverDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            40,
            36);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState silkBind = new(
            SilkBindMoveId,
            SilkBindMove,
            new SingleAttackIntent(() => SilkBindDamage),
            new DebuffIntent());
        MoveState eightSlash = new(
            "EIGHT_SLASH_MOVE",
            EightSlashMove,
            new MultiAttackIntent(EightSlashDamage, () => EightSlashHits));
        MoveState webWeave = new(
            "WEB_WEAVE_MOVE",
            WebWeaveMove,
            new DefendIntent(),
            new HealIntent());
        MoveState sever = new(
            "SEVER_MOVE",
            SeverMove,
            new SingleAttackIntent(() => SeverDamage));
        return ModMonsterMoveStateMachines.Cycle(
            silkBind,
            eightSlash,
            webWeave,
            sever);
    }

    private async Task SilkBindMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(SilkBindDamage);
        await ApplySilkBind(targets);
    }

    private Task ApplySilkBind(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyAttackDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            SilkBindAttackDown,
            Creature);
    }

    private Task EightSlashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(EightSlashDamage, EightSlashHits);
    }

    private async Task WebWeaveMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        Creature.GetPower<HighDimensionalShellPower>()?
            .Restore(HbrResourceValueHelper.Fixed(Creature, WebWeaveDp));
        await CreatureCmd.GainBlock(
            Creature,
            WebWeaveBlock,
            ValueProp.Move,
            null);
    }

    private Task SeverMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(SeverDamage);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id switch
        {
            SilkBindMoveId => ApplySilkBind(targets),
            _ => Task.CompletedTask
        };
    }
}
