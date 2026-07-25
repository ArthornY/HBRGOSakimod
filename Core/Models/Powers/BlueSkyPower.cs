using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class BlueSkyPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CounterDamageBonus { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("70D9FF");

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target != Owner ||
            dealer == null ||
            dealer.Side == Owner.Side ||
            amount <= 0m ||
            !props.IsPoweredAttack())
        {
            return 1m;
        }

        decimal reduction = Math.Clamp(Amount, 0, 100) / 100m;
        decimal reducedDamage = Math.Floor(amount * (1m - reduction));
        return reducedDamage / amount;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target != Owner ||
            CounterDamageBonus <= 0 ||
            result == null ||
            !result.WasFullyBlocked ||
            dealer == null ||
            dealer.Side == Owner.Side ||
            !dealer.IsHittable ||
            !props.IsPoweredAttack())
        {
            return;
        }

        Flash();
        decimal damage =
            ModelDb.Card<StrikeHbrgoSaki>().DynamicVars.Damage.BaseValue +
            CounterDamageBonus;
        await GeneratedStrikeHelper.Play(choiceContext, Owner.Player, dealer, damage);
    }

    public void AddCounterDamageBonus(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        AssertMutable();
        CounterDamageBonus += amount;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}
