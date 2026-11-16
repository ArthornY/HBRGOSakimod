using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DualSwordsEnhancementPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasEnhancedDamage { get; private set; }

    public decimal DamageMultiplier => 1m;

    public override LocString Description => HasEnhancedDamage
        ? new LocString(
            "powers",
            "DUAL_SWORDS_ENHANCEMENT_POWER.upgradedDescription")
        : base.Description;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        if (cardSource is DualSwordsEnhancementHbrgoSaki
            {
                HasEnhancedDamage: true
            })
        {
            AssertMutable();
            HasEnhancedDamage = true;
        }

        return Task.CompletedTask;
    }
}
