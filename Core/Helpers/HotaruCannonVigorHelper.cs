using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

// Runtime-only cannon volleys share one next-attack Vigor allowance per batch.
// Neither candidate selection nor previews consume charges, dice or Vigor.
internal static class HotaruCannonVigorHelper
{
    internal sealed class Candidate
    {
        internal Creature Cannon;
        internal HotaruCannonPhase Phase;
        internal bool Begun;
        internal bool Completed;
        internal bool CompleteAction;
        internal bool FireHappiness;
    }

    private sealed class OwnerBatch
    {
        internal BatchScope Current;
    }

    private static readonly AsyncLocal<BatchScope> CurrentBatch = new();
    private static readonly AsyncLocal<AttackScope> CurrentAttack = new();
    private static readonly ConditionalWeakTable<Player, OwnerBatch> OwnerBatches = new();

    internal sealed class BatchScope : IDisposable
    {
        internal readonly Millsage Relic;
        internal readonly Player Owner;
        internal readonly Creature Dealer;
        internal readonly ICombatState Combat;
        internal readonly BatchScope Previous;
        internal readonly Candidate[] Candidates;
        internal bool Active = true;
        internal bool Spent;
        private readonly OwnerBatch _reservation;

        internal BatchScope(Millsage relic,
            IEnumerable<(Creature Cannon, HotaruCannonPhase Phase)> candidates, bool completeActions = false,
            bool fireHappiness = false)
        {
            Relic = relic;
            Owner = relic?.Owner;
            Dealer = Owner?.Creature;
            Combat = Dealer?.CombatState;
            Previous = CurrentBatch.Value;
            var planned = new List<Candidate>();
            foreach (var (cannon, phase) in candidates ?? Array.Empty<(Creature, HotaruCannonPhase)>())
            {
                if (cannon == null || planned.Any(item => ReferenceEquals(item.Cannon, cannon) && item.Phase == phase))
                    continue;
                planned.Add(new Candidate { Cannon = cannon, Phase = phase, CompleteAction = completeActions,
                    FireHappiness = fireHappiness });
            }
            Candidates = planned.ToArray();
            if (Owner != null)
            {
                _reservation = OwnerBatches.GetValue(Owner, _ => new OwnerBatch());
                if (_reservation.Current == null || !_reservation.Current.Active ||
                    !_reservation.Current.IsSameCombat())
                    _reservation.Current = this;
            }
            CurrentBatch.Value = this;
        }

        internal bool IsSameCombat() => Owner != null && Combat != null &&
            ReferenceEquals(Relic?.Owner, Owner) && ReferenceEquals(Owner.Creature, Dealer) &&
            ReferenceEquals(Dealer.CombatState, Combat);

        internal bool CanClaim => Active && !Spent && IsSameCombat() && Dealer.IsAlive &&
            !CombatManager.Instance.IsOverOrEnding && ReferenceEquals(_reservation?.Current, this);

        internal Candidate Winner(Candidate current = null)
        {
            if (!CanClaim || !Combat.Enemies.Any(enemy => Millsage.IsEnemy(enemy, Owner))) return null;
            Candidate winner = null;
            int maximum = 0;
            // Keep the caller's actual execution order for equal segment counts.
            foreach (Candidate candidate in Candidates)
            {
                if (candidate.Completed || candidate.Begun && !ReferenceEquals(candidate, current) ||
                    !Millsage.IsOwnedCannon(candidate.Cannon, Owner)) continue;
                HotaruCannonKind kind = ((HotaruCannonModel)candidate.Cannon.Monster).Kind;
                int hits = candidate.CompleteAction ? GetCompleteActionHitCount(Relic, kind, candidate.FireHappiness)
                    : GetPlannedHitCount(Relic, kind, candidate.Phase);
                if (hits <= maximum) continue;
                maximum = hits;
                winner = candidate;
            }
            return winner;
        }

        public void Dispose()
        {
            if (!Active) return;
            Active = false;
            if (ReferenceEquals(_reservation?.Current, this)) _reservation.Current = null;
            if (ReferenceEquals(CurrentBatch.Value, this)) CurrentBatch.Value = Previous;
        }
    }

    internal sealed class AttackScope
    {
        private readonly Creature _cannon;
        private readonly HotaruCannonPhase _phase;
        private readonly BatchScope _batch;
        private readonly Candidate _candidate;
        private readonly AttackScope _previous;
        private readonly VigorPower _power;
        private readonly int _amount;
        private readonly bool _coversAllPhases;
        private bool _active = true;
        private bool _started;
        private bool _completed;

        internal AttackScope(Creature cannon, HotaruCannonPhase phase, BatchScope batch, bool coversAllPhases = false)
        {
            _cannon = cannon;
            _phase = phase;
            _coversAllPhases = coversAllPhases;
            _batch = batch;
            _previous = CurrentAttack.Value;
            if (batch != null)
            {
                _candidate = batch.Candidates.FirstOrDefault(item => !item.Begun && !item.Completed &&
                    ReferenceEquals(item.Cannon, cannon) && item.Phase == phase);
                bool winner = _candidate != null && ReferenceEquals(batch.Winner(), _candidate);
                if (_candidate != null) _candidate.Begun = true;
                VigorPower power = batch.Dealer?.GetPower<VigorPower>();
                if (winner && power?.Amount > 0 &&
                    !HotaruCannonVigorPatch.IsVigorReservedForNativeAttack(power))
                {
                    _power = power;
                    _amount = power.Amount;
                }
            }
            CurrentAttack.Value = this;
        }

        internal Creature Cannon => _cannon;
        internal AttackScope Previous => _previous;
        internal bool Active => _active;
        internal decimal GetBonus(VigorPower vigor, Creature cannon, HotaruCannonPhase? phase) =>
            ReferenceEquals(_cannon, cannon) && (!phase.HasValue || _coversAllPhases || phase == _phase) &&
            ReferenceEquals(_power, vigor) && CanApply ? _amount : 0m;

        private bool HasCurrentPower() => _batch?.IsSameCombat() == true && _power != null &&
            ReferenceEquals(_power.Owner, _batch.Dealer) &&
            ReferenceEquals(_batch.Dealer.GetPower<VigorPower>(), _power);

        private bool CanApply => _active && _batch?.Active == true && _amount > 0 && HasCurrentPower() && _power.Amount > 0 &&
            _batch.Dealer.IsAlive && Millsage.IsOwnedCannon(_cannon, _batch.Owner) &&
            !HotaruCannonVigorPatch.IsVigorReservedForNativeAttack(_power) &&
            (_started || ReferenceEquals(_batch.Winner(_candidate), _candidate));

        internal void MarkPrimary()
        {
            if (_started || !CanApply || !_batch.CanClaim || _power.Amount <= 0) return;
            // Reserve before the real command can await, dispatch a reaction or
            // enter another batch. A blocked/DP hit still spends one attack.
            _started = true;
            _batch.Spent = true;
        }

        // A synchronous wrapper restores the caller's AsyncLocal context before
        // awaiting consumption; an async method's own context cannot do that.
        internal Task Complete(PlayerChoiceContext context)
        {
            if (_completed)
            {
                if (ReferenceEquals(CurrentAttack.Value, this)) CurrentAttack.Value = _previous;
                return Task.CompletedTask;
            }
            _completed = true;
            _active = false;
            if (_candidate != null) _candidate.Completed = true;
            if (ReferenceEquals(CurrentAttack.Value, this)) CurrentAttack.Value = _previous;
            return Consume(context);
        }

        private async Task Consume(PlayerChoiceContext context)
        {
            if (!_started || !HasCurrentPower() || !_batch.Dealer.IsAlive) return;
            int consume = Math.Min(_amount, Math.Max(0, _power.Amount));
            if (consume > 0)
                // Do not remove a replacement power or Vigor granted during
                // this volley. Native amount mutation preserves its hooks.
                await PowerCmd.ModifyAmount(context, _power, -consume, null, null);
        }
    }

    internal static IDisposable BeginBatch(Millsage relic,
        IEnumerable<(Creature Cannon, HotaruCannonPhase Phase)> candidates) => new BatchScope(relic, candidates);

    internal static IDisposable BeginCompleteActionBatch(Millsage relic, IEnumerable<Creature> cannons,
        bool fireHappiness = false) =>
        new BatchScope(relic, cannons.Select(cannon => (cannon,
            cannon?.Monster is HotaruAbsolutionCannon || cannon?.Monster is HotaruFinalCannon && relic.EndgameStartHitCount > 0
                ? HotaruCannonPhase.Start : HotaruCannonPhase.End)),
            completeActions: true, fireHappiness: fireHappiness);

    private static int GetCompleteActionHitCount(Millsage relic, HotaruCannonKind kind, bool fireHappiness) => kind switch
    {
        HotaruCannonKind.Happiness when fireHappiness => GetPlannedHitCount(relic, kind, HotaruCannonPhase.End),
        HotaruCannonKind.Absolution => GetPlannedHitCount(relic, kind, HotaruCannonPhase.Start) +
            GetPlannedHitCount(relic, kind, HotaruCannonPhase.End),
        HotaruCannonKind.Endgame => (relic?.EndgameStartHitCount ?? 0) + (relic?.EndgameEndHitCount ?? 0),
        HotaruCannonKind.Contest or HotaruCannonKind.Longing =>
            GetPlannedHitCount(relic, kind, HotaruCannonPhase.End),
        _ => 0
    };

    internal static AttackScope BeginAttack(Creature cannon, HotaruCannonPhase phase)
    {
        BatchScope batch = CurrentBatch.Value;
        while (batch != null && (!batch.Active || !ReferenceEquals(batch.Owner, cannon?.PetOwner)))
            batch = batch.Previous;
        return new AttackScope(cannon, phase, batch);
    }

    internal static AttackScope BeginCompleteAction(Creature cannon, HotaruCannonPhase phase)
    {
        BatchScope batch = CurrentBatch.Value;
        while (batch != null && (!batch.Active || !ReferenceEquals(batch.Owner, cannon?.PetOwner)))
            batch = batch.Previous;
        return new AttackScope(cannon, phase, batch, coversAllPhases: true);
    }

    internal static void MarkPrimaryCommand(Creature cannon)
    {
        AttackScope scope = FindAttack(cannon);
        if (scope != null && ReferenceEquals(scope.Cannon, cannon)) scope.MarkPrimary();
    }

    private static AttackScope FindAttack(Creature cannon)
    {
        AttackScope scope = CurrentAttack.Value;
        while (scope != null)
        {
            if (scope.Active && ReferenceEquals(scope.Cannon?.PetOwner, cannon?.PetOwner)) return scope;
            scope = scope.Previous;
        }
        return null;
    }

    internal static decimal ResolveBonus(VigorPower vigor, Creature cannon,
        HotaruCannonPhase? phase, bool isPreview, bool isReaction, decimal nativeBonus)
    {
        if (vigor == null || cannon == null || isReaction ||
            !Millsage.IsOwnedCannon(cannon, cannon.PetOwner) ||
            !ReferenceEquals(vigor.Owner, cannon.PetOwner.Creature) ||
            HotaruCannonVigorPatch.IsVigorReservedForNativeAttack(vigor)) return 0m;
        AttackScope scope = FindAttack(cannon);
        if (scope != null)
        {
            return scope.GetBonus(vigor, cannon, phase);
        }
        if (!isPreview) return 0m;
        HotaruCannonPhase previewPhase = phase ?? HotaruCannonPhase.End;
        Millsage relic = cannon.PetOwner.GetRelic<Millsage>();
        if (relic == null || !cannon.CombatState.Enemies.Any(enemy => Millsage.IsEnemy(enemy, cannon.PetOwner)))
            return 0m;
        // Happiness's damage intent describes its manual firing action; its
        // natural END only grows stored damage and is never a damage candidate.
        if (cannon.Monster is HotaruHappinessCannon)
            return previewPhase == HotaruCannonPhase.End ? Math.Max(0, vigor.Amount) : 0m;
        Creature winner = null;
        int maximum = 0;
        foreach (HotaruCannonKind kind in Enum.GetValues<HotaruCannonKind>())
        {
            if (kind == HotaruCannonKind.Happiness) continue;
            Creature candidate = relic.GetCannon(kind);
            if (candidate == null) continue;
            int hits = GetPlannedHitCount(relic, kind, previewPhase);
            if (hits <= maximum) continue;
            maximum = hits;
            winner = candidate;
        }
        return ReferenceEquals(winner, cannon) ? Math.Max(0, vigor.Amount) : 0m;
    }

    internal static int GetPlannedHitCount(Millsage relic, HotaruCannonKind kind, HotaruCannonPhase phase) =>
        phase switch
        {
            HotaruCannonPhase.Start => kind == HotaruCannonKind.Absolution ? 3 :
                kind == HotaruCannonKind.Endgame ? relic?.EndgameStartHitCount ?? 0 : 0,
            HotaruCannonPhase.End => kind switch
            {
                HotaruCannonKind.Happiness => relic?.HasCannonUpgrade(kind, 7) == true ? 2 : 1,
                HotaruCannonKind.Contest => relic?.HasCannonUpgrade(kind, 7) == true ? 2 : 1,
                HotaruCannonKind.Longing => 1,
                HotaruCannonKind.Absolution => HotaruAbsolutionUpgradeHelper.GetEndHitCount(relic),
                HotaruCannonKind.Endgame => relic?.EndgameEndHitCount ?? 0,
                _ => 0
            },
            _ => 0
        };
}
