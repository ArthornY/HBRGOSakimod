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

        return BaseMaxAmount +
            SwordheartClarityPower.GetSwordheartMaxBonus(creature) +
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

        MagicSwordModePower magicSwordMode =
            target.GetPower<MagicSwordModePower>();
        if (magicSwordMode != null &&
            !magicSwordMode.IsGrantingClaritySwordheart)
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

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        if (Owner.Player?.Character is not HbrgoSaki31O ||
            power is not SwordheartPower ||
            power.Owner != Owner ||
            power.Amount < GetMaxAmount(Owner) ||
            Owner.HasPower<MagicSwordModePower>())
        {
            return;
        }

        if (Owner.Player?.PlayerCombatState == null ||
            Owner.CombatState == null ||
            Owner.Player.PlayerCombatState.AllCards.Any(card =>
                card is MagicSwordModeHbrgoSaki &&
                !card.HasBeenRemovedFromState &&
                card.Pile?.IsCombatPile == true &&
                card.Pile.Type != PileType.Exhaust))
        {
            return;
        }

        CardModel magicSwordModeCard =
            Owner.CombatState.CreateCard<MagicSwordModeHbrgoSaki>(Owner.Player);
        using (MagicSwordHandCapacityHelper.AllowOneExtraSlot())
        {
            await CardPileCmd.AddGeneratedCardToCombat(
                magicSwordModeCard,
                PileType.Hand,
                Owner.Player);
        }
    }
}
