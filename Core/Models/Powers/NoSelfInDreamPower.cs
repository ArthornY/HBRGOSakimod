using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NoSelfInDreamPower : PowerModel
{
    public const decimal FinalDamageMultiplier = 1.5m;

    private const decimal _allyDamageMultiplier = 0.1m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F85C5C");

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        decimal multiplier =
            dealer == Owner &&
            target?.Side != Owner.Side &&
            amount > 0m
                ? FinalDamageMultiplier
                : 1m;
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
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
            result.TotalDamage <= 0 ||
            Owner.HasPower<CrimsonBladeFlowPower>())
        {
            return;
        }

        decimal allyDamage = result.TotalDamage * _allyDamageMultiplier;
        if (allyDamage <= 0)
        {
            return;
        }

        Creature[] allies = Owner.CombatState
            .GetTeammatesOf(Owner)
            .Where(creature => creature.IsAlive)
            .ToArray();
        if (allies.Length == 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            allies,
            allyDamage,
            ValueProp.Unpowered,
            null,
            null);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.TickDownDuration(this);
    }
}
