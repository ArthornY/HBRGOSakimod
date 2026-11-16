using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SwordheartClarityPower : PowerModel
{
    public const int SwordheartMaximumMultiplier = 2;

    public const int SwordheartConsumptionMultiplier = 2;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    public static int GetSwordheartMaximumMultiplier(Creature creature)
    {
        return creature?.Player?.Character is HbrgoSaki31O &&
               creature.GetPower<SwordheartClarityPower>()?.Amount > 0
            ? SwordheartMaximumMultiplier
            : 1;
    }

    public static decimal GetSwordheartConsumptionAmount(Creature creature, decimal amount)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        return creature?.Player?.Character is not HbrgoSaki31O ||
               creature.GetPower<SwordheartClarityPower>()?.Amount is not > 0
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

}
