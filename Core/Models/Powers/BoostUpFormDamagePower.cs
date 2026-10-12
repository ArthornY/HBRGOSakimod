using System;
using System.Collections.Generic;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class BoostUpFormDamagePower : PowerModel, ICustomPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BowPercent { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DualSwordsPercent { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ScythePercent { get; private set; }

    private decimal? _exactBowPercent;
    private decimal? _exactDualSwordsPercent;
    private decimal? _exactScythePercent;
    public decimal ExactBowPercent => DamageAmplificationLimits.Clamp(_exactBowPercent ?? BowPercent);
    public decimal ExactDualSwordsPercent => DamageAmplificationLimits.Clamp(_exactDualSwordsPercent ?? DualSwordsPercent);
    public decimal ExactScythePercent => DamageAmplificationLimits.Clamp(_exactScythePercent ?? ScythePercent);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string BowPercentData
    {
        get => _exactBowPercent.HasValue ? DamageAmplificationLimits.Format(ExactBowPercent) : string.Empty;
        private set { if (!string.IsNullOrEmpty(value)) SetBow(DamageAmplificationLimits.Parse(value)); }
    }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string DualSwordsPercentData
    {
        get => _exactDualSwordsPercent.HasValue ? DamageAmplificationLimits.Format(ExactDualSwordsPercent) : string.Empty;
        private set { if (!string.IsNullOrEmpty(value)) SetDualSwords(DamageAmplificationLimits.Parse(value)); }
    }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string ScythePercentData
    {
        get => _exactScythePercent.HasValue ? DamageAmplificationLimits.Format(ExactScythePercent) : string.Empty;
        private set { if (!string.IsNullOrEmpty(value)) SetScythe(DamageAmplificationLimits.Parse(value)); }
    }

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
            if (!IsMutable)
                return new LocString("powers", Id.Entry + ".canonicalDescription");
            LocString text = base.Description;
            text.Add("BowPercent", ExactBowPercent);
            text.Add("DualSwordsPercent", ExactDualSwordsPercent);
            text.Add("ScythePercent", ExactScythePercent);
            text.Add("ActivePercent", GetActiveDamagePercent(null));
            return text;
        }
    }

    internal void AddDamageBonus(IReadOnlyList<ReptureCardMode> forms, int percent)
    {
        AssertMutable();
        foreach (ReptureCardMode form in forms)
        {
            switch (form)
            {
                case ReptureCardMode.Bow:
                    SetBow(ExactBowPercent + Math.Max(0, percent));
                    break;
                case ReptureCardMode.DualSwords:
                    SetDualSwords(ExactDualSwordsPercent + Math.Max(0, percent));
                    break;
                case ReptureCardMode.Scythe:
                    SetScythe(ExactScythePercent + Math.Max(0, percent));
                    break;
            }
        }
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }

    internal decimal GetActiveDamagePercent(CardModel cardSource)
    {
        if (Owner == null) return 0m;
        // An off-mode attack switches weapons before dealing damage. Its preview
        // must use that resulting weapon rather than the currently equipped one.
        if (cardSource?.Owner?.Creature == Owner &&
            ModeTransitionHelper.TryGetDamagePreview(cardSource, out _))
            return GetModePercent(ModeTransitionHelper.GetCardMode(cardSource));

        return (Owner.HasPower<ReptureBowPower>() ? ExactBowPercent : 0m) +
            (Owner.HasPower<ReptureDualSwordsPower>() ? ExactDualSwordsPercent : 0m) +
            (Owner.HasPower<RaptureScythePower>() ? ExactScythePercent : 0m);
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (dealer != Owner || props.HasFlag(ValueProp.Unpowered)) return 1m;
        return HbrgoSakiDamageMultiplierHelper.Add(1m + GetActiveDamagePercent(cardSource) / 100m, this);
    }

    private decimal GetModePercent(ReptureCardMode form) => form switch
    {
        ReptureCardMode.Bow => ExactBowPercent,
        ReptureCardMode.DualSwords => ExactDualSwordsPercent,
        ReptureCardMode.Scythe => ExactScythePercent,
        _ => 0
    };

    private void SetBow(decimal percent)
    {
        _exactBowPercent = DamageAmplificationLimits.Clamp(percent);
        BowPercent = ToLegacy(ExactBowPercent);
    }
    private void SetDualSwords(decimal percent)
    {
        _exactDualSwordsPercent = DamageAmplificationLimits.Clamp(percent);
        DualSwordsPercent = ToLegacy(ExactDualSwordsPercent);
    }
    private void SetScythe(decimal percent)
    {
        _exactScythePercent = DamageAmplificationLimits.Clamp(percent);
        ScythePercent = ToLegacy(ExactScythePercent);
    }
    private static int ToLegacy(decimal percent) => (int)Math.Min(int.MaxValue, decimal.Truncate(percent));
}
