using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

internal interface IHighDimensionalShellObserver
{
    void OnHighDimensionalShellApplied(
        HighDimensionalShellPower shell);

    void OnHighDimensionalShellAmountChanged(
        decimal previousAmount,
        decimal currentAmount,
        decimal maximumAmount);

    Task AfterHighDimensionalShellBreak(
        PlayerChoiceContext choiceContext);
}

internal interface IHighDimensionalShellBreakOverride
{
    bool ShouldSuppressDefaultHighDimensionalShellBreak { get; }

    Task AfterHighDimensionalShellRemoved(
        PlayerChoiceContext choiceContext);
}

public class HighDimensionalShellPower : PowerModel
{
    private static readonly HarmonyLib.AccessTools.FieldRef<PowerModel, int> NativeAmount =
        HarmonyLib.AccessTools.FieldRefAccess<PowerModel, int>("_amount");
    public const int NormalBreakAmount = 3;
    public const int EliteBreakAmount = 4;
    public const int BossBreakAmount = 5;

    private const int EnhancedShellAscensionLevel = 10;
    private const decimal IncomingDamageMultiplier = 0.75m;
    private const decimal BaseMaxHpRatio = 0.2m;
    private const decimal EnhancedEnemyShellMultiplier = 1.5m;

    private static int _suppressDamageReductionDepth;

    protected bool _pendingBreak;
    protected decimal _lastAbsorbedDamage;
    private Stack<decimal> _pendingExtraDpDamage = new();
    private decimal _currentDp;
    private bool _hasExactDp;
    private bool _retainExactDpOnAttachment;
    private decimal? _preparedDp;
    private decimal _preparedDpDamage;
    private int _nativeWriteDepth;
    private bool _dispatchingDpChanges;
    private Queue<NativeDpWrite> _dpChanges = new();

    // Native Amount remains a bounded compatibility value. Gameplay reads this
    // exact pool; the string bridge is supported by native save/packet properties.
    public decimal CurrentDp => _hasExactDp ? _currentDp : Math.Max(0, Amount);
    public decimal MaximumDp => MaximumAmount;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string CurrentDpData
    {
        get => _hasExactDp ? DpAmountCodec.Format(_currentDp) : string.Empty;
        private set
        {
            if (string.IsNullOrEmpty(value)) return;
            _currentDp = DpAmountCodec.Parse(value);
            _hasExactDp = true;
            NativeAmount(this) = DpAmountCodec.ToNative(_currentDp);
            _retainExactDpOnAttachment = true;
        }
    }

    internal sealed class NativeDpWrite
    {
        internal decimal Previous;
        internal decimal Current;
        internal decimal Maximum;
        internal bool RefreshProxy;
        internal bool Completed;
    }

    internal decimal? PrepareNativeDp(decimal amount)
    {
        decimal? previous = _preparedDp;
        _preparedDp = BigCombatNumbers.ClampStat(amount);
        _preparedDpDamage = 0m;
        return previous;
    }

    internal decimal? PrepareInitialNativeDp(decimal amount)
    {
        bool retain = _retainExactDpOnAttachment && amount == Amount;
        _retainExactDpOnAttachment = false;
        return PrepareNativeDp(retain ? CurrentDp : amount);
    }

    internal void RestorePreparedNativeDp(decimal? amount) => _preparedDp = amount;

    internal int PrepareNativeDpOffset(decimal offset)
    {
        decimal previous = CurrentDp;
        decimal next = DpAmountCodec.AddOffset(CurrentDp, offset);
        PrepareNativeDp(next);
        if (offset < 0m) _preparedDpDamage = Math.Max(0m, previous - next);
        return DpAmountCodec.ToNative(next) - Amount;
    }

    internal NativeDpWrite BeginNativeDpWrite(ref int amount)
    {
        AssertMutable();
        decimal previous = CurrentDp;
        decimal current = _preparedDp ?? BigCombatNumbers.ClampStat(amount);
        decimal dpDamage = _preparedDpDamage;
        // Consume before native UI/owner callbacks can synchronously reenter.
        _preparedDp = null;
        _preparedDpDamage = 0m;
        _retainExactDpOnAttachment = false;
        _currentDp = current;
        _hasExactDp = true;
        CardDamageHistoryHelper.RecordDpDamage(Owner,
            Math.Min(dpDamage, Math.Max(0m, previous - current)));
        int proxy = DpAmountCodec.ToNative(current);
        var write = new NativeDpWrite
        {
            Previous = previous, Current = current,
            Maximum = Owner != null ? MaximumAmount : 0m,
            RefreshProxy = previous != current && Amount == proxy
        };
        amount = proxy;
        _nativeWriteDepth++;
        _dpChanges.Enqueue(write);
        return write;
    }

    internal void FinishNativeDpWrite(NativeDpWrite write)
    {
        if (write == null || write.Completed) return;
        write.Completed = true;
        _nativeWriteDepth = Math.Max(0, _nativeWriteDepth - 1);
        if (_nativeWriteDepth != 0 || _dispatchingDpChanges) return;
        _dispatchingDpChanges = true;
        try
        {
            while (_dpChanges.TryDequeue(out NativeDpWrite change))
            {
                if (change.Previous == change.Current) continue;
                if (change.RefreshProxy)
                {
                    InvokeDisplayAmountChanged();
                    Owner?.InvokePowerModified(this, 0, true);
                }
                NotifyAmountChanged(change.Previous, change.Current, change.Maximum);
            }
        }
        finally { _dispatchingDpChanges = false; }
    }

    internal void SetDpAmount(decimal amount, bool silent = false)
    {
        decimal previousDamage = _preparedDpDamage;
        decimal? previous = PrepareNativeDp(amount);
        try { SetAmount(DpAmountCodec.ToNative(amount), silent); }
        finally
        {
            RestorePreparedNativeDp(previous);
            _preparedDpDamage = previousDamage;
        }
    }

    protected decimal PendingExtraDpDamage =>
        _pendingExtraDpDamage.TryPeek(out decimal amount) ? amount : 0m;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            if (IsMutable && Owner != null) text.Add("Amount", CurrentDp);
            return text;
        }
    }

    internal virtual LocString CompactDescription
    {
        get
        {
            LocString text = new("powers", Id.Entry + ".compactDescription");
            if (GetType() == typeof(HighDimensionalShellPower) &&
                IsMutable && Owner != null && IsDefaultBreakSuppressed())
                return new LocString("powers", Id.Entry + ".compactSuppressedBreakDescription");

            text.Add("breakAmount", GetDisplayedBreakAmount());
            return text;
        }
    }

    private string GetDisplayedBreakAmount()
    {
        if (!IsMutable || Owner == null)
            return $"{NormalBreakAmount}/{EliteBreakAmount}/{BossBreakAmount}";

        if (this is PartialDerivativeShieldPower) return "永久";
        if (this is PhantomHighDimensionalShellPower || IsDefaultBreakSuppressed())
            return "0";

        int amount = GetBreakAmount();
        if (Owner.HasPower<SkullFeatherPower>())
        {
            int currentBreak = Owner.GetPower<BreakPower>()?.Amount ?? 0;
            amount = Math.Max(amount, SkullFeatherPower.BreakDuration +
                HelloWorldCombatHelper.GetInitialBreakBonus(Owner) - currentBreak);
        }

        return amount.ToString(CultureInfo.InvariantCulture);
    }

    private bool IsDefaultBreakSuppressed() =>
        Owner.HasPower<PhantomPower>() ||
        GetObservers().OfType<IHighDimensionalShellBreakOverride>().Any(
            observer => observer.ShouldSuppressDefaultHighDimensionalShellBreak);

    public static decimal CalculateAmount(Creature creature)
    {
        if (creature == null) return 0m;
        decimal maxHpRatio = BaseMaxHpRatio;
        if (creature.Side == CombatSide.Enemy)
        {
            int configuredPercentage = Math.Clamp(
                HBRGOSakiConfig.EnemyHighDimensionalShellPercent,
                0,
                HBRGOSakiConfig.MaxEnemyHighDimensionalShellPercent);
            if (configuredPercentage <= 0)
            {
                return 0;
            }

            maxHpRatio = configuredPercentage / 100m;
            maxHpRatio *= GetEnemyShellMultiplier(creature);
        }

        return CalculateAmount(LargeCreatureHealthHelper.GetMaxHp(creature), maxHpRatio);
    }

    internal static decimal GetEnemyShellMultiplier(Creature creature) =>
        EnemyStatScalingHelper.GetDpGrowthMultiplier(creature) *
        (creature.Side == CombatSide.Enemy &&
        creature.CombatState?.RunState.AscensionLevel >= EnhancedShellAscensionLevel &&
        !PhantomAscensionHelper.ShouldSuppressEnemyShellEnhancement(creature)
            ? EnhancedEnemyShellMultiplier
            : 1m);

    internal static decimal CalculatePlayerAmount(decimal maxHp) =>
        CalculateAmount(maxHp, BaseMaxHpRatio);

    private static decimal CalculateAmount(decimal maxHp, decimal maxHpRatio) =>
        BigCombatNumbers.ScaleHealth(Math.Max(0m, maxHp), maxHpRatio);

    protected virtual decimal MaximumAmount => CalculateAmount(Owner);

    internal decimal MaxAmount => MaximumAmount;

    internal bool CanRestoreDp => !_pendingBreak &&
        (CurrentDp > 0m || PhantomHelper.IsOwnedMainPhantom(Owner, Owner?.PetOwner));

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        NotifyShellApplied();
        return CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        return CombatPreviewRefreshHelper.RefreshOpponentIntents(oldOwner);
    }

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (HpOnlyDamageContext.IsCurrentHpDamage(target, dealer, cardSource, props))
            return Task.CompletedTask;
        if (target == Owner)
        {
            // Consume this native hit's share even when block/DP prevents HP
            // loss. The later receipt may claim growth from the same share.
            HbrDestructionGainHelper.ObserveHit(target);
            _pendingExtraDpDamage.Push(
                ReptureBowPower.CalculateExtraDpDamage(
                    target,
                    props,
                    dealer,
                    cardSource));
        }

        return Task.CompletedTask;
    }

    public virtual decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target != Owner ||
            amount <= 0m ||
            _suppressDamageReductionDepth > 0)
        {
            return 1m;
        }

        return IncomingDamageMultiplier;
    }

    public virtual bool TryAbsorb(decimal amount)
    {
        _lastAbsorbedDamage = 0;
        if (amount <= 0m || CurrentDp <= 0m || _pendingBreak)
        {
            return false;
        }

        decimal extraDpDamage = PendingExtraDpDamage;
        decimal shellToSpend = Math.Min(CurrentDp,
            BigCombatNumbers.ClampStat(amount + extraDpDamage));
        if (shellToSpend <= 0)
        {
            return false;
        }

        SetDpAmount(CurrentDp - shellToSpend);
        CardDamageHistoryHelper.RecordDpDamage(Owner, Math.Max(0m, shellToSpend - amount));
        _lastAbsorbedDamage = shellToSpend;
        Flash();
        _pendingBreak = CurrentDp <= 0m;
        return true;
    }

    // The amount has already been resolved from another effect (for example,
    // actual block gained). Spend only DP: no block, damage multipliers or HP.
    // Callers dispatch their own owner/cannon receipt from the returned amount.
    internal async Task<decimal> DamageDpOnly(PlayerChoiceContext choiceContext, decimal amount)
    {
        if (!IsMutable || Owner?.IsAlive != true || amount <= 0m || CurrentDp <= 0m ||
            _pendingBreak || NararaNullificationHelper.IsCardNullified(Owner)) return 0m;

        decimal previousAbsorbed = _lastAbsorbedDamage;
        decimal actualDamage;
        // A nested DP effect must not inherit a bow bonus reserved for an
        // enclosing ordinary hit, or overwrite that hit's absorption receipt.
        _pendingExtraDpDamage.Push(0m);
        try
        {
            if (!TryAbsorb(BigCombatNumbers.ClampStat(amount))) return 0m;
            actualDamage = ConsumeLastAbsorbedDamage();
        }
        finally
        {
            _pendingExtraDpDamage.Pop();
            _lastAbsorbedDamage = previousAbsorbed;
        }

        // Layered DP can refill its next bar during TryAbsorb. Its receipt,
        // rather than a before/after subtraction, preserves the actual spend.
        CardDamageHistoryHelper.RecordDpDamage(Owner, actualDamage);
        if (_pendingBreak)
        {
            _pendingBreak = false;
            await ResolveBreak(choiceContext, 0);
        }
        else if (actualDamage > 0m)
        {
            await CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
        }

        return actualDamage;
    }

    public decimal Restore(decimal amount)
    {
        if (amount <= 0 || !CanRestoreDp)
        {
            return 0;
        }

        decimal maxAmount = MaximumAmount;
        decimal restoredAmount = Math.Min(BigCombatNumbers.ClampStat(amount),
            Math.Max(0m, maxAmount - CurrentDp));
        if (restoredAmount <= 0)
        {
            return 0;
        }

        SetDpAmount(CurrentDp + restoredAmount);
        Flash();
        return restoredAmount;
    }

    internal void ClampToMaximum()
    {
        decimal maximumAmount = MaximumAmount;
        if (CurrentDp > maximumAmount)
        {
            SetDpAmount(maximumAmount);
        }
        // Maximum DP can change while the current amount remains below it.
        // The HUD also needs to refresh in that case.
        InvokeDisplayAmountChanged();
    }

    internal decimal LastAbsorbedDamage => _lastAbsorbedDamage;

    internal decimal ConsumeLastAbsorbedDamage()
    {
        decimal absorbedDamage = _lastAbsorbedDamage;
        _lastAbsorbedDamage = 0;
        return absorbedDamage;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (HpOnlyDamageContext.IsCurrentHpDamage(target, dealer, cardSource, props)) return;
        if (target != Owner)
        {
            return;
        }

        HbrDestructionGainHelper.ObserveHit(target);

        if (_pendingExtraDpDamage.Count > 0)
        {
            _pendingExtraDpDamage.Pop();
        }

        if (!_pendingBreak)
        {
            return;
        }

        Creature brokenOwner = Owner;
        bool hbrOrdinaryShell = GetType() == typeof(HighDimensionalShellPower) &&
            HbrBattleStatsHelper.IsEnabled(brokenOwner);
        _pendingBreak = false;
        await ResolveBreak(choiceContext, 0);
        // The ordinary DP absorber discards this hit's HP overflow. Its break
        // still earns a fixed destruction share after the hit, while layered
        // bosses and other custom shell subclasses keep their own rules.
        if (hbrOrdinaryShell)
            brokenOwner.GetPower<DestructionRatePower>()?.AddHbrHitGain(dealer, cardSource, props);
    }

    internal virtual async Task BreakImmediately(
        PlayerChoiceContext choiceContext,
        int extraBreak)
    {
        if (CurrentDp <= 0m && !_pendingBreak)
        {
            return;
        }

        decimal consumedDp = CurrentDp;
        SetDpAmount(0m);
        CardDamageHistoryHelper.RecordDpDamage(Owner, consumedDp);
        _lastAbsorbedDamage = 0;
        _pendingBreak = false;
        await ResolveBreak(
            choiceContext,
            Math.Max(0, extraBreak));
    }

    private async Task ResolveBreak(
        PlayerChoiceContext choiceContext,
        int extraBreak)
    {
        IHighDimensionalShellObserver[] observers = GetObservers();
        IHighDimensionalShellBreakOverride[] breakOverrides = observers
            .OfType<IHighDimensionalShellBreakOverride>()
            .ToArray();
        bool suppressDefaultBreak = breakOverrides.Any(
            observer =>
                observer.ShouldSuppressDefaultHighDimensionalShellBreak);

        if (!Owner.IsDead && !Owner.HasPower<PhantomPower>())
        {
            if (!suppressDefaultBreak)
            {
                await ApplyBreakPower(
                    choiceContext,
                    GetBreakAmount() + extraBreak);
            }

            foreach (IHighDimensionalShellObserver observer in observers)
            {
                await observer.AfterHighDimensionalShellBreak(
                    choiceContext);
            }

            if (!suppressDefaultBreak &&
                !Owner.HasPower<DestructionRatePower>())
            {
                await PowerCmd.Apply<DestructionRatePower>(
                    choiceContext,
                    Owner,
                    DestructionRatePower.BaselineAmount,
                    Owner,
                    null);
            }

            // Resume a registered interrupted intent after the stun instead
            // of falling back to the monster's last performed move.
            if (!suppressDefaultBreak)
            {
                string interruptedMoveId = null;
                if (Owner.Monster?.NextMove is { } interruptedMove &&
                    Owner.Monster.MoveStateMachine.States.ContainsKey(
                        interruptedMove.Id))
                {
                    interruptedMoveId = interruptedMove.Id;
                }

                await CreatureCmd.Stun(Owner, interruptedMoveId);
            }
        }

        await PowerCmd.Remove(this);

        foreach (IHighDimensionalShellBreakOverride breakOverride in
                 breakOverrides)
        {
            await breakOverride.AfterHighDimensionalShellRemoved(
                choiceContext);
        }
    }

    private int GetBreakAmount()
    {
        RoomType roomType =
            Owner.CombatState?.Encounter?.RoomType ??
            Owner.CombatState?.RunState.CurrentRoom?.RoomType ??
            RoomType.Monster;

        int baseAmount = roomType switch
        {
            RoomType.Boss => BossBreakAmount,
            RoomType.Elite => EliteBreakAmount,
            _ => NormalBreakAmount,
        };
        return baseAmount + HelloWorldCombatHelper.GetInitialBreakBonus(Owner);
    }

    protected virtual Task ApplyBreakPower(
        PlayerChoiceContext choiceContext,
        int amount)
    {
        return PowerCmd.Apply<BreakPower>(
            choiceContext,
            Owner,
            amount,
            Owner,
            null);
    }

    private void NotifyShellApplied()
    {
        foreach (IHighDimensionalShellObserver observer in GetObservers())
        {
            observer.OnHighDimensionalShellApplied(this);
        }
    }

    private void NotifyAmountChanged(decimal previousAmount, decimal currentAmount, decimal maximumAmount)
    {
        if (previousAmount == currentAmount)
        {
            return;
        }

        foreach (IHighDimensionalShellObserver observer in GetObservers())
        {
            observer.OnHighDimensionalShellAmountChanged(
                previousAmount,
                currentAmount,
                maximumAmount);
        }
    }

    private IHighDimensionalShellObserver[] GetObservers()
    {
        return Owner?.Powers
            .OfType<IHighDimensionalShellObserver>()
            .ToArray() ?? Array.Empty<IHighDimensionalShellObserver>();
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingBreak = false;
        _lastAbsorbedDamage = 0;
        _pendingExtraDpDamage = new Stack<decimal>();
        _preparedDp = null;
        _preparedDpDamage = 0m;
        _nativeWriteDepth = 0;
        _dispatchingDpChanges = false;
        _dpChanges = new Queue<NativeDpWrite>();
        _retainExactDpOnAttachment = _hasExactDp;
    }

    internal static IDisposable SuppressDamageReduction()
    {
        _suppressDamageReductionDepth++;
        return new DamageReductionSuppressionScope();
    }

    private sealed class DamageReductionSuppressionScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _suppressDamageReductionDepth = Math.Max(
                0,
                _suppressDamageReductionDepth - 1);
        }
    }
}
