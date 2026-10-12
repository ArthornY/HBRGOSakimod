using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Models retain their IDs, saved-property names and presentation. This state
// only keeps their exact percentage synchronized with the native integer proxy.
internal interface IExactDamagePercentPower
{
    decimal DamagePercent { get; }
    ExactDamagePercentState PercentState { get; }
    void RefreshNativePercentProxy();
}

internal sealed class ExactDamagePercentState
{
    private static readonly AccessTools.FieldRef<PowerModel, int> NativeAmount =
        AccessTools.FieldRefAccess<PowerModel, int>("_amount");
    private decimal _damagePercent;
    private bool _hasExactPercent;
    private bool _retainExactPercentOnAttachment;
    private decimal? _preparedPercent;
    private int _nativeWriteDepth;
    private bool _dispatchingChanges;
    private readonly Queue<NativePercentWrite> _changes = new();

    internal decimal GetPercent(int nativeAmount) => _hasExactPercent
        ? _damagePercent : Math.Max(0, nativeAmount);

    internal string SavedData => _hasExactPercent
        ? DamageAmplificationLimits.Format(_damagePercent) : string.Empty;

    internal void LoadSavedData(PowerModel owner, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        _damagePercent = DamageAmplificationLimits.Parse(value);
        _hasExactPercent = true;
        NativeAmount(owner) = ToNativePercent(_damagePercent);
        _retainExactPercentOnAttachment = true;
    }

    internal class NativePercentWrite
    {
        public NativePercentWrite() { }
        internal decimal Previous;
        internal decimal Current;
        internal bool RefreshProxy;
        internal bool Completed;
        internal ExactDamagePercentState State;
        internal IExactDamagePercentPower Power;
    }

    internal static int ToNativePercent(decimal amount) => amount > 0m
        ? Math.Max(1, DamageAmplificationLimits.ToNative(amount)) : 0;

    internal decimal? PrepareNativePercent(decimal amount)
    {
        decimal? previous = _preparedPercent;
        _preparedPercent = DamageAmplificationLimits.Clamp(amount);
        return previous;
    }

    internal decimal? PrepareInitialNativePercent(int nativeAmount, decimal amount)
    {
        bool retain = _retainExactPercentOnAttachment && amount == nativeAmount;
        _retainExactPercentOnAttachment = false;
        return PrepareNativePercent(retain ? GetPercent(nativeAmount) : amount);
    }

    internal void RestorePreparedNativePercent(decimal? amount) => _preparedPercent = amount;

    internal int PrepareNativePercentOffset(int nativeAmount, decimal offset)
    {
        decimal current = GetPercent(nativeAmount);
        decimal next = offset >= 0m && offset >= DamageAmplificationLimits.MaximumTemporaryPercent - current
            ? DamageAmplificationLimits.MaximumTemporaryPercent
            : DamageAmplificationLimits.Clamp(current + offset);
        PrepareNativePercent(next);
        return ToNativePercent(next) - nativeAmount;
    }

    internal TWrite BeginNativePercentWrite<TWrite>(PowerModel owner, ref int amount)
        where TWrite : NativePercentWrite, new()
    {
        owner.AssertMutable();
        decimal previous = GetPercent(owner.Amount);
        decimal current = _preparedPercent ?? DamageAmplificationLimits.Clamp(amount);
        _preparedPercent = null;
        _retainExactPercentOnAttachment = false;
        _damagePercent = current;
        _hasExactPercent = true;
        int proxy = ToNativePercent(current);
        var write = new TWrite { Previous = previous, Current = current,
            RefreshProxy = previous != current && owner.Amount == proxy,
            State = this, Power = (IExactDamagePercentPower)owner };
        amount = proxy;
        _nativeWriteDepth++;
        _changes.Enqueue(write);
        return write;
    }

    internal void FinishNativePercentWrite(NativePercentWrite write)
    {
        if (write == null || write.Completed) return;
        write.Completed = true;
        _nativeWriteDepth = Math.Max(0, _nativeWriteDepth - 1);
        if (_nativeWriteDepth != 0 || _dispatchingChanges) return;
        _dispatchingChanges = true;
        try
        {
            while (_changes.TryDequeue(out NativePercentWrite change))
                if (change.RefreshProxy) change.Power.RefreshNativePercentProxy();
        }
        finally { _dispatchingChanges = false; }
    }

    internal ExactDamagePercentState CloneForAttachment() => new()
    {
        _damagePercent = _damagePercent,
        _hasExactPercent = _hasExactPercent,
        _retainExactPercentOnAttachment = _hasExactPercent
    };
}
