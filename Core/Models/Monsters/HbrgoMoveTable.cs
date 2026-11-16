using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Monsters;

// Who an original skill hits: Single (every player creature, as native
// monster attacks do), All (also the player side's summons; a phantom hits
// every enemy), MinimumDp (the player, or for a phantom the enemy, with the
// least DP), Self, or AllyAll (every unit on the monster's own side, itself
// included; a phantom's side is its summoner's).
internal enum HbrgoMoveTarget
{
    Single,
    All,
    Self,
    MinimumDp,
    AllyAll
}

// A phantom replays an attack intent's damage instead of running the move
// body. A move-table monster restores the move's statuses once per replayed
// intent: on every attacked opponent, or for chance-based ailments only on
// those whose damage passed block (proposal M8). passedDamage is the DP and HP
// the replay took, for an absorbing skill.
internal interface IHbrgoPhantomAttackResults
{
    Task AfterPhantomAttack(
        MoveState move,
        IReadOnlyList<Creature> attackedTargets,
        IReadOnlyList<Creature> passedTargets,
        decimal passedDamage);
}

// A tracked monster attack: the units it could hit (alive when it started),
// those whose damage passed block and are still alive, and the DP and HP the
// attack took from its units.
internal sealed record HbrgoAttackHits(
    IReadOnlyList<Creature> Units,
    IReadOnlyList<Creature> Passed,
    decimal PassedDamage);

// One row of an original hbr.quest move pattern: the skill label, its weight
// within the step, and its once/condition flags. Rows of one order form a step.
internal sealed record HbrgoMoveOption(string Skill, int Weight, bool Once, string Condition);

internal sealed record HbrgoMoveStep(IReadOnlyList<HbrgoMoveOption> Options);

internal sealed record HbrgoMoveTable(string Label, IReadOnlyList<HbrgoMoveStep> Steps)
{
    internal static readonly HbrgoMoveTable None = new(string.Empty, Array.Empty<HbrgoMoveStep>());

    internal bool IsEmpty => Steps.Count == 0;
}

// An original enemy label's two tables: dp while its shell holds, hp in Break.
internal sealed record HbrgoMoveTables(string EnemyLabel, HbrgoMoveTable Dp, HbrgoMoveTable Hp);

// The original skill behind a mod move, and what the move state cannot carry.
internal sealed record HbrgoTableMove(
    string Skill,
    string MoveId,
    HbrgoMoveTarget Target,
    bool SpiritDefense = false);

internal static class HbrgoMoveTableBuilder
{
    internal const string EnemyChargeCondition = "IsEnemyCharge()";

    internal static HbrgoMoveStep Step(string skill, string condition = "") =>
        new(new[] { new HbrgoMoveOption(skill, 100, false, condition) });

    internal static HbrgoMoveStep OnceStep(string skill) =>
        new(new[] { new HbrgoMoveOption(skill, 100, true, string.Empty) });

    internal static HbrgoMoveStep Pick(params (string Skill, int Weight)[] options) =>
        new(options.Select(option =>
            new HbrgoMoveOption(option.Skill, option.Weight, false, string.Empty)).ToArray());

    internal static HbrgoMoveStep PickOnce(params (string Skill, int Weight)[] options) =>
        new(options.Select(option =>
            new HbrgoMoveOption(option.Skill, option.Weight, true, string.Empty)).ToArray());

    internal static HbrgoMoveTable Table(string label, params HbrgoMoveStep[] steps) =>
        new(label, steps);
}

// The machine's only non-move state. Every table move follows up here, and
// the next move comes from the monster's saved table progress rather than
// from graph edges, so a regenerated machine continues where it was.
internal sealed class HbrgoMoveTableState : MonsterState
{
    internal const string StateId = "HBR_MOVE_TABLE";

    public override string Id => StateId;

    public override bool ShouldAppearInLogs => false;

    public override string GetNextState(Creature owner, Rng rng) =>
        owner?.Monster is HbrgoMoveTableMonster monster
            ? monster.RollTableMove(rng)
            : throw new InvalidOperationException(StateId + " needs a move-table monster.");

    public override void RegisterStates(Dictionary<string, MonsterState> monsterStates) =>
        monsterStates.Add(Id, this);
}

// A monster that follows its original dp/hp move tables (proposal M1-M3):
// weighted options per step, rolled with the native monster AI RNG; "once"
// steps; conditions; DP Break switches to the hp table from the move after the
// current intent, and the returning shell restarts the dp table with its used
// "once" steps skipped. Without an hp table the dp table simply continues.
public abstract partial class HbrgoMoveTableMonster : HbrgoStaticMonster
{
    internal const int DpTable = 0;
    internal const int HpTable = 1;

    // Native AssetPaths/GetIntents builds machines before Creature is bound,
    // including on mutable models. The public getter throws in that phase.
    private static readonly AccessTools.FieldRef<MonsterModel, Creature> ReadBoundCreature =
        AccessTools.FieldRefAccess<MonsterModel, Creature>("_creature");
    private protected Creature BoundCreature => ReadBoundCreature(this);

    private int _moveTable;
    private int _moveTableStep;
    private int _dpOnceUsed;
    private int _hpOnceUsed;
    private int _variant;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MoveTableIndex
    {
        get => _moveTable;
        protected set
        {
            AssertMutable();
            _moveTable = value == HpTable ? HpTable : DpTable;
        }
    }

    // Index of the next step to evaluate in the current table.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MoveTableStep
    {
        get => _moveTableStep;
        protected set
        {
            AssertMutable();
            _moveTableStep = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DpOnceUsedMask
    {
        get => _dpOnceUsed;
        protected set
        {
            AssertMutable();
            _dpOnceUsed = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HpOnceUsedMask
    {
        get => _hpOnceUsed;
        protected set
        {
            AssertMutable();
            _hpOnceUsed = value;
        }
    }

    // Which original variant's tables this copy uses; set by its encounter.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MoveTableVariant
    {
        get => _variant;
        internal set
        {
            AssertMutable();
            _variant = Math.Clamp(value, 0, Math.Max(0, MoveTableVariantCount - 1));
        }
    }

    internal virtual int MoveTableVariantCount => 1;

    internal abstract HbrgoMoveTables GetMoveTables(int variant);

    internal abstract IReadOnlyList<HbrgoTableMove> TableMoves { get; }

    // One native move per original skill, with the ids listed in TableMoves.
    internal abstract IEnumerable<MoveState> CreateTableMoveStates();

    internal HbrgoMoveTables MoveTables => GetMoveTables(MoveTableVariant);

    internal HbrgoMoveTable CurrentMoveTable =>
        MoveTableIndex == HpTable && !MoveTables.Hp.IsEmpty ? MoveTables.Hp : MoveTables.Dp;

    internal static HbrgoTableMove FindTableMove(MonsterModel monster, string moveId) =>
        monster is HbrgoMoveTableMonster table && !string.IsNullOrEmpty(moveId)
            ? table.TableMoves.FirstOrDefault(move => move.MoveId == moveId)
            : null;

    internal static HbrgoMoveTarget? MoveTargetOf(MonsterModel monster, string moveId) =>
        FindTableMove(monster, moveId)?.Target;

    protected sealed override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        HbrgoMoveTableState table = new();
        List<MonsterState> states = new() { table };
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (MoveState move in BuildFlavorAwareMoveStates())
        {
            move.FollowUpState = table;
            ids.Add(move.Id);
            states.Add(move);
        }

        foreach (HbrgoTableMove move in TableMoves)
        {
            if (!ids.Contains(move.MoveId))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} has no move {move.MoveId} for {move.Skill}.");
            }
        }

        return new MonsterMoveStateMachine(states, table);
    }

    internal string RollTableMove(Rng rng) => RollFlavorAwareTableMove(rng);

    private string RollTableMoveCore(Rng rng)
    {
        HbrgoMoveTable table = CurrentMoveTable;
        int count = table.Steps.Count;
        if (count == 0)
        {
            throw new InvalidOperationException(GetType().Name + " has an empty move table.");
        }

        bool hp = !ReferenceEquals(table, MoveTables.Dp);
        int start = MoveTableStep % count;
        for (int offset = 0; offset < count; offset++)
        {
            int index = (start + offset) % count;
            bool used = IsOnceUsed(hp, index);
            HbrgoMoveOption[] options = table.Steps[index].Options
                .Where(option => option.Weight > 0 && !(option.Once && used) &&
                    IsConditionMet(option.Condition) && IsOptionAvailable(option))
                .ToArray();
            if (options.Length == 0)
            {
                continue;
            }

            HbrgoMoveOption chosen = Choose(options, rng);
            MoveTableStep = (index + 1) % count;
            if (chosen.Once)
            {
                MarkOnceUsed(hp, index);
            }

            return GetMoveId(chosen.Skill);
        }

        // Every step is spent or waits for a condition. Repeat the table's
        // last option instead of stalling the native AI.
        return GetMoveId(table.Steps[^1].Options[0].Skill);
    }

    // Called by the shell when DP breaks. Breaking DP no longer stuns (user,
    // 2026-11-09): the current intent still goes ahead, and the move after it
    // comes from the hp table's first step. Without an hp table nothing changes.
    internal void OnShellBroken()
    {
        if (MoveTables.Hp.IsEmpty)
        {
            return;
        }

        MoveTableIndex = HpTable;
        MoveTableStep = 0;
        OnFlavorTablePhaseChanged();
    }

    // Called when a shell is applied. After Break the dp table restarts at
    // its first step; the initial shell at combat start changes nothing.
    internal void OnShellApplied()
    {
        if (MoveTableIndex != HpTable)
        {
            return;
        }

        MoveTableIndex = DpTable;
        MoveTableStep = 0;
    }

    // Starts the dp table at a later step, as if the earlier steps had been
    // played: their "once" flags are spent, so they never come back. Set by an
    // encounter before combat (the second of two MolArms runs one step ahead).
    internal void StartDpTableAt(int step)
    {
        HbrgoMoveTable dp = MoveTables.Dp;
        step = Math.Clamp(step, 0, Math.Max(0, dp.Steps.Count - 1));
        for (int index = 0; index < step; index++)
        {
            if (dp.Steps[index].Options.Any(option => option.Once))
            {
                MarkOnceUsed(false, index);
            }
        }

        MoveTableIndex = DpTable;
        MoveTableStep = step;
    }

    internal bool IsConditionMet(string condition) => condition switch
    {
        null or "" => true,
        HbrgoMoveTableBuilder.EnemyChargeCondition =>
            Creature.HasPower<HbrEnemyChargePower>(),
        _ => false
    };

    // A mod-side filter on top of the original rows; a filtered option is
    // treated like an unmet condition (GoldHopper's escape on the player side).
    private protected virtual bool IsOptionAvailable(HbrgoMoveOption option) => true;

    private string GetMoveId(string skill) =>
        TableMoves.FirstOrDefault(move => move.Skill == skill)?.MoveId ??
        throw new InvalidOperationException($"{GetType().Name} has no move for {skill}.");

    private static HbrgoMoveOption Choose(IReadOnlyList<HbrgoMoveOption> options, Rng rng)
    {
        if (options.Count == 1)
        {
            return options[0];
        }

        int roll = rng.NextInt(options.Sum(option => option.Weight));
        foreach (HbrgoMoveOption option in options)
        {
            roll -= option.Weight;
            if (roll < 0)
            {
                return option;
            }
        }

        return options[^1];
    }

    private bool IsOnceUsed(bool hp, int index) =>
        ((hp ? HpOnceUsedMask : DpOnceUsedMask) & (1 << index)) != 0;

    private void MarkOnceUsed(bool hp, int index)
    {
        if (hp)
        {
            HpOnceUsedMask |= 1 << index;
        }
        else
        {
            DpOnceUsedMask |= 1 << index;
        }
    }

    // The units an attack with this target scope hits, from the move's
    // player targets.
    private protected IReadOnlyList<Creature> UnitsInRange(IReadOnlyList<Creature> players,
        HbrgoAttackTargets scope) =>
        HbrAttackTargeting.Resolve(Creature, players, scope).Where(unit => unit.IsAlive).ToArray();

    // The attack, then which of its units had damage pass their block into DP
    // or HP, and how much (an absorbing skill). HBR enemy control ailments use
    // Units; legacy and phantom replay retain the pass-through distinction.
    private protected async Task<HbrgoAttackHits> PerformTrackedAttack(
        int damage,
        int hitCount,
        IReadOnlyList<Creature> players,
        HbrgoAttackTargets scope = HbrgoAttackTargets.Players)
    {
        IReadOnlyList<Creature> units = UnitsInRange(players, scope);
        using HbrEnemyHitTracker.Scope hits = HbrEnemyHitTracker.Begin();
        await PerformAttackOn(damage, hitCount, scope);
        return new HbrgoAttackHits(
            units.Where(unit => unit.IsAlive).ToArray(),
            units.Where(unit => unit.IsAlive && hits.Passed(unit)).ToArray(),
            units.Sum(hits.Amount));
    }

    // Fixed share of this creature's maximum shell, as an original HealDp
    // amount over the original maximum DP (proposal M6). Break blocks it.
    protected void RestoreDpRatio(decimal ratio)
    {
        HighDimensionalShellPower shell = Creature.GetPower<HighDimensionalShellPower>();
        if (shell == null || ratio <= 0m)
        {
            return;
        }

        shell.Restore(decimal.Ceiling(shell.MaxAmount * Math.Min(1m, ratio)));
    }

    // An original AllyAll HealDp: every living unit on this monster's side,
    // itself included, regains the share of its own maximum DP (M6); a unit in
    // Break regains nothing. On the player side (a 30A phantom) that is the
    // other summons' shells and the players' Deflector Protection.
    protected void RestoreAlliesDpRatio(decimal ratio)
    {
        if (ratio <= 0m || CombatState == null)
        {
            return;
        }

        decimal share = Math.Min(1m, ratio);
        foreach (Creature ally in CombatState.GetTeammatesOf(Creature)
                     .Where(ally => ally.IsAlive && !ally.HasPower<BreakPower>())
                     .ToArray())
        {
            if (ally.GetPower<HighDimensionalShellPower>() is { } shell)
            {
                shell.Restore(decimal.Ceiling(shell.MaxAmount * share));
            }
            else
            {
                ally.Player?.GetRelic<DeflectorProtection>()?.RestoreDpByMaxPercent(share);
            }
        }
    }
}
