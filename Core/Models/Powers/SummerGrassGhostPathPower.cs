using System;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SummerGrassGhostPathPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F85C5C");
    public string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/ghost_state_power.tres";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal static decimal GetDamageMultiplier(int amount)
    {
        decimal multiplier = 1m;
        decimal maximum = 1m + DamageAmplificationLimits.MaximumTemporaryPercent / 100m;
        for (int index = 0; index < Math.Max(0, amount); index++)
        {
            if (multiplier >= maximum / 2m) return maximum;
            multiplier *= 2m;
        }
        return multiplier;
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        // Return the factor directly rather than joining the mod's additive
        // damage-bonus group: this doubles the entire existing damage result.
        // A null target is the character-status/untargeted preview path.
        return dealer == Owner && target?.Side != Owner.Side && amount > 0m
            ? GetDamageMultiplier(Amount)
            : 1m;
    }
}
