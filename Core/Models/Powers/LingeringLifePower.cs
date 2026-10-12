using System;
using System.Globalization;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class LingeringLifePower : PowerModel
{
    // Kept readable for saves created with the former delayed-death effect.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsCountingDown { get; private set; }

    public decimal InheritedDamageBonusPercent { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string InheritedDamageBonusData
    {
        get => InheritedDamageBonusPercent.ToString(CultureInfo.InvariantCulture);
        private set
        {
            AssertMutable();
            InheritedDamageBonusPercent = decimal.TryParse(value, NumberStyles.Number,
                CultureInfo.InvariantCulture, out decimal bonus) ? DamageAmplificationLimits.Clamp(bonus) : 0m;
        }
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("E9A8C7");

    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("InheritedDamageBonus", InheritedDamageBonusPercent);
            return description;
        }
    }

    // MomentOfReign supplies the pre-death snapshot before losing a phantom level.
    internal async Task AfterPhantomDeath(PlayerChoiceContext choiceContext, decimal damageBonusPercent)
    {
        AssertMutable();
        decimal gain = Math.Max(0m, damageBonusPercent) / 2m;
        InheritedDamageBonusPercent = gain >= DamageAmplificationLimits.MaximumTemporaryPercent - InheritedDamageBonusPercent
            ? DamageAmplificationLimits.MaximumTemporaryPercent
            : DamageAmplificationLimits.Clamp(InheritedDamageBonusPercent + gain);
        Flash();
        await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (dealer != Owner || props.HasFlag(ValueProp.Unpowered))
            return 1m;

        return HbrgoSakiDamageMultiplierHelper.Add(
            1m + InheritedDamageBonusPercent / 100m, this);
    }
}
