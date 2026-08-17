using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MagicSwordEnhancementPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasEnhancedHpDamage { get; private set; }

    public override LocString Description => HasEnhancedHpDamage
        ? new LocString(
            "powers",
            "MAGIC_SWORD_ENHANCEMENT_POWER.upgradedDescription")
        : base.Description;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        if (cardSource is MagicSwordEnhancementHbrgoSaki
            {
                HasEnhancedHpDamage: true
            })
        {
            AssertMutable();
            HasEnhancedHpDamage = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel cardSource)
    {
        if (dealer != Owner ||
            target == null ||
            target.Side == Owner.Side ||
            props.HasFlag(ValueProp.Unblockable) ||
            !Owner.HasPower<MagicSwordModePower>())
        {
            return;
        }

        HighDimensionalShellPower shell =
            target.GetPower<HighDimensionalShellPower>();
        int absorbedDamage =
            shell?.ConsumeLastAbsorbedDamage() ?? 0;
        if (absorbedDamage <= 0)
        {
            return;
        }

        decimal divisor = HasEnhancedHpDamage ? 2m : 3m;
        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            target,
            absorbedDamage / divisor,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner,
            cardSource);
    }
}
