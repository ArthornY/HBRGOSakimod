using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomIncomingDamageHelper
{
    internal static Creature GetReductionOwner(Creature target)
    {
        return target?.PetOwner?.Creature is { } owner &&
            (target.HasPower<PhantomPower>() || PhantomHelper.IsPhantomMinion(target))
                ? owner : null;
    }

    private static Creature GetReductionOwner(AbstractModel model, Creature target)
    {
        Creature owner = GetReductionOwner(target);
        if (owner == null) return null;
        return model switch
        {
            PowerModel power when power.Owner == owner => owner,
            RelicModel relic when relic.Owner == owner.Player => owner,
            CardModel card when card.Owner == owner.Player => owner,
            _ => null
        };
    }

    internal static decimal ModifyDamageAdditive(AbstractModel model, Creature target,
        decimal amount, ValueProp props, Creature dealer, CardModel cardSource, CardPlay cardPlay)
    {
        decimal original = DamageModifierCompatibility.ModifyDamageAdditive(
            model, target, amount, props, dealer, cardSource, cardPlay);
        Creature owner = GetReductionOwner(model, target);
        if (owner == null) return original;
        decimal shared = DamageModifierCompatibility.ModifyDamageAdditive(
            model, owner, amount, props, dealer, cardSource, cardPlay);
        // A model that already affects all allies must still contribute only once.
        return shared < 0m ? Math.Min(original, shared) : original;
    }

    internal static decimal ModifyDamageMultiplicative(AbstractModel model, Creature target,
        decimal amount, ValueProp props, Creature dealer, CardModel cardSource, CardPlay cardPlay)
    {
        Creature owner = GetReductionOwner(model, target);
        if (owner == null)
            return DamageModifierCompatibility.ModifyDamageMultiplicative(
                model, target, amount, props, dealer, cardSource, cardPlay);

        // Some HBR powers contribute to an additive multiplier group instead of
        // returning a factor. Isolate both evaluations so previewing the owner's
        // protection cannot add a second copy of a global modifier to that group.
        (decimal Factor, AdditiveDamageResult Group) Evaluate(Creature recipient)
        {
            HbrgoSakiDamageMultiplierHelper.Begin(dealer);
            bool completed = false;
            try
            {
                decimal factor = DamageModifierCompatibility.ModifyDamageMultiplicative(
                    model, recipient, amount, props, dealer, cardSource, cardPlay);
                AdditiveDamageResult group = HbrgoSakiDamageMultiplierHelper.Complete();
                completed = true;
                return (factor, group);
            }
            finally
            {
                if (!completed) HbrgoSakiDamageMultiplierHelper.Abort();
            }
        }

        var original = Evaluate(target);
        var shared = Evaluate(owner);
        HbrgoSakiDamageMultiplierHelper.AddPermanent(1m + original.Group.PermanentMultiplierOffset, model);
        HbrgoSakiDamageMultiplierHelper.AddTemporary(1m + original.Group.TemporaryPositiveMultiplierOffset, model);
        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + original.Group.OtherPositiveMultiplierOffset, model);
        HbrgoSakiDamageMultiplierHelper.AddNonTemporary(1m + Math.Min(
            original.Group.NegativeMultiplierOffset, shared.Group.NegativeMultiplierOffset), model);
        // Share reductions only, not the summoner's Vulnerable or damage bonuses.
        return shared.Factor < 1m ? Math.Min(original.Factor, shared.Factor) : original.Factor;
    }

    internal static decimal ModifyDamageCap(AbstractModel model, Creature target,
        ValueProp props, Creature dealer, CardModel cardSource, CardPlay cardPlay)
    {
        decimal original = DamageModifierCompatibility.ModifyDamageCap(
            model, target, props, dealer, cardSource, cardPlay);
        Creature owner = GetReductionOwner(model, target);
        return owner == null ? original : Math.Min(original,
            DamageModifierCompatibility.ModifyDamageCap(
                model, owner, props, dealer, cardSource, cardPlay));
    }
}
