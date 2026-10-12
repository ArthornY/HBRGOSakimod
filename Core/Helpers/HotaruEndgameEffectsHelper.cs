using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

// Consume the existing per-hit receipts. This keeps DP, HP, reactions and
// ordinary cannons on one calculation path without scanning combat each frame.
internal static class HotaruEndgameEffectsHelper
{
    private static Millsage ActiveRelic(Player player)
    {
        Millsage relic = player?.GetRelic<Millsage>();
        return relic != null && relic.EndgameSign != 0 && player.Creature?.IsAlive == true &&
            relic.GetCannon(HotaruCannonKind.Endgame) != null ? relic : null;
    }

    internal static bool HasAdvantage(Player player) =>
        ActiveRelic(player)?.WasCannonRetired(HotaruCannonKind.Longing) == true;

    internal static decimal GetBlockMultiplier(Creature creature)
    {
        Player player = creature?.Player;
        if (player == null && creature?.Monster is HotaruCannonModel)
            player = creature.PetOwner;
        Millsage relic = ActiveRelic(player);
        return relic?.EndgameSign > 0 && relic.WasCannonRetired(HotaruCannonKind.Coffin)
            ? 1.5m : 1m;
    }

    internal static Task ClearTurnDamage(Player player) =>
        player?.Creature != null ? PowerCmd.Remove<HotaruTurnDamageAmplificationPower>(player.Creature)
            : Task.CompletedTask;

    internal static Task OnTurnStart(Millsage relic, PlayerChoiceContext context)
    {
        if (relic == null || ActiveRelic(relic.Owner) != relic || relic.EndgameSign <= 0 ||
            !relic.WasCannonRetired(HotaruCannonKind.Happiness) || CombatManager.Instance.IsOverOrEnding)
            return Task.CompletedTask;
        decimal amount = Math.Max(1, relic.Owner.Creature.CombatState.RoundNumber) * 20m;
        // Happiness accumulates for this battle; Contest uses the separate
        // expiring pool below and cannot remove this or sacrifice's grant.
        return PowerCmd.Apply<HotaruDamageAmplificationPower>(context, relic.Owner.Creature,
            amount, relic.Owner.Creature, null);
    }

    internal static async Task OnCannonDamage(PlayerChoiceContext context, CannonHitReceipt receipt)
    {
        if (receipt?.ActualDamage <= 0m || receipt?.Cannon?.Monster is not HotaruCannonModel model ||
            !Millsage.IsOwnedCannon(receipt.Cannon, receipt.Cannon.PetOwner)) return;
        Millsage relic = ActiveRelic(receipt.Cannon.PetOwner);
        if (relic == null || CombatManager.Instance.IsOverOrEnding) return;

        if (relic.EndgameSign > 0)
        {
            if (relic.WasCannonRetired(HotaruCannonKind.Contest))
                await PowerCmd.Apply<HotaruTurnDamageAmplificationPower>(context, relic.Owner.Creature,
                    receipt.ActualDamage, relic.Owner.Creature, receipt.CardSource);
            return;
        }

        if (model.Kind != HotaruCannonKind.Endgame) return;
        if (relic.WasCannonRetired(HotaruCannonKind.Coffin))
            await HotaruCannonBlockHelper.Gain(relic.Owner.Creature, receipt.ActualDamage * 0.05m,
                receipt.CardPlay, receipt.Cannon);
        if (receipt.IsPrimary && receipt.ActualDpDamage > 0m &&
            relic.WasCannonRetired(HotaruCannonKind.Contest) && Millsage.IsEnemy(receipt.Target, relic.Owner))
        {
            // This supplement is already calculated from actual DP spent.
            // Do not tag this command as another cannon base hit: that would
            // scale the 25% again by Soul Power. Native HP-only damage retains
            // targeting, target caps, damage history and death callbacks.
            var results = await HpOnlyDamageContext.Damage(context, receipt.Target,
                receipt.ActualDpDamage * 0.25m, relic.Owner.Creature, receipt.CardSource);
            if (relic.WasCannonRetired(HotaruCannonKind.Coffin) && relic.Owner.Creature.IsAlive)
            {
                decimal actualHp = results.Sum(hit => Math.Max(0m, hit.UnblockedDamage));
                if (actualHp > 0m)
                    await HotaruCannonBlockHelper.Gain(relic.Owner.Creature, actualHp * 0.05m,
                        receipt.CardPlay, receipt.Cannon);
            }
        }
    }
}
