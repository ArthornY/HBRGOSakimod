using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SwordheartPower : PowerModel
{
    public const int BaseMaxAmount = 7;

    public const int MaxAmount = BaseMaxAmount;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F8D36A");

    public static int GetMaxAmount(Creature creature)
    {
        if (creature?.Player?.Character is not HbrgoSaki31O)
        {
            return 0;
        }

        return GetKeptMaximum(creature) *
            SwordheartClarityPower.GetSwordheartMaximumMultiplier(creature);
    }

    // Swordheart is kept between combats (design v3: 「跨回合、跨战斗都保留」), up to the base
    // seven plus lasting raises; raises that last only this combat end with it.
    public static int GetKeptMaximum(Creature creature)
    {
        if (creature?.Player?.Character is not HbrgoSaki31O)
        {
            return 0;
        }

        return BaseMaxAmount +
            HbrgoSakiHextechCompatLoader
                .GetSwordheartMaximumBonus(creature);
    }

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        return Owner.Player?.Character is HbrgoSaki31O
            ? Task.CompletedTask
            : PowerCmd.Remove(this);
    }

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;

        if (target != Owner || canonicalPower is not SwordheartPower || amount <= 0m)
        {
            return false;
        }

        if (Owner.Player?.Character is not HbrgoSaki31O)
        {
            modifiedAmount = 0m;
            return true;
        }

        if (target.HasPower<MagicSwordModePower>())
        {
            modifiedAmount = 0m;
            return true;
        }

        int remaining = GetMaxAmount(target) - Amount;
        decimal cappedAmount = Math.Min(amount, Math.Max(0, remaining));
        if (cappedAmount == amount)
        {
            return false;
        }

        modifiedAmount = cappedAmount;
        return true;
    }
}
