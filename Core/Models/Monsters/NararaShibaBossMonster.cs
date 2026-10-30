using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using VoidStatusCard = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace HBRGOSakiMod.Core.Models.Monsters;

public sealed class NararaShibaBossMonster : HbrgoStaticMonster
{
    public const string GoodSleepMoveId = "GOOD_SLEEP_MOVE";
    public const string DoNotWantToWorkMoveId =
        "DO_NOT_WANT_TO_WORK_MOVE";
    public const string LotsOfMoneyMoveId = "LOTS_OF_MONEY_MOVE";
    public const string WorkMoveId = "WORK_MOVE";
    public const string UpdateMoveId = "UPDATE_MOVE";
    public const string OffWorkMoveId = "OFF_WORK_MOVE";

    private const int StartingSleep = 5;
    private const int WorkDamage = 12;
    private const int NormalWorkHitCount = 6;
    private const int AscendedWorkHitCount = 7;
    private const int AscendedWorkLevel = 9;
    private const int NonHbrgoSakiHpDivisor = 10;
    internal const int MaximumDpLayers = 3;

    private static readonly Type[] WorkStatusTypes =
    {
        typeof(Toxic),
        typeof(Infection),
        typeof(Wound),
        typeof(VoidStatusCard),
        typeof(Dazed),
        typeof(Wither)
    };
    private static readonly PileType[] WorkStatusPiles =
    {
        PileType.Draw,
        PileType.Discard,
        PileType.Hand
    };
    private static int WorkStatusCardCount =>
        WorkStatusTypes.Length * WorkStatusPiles.Length;

    private MoveState _awakeStartState;
    private bool _hasInitializedStartingPowers;

    // Event combat freezes this value before Creature construction. Old saves
    // and non-event summons without a snapshot retain their previous fallback.
    public override int MinInitialHp => InitialFloor > 0 ? InitialFloor : NararaShibaFloorSync.CurrentFloor;

    public override int MaxInitialHp => MinInitialHp;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int InitialFloor { get; internal set; }

    protected override string VisualsPath =>
        SceneHelper.GetScenePath("creature_visuals/narara_shiba");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasInitializedStartingPowers
    {
        get => _hasInitializedStartingPowers;
        private set
        {
            AssertMutable();
            _hasInitializedStartingPowers = value;
        }
    }

    private decimal _exactDpLayerMaximum;
    private bool _hasExactDpLayerMaximum;

    // Keep the old integer property readable for existing saved monster data.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DpLayerMaximum
    {
        get => DpAmountCodec.ToNative(ExactDpLayerMaximum);
        private set
        {
            _exactDpLayerMaximum = Math.Max(0m, value);
            _hasExactDpLayerMaximum = true;
        }
    }

    internal decimal ExactDpLayerMaximum
    {
        get => _hasExactDpLayerMaximum ? _exactDpLayerMaximum : 0m;
        private set
        {
            AssertMutable();
            _exactDpLayerMaximum = BigCombatNumbers.ClampStat(value);
            _hasExactDpLayerMaximum = true;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string DpLayerMaximumData
    {
        get => _hasExactDpLayerMaximum ? DpAmountCodec.Format(_exactDpLayerMaximum) : string.Empty;
        private set
        {
            if (string.IsNullOrEmpty(value)) return;
            _exactDpLayerMaximum = DpAmountCodec.Parse(value);
            _hasExactDpLayerMaximum = true;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DpLayersRemaining { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastDpBreakRound { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool LastDpTurnEndTriggered { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TriggeredHpThresholdsMask { get; private set; }

    internal bool CanBreakDpLayerThisRound =>
        LastDpBreakRound != Math.Max(1, CombatState?.RoundNumber ?? 1);

    internal void InitializeDpLayers()
    {
        AssertMutable();
        if (ExactDpLayerMaximum > 0m) return;
        // Keep the initial snapshot before permanent phantom DP loss; the
        // effective cap applies that loss once, including after resummoning.
        ExactDpLayerMaximum = NararaLayeredDpPower.CalculateUnadjustedInitialAmount(Creature);
        DpLayersRemaining = MaximumDpLayers;
    }

    internal void RecordDpLayerBreak()
    {
        AssertMutable();
        DpLayersRemaining = Math.Max(0, DpLayersRemaining - 1);
        LastDpBreakRound = Math.Max(1, CombatState?.RoundNumber ?? 1);
        if (DpLayersRemaining == 0 && !LastDpTurnEndTriggered &&
            Creature?.Side == CombatSide.Enemy)
        {
            LastDpTurnEndTriggered = true;
            NararaForcedTurnEndHelper.Request(Creature);
        }
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Creature || delta >= 0m || Creature?.Side != CombatSide.Enemy ||
            !Creature.IsAlive || LargeCreatureHealthHelper.GetMaxHp(Creature) <= 0) return Task.CompletedTask;

        int crossed = 0;
        for (int threshold = 1; threshold <= 3; threshold++)
        {
            // Decimal cross multiplication keeps 75/50/25% exact for odd HP
            // totals and avoids overflow or rounding away a threshold.
            if (LargeCreatureHealthHelper.GetCurrentHp(Creature) * 4m <=
                LargeCreatureHealthHelper.GetMaxHp(Creature) * (4 - threshold))
                crossed |= 1 << (threshold - 1);
        }
        int newThresholds = crossed & ~TriggeredHpThresholdsMask;
        if (newThresholds == 0) return Task.CompletedTask;

        AssertMutable();
        // Consume every crossed threshold together. Healing, restored DP and
        // extra turns never reset these once-per-combat milestones. If the
        // player turn is already ending, do not carry them into the next turn.
        TriggeredHpThresholdsMask |= newThresholds;
        NararaForcedTurnEndHelper.Request(Creature);
        return Task.CompletedTask;
    }

    internal bool EnsureRecoveredActiveDpLayer()
    {
        AssertMutable();
        if (DpLayersRemaining > 0) return false;
        // Partial phantom DP recovery revives the active layer without adding
        // reserves or resetting this round's break lock and combat milestones.
        DpLayersRemaining = 1;
        return true;
    }

    internal async Task RestoreOneDpLayer(PlayerChoiceContext choiceContext)
    {
        InitializeDpLayers();
        decimal effectiveMaximum = NararaLayeredDpPower.CalculateInitialAmount(Creature);
        int previousLayers = DpLayersRemaining;
        DpLayersRemaining = Math.Min(MaximumDpLayers, previousLayers + 1);
        if (Creature.GetPower<NararaLayeredDpPower>() is { } shell)
        {
            // A recovered reserve is full; do not also heal the active layer.
            if (previousLayers >= MaximumDpLayers)
                shell.Restore(effectiveMaximum);
            shell.RefreshLayerDisplay();
            return;
        }

        if (effectiveMaximum <= 0) return;

        // Removing Break normally regenerates an ordinary shell. Restore the
        // Shiba's own shell without resetting the current round's break lock.
        if (Creature.GetPower<BreakPower>() is { } breakPower)
            await PowerCmd.Remove(breakPower);
        if (Creature.GetPower<DestructionRatePower>() is { } destruction)
            await PowerCmd.Remove(destruction);
        await PowerCmd.Apply<NararaLayeredDpPower>(
            choiceContext, Creature, effectiveMaximum, Creature, null);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (HasInitializedStartingPowers)
        {
            return;
        }

        HasInitializedStartingPowers = true;
        bool hasReducedHp = !CombatState.Players.Any(
            player => player.Character is HbrgoSaki or HbrgoHotaru);
        if (hasReducedHp)
        {
            int reducedHp = Math.Max(
                1,
                MinInitialHp /
                NonHbrgoSakiHpDivisor);
            await CreatureCmd.SetMaxAndCurrentHp(Creature, reducedHp);
        }

        // Normalize HP before installing starting powers. The outer room
        // wrapper uses the same idempotent helper and will not multiply twice.
        EnemyHealthScalingHelper.ApplyInitialHealth(Creature);

        var choiceContext = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<TourGuidePower>(
            choiceContext,
            Creature,
            1,
            Creature,
            null);
        await PowerCmd.Apply<NararaOverworkPower>(
            choiceContext,
            Creature,
            1,
            Creature,
            null);
        await PowerCmd.Apply<NararaGiveYouPower>(
            choiceContext,
            Creature,
            1,
            Creature,
            null);
        await PowerCmd.Apply<NararaNullificationPower>(
            choiceContext, Creature, 1, Creature, null);
        await PowerCmd.Apply<NararaSleepingPower>(
            choiceContext,
            Creature,
            StartingSleep,
            Creature,
            null);

        // Allied phantom entry does not pass through the enemy shell hook.
        // Preserve its existing initial DP and plating at this entry point.
        if (Creature.Side != CombatSide.Enemy)
            await InitializeStartingDp(choiceContext);
    }

    internal async Task InitializeStartingDp(PlayerChoiceContext choiceContext)
    {
        AssertMutable();
        if (ExactDpLayerMaximum > 0m) return;

        // The combat-start hook runs after players select their opening
        // phantoms, so difficulty suppression is finalized before this snapshot.
        // Later HP growth, room callbacks and resleep never resize the cap.
        InitializeDpLayers();
        await PowerCmd.Apply<NararaLayeredDpPower>(
            choiceContext, Creature,
            NararaLayeredDpPower.CalculateInitialAmount(Creature), Creature, null);

        decimal shellAmount =
            Creature.GetPower<HighDimensionalShellPower>()?.CurrentDp ?? 0m;
        if (shellAmount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            Creature,
            Math.Min(shellAmount, BigCombatNumbers.NativeStatCap),
            Creature,
            null);
        Creature.GetPower<PlatingPower>()?.SetAmount(DpAmountCodec.ToNative(shellAmount));
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState goodSleep = Move(
            GoodSleepMoveId,
            GoodSleepMove,
            new SleepIntent(),
            new BuffIntent(),
            new HealIntent());
        _awakeStartState = Move(
            DoNotWantToWorkMoveId,
            DoNotWantToWorkMove,
            new DebuffIntent(strong: true));
        MoveState lotsOfMoney = Move(
            LotsOfMoneyMoveId,
            LotsOfMoneyMove,
            new SingleAttackIntent(() => GetLotsOfMoneyDamage()));
        MoveState work = Move(
            WorkMoveId,
            WorkMove,
            new MultiAttackIntent(WorkDamage, GetWorkHitCount),
            new StatusIntent(WorkStatusCardCount));
        MoveState update = Move(
            UpdateMoveId,
            UpdateMove,
            new BuffIntent());
        MoveState offWork = Move(
            OffWorkMoveId,
            OffWorkMove,
            new SleepIntent());
        var sleepRouter = new ConditionalBranchState("SLEEP_ROUTER");

        goodSleep.FollowUpState = sleepRouter;
        _awakeStartState.FollowUpState = lotsOfMoney;
        lotsOfMoney.FollowUpState = work;
        work.FollowUpState = update;
        update.FollowUpState = offWork;
        offWork.FollowUpState = sleepRouter;
        sleepRouter.AddState(
            goodSleep,
            () => Creature.HasPower<NararaSleepingPower>());
        sleepRouter.AddState(
            _awakeStartState,
            () => !Creature.HasPower<NararaSleepingPower>());

        return new MonsterMoveStateMachine(
            new MonsterState[]
            {
                sleepRouter,
                goodSleep,
                _awakeStartState,
                lotsOfMoney,
                work,
                update,
                offWork
            },
            goodSleep);
    }

    internal void ForceAwakeCycle()
    {
        if (_awakeStartState == null || Creature?.IsAlive != true)
        {
            return;
        }

        SetMoveImmediate(_awakeStartState, forceTransition: true);
    }

    private static MoveState Move(
        string id,
        Func<IReadOnlyList<Creature>, Task> action,
        params AbstractIntent[] intents)
    {
        return new MoveState(id, action, intents);
    }

    private async Task GoodSleepMove(IReadOnlyList<Creature> _)
    {
        var choiceContext = new ThrowingPlayerChoiceContext();
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.15f);
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Creature,
            3,
            Creature,
            null);
        await CreatureCmd.Heal(Creature, LargeCreatureHealthHelper.GetMaxHp(Creature) * 0.1m);
    }

    private async Task DoNotWantToWorkMove(
        IReadOnlyList<Creature> _)
    {
        // The enemy boss affects everyone else; its allied phantom must not
        // copy that friendly fire onto the summoner and other allied pets.
        Creature[] targets = (Creature.Side == CombatSide.Player ||
            Creature.HasPower<PhantomPower>()
                ? CombatState.GetOpponentsOf(Creature)
                    .Where(PhantomHelper.IsValidPhantomTarget)
                : CombatState.Creatures
                    .Where(creature => creature != Creature && creature.IsAlive &&
                        creature.Monster is not HotaruCannonModel))
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        var choiceContext = new ThrowingPlayerChoiceContext();
        await HbrPercentDebuffApplication.ApplyAttackDown(
            choiceContext,
            targets,
            99,
            Creature,
            null);
        await PowerCmd.Apply<FrailPower>(
            choiceContext,
            targets,
            99,
            Creature,
            null);
        await HbrPercentDebuffApplication.ApplyDefenseDown(
            choiceContext,
            targets,
            99,
            Creature,
            null);
    }

    private Task LotsOfMoneyMove(IReadOnlyList<Creature> _)
    {
        return PerformAttack(GetLotsOfMoneyDamage());
    }

    internal Task AfterPhantomIntentPerformed(
        MoveState move,
        IReadOnlyList<Creature> targets)
    {
        if (move?.Id == WorkMoveId && targets.Any(target =>
                PhantomHelper.IsValidPhantomTarget(target) &&
                target.Side != Creature.Side &&
                ReferenceEquals(target.CombatState, CombatState)))
        {
            PhantomPlayerBenefitContext.QueueDraw(WorkStatusCardCount);
        }

        return Task.CompletedTask;
    }

    private async Task WorkMove(IReadOnlyList<Creature> _)
    {
        await PerformAttack(WorkDamage, GetWorkHitCount());

        foreach (Player player in CombatState.Players.Where(
                     player => player.Creature.IsAlive))
        {
            foreach (PileType pile in WorkStatusPiles)
            {
                foreach (Type statusType in WorkStatusTypes)
                {
                    CardModel canonicalStatus = ModelDb.GetById<CardModel>(
                        ModelDb.GetId(statusType));
                    CardModel status = CombatState.CreateCard(
                        canonicalStatus,
                        player);
                    await CardPileCmd.AddGeneratedCardToCombat(
                        status,
                        pile,
                        null);
                }
            }
        }
    }

    private async Task UpdateMove(IReadOnlyList<Creature> _)
    {
        int statusCount = CombatState.Players.Sum(player =>
            player.PlayerCombatState.Hand.Cards.Count(
                card => card.Type == CardType.Status));
        if (statusCount <= 0)
        {
            return;
        }

        await CreatureCmd.SetMaxHp(
            Creature,
            LargeCreatureHealthHelper.GetMaxHp(Creature) + statusCount * 100m);
        var choiceContext = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Creature,
            statusCount,
            Creature,
            null);

        PlatingPower plating = Creature.GetPower<PlatingPower>();
        int previousPlating = plating?.Amount ?? 0;
        int platingGain = statusCount * 30;
        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            Creature,
            platingGain,
            Creature,
            null);
        Creature.GetPower<PlatingPower>()?.SetAmount(
            previousPlating + platingGain);
    }

    private async Task OffWorkMove(IReadOnlyList<Creature> _)
    {
        var choiceContext = new ThrowingPlayerChoiceContext();
        List<CardModel> statuses = CombatState.Players
            .SelectMany(player => player.PlayerCombatState.AllCards)
            .Where(card =>
                card.Type == CardType.Status &&
                !card.HasBeenRemovedFromState &&
                card.Pile?.Type != PileType.Exhaust)
            .ToList();
        foreach (CardModel status in statuses)
        {
            await CardExhaustRuntimeCompatibility.Exhaust(choiceContext, status);
        }

        if (statuses.Count <= 0)
        {
            return;
        }

        await PowerCmd.Apply<NararaSleepingPower>(
            choiceContext,
            Creature,
            statuses.Count,
            Creature,
            null);
        await RestoreOneDpLayer(choiceContext);
    }

    private int GetLotsOfMoneyDamage()
    {
        return Math.Max(
            0,
            CombatState?.Players
                .Where(player => player.Creature.IsAlive)
                .Select(player => player.Gold / 2)
                .DefaultIfEmpty(0)
                .Max() ?? 0);
    }

    private int GetWorkHitCount()
    {
        return CombatState?.RunState.AscensionLevel >=
            AscendedWorkLevel
                ? AscendedWorkHitCount
                : NormalWorkHitCount;
    }
}
