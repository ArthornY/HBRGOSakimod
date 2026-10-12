using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruCannonDamageUpgradeHelper
{
    internal static bool Has(Creature cannon, HotaruCannonKind kind, int tier) =>
        cannon?.Monster is HotaruCannonModel model && model.Kind == kind &&
        Millsage.IsOwnedCannon(cannon, cannon.PetOwner) &&
        cannon.PetOwner.GetRelic<Millsage>()?.HasCannonUpgrade(kind, tier) == true;

    internal static decimal PrepareBaseDamage(Creature cannon, Creature target, decimal amount, int phaseMultiplier = 1)
    {
        if (Has(cannon, HotaruCannonKind.Contest, 4))
        {
            amount = DamageAmplificationLimits.AddSaturated(amount,
                Math.Max(0, target?.GetPowerAmount<FragilePower>() ?? 0) * Math.Clamp(phaseMultiplier, 1, 2));
        }
        return amount;
    }

    internal static void ContributeCannonRollMultiplier(Creature target, Creature dealer,
        CardModel card, ValueProp props)
    {
        if (!HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, card, props, out Creature cannon)) return;
        decimal multiplier = HotaruCannonSupportUpgradeHelper.GetCannonRollMultiplier(cannon,
            CardType.Attack, resolve: false, previewPhase: HotaruCannonStatsHelper.CurrentPreviewPhase);
        if (multiplier != 1m)
            HbrgoSakiDamageMultiplierHelper.AddTemporary(multiplier, cannon.PetOwner.GetRelic<Millsage>());
    }

    internal static async Task AfterCannonDamage(PlayerChoiceContext context, CannonHitReceipt receipt)
    {
        if (receipt?.Cannon == null || receipt.ActualDamage <= 0m) return;
        Creature cannon = receipt.Cannon;
        Millsage relic = cannon.PetOwner?.GetRelic<Millsage>();
        if (cannon.Monster is HotaruHappinessCannon)
        {
            relic?.ApplyHappinessHitGrowth();
            if (Has(cannon, HotaruCannonKind.Happiness, 4))
                await PowerCmd.Apply<HotaruDamageAmplificationPower>(context, receipt.Dealer,
                    receipt.ActualDamage, receipt.Dealer, receipt.CardSource);
        }
        if (cannon.Monster is not HotaruContestCannon) return;
        if (receipt.ActualHpDamage > 0m && Has(cannon, HotaruCannonKind.Contest, 1) &&
            Millsage.IsEnemy(receipt.Target, cannon.PetOwner))
            await PowerCmd.Apply<FragilePower>(context, receipt.Target, 1m, receipt.Dealer, receipt.CardSource);
        if (receipt.IsPrimary && receipt.ActualDpDamage > 0m && Has(cannon, HotaruCannonKind.Contest, 2) &&
            Millsage.IsEnemy(receipt.Target, cannon.PetOwner))
            await HotaruCannonStatsHelper.ReactionDamage(context, cannon, receipt.Target, 1m,
                receipt.CardSource, receipt.CardPlay, hpOnly: true);
        if (receipt.IsPrimary && receipt.ActualDpDamage > 0m && Has(cannon, HotaruCannonKind.Contest, 5) &&
            Millsage.IsEnemy(receipt.Target, cannon.PetOwner))
        {
            // Preserve the pre-hit current DP bar snapshot. The replacement bar
            // and its remaining bar count must not enlarge this one reaction.
            decimal baseDamage = HotaruAbsolutionUpgradeHelper.GetDpDamageBonus(receipt.ActiveDpMaximum);
            if (baseDamage > 0m)
                await HotaruCannonStatsHelper.ReactionDamage(context, cannon, receipt.Target, baseDamage,
                    receipt.CardSource, receipt.CardPlay, hpOnly: false);
        }
    }

    internal static Task GainStartingStrength(PlayerChoiceContext context, Millsage relic) =>
        relic?.HasCannonUpgrade(HotaruCannonKind.Contest, 3) == true
            ? PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(context,
                relic.Owner.Creature, 2m, relic.Owner.Creature, null)
            : Task.CompletedTask;
}
