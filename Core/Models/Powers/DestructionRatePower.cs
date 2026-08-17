using System;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DestructionRatePower : PowerModel
{
    public const int BaselineAmount = 1;

    private const decimal DamageConversionMultiplier = 0.5m;
    private const decimal DamageIncreasePerStack = 0.01m;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Math.Max(0, Amount - BaselineAmount);

    public override Color AmountLabelColor => new("FF9C6E");

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        decimal multiplier = target == Owner && amount > 0m
            ? 1m + DisplayAmount * DamageIncreasePerStack
            : 1m;
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
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
            result.UnblockedDamage <= 0 ||
            !Owner.HasPower<BreakPower>())
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<DestructionRatePower>(
            choiceContext,
            Owner,
            result.UnblockedDamage *
            DamageConversionMultiplier *
            MimicEvilEyeTidalSlashHbrgoSaki
                .GetDestructionRateMultiplier(cardSource),
            dealer ?? Owner,
            cardSource);
    }
}
