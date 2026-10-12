using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class HotaruDamageAmplificationPower : PowerModel, ICustomPower, IExactDamagePercentPower
{
    private ExactDamagePercentState _percent = new();
    public decimal DamagePercent => _percent.GetPercent(Amount);
    ExactDamagePercentState IExactDamagePercentPower.PercentState => _percent;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string AmplificationPercentData
    {
        get => _percent.SavedData;
        private set => _percent.LoadSavedData(this, value);
    }

    internal sealed class NativePercentWrite : ExactDamagePercentState.NativePercentWrite
    {
        public NativePercentWrite() { }
    }

    internal static int ToNativePercent(decimal amount) => ExactDamagePercentState.ToNativePercent(amount);
    internal decimal? PrepareNativePercent(decimal amount) => _percent.PrepareNativePercent(amount);
    internal decimal? PrepareInitialNativePercent(decimal amount) => _percent.PrepareInitialNativePercent(Amount, amount);
    internal void RestorePreparedNativePercent(decimal? amount) => _percent.RestorePreparedNativePercent(amount);
    internal int PrepareNativePercentOffset(decimal offset) => _percent.PrepareNativePercentOffset(Amount, offset);
    internal NativePercentWrite BeginNativePercentWrite(ref int amount) => _percent.BeginNativePercentWrite<NativePercentWrite>(this, ref amount);
    internal void FinishNativePercentWrite(NativePercentWrite write) => _percent.FinishNativePercentWrite(write);

    void IExactDamagePercentPower.RefreshNativePercentProxy()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            if (IsMutable) text.Add("Amount", DamagePercent);
            return text;
        }
    }

    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F2C15F");

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (dealer != Owner || props.HasFlag(ValueProp.Unpowered))
        {
            return 1m;
        }

        decimal multiplier = 1m + DamagePercent / 100m;
        return HbrgoSakiDamageMultiplierHelper.Add(
            multiplier,
            this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _percent = _percent.CloneForAttachment();
    }
}
