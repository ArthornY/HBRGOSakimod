using System;
using System.Collections.Generic;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed record PersonalTurnTicket<T>(long RequestId, int Ordinal, T Owner);

// Replicated by the source card's ordinary synchronized command. Only the
// synchronized begin/finish commands advance this FIFO; local UI never does.
internal sealed class PersonalTurnSchedule<T>
{
    private sealed class Batch
    {
        internal long Id;
        internal T Owner;
        internal int Remaining;
        internal int Ordinal;
    }

    private readonly Queue<Batch> _pending = new();
    private long _sequence;
    internal PersonalTurnTicket<T> Active { get; private set; }
    internal bool HasPending => _pending.Count > 0;
    internal bool IsBusy => Active != null || HasPending;
    internal PersonalTurnTicket<T> Next => _pending.TryPeek(out Batch batch)
        ? new PersonalTurnTicket<T>(batch.Id, batch.Ordinal, batch.Owner) : null;

    internal void Request(T owner, int count)
    {
        if (owner == null || count <= 0) return;
        _pending.Enqueue(new Batch { Id = ++_sequence, Owner = owner, Remaining = count });
    }

    internal bool TryBegin(long requestId, int ordinal, T owner)
    {
        if (Active != null || !_pending.TryPeek(out Batch batch) || batch.Id != requestId ||
            batch.Ordinal != ordinal || !EqualityComparer<T>.Default.Equals(batch.Owner, owner)) return false;
        Active = new PersonalTurnTicket<T>(batch.Id, batch.Ordinal, batch.Owner);
        batch.Ordinal++;
        if (--batch.Remaining <= 0) _pending.Dequeue();
        return true;
    }

    internal bool Complete(long requestId, int ordinal)
    {
        if (Active == null || Active.RequestId != requestId || Active.Ordinal != ordinal) return false;
        Active = null;
        return true;
    }

    internal void Prune(Func<T, bool> keep)
    {
        int count = _pending.Count;
        while (count-- > 0)
        {
            Batch batch = _pending.Dequeue();
            if (keep(batch.Owner)) _pending.Enqueue(batch);
        }
    }

    internal void Clear()
    {
        _pending.Clear();
        Active = null;
        // Do not recycle tickets while stale Any commands can still arrive.
    }
}
