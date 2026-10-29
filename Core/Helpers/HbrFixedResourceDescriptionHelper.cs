using System;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Only authored, fixed resource fields belong here. Damage receipts, full
// recoveries and percentage values keep their existing combat-unit previews.
internal static class HbrFixedResourceDescriptionHelper
{
    internal static void AddParameters(CardModel card, LocString text)
    {
        string[] names = card switch
        {
            BindingRedemptionHbrgoSaki or EmberRecoveryHbrgoSaki or
                ShelterTheAfterimageHbrgoSaki or WorldsFinestHbrgoSaki => new[] { "Recovery" },
            OverdrawMovementHbrgoSaki or DpSoulStrikeHbrgoSaki or
                SelfSacrificeSlashHbrgoSaki or MercyAndCompassionHbrgoSaki or
                BorrowedEdgeHbrgoSaki => new[] { "DpLoss" },
            PressOnThroughPainHbrgoSaki or PainTransmissionHbrgoSaki or
                HolyOneHbrgoSaki or RapidChargingHbrgoSaki or BorrowLifeTogetherHbrgoSaki => new[] { "HpLoss" },
            FateHbrgoSaki => new[] { "MaxHpLoss" },
            UnbrokenRemnantHbrgoSaki => new[] { "UnbrokenRemnantPower" },
            _ => Array.Empty<string>()
        };
        foreach (string name in names)
        {
            decimal amount = card.DynamicVars[name].BaseValue;
            decimal displayed = card.IsMutable && card.Owner != null
                ? HbrResourceValueHelper.Fixed(card.Owner.Creature, amount)
                : BigCombatNumbers.MultiplyPositive(amount, HbrResourceValueHelper.FixedResourceScale);
            text.Add("Resource" + name, displayed);
        }
    }
}
