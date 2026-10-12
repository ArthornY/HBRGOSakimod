using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class LoliFormPower : PowerModel
{
    private const decimal _baseOutgoingDamageMultiplier = 0.5m;
    private const decimal _upgradedOutgoingDamageMultiplier = 0.75m;
    private const decimal _blockGainMultiplier = 1.25m;
    private const decimal _gentlenessMultiplier = 1.25m;
    private const decimal _madnessMultiplier = 0.75m;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasUpgradedOutgoingDamage { get; private set; }

    public override LocString Description => HasUpgradedOutgoingDamage
        ? new LocString("powers", "LOLI_FORM_POWER.upgradedDescription")
        : base.Description;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8A8D8");

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        if (cardSource is LoliFormHbrgoSaki { HasEnhancedOutgoingDamage: true })
        {
            AssertMutable();
            HasUpgradedOutgoingDamage = true;
        }

        return Task.CompletedTask;
    }

    public decimal GetSenseMultiplier(SixSenseAttribute attribute)
    {
        return attribute switch
        {
            SixSenseAttribute.Gentleness => _gentlenessMultiplier,
            SixSenseAttribute.Madness => _madnessMultiplier,
            _ => 1m
        };
    }

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        if (target != Owner || block <= 0m)
        {
            return 1m;
        }

        return _blockGainMultiplier;
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        decimal multiplier = 1m;
        if (dealer == Owner)
        {
            multiplier *= HasUpgradedOutgoingDamage
                ? _upgradedOutgoingDamageMultiplier
                : _baseOutgoingDamageMultiplier;
        }

        return multiplier;
    }
}
