using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class BeyondProtectionPower : PowerModel, ICustomPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ConvertedDp { get; private set; }

    private decimal? _exactDamagePercent;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string DamagePercentData
    {
        get => _exactDamagePercent.HasValue ? DamageAmplificationLimits.Format(DamagePercent) : string.Empty;
        private set
        {
            if (string.IsNullOrEmpty(value)) return;
            _exactDamagePercent = DamageAmplificationLimits.Parse(value);
            ConvertedDp = (int)Math.Min(int.MaxValue, decimal.Truncate(DamagePercent * 2m));
        }
    }

    // The old integer remains readable; gameplay and new saves use an exact
    // percentage bank without changing DP/HP that must later be returned.
    public decimal DamagePercent => DamageAmplificationLimits.Clamp(_exactDamagePercent ?? ConvertedDp / 2m);

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Color AmountLabelColor => new("F2C15F");
    public string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/phantom_damage_inheritance_power.tres";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            DeflectorProtection protection = IsMutable ? Owner?.Player?.GetRelic<DeflectorProtection>() : null;
            text.Add("DamagePercent", DamagePercent);
            text.Add("TemporaryMaxHp", (decimal)(protection?.BeyondProtectionPlayerMaxHp ?? 0) +
                (protection?.BeyondProtectionPhantomMaxHp ?? 0));
            text.Add("DpToReturn", protection?.BeyondProtectionDpToReturn ?? 0);
            return text;
        }
    }

    internal void AddConvertedDp(int removedDp)
    {
        AssertMutable();
        _exactDamagePercent = DamageAmplificationLimits.Clamp(DamagePercent + Math.Max(0, removedDp) / 2m);
        ConvertedDp = (int)Math.Min(int.MaxValue, decimal.Truncate(DamagePercent * 2m));
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        dealer == Owner && !props.HasFlag(ValueProp.Unpowered)
            ? HbrgoSakiDamageMultiplierHelper.Add(1m + DamagePercent / 100m, this)
            : 1m;

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ConvertedDp = 0;
        _exactDamagePercent = null;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
