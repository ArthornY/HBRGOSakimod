using HBRGOSakiMod.Core.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class RedCrimsonBossMonster : HbrgoStaticMonster
{
    private const string WorldsEndSuperNovaOneMoveId =
        "WORLDS_END_SUPER_NOVA_1_MOVE";
    private const string WorldsEndSuperNovaTwoMoveId =
        "WORLDS_END_SUPER_NOVA_2_MOVE";

    private bool _hasAppliedStartingIntangible;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            530,
            500);

    public override int MaxInitialHp => MinInitialHp;

    private int DisappearRayDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            15,
            12);

    private int SuperNovaHitCount =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    protected override string VisualsPath =>
        SceneHelper.GetScenePath(
            "creature_visuals/red_crimson_boss");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasAppliedStartingIntangible
    {
        get => _hasAppliedStartingIntangible;
        private set
        {
            AssertMutable();
            _hasAppliedStartingIntangible = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (HasAppliedStartingIntangible)
        {
            return;
        }

        HasAppliedStartingIntangible = true;
        if (!Creature.HasPower<IntangiblePower>())
        {
            await PowerCmd.Apply<IntangiblePower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                99,
                Creature,
                null);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState weakeningEndOne = new(
            "WEAKENING_END_1_MOVE",
            WeakeningEndMove,
            new BuffIntent());
        MoveState bloodRain = new(
            "BLOOD_RAIN_MOVE",
            BloodRainMove,
            new MultiAttackIntent(2, 5));
        MoveState superNovaOne = new(
            WorldsEndSuperNovaOneMoveId,
            WorldsEndSuperNovaMove,
            new MultiAttackIntent(7, SuperNovaHitCount));
        MoveState stunOne = new(
            "STUN_1_MOVE",
            _ => Task.CompletedTask,
            new StunIntent());
        MoveState weakeningEndTwo = new(
            "WEAKENING_END_2_MOVE",
            WeakeningEndMove,
            new BuffIntent());
        MoveState disappearRay = new(
            "DISAPPEAR_RAY_MOVE",
            DisappearRayMove,
            new SingleAttackIntent(() => DisappearRayDamage),
            new DebuffIntent());
        MoveState superNovaTwo = new(
            WorldsEndSuperNovaTwoMoveId,
            WorldsEndSuperNovaMove,
            new MultiAttackIntent(7, SuperNovaHitCount));
        MoveState stunTwo = new(
            "STUN_2_MOVE",
            _ => Task.CompletedTask,
            new StunIntent());

        return ModMonsterMoveStateMachines.Cycle(
            superNovaOne,
            stunOne,
            weakeningEndOne,
            disappearRay,
            superNovaTwo,
            stunTwo,
            bloodRain,
            weakeningEndTwo);
    }

    internal Task AfterPhantomIntentPerformed(MoveState move)
    {
        return move?.Id is WorldsEndSuperNovaOneMoveId or
            WorldsEndSuperNovaTwoMoveId
                ? PowerCmd.Remove<IntangiblePower>(Creature)
                : Task.CompletedTask;
    }

    internal Task BeforePhantomIntentPerformed(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        // Disappear Ray's Vulnerable must affect this attack, as it does in
        // the native move. Replaying the native move would also attack twice.
        return move?.Id == "DISAPPEAR_RAY_MOVE"
            ? ApplyDisappearRayDebuffs(targets)
            : Task.CompletedTask;
    }

    private async Task WeakeningEndMove(IReadOnlyList<Creature> _)
    {
        PlayerChoiceContext choiceContext =
            new ThrowingPlayerChoiceContext();
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<IntangiblePower>(
            choiceContext,
            Creature,
            99,
            Creature,
            null);
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Creature,
            3,
            Creature,
            null);
    }

    private Task BloodRainMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(2, 5);
    }

    private async Task DisappearRayMove(
        IReadOnlyList<Creature> targets)
    {
        await ApplyDisappearRayDebuffs(targets);
        await PerformAttack(DisappearRayDamage);
    }

    private async Task ApplyDisappearRayDebuffs(
        IReadOnlyList<Creature> targets)
    {
        List<Creature> livingTargets = targets
            .Where(target => target.IsAlive)
            .ToList();
        PlayerChoiceContext choiceContext =
            new ThrowingPlayerChoiceContext();
        await HbrBossDebuffApplication.ApplyDefenseDown(
            choiceContext,
            livingTargets,
            2,
            Creature,
            null);
        await HbrBossDebuffApplication.ApplyAttackDown(
            choiceContext,
            livingTargets,
            2,
            Creature,
            null);
    }

    private async Task WorldsEndSuperNovaMove(
        IReadOnlyList<Creature> _)
    {
        await PerformAttack(7, SuperNovaHitCount);
        await PowerCmd.Remove<IntangiblePower>(Creature);
    }
}
