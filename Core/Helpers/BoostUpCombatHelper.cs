using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class BoostUpCombatHelper
{
    internal static int GetDpLoss(int maximumDp) => Math.Max(0, maximumDp) / 10;
    internal static decimal GetDpLoss(decimal maximumDp) => decimal.Floor(Math.Max(0m, maximumDp) / 10m);

    internal static async Task ApplyUpgradeEffects(PlayerChoiceContext context,
        BoostUpHbrgoSaki card, int remainingHandCount)
    {
        Creature owner = card.Owner.Creature;
        if (card.HasUpgradeEffect(0))
            await ApplyDamageBonus(context, card, 50);

        if (card.HasUpgradeEffect(1))
        {
            await PlayerCmd.GainEnergy(1m, card.Owner);
            Creature[] allies = owner.CombatState.GetTeammatesOf(owner)
                .Where(ally => ally.IsAlive).ToArray();
            foreach (Creature ally in allies)
            {
                await LoseAllyDp(context, card, ally);
                if (ally.IsAlive)
                    await PowerCmd.Apply<StrengthPower>(context, ally, 3m, owner, card);
            }
        }

        if (card.HasUpgradeEffect(2) && remainingHandCount > 5)
            await PowerCmd.Apply<HotaruDamageAmplificationPower>(context, owner, 50m, owner, card);

        if (card.HasUpgradeEffect(4))
            await PowerCmd.Apply<BoostUpExtraTurnPower>(context, owner, 1m, owner, card);

        if (card.HasUpgradeEffect(5))
        {
            await PowerCmd.Apply<StrengthPower>(context, owner, 1m, owner, card);
            await PlayerCmd.GainEnergy(1m, card.Owner);
            await ApplyDamageBonus(context, card, 25);
        }
    }

    private static Task ApplyDamageBonus(PlayerChoiceContext context,
        BoostUpHbrgoSaki card, int percent)
    {
        if (!card.UsesThirtyAEffects)
            return BoostUpFormHelper.ApplyDamageBonus(context, card, percent);

        // Like other 30A damage bonuses, this belongs to the living phantom
        // that receives it, rather than to forms or later summons.
        BoostUpPhantomHelper.ApplyDamageBonus(card.Owner, percent);
        return Task.CompletedTask;
    }

    private static async Task LoseAllyDp(PlayerChoiceContext context,
        BoostUpHbrgoSaki card, Creature ally)
    {
        if (ally.Player?.GetRelic<DeflectorProtection>() is { } protection)
        {
            protection.LoseDp(GetDpLoss(protection.MaxDp));
            await protection.ResolveMentalStrengthDpLossRewards(context, card);
        }
        else if (ally.GetPower<HighDimensionalShellPower>() is { } shell)
        {
            decimal loss = Math.Min(shell.CurrentDp, GetDpLoss(shell.MaximumDp));
            if (loss > 0)
                await PowerCmd.ModifyAmount(context, shell, -loss, card.Owner.Creature, card);
        }
    }
}
