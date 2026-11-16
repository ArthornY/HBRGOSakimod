using System.Globalization;
using System.Numerics;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

// Faith is an exact resource, independent of percentage-buff potency/duration.
// Native Amount is only its bounded UI proxy; consumers use ExactAmount.
[CustomID("HBR_FAITH_POWER")]
public sealed class HbrFaithPower : PowerModel, ICustomPower
{
    private bool _writingProxy;
    private BigInteger? _preparedExact;
    internal bool IsWritingProxy => _writingProxy;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string FaithData { get; private set; } = string.Empty;
    public BigInteger ExactAmount => BigInteger.TryParse(FaithData, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out BigInteger value) ? BigInteger.Max(BigInteger.Zero, value) : BigInteger.Max(0, Amount);
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Proxy(ExactAmount);
    public string CustomPackedIconPath => "res://images/powers/tiny_moments_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    internal static int Proxy(BigInteger value) => (int)BigInteger.Min(BigCombatNumbers.NativeStatCap, BigInteger.Max(0, value));
    internal int PrepareNativeOffset(decimal offset)
    {
        _preparedExact = BigInteger.Max(BigInteger.Zero, ExactAmount + new BigInteger(decimal.Truncate(offset)));
        return Proxy(_preparedExact.Value) - Amount;
    }
    internal void InitializeExact(BigInteger value) { AssertMutable(); FaithData = BigInteger.Max(0, value).ToString(CultureInfo.InvariantCulture); }
    internal void ApplyExact(Creature holder, BigInteger value)
    {
        InitializeExact(value); _writingProxy = true;
        try { ApplyInternal(holder, Proxy(value)); } finally { _writingProxy = false; }
    }
    internal void SyncNativeAmount(int before)
    {
        if (_writingProxy || !IsMutable) return;
        if (_preparedExact is { } prepared)
        {
            _preparedExact = null;
            InitializeExact(prepared);
            InvokeDisplayAmountChanged();
            return;
        }
        bool known = BigInteger.TryParse(FaithData, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger exact);
        InitializeExact(Amount <= 0 ? BigInteger.Zero : known ? exact + Amount - before : Amount);
        InvokeDisplayAmountChanged();
    }
    internal void SetExact(BigInteger value)
    {
        InitializeExact(value);
        if (ExactAmount.IsZero) { RemoveInternal(); return; }
        _writingProxy = true;
        try { SetAmount(DisplayAmount); } finally { _writingProxy = false; }
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }
    public override LocString Description
    {
        get { LocString text = new("powers", Id.Entry + ".description"); text.Add("Faith", IsMutable ? ExactAmount.ToString(CultureInfo.InvariantCulture) : "0"); return text; }
    }
}
