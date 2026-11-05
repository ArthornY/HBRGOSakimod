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

// HBR 长颈怪环 (GiraffeHaloDefault), elite. Generated from the act two/three roster table; see
// docs/hbr-act-rosters.md for the design and calibration.
public sealed class GiraffeHalo : HbrgoStaticMonster, IHbrgoPhantomAttackEffects
{
    internal const string HaloMoveId = "HALO_MOVE";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            256,
            240);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            266,
            250);

    private int HaloDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            12,
            10);

    internal const int HaloDefenseDown = 2;

    private int NeckLashDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            27,
            24);

    private int HaloShotsDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private int HaloShotsHits =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            5);

    internal const int PrayerStrength = 3;

    private int PrayerBlock =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            22,
            18);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState halo = new(
            HaloMoveId,
            HaloMove,
            new SingleAttackIntent(() => HaloDamage),
            new DebuffIntent());
        MoveState neckLash = new(
            "NECK_LASH_MOVE",
            NeckLashMove,
            new SingleAttackIntent(() => NeckLashDamage));
        MoveState haloShots = new(
            "HALO_SHOTS_MOVE",
            HaloShotsMove,
            new MultiAttackIntent(HaloShotsDamage, () => HaloShotsHits));
        MoveState prayer = new(
            "PRAYER_MOVE",
            PrayerMove,
            new BuffIntent(),
            new DefendIntent());
        return ModMonsterMoveStateMachines.Cycle(
            halo,
            neckLash,
            haloShots,
            prayer);
    }

    private async Task HaloMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(HaloDamage);
        await ApplyHalo(targets);
    }

    private Task ApplyHalo(IEnumerable<Creature> targets)
    {
        return HbrPercentDebuffApplication.ApplyDefenseDown(
            new ThrowingPlayerChoiceContext(),
            targets.Where(target => target.IsAlive).ToArray(),
            HaloDefenseDown,
            Creature);
    }

    private Task NeckLashMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(NeckLashDamage);
    }

    private Task HaloShotsMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(HaloShotsDamage, HaloShotsHits);
    }

    private async Task PrayerMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            PrayerStrength,
            Creature,
            null);
        await CreatureCmd.GainBlock(
            Creature,
            PrayerBlock,
            ValueProp.Move,
            null);
    }

    Task IHbrgoPhantomAttackEffects.AfterPhantomAttackIntent(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        return move?.Id switch
        {
            HaloMoveId => ApplyHalo(targets),
            _ => Task.CompletedTask
        };
    }
}
