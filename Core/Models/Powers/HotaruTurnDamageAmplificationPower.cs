using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_TURN_DAMAGE_AMPLIFICATION_POWER")]
public sealed class HotaruTurnDamageAmplificationPower : PowerModel, ICustomPower, IExactDamagePercentPower
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

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

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

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        dealer == Owner && !props.HasFlag(ValueProp.Unpowered)
            ? HbrgoSakiDamageMultiplierHelper.Add(1m + DamagePercent / 100m, this) : 1m;

    // Millsage clears this at its guarded START boundary, including OD. An
    // independent native hook could run after the first new-turn cannon hit
    // and erase the damage that hit has just earned.
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _percent = _percent.CloneForAttachment();
    }
}
