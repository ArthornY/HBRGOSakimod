using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Helpers;

// Records which creatures had damage pass their block during an attack, and
// how much. DP absorption is reported to native code as blocked damage, so
// DamageResult cannot tell block from DP; the block receiver patch reports
// the remainder.
internal static class HbrEnemyHitTracker
{
    private static readonly List<Scope> Scopes = new();
    private static readonly List<Creature> Receivers = new();

    internal static Scope Begin()
    {
        if (Scopes.Count == 0)
        {
            Receivers.Clear();
        }

        Scope scope = new();
        Scopes.Add(scope);
        return scope;
    }

    // The block receiver patch enters for every creature whose block resolves
    // a hit. A redirect (30A's phantom block, RedCrimson) resolves inside the
    // original receiver's call, so the outermost receiver is the hit's target.
    internal static void EnterBlock(Creature receiver)
    {
        if (Scopes.Count > 0 && receiver != null)
        {
            Receivers.Add(receiver);
        }
    }

    internal static Creature ExitBlock(Creature receiver)
    {
        if (Scopes.Count == 0)
        {
            Receivers.Clear();
            return receiver;
        }

        Creature target = Receivers.Count > 0 ? Receivers[0] : receiver;
        int index = Receivers.LastIndexOf(receiver);
        if (index >= 0)
        {
            Receivers.RemoveRange(index, Receivers.Count - index);
        }

        return target ?? receiver;
    }

    // Damage that remained after the receiver's own block. A summon taking
    // its summoner's hit through a block redirect reports for both; a summon
    // hit directly (an all-unit attack) reports only for itself.
    internal static void RecordPassThrough(Creature receiver, decimal amount, Creature target = null)
    {
        if (receiver == null || Scopes.Count == 0)
        {
            return;
        }

        foreach (Scope scope in Scopes)
        {
            scope.Add(receiver, amount);
            if (target != null && !ReferenceEquals(target, receiver))
            {
                scope.Add(target, amount);
            }
        }
    }

    internal sealed class Scope : IDisposable
    {
        private readonly Dictionary<Creature, decimal> _passed = new(ReferenceEqualityComparer.Instance);

        internal bool Passed(Creature creature) => creature != null && _passed.ContainsKey(creature);

        internal decimal Amount(Creature creature) =>
            creature != null && _passed.TryGetValue(creature, out decimal amount) ? amount : 0m;

        internal IReadOnlyList<Creature> PassedTargets => _passed.Keys.ToArray();

        internal void Add(Creature creature, decimal amount) =>
            _passed[creature] = DamageAmplificationLimits.AddSaturated(Amount(creature), Math.Max(0m, amount));

        public void Dispose() => Scopes.Remove(this);
    }
}

// Widens or narrows a monster attack's native target list (players only):
// the original all-unit skills also hit the player side's hittable summons,
// and MinimumDp hits the living player with the least DP.
internal static class HbrAttackTargeting
{
    private sealed class Mark
    {
        internal HbrgoAttackTargets Targets;
    }

    private static readonly ConditionalWeakTable<AttackCommand, Mark> Marks = new();

    internal static void Set(AttackCommand attack, HbrgoAttackTargets targets)
    {
        if (attack != null && targets != HbrgoAttackTargets.Players)
        {
            Marks.AddOrUpdate(attack, new Mark { Targets = targets });
        }
    }

    internal static bool TryGet(AttackCommand attack, out HbrgoAttackTargets targets)
    {
        targets = HbrgoAttackTargets.Players;
        if (attack == null || !Marks.TryGetValue(attack, out Mark mark))
        {
            return false;
        }

        targets = mark.Targets;
        return true;
    }

    internal static IReadOnlyList<Creature> Resolve(Creature attacker, IReadOnlyList<Creature> players,
        HbrgoAttackTargets targets)
    {
        players ??= Array.Empty<Creature>();
        switch (targets)
        {
            case HbrgoAttackTargets.AllUnits when attacker?.CombatState is { } combat:
                return players.Concat(combat.GetOpponentsOf(attacker)
                        .Where(unit => !unit.IsPlayer && unit.IsAlive && unit.IsHittable))
                    .Distinct()
                    .ToArray();
            case HbrgoAttackTargets.MinimumDp:
                Creature lowest = players.Where(player => player.IsAlive)
                    .OrderBy(CurrentDp)
                    .FirstOrDefault();
                return lowest == null ? players : new[] { lowest };
            default:
                return players;
        }
    }

    // A player's Deflector Protection; a character without it counts as 0.
    internal static decimal CurrentDp(Creature creature) =>
        creature?.Player?.GetRelic<DeflectorProtection>()?.Dp ?? 0m;
}

// Application of the original enemy statuses (proposal section 3.3). Players
// are the targets of monsters; enemies are the targets of 30A phantoms, which
// receive each status's enemy-side rule instead.
internal static class HbrEnemyEffects
{
    // The stronger contact rule belongs to an HBR-enabled enemy/run. A mixed
    // team shares the caster's rule; never weaken it for an original teammate.
    internal static IReadOnlyList<Creature> ControlTargets(HbrgoAttackHits hits, Creature caster) =>
        HbrBattleStatsHelper.IsEnabled(caster) ? hits.Units : hits.Passed;

    internal const string StunnedMoveId = "STUNNED";
    internal const decimal ContinuousDpDamageRate = 0.05m;
    internal const decimal PhantomDpBreakShare = 0.5m;

    private static readonly ConditionalWeakTable<Creature, object> ActionLockedEnemies = new();
    private static readonly object Marker = new();

    internal static bool IsEnemyTarget(Creature target) =>
        target?.Side == CombatSide.Enemy;

    internal static bool HasPlayerDp(Creature target) =>
        target?.Player?.GetRelic<DeflectorProtection>() != null;

    // Monsters hit every player creature (native monster attacks do the same);
    // a phantom's single-target status picks one opponent, as its attacks do.
    internal static IReadOnlyList<Creature> SingleTargets(Creature actor, IReadOnlyList<Creature> targets)
    {
        Creature[] living = targets?.Where(target => target?.IsAlive == true).ToArray() ??
            Array.Empty<Creature>();
        if (actor?.Side != CombatSide.Player || living.Length <= 1)
        {
            return living;
        }

        // The same synced target RNG as the phantom's own attacks.
        return actor.CombatState?.RunState?.Rng?.CombatTargets is { } rng
            ? new[] { rng.NextItem(living) }
            : new[] { living[0] };
    }

    internal static Task ApplyTimedBuff<T>(Creature owner, int percent)
        where T : HbrTimedPercentBuffPower => owner?.IsAlive == true && percent > 0
            ? HbrEffectApplication.Apply<T>(new ThrowingPlayerChoiceContext(), owner, percent,
                owner, profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2))
            : Task.CompletedTask;

    internal static Task ApplyCharge(Creature owner) =>
        owner?.IsAlive == true && (HbrBattleStatsHelper.IsEnabled(owner) || !owner.HasPower<HbrEnemyChargePower>())
            ? PowerCmd.Apply<HbrEnemyChargePower>(new ThrowingPlayerChoiceContext(), owner, 1m, owner, null)
            : Task.CompletedTask;

    internal static async Task RemoveCharge(Creature owner)
    {
        if (owner == null) return;
        foreach (var charge in owner.Powers.OfType<HbrEnemyChargePower>().ToArray()) await PowerCmd.Remove(charge);
    }

    internal static async Task ApplyStatRateDown<T>(IEnumerable<Creature> targets, Creature applier)
        where T : HbrStatRateDownPower
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets.Where(target => target?.IsAlive == true))
            await HbrEffectApplication.Apply<T>(new ThrowingPlayerChoiceContext(), target,
                ModelDb.Power<T>().BasePercent, applier);
    }

    // Original ImprisonRandom. Players: next turn's cards costing 2 or more
    // cannot be played. Enemies keep only a plain attack: any other next move
    // is skipped (shown as a stun). Player-side summons are not affected.
    internal static async Task ApplyImprison(IEnumerable<Creature> targets, Creature applier)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets.Where(target => target?.IsAlive == true && IsCardOrEnemyHolder(target)))
        {
            if (IsEnemyTarget(target))
            {
                await SkipUnlessPlainAttack(target);
                continue;
            }

            if (HbrBattleStatsHelper.IsEnabled(target) || !target.HasPower<HbrImprisonPower>())
            {
                await PowerCmd.Apply<HbrImprisonPower>(new ThrowingPlayerChoiceContext(), target, 1m, applier, null);
            }
        }
    }

    // Original BindRandom/StunRandom. Players lose two energy next turn in HBR mode (one in legacy); an
    // enemy is stunned for a turn, at most once per combat per enemy.
    // Player-side summons are not affected.
    internal static async Task ApplyActionLock<T>(IEnumerable<Creature> targets, Creature applier)
        where T : HbrActionLockPower
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets.Where(target => target?.IsAlive == true && IsCardOrEnemyHolder(target)))
        {
            if (IsEnemyTarget(target))
            {
                await StunEnemyOnce(target);
                continue;
            }

            if (HbrBattleStatsHelper.IsEnabled(target) || !target.HasPower<T>())
            {
                await PowerCmd.Apply<T>(new ThrowingPlayerChoiceContext(), target, 1m, applier, null);
            }
        }
    }

    // Original ContinuousDpDamageRate. Without a DP pool (original characters)
    // there is nothing to drain, so the status is not applied.
    internal static async Task ApplyContinuousDpDamage(IEnumerable<Creature> targets, Creature applier)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets.Where(target => target?.IsAlive == true))
        {
            if (!HasPlayerDp(target) && target.GetPower<HighDimensionalShellPower>() == null)
            {
                continue;
            }

            if (HbrBattleStatsHelper.IsEnabled(target))
            {
                await HbrEffectApplication.Apply<HbrContinuousDpDamagePower>(new ThrowingPlayerChoiceContext(), target, 5m, applier);
                continue;
            }

            if (target.GetPower<HbrContinuousDpDamagePower>() is { } existing)
            {
                if (existing.Amount < HbrContinuousDpDamagePower.InitialDuration)
                {
                    await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), existing,
                        HbrContinuousDpDamagePower.InitialDuration - existing.Amount, applier, null);
                }

                continue;
            }

            await PowerCmd.Apply<HbrContinuousDpDamagePower>(new ThrowingPlayerChoiceContext(), target,
                HbrContinuousDpDamagePower.InitialDuration, applier, null);
        }
    }

    // Original VirusRandom: a player's Deflector Protection or an enemy's
    // shell has a lower maximum for 2 turns. Original characters (no DP),
    // summons and enemies without a shell (Break) are not affected. The same
    // status restarts at 2 turns rather than stacks.
    internal static async Task ApplyVirus(IEnumerable<Creature> targets, Creature applier)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets.Where(target => target?.IsAlive == true))
        {
            bool holdsDp = target.IsPlayer
                ? HasPlayerDp(target)
                : IsEnemyTarget(target) && target.GetPower<HighDimensionalShellPower>() != null;
            if (!holdsDp)
            {
                continue;
            }

            if (HbrBattleStatsHelper.IsEnabled(target))
            {
                await HbrEffectApplication.Apply<HbrVirusPower>(new ThrowingPlayerChoiceContext(), target, 30m, applier);
                continue;
            }

            if (target.GetPower<HbrVirusPower>() is { } existing)
            {
                if (existing.Amount < HbrVirusPower.InitialDuration)
                {
                    await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), existing,
                        HbrVirusPower.InitialDuration - existing.Amount, applier, null);
                }

                continue;
            }

            await PowerCmd.Apply<HbrVirusPower>(new ThrowingPlayerChoiceContext(), target,
                HbrVirusPower.InitialDuration, applier, null);
        }
    }

    // Reserved effects; no current monster calls either adapter.
    internal static async Task ApplyContinuousDamage(IEnumerable<Creature> targets, Creature applier,
        decimal percent = 15m, int remaining = 4)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets.Where(target => target?.IsAlive == true))
            await HbrEffectApplication.Apply<HbrContinuousDamagePower>(new ThrowingPlayerChoiceContext(),
                target, percent, applier, remaining: remaining);
    }

    internal static async Task ApplyEnergyDrain(IEnumerable<Creature> targets, Creature applier,
        decimal energy = 2m, int remaining = 4)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets.Where(target => target?.IsAlive == true && target.IsPlayer))
            await HbrEffectApplication.Apply<HbrEnergyDrainPower>(new ThrowingPlayerChoiceContext(),
                target, energy, applier, remaining: remaining);
    }

    // The maximum DP changed (Virus started or ended): DP above it is lost
    // (Mental Strength rewards included) and the DP display refreshes.
    internal static async Task SyncMaximumDp(PlayerChoiceContext context, Creature owner)
    {
        if (owner?.Player?.GetRelic<DeflectorProtection>() is { } protection)
        {
            if (protection.SyncAfterMaximumChanged() > 0)
            {
                await protection.ResolveMentalStrengthDpLossRewards(context);
            }

            return;
        }

        owner?.GetPower<HighDimensionalShellPower>()?.ClampToMaximum();
    }

    // A share of the maximum DP, spent from DP only. Players lose Deflector
    // Protection DP; an enemy's shell takes DP damage and can break.
    internal static async Task<decimal> LoseDpShare(PlayerChoiceContext context, Creature owner,
        decimal share, Creature dealer)
    {
        if (owner?.IsAlive != true || share <= 0m)
        {
            return 0m;
        }

        if (owner.Player?.GetRelic<DeflectorProtection>() is { } protection)
        {
            protection.EnsureDpInitialized();
            if (protection.Dp <= 0 || protection.MaxDp <= 0)
            {
                return 0m;
            }

            int loss = Math.Max(1, (int)decimal.Floor(protection.MaxDp * share));
            int lost = protection.LoseDp(loss);
            if (lost > 0)
            {
                if (protection.Dp == 0) await HbrDpBreakStatusHelper.Apply(context, owner);
                await protection.ResolveMentalStrengthDpLossRewards(context);
            }

            return lost;
        }

        if (owner.GetPower<HighDimensionalShellPower>() is { } shell && shell.CurrentDp > 0m)
        {
            decimal loss = Math.Max(1m, decimal.Floor(shell.MaxAmount * share));
            return await shell.DamageDpOnly(context, loss, dealer);
        }

        return 0m;
    }

    // Original DpBreakRandom. A player's Deflector Protection drops to 0. A
    // phantom halves an enemy's current DP instead of forcing a shell break.
    internal static async Task BreakDp(PlayerChoiceContext context, Creature target, Creature dealer)
    {
        if (target?.IsAlive != true)
        {
            return;
        }

        if (target.Player?.GetRelic<DeflectorProtection>() is { } protection)
        {
            protection.EnsureDpInitialized();
            int lost = protection.LoseDp(protection.Dp);
            if (lost > 0)
            {
                await HbrDpBreakStatusHelper.Apply(context, target);
                await protection.ResolveMentalStrengthDpLossRewards(context);
            }

            return;
        }

        if (IsEnemyTarget(target) && target.GetPower<HighDimensionalShellPower>() is { } shell &&
            shell.CurrentDp > 0m)
        {
            decimal loss = decimal.Floor(shell.CurrentDp * PhantomDpBreakShare);
            if (loss > 0m)
            {
                await shell.DamageDpOnly(context, loss, dealer);
            }
        }
    }

    // Original IllusionRandom. Players and enemies (a phantom's targets) take
    // layers; the same status keeps the higher layer count and its 2 turns
    // restart. Summons are not affected (they play no attack cards).
    internal static async Task ApplyIllusion(IEnumerable<Creature> targets, int layers, Creature applier)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets.Where(target => target?.IsAlive == true && IsCardOrEnemyHolder(target)))
        {
            if (HbrBattleStatsHelper.IsEnabled(target))
            {
                await HbrEffectApplication.Apply<HbrIllusionPower>(new ThrowingPlayerChoiceContext(), target, layers, applier);
                continue;
            }

            if (target.GetPower<HbrIllusionPower>() is { } existing)
            {
                existing.RefreshLegacy(layers);

                continue;
            }

            await PowerCmd.Apply<HbrIllusionPower>(new ThrowingPlayerChoiceContext(), target, layers, applier, null);
        }
    }

    // Original ConfusionRandom: 3 turns on a player; an enemy keeps it until
    // its next attack. The same status restarts rather than stacks.
    internal static async Task ApplyConfusion(IEnumerable<Creature> targets, Creature applier)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets.Where(target => target?.IsAlive == true && IsCardOrEnemyHolder(target)))
        {
            if (HbrBattleStatsHelper.IsEnabled(target))
            {
                await HbrEffectApplication.Apply<HbrConfusionPower>(new ThrowingPlayerChoiceContext(), target, 1m, applier);
                continue;
            }

            if (target.GetPower<HbrConfusionPower>() is { } existing)
            {
                if (existing.Amount < HbrConfusionPower.InitialDuration)
                {
                    await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), existing,
                        HbrConfusionPower.InitialDuration - existing.Amount, applier, null);
                }

                continue;
            }

            await PowerCmd.Apply<HbrConfusionPower>(new ThrowingPlayerChoiceContext(), target,
                HbrConfusionPower.InitialDuration, applier, null);
        }
    }

    // Original OverDrivePointDown: each player's Overclock gauge loses a share
    // of one level (a level is 100%); summons and original characters have no
    // gauge. A phantom's targets have none either, so the phantom's summoner
    // gains the same share instead, once per replay however many it hit.
    internal static void LowerOverdrive(IEnumerable<Creature> targets, decimal percent, Creature applier)
    {
        bool enemyHit = false;
        foreach (Creature target in targets.Where(target => target?.IsAlive == true))
        {
            if (IsEnemyTarget(target))
            {
                enemyHit = true;
                continue;
            }

            if (target.IsPlayer)
            {
                target.Player?.GetRelic<Overclock>()?.LoseLevelProgress(percent);
            }
        }

        if (enemyHit)
        {
            applier?.PetOwner?.GetRelic<Overclock>()?.GainLevelProgress(percent);
        }
    }

    // Original HealDpByDamage: the attacker restores DP from the DP and HP its
    // attack took (400%; legacy DP is a fifth of HP, so 50% there). Break
    // (no shell) blocks it.
    internal const decimal HbrAbsorbRate = 4m;
    internal const decimal LegacyAbsorbRate = 0.5m;

    internal static decimal AbsorbDp(Creature attacker, decimal passedDamage)
    {
        if (attacker?.IsAlive != true || passedDamage <= 0m ||
            attacker.GetPower<HighDimensionalShellPower>() is not { } shell)
        {
            return 0m;
        }

        decimal rate = HbrBattleStatsHelper.IsEnabled(attacker) ? HbrAbsorbRate : LegacyAbsorbRate;
        return shell.Restore(decimal.Floor(passedDamage * rate));
    }

    // Player-side statuses that act on cards (or an enemy's own attacks).
    private static bool IsCardOrEnemyHolder(Creature target) =>
        target.IsPlayer || IsEnemyTarget(target);

    internal static bool IsPlainSingleAttack(MoveState move) =>
        move?.Intents is { Count: 1 } intents && intents[0] is SingleAttackIntent;

    internal static bool WasActionLocked(Creature enemy) =>
        enemy != null && ActionLockedEnemies.TryGetValue(enemy, out _);

    private static async Task StunEnemyOnce(Creature enemy)
    {
        if (enemy.Monster == null || enemy.Monster.NextMove?.Id == StunnedMoveId ||
            WasActionLocked(enemy))
        {
            return;
        }

        ActionLockedEnemies.AddOrUpdate(enemy, Marker);
        string interrupted = enemy.Monster.NextMove is { } next &&
            enemy.Monster.MoveStateMachine?.States.ContainsKey(next.Id) == true
                ? next.Id
                : null;
        await CreatureCmd.Stun(enemy, interrupted);
    }

    private static async Task SkipUnlessPlainAttack(Creature enemy)
    {
        MoveState next = enemy.Monster?.NextMove;
        if (next == null || next.Id == StunnedMoveId || IsPlainSingleAttack(next))
        {
            return;
        }

        string followUp = next.FollowUpState?.Id ?? next.FollowUpStateId;
        if (string.IsNullOrEmpty(followUp) ||
            enemy.Monster.MoveStateMachine?.States.ContainsKey(followUp) != true)
        {
            followUp = null;
        }

        await CreatureCmd.Stun(enemy, followUp);
    }
}
