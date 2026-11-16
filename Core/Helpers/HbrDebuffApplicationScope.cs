using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;

namespace HBRGOSakiMod.Core.Helpers;

// One bonus per application round, shared across its targets. Card tokens also
// cover authored AoE loops; repeat applications to a target start a new round.
internal static class HbrDebuffApplicationScope
{
    private static readonly AsyncLocal<Batch> Current = new();
    private static readonly ConditionalWeakTable<object, Batch> CardBatches = new();
    private sealed class Reservations { internal int Count; }
    private static readonly ConditionalWeakTable<ThirtyOneAYukiDebuffPower, Reservations> Reserved = new();
    private static readonly Claim Empty = new(null);
    private static bool HasYuki(Creature applier) =>
        ((applier?.Player ?? applier?.PetOwner)?.Creature ?? applier)?.GetPower<ThirtyOneAYukiDebuffPower>()?.Amount > 0;

    internal sealed class Claim
    {
        private readonly ThirtyOneAYukiDebuffPower _power;
        private Task _completion;
        internal decimal Multiplier => _power == null ? 1m : 1.3m;
        internal bool Batched;
        internal Claim(Creature applier)
        {
            Creature owner = (applier?.Player ?? applier?.PetOwner)?.Creature ?? applier;
            var power = owner?.GetPower<ThirtyOneAYukiDebuffPower>();
            if (power == null) return;
            Reservations reserved = Reserved.GetOrCreateValue(power);
            if (power.Amount <= reserved.Count) return;
            reserved.Count++;
            _power = power;
        }
        internal Task Complete() => _completion ??= Consume();
        private async Task Consume()
        {
            if (_power == null) return;
            try
            {
                if (_power.Owner?.Powers.Contains(_power) == true && _power.Amount > 0)
                    await PowerCmd.Decrement(_power);
            }
            finally { Reserved.GetOrCreateValue(_power).Count--; }
        }
        internal void Release()
        {
            if (_power != null && _completion == null)
            { Reserved.GetOrCreateValue(_power).Count--; _completion = Task.CompletedTask; }
        }
    }

    internal sealed class Batch : IDisposable
    {
        internal Creature Applier;
        internal Batch Previous;
        internal bool Active = true;
        private readonly Dictionary<(Type Kind, Creature Target), int> _rounds = new();
        private readonly Dictionary<(Type Kind, int Round), Claim> _boosts = new();

        internal Claim Get(Creature applier, Type kind, Creature target)
        {
            var key = (kind, target);
            int round = _rounds.GetValueOrDefault(key);
            _rounds[key] = round + 1;
            var grant = (kind, round);
            if (!_boosts.TryGetValue(grant, out Claim boost))
            {
                boost = new Claim(applier) { Batched = true };
                _boosts.Add(grant, boost);
            }
            return boost;
        }

        public void Dispose()
        {
            Active = false;
            foreach (Claim claim in _boosts.Values) claim.Release();
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }
    }

    internal static Batch BeginBatch(Creature applier)
    {
        Batch batch = new() { Applier = applier, Previous = Current.Value };
        Current.Value = batch;
        return batch;
    }

    internal static void RestoreCaller(Batch batch)
    {
        if (batch != null && ReferenceEquals(Current.Value, batch)) Current.Value = batch.Previous;
    }

    internal static Claim GetClaim(Creature applier, Type kind, Creature target, CardModel card = null)
    {
        if (applier == null || target == null) return Empty;
        Batch batch = Current.Value;
        if (batch?.Active != true || !ReferenceEquals(batch.Applier, applier))
        {
            object token = CardPlayContextHelper.GetCurrentPlayToken(card);
            if (token != null && !CardBatches.TryGetValue(token, out batch) && HasYuki(applier))
                batch = CardBatches.GetValue(token, _ => new Batch { Applier = applier });
            else if (token == null) batch = null;
        }
        return batch?.Get(applier, kind, target) ?? (HasYuki(applier) ? new Claim(applier) : Empty);
    }

    internal static void CloseCard(object token)
    {
        if (token != null && CardBatches.TryGetValue(token, out Batch batch))
        { batch.Dispose(); CardBatches.Remove(token); }
    }

    internal static async Task<decimal> GetMultiplier(Creature applier, Type kind, Creature target, CardModel card = null)
    {
        Claim claim = GetClaim(applier, kind, target, card);
        await claim.Complete();
        return claim.Multiplier;
    }
}
