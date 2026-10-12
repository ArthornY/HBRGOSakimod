using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SwordheartClarityPower : PowerModel
{
    public const int SwordheartMaxBonus = 7;

    public const int SwordheartConsumptionMultiplier = 2;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F8D36A");

    public static int GetSwordheartMaxBonus(Creature creature)
    {
        return creature?.Player?.Character is HbrgoSaki31O &&
               creature.GetPower<SwordheartClarityPower>() != null
            ? SwordheartMaxBonus
            : 0;
    }

    public static decimal GetSwordheartConsumptionAmount(Creature creature, decimal amount)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        return creature?.Player?.Character is not HbrgoSaki31O ||
               creature.GetPower<SwordheartClarityPower>() == null
            ? amount
            : amount * SwordheartConsumptionMultiplier;
    }

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        return Owner.Player?.Character is HbrgoSaki31O
            ? Task.CompletedTask
            : PowerCmd.Remove(this);
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner.Player?.Character is not HbrgoSaki31O ||
            Amount <= 0 ||
            side != Owner.Side ||
            !participants.Contains(Owner))
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<SwordheartPower>(choiceContext, Owner, Amount, Owner, null);
    }
}
