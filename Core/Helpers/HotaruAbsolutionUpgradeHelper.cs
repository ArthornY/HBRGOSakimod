using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruAbsolutionUpgradeHelper
{
    internal const decimal DoomPerDamage = 0.5m;
    internal const decimal CurrentDpMaximumDamageRatio = 0.005m;
    internal const decimal DamagePerDoomStack = 0.2m;

    internal static bool HasUpgrade(Millsage relic, int tier) =>
        relic?.HasCannonUpgrade(HotaruCannonKind.Absolution, tier) == true;

    internal static bool HasUpgrade(Creature cannon, int tier) =>
        cannon?.Monster is HotaruAbsolutionCannon &&
        Millsage.IsOwnedCannon(cannon, cannon.PetOwner) &&
        HasUpgrade(cannon.PetOwner.GetRelic<Millsage>(), tier);

    internal static bool HasOwnerUpgrade(Creature dealer, int tier)
    {
        Player player = dealer?.Player ?? (dealer?.Monster is HotaruCannonModel ? dealer.PetOwner : null);
        return player?.Creature?.CombatState != null && dealer.CombatState == player.Creature.CombatState &&
            HasUpgrade(player.GetRelic<Millsage>(), tier);
    }

    internal static bool HasActiveDp(Creature target) =>
        target?.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m;

    internal static bool CanAccumulateDestructionWithDp(Creature target, Creature dealer) =>
        target?.IsAlive == true && dealer != null && target.CombatState == dealer.CombatState &&
        target.Side != dealer.Side && HasActiveDp(target) && HasOwnerUpgrade(dealer, 6);

    // The caption has no damage dealer. It shows the DP multiplier available
    // to upgraded Hotaru owners; the damage hook also gates it per dealer so
    // an unupgraded teammate does not receive that benefit.
    internal static decimal GetDestructionRateEffectScale(Creature target)
    {
        if (!HasActiveDp(target)) return 1m;
        bool enabled = target.CombatState?.Players.Any(player => player.Creature.Side != target.Side &&
            HasUpgrade(player.GetRelic<Millsage>(), 6)) == true;
        return enabled ? 0.5m : 0m;
    }

    internal static decimal GetDestructionRateEffectScale(Creature target, Creature dealer) =>
        !HasActiveDp(target) ? 1m : CanAccumulateDestructionWithDp(target, dealer) ? 0.5m : 0m;

    internal static int GetEndHitCount(Millsage relic) => HasUpgrade(relic, 1) ? 3 : 1;

    internal static Task GainTurnEnergy(Millsage relic, Player player) =>
        player != null && player == relic?.Owner && player.Creature.IsAlive && HasUpgrade(relic, 4)
            ? PlayerCmd.GainEnergy(1m, player)
            : Task.CompletedTask;

    internal static void AfterCombatEnd(Millsage relic)
    {
        if (HasUpgrade(relic, 5)) relic.Owner?.GetRelic<Overclock>()?.RestoreFloorLevels(1);
    }

    // Snapshot the active pool before a hit. Narara's MaximumDp is one layer's
    // maximum, so neither a later replacement layer nor the layer count enters
    // the follow-up calculation.
    internal static decimal GetDpDamageBonus(decimal activeDpMaximum) =>
        Math.Max(0m, activeDpMaximum) * CurrentDpMaximumDamageRatio;

    internal static decimal GetDoomFlatDamageBonus(Creature cannon, Creature target) =>
        HasUpgrade(cannon, 7) ? Math.Max(0, target?.GetPowerAmount<DoomPower>() ?? 0) * DamagePerDoomStack : 0m;

    internal static decimal GetDoomGain(decimal actualHpDamage, decimal actualDpDamage, int existingDoom)
    {
        decimal actualDamage = DamageAmplificationLimits.AddSaturated(
            Math.Max(0m, actualHpDamage), Math.Max(0m, actualDpDamage));
        return Math.Min(Math.Max(0L, int.MaxValue - (long)Math.Max(0, existingDoom)),
            decimal.Floor(actualDamage * DoomPerDamage));
    }

    internal static async Task OnOwnerDpDamage(PlayerChoiceContext context, Creature dealer,
        Creature target, decimal actualDpDamage, CardModel cardSource,
        ValueProp props = ValueProp.Move, bool primary = true)
    {
        if (actualDpDamage <= 0m || target?.IsAlive != true || dealer == null ||
            target.Side == dealer.Side || target.CombatState != dealer.CombatState ||
            !HasOwnerUpgrade(dealer, 6)) return;

        // Establish the hidden counter separately: its baseline is metadata,
        // and must not be doubled by the Contest cannon's amount-gain hook.
        if (!target.HasPower<DestructionRatePower>())
            await PowerCmd.Apply<DestructionRatePower>(context, target,
                DestructionRatePower.BaselineAmount, target, null);
        if (HbrBattleStatsHelper.IsEnabled(target))
        {
            // A normal shell's breaking hit already advanced destruction after
            // creating Break. Its later receipt has no active DP and must not
            // advance twice. A still-active (including layered) pool retains
            // the upgrade's ability to accumulate destruction before Break.
            if (primary && HasActiveDp(target))
                target.GetPower<DestructionRatePower>()?.AddHbrHitGain(dealer, cardSource, props);
            return;
        }
        if (target.IsAlive)
            await PowerCmd.Apply<DestructionRatePower>(context, target,
                DamageAmplificationLimits.MultiplySaturated(actualDpDamage * DoomPerDamage,
                    MimicEvilEyeTidalSlashHbrgoSaki.GetDestructionRateMultiplier(cardSource)),
                dealer, cardSource);
    }

    internal static async Task OnCannonDamage(PlayerChoiceContext context, CannonHitReceipt receipt)
    {
        if (receipt == null || receipt.Cannon?.Monster is not HotaruAbsolutionCannon ||
            !Millsage.IsOwnedCannon(receipt.Cannon, receipt.Cannon.PetOwner)) return;

        await ApplyDoom(context, receipt.Cannon, receipt.Target,
            receipt.ActualHpDamage, receipt.ActualDpDamage, receipt.CardSource);

    }

    internal static async Task ApplyDoom(PlayerChoiceContext context, Creature cannon,
        Creature target, decimal actualHpDamage, decimal actualDpDamage, CardModel cardSource)
    {
        if (!HasUpgrade(cannon, 3) || target?.IsAlive != true || target.Side == cannon.Side) return;
        decimal amount = GetDoomGain(actualHpDamage, actualDpDamage, target.GetPowerAmount<DoomPower>());
        if (amount > 0m)
            await PowerCmd.Apply<DoomPower>(context, target, amount, cannon.PetOwner.Creature, cardSource);
    }
}
