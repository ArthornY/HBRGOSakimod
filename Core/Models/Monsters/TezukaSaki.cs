using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class TezukaSaki : HbrgoStaticMonster
{
    private const string LingxiaoHammerMoveId = "LINGXIAO_HAMMER_MOVE";

    internal const string FocusedBreakthroughMoveId =
        "FOCUSED_BREAKTHROUGH_MOVE";

    private int _lingxiaoHammerUses;
    private int _focusedBreakthroughUses;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            110,
            100);

    public override int MaxInitialHp => MinInitialHp;

    private int AttackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10,
            9);

    protected override string VisualsPath =>
        SceneHelper.GetScenePath("creature_visuals/tezuka_saki");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LingxiaoHammerUses
    {
        get => _lingxiaoHammerUses;
        private set
        {
            AssertMutable();
            _lingxiaoHammerUses = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FocusedBreakthroughUses
    {
        get => _focusedBreakthroughUses;
        private set
        {
            AssertMutable();
            _focusedBreakthroughUses = Math.Max(0, value);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState lingxiaoHammer = new(
            LingxiaoHammerMoveId,
            LingxiaoHammerMove,
            new AdaptiveMultiAttackIntent(
                AttackDamage,
                () => Creature.HasPower<GhostStatePower>() ? 2 : 1),
            new DebuffIntent());
        MoveState focusedBreakthrough = new(
            FocusedBreakthroughMoveId,
            FocusedBreakthroughMove,
            new SingleAttackIntent(() => AttackDamage),
            new DebuffIntent());
        MoveState charge = new(
            "CHARGE_MOVE",
            ChargeMove,
            new BuffIntent());
        MoveState threeGunFrenzy = new(
            "THREE_GUN_FRENZY_MOVE",
            ThreeGunFrenzyMove,
            new MultiAttackIntent(AttackDamage, 3));
        return ModMonsterMoveStateMachines.Cycle(
            lingxiaoHammer,
            focusedBreakthrough,
            charge,
            threeGunFrenzy);
    }

    internal Task AfterPhantomIntentPerformed(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        // Phantom attacks resolve their damage separately, but retain the move's effects.
        Creature[] livingOpponents = targets
            .Where(target => target.IsAlive && target.Side != Creature.Side)
            .ToArray();
        switch (move?.Id)
        {
            case LingxiaoHammerMoveId:
                LingxiaoHammerUses++;
                return ApplyLingxiaoHammerDebuff(livingOpponents);
            case FocusedBreakthroughMoveId:
                FocusedBreakthroughUses++;
                return ApplyFocusedBreakthroughDebuff(livingOpponents);
            default:
                return Task.CompletedTask;
        }
    }

    private async Task LingxiaoHammerMove(
        IReadOnlyList<Creature> targets)
    {
        LingxiaoHammerUses++;
        int hitCount = Creature.HasPower<GhostStatePower>() ? 2 : 1;
        await PerformAttack(AttackDamage, hitCount);

        await ApplyLingxiaoHammerDebuff(targets);
    }

    private async Task ApplyLingxiaoHammerDebuff(IReadOnlyList<Creature> targets)
    {
        if (LingxiaoHammerUses % 2 == 0)
        {
            await PowerCmd.Apply<DexterityPower>(
                new ThrowingPlayerChoiceContext(),
                targets,
                -1,
                Creature,
                null);
        }
        else
        {
            await PowerCmd.Apply<FrailPower>(
                new ThrowingPlayerChoiceContext(),
                targets,
                2,
                Creature,
                null);
        }
    }

    private async Task FocusedBreakthroughMove(
        IReadOnlyList<Creature> targets)
    {
        FocusedBreakthroughUses++;
        await PerformAttack(AttackDamage);

        await ApplyFocusedBreakthroughDebuff(targets);
    }

    private async Task ApplyFocusedBreakthroughDebuff(IReadOnlyList<Creature> targets)
    {
        if (FocusedBreakthroughUses % 2 == 0)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                targets,
                -1,
                Creature,
                null);
        }
        else
        {
            await PowerCmd.Apply<VulnerablePower>(
                new ThrowingPlayerChoiceContext(),
                targets,
                2,
                Creature,
                null);
        }
    }

    private async Task ChargeMove(IReadOnlyList<Creature> _)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            2,
            Creature,
            null);
    }

    private Task ThreeGunFrenzyMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(AttackDamage, 3);
    }

    private sealed class AdaptiveMultiAttackIntent : MultiAttackIntent
    {
        public AdaptiveMultiAttackIntent(
            int damage,
            Func<int> repeatCalc)
            : base(damage, repeatCalc)
        {
        }

        protected override LocString IntentLabelFormat =>
            new(
                "intents",
                Repeats > 1
                    ? "FORMAT_DAMAGE_MULTI"
                    : "FORMAT_DAMAGE_SINGLE");

        public override LocString GetIntentLabel(
            IEnumerable<Creature> targets,
            Creature owner)
        {
            LocString label = IntentLabelFormat;
            label.Add("Damage", GetSingleDamage(targets, owner));
            if (Repeats > 1)
            {
                label.Add("Repeat", Repeats);
            }

            return label;
        }
    }
}
