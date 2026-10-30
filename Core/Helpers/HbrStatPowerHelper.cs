using System;
using HBRGOSakiMod.Core.Models.Monsters;
using CalamityPower = HBRGOSakiMod.Core.Models.Powers.HbrCalamityPower;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Status counters stay native and signed. Attribute views are computed on read,
// never saved back to the manual or applied cumulatively while previewing.
internal static class HbrStatPowerHelper
{
    internal static HbrBattleStats Apply(Creature creature, HbrBattleStats stats)
        => ApplyWithAdditionalStrength(creature, stats, 0m);

    internal static HbrBattleStats ApplyWithAdditionalStrength(Creature creature,
        HbrBattleStats stats, decimal additionalStrength)
    {
        if (!HbrBattleStatsHelper.IsEnabled(creature) || EnemyStatScalingHelper.IsEnemy(creature)) return stats;
        Creature source = creature?.Monster is HotaruCannonModel
            ? creature.PetOwner?.Creature ?? creature : creature;
        decimal strength = DamageAmplificationLimits.AddSaturated(
            source?.GetPowerAmount<StrengthPower>() ?? 0, additionalStrength);
        decimal dexterity = (source?.GetPowerAmount<DexterityPower>() ?? 0) +
            HotaruSeeYouAgainHelper.GetExtraDexterity(source?.Player);
        decimal calamity = Math.Max(0, creature?.GetPowerAmount<CalamityPower>() ?? 0);
        // Cannons inherit their summoner's live attributes. A debuff applied to
        // the cannon itself is local; phantoms retain their independent buffs.
        if (source != creature)
            calamity += Math.Max(0, source?.GetPowerAmount<CalamityPower>() ?? 0);
        return stats with
        {
            Strength = ApplyPercent(stats.Strength, strength, calamity),
            Dexterity = ApplyPercent(stats.Dexterity, strength, calamity),
            Intelligence = ApplyPercent(stats.Intelligence, dexterity, calamity),
            Vitality = Adjust(stats.Vitality, -calamity),
            Spirit = Adjust(stats.Spirit, -calamity),
            Luck = Adjust(stats.Luck, -calamity)
        };
    }

    internal static decimal GetEnemyIgnorePerLayer(Creature creature) =>
        Math.Max(0, creature?.CombatState?.RunState?.CurrentActIndex ?? 0) switch
        {
            0 => 1m, 1 => 2m, 2 => 3m, 3 => 4m, _ => 8m
        };

    internal static decimal GetEnemyStrengthIgnorePerLayer(Creature creature) => GetEnemyIgnorePerLayer(creature);

    internal static HbrBattleStats GetIncomingDefense(Creature dealer, HbrBattleStats defense)
    {
        if (!EnemyStatScalingHelper.IsEnemy(dealer) || !HbrBattleStatsHelper.IsEnabled(dealer)) return defense;
        int layers = dealer.GetPowerAmount<StrengthPower>();
        if (layers == 0) return defense;
        decimal ignored = (decimal)layers * GetEnemyIgnorePerLayer(dealer);
        return defense with
        {
            Vitality = Adjust(defense.Vitality, -ignored),
            Spirit = Adjust(defense.Spirit, -ignored)
        };
    }

    internal static HbrBattleStats GetOutgoingAttack(Creature target, Creature dealer,
        HbrBattleStats attack, CardModel card, ValueProp props)
    {
        // Happiness retains its upgrade: a second copy of positive Strength's
        // attribute bonus applies to this cannon's attack only.
        if (HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, card, props, out Creature cannon))
        {
            if (HotaruCannonDamageUpgradeHelper.Has(cannon, HotaruCannonKind.Happiness, 3))
            {
                decimal extra = Math.Max(0, dealer?.GetPowerAmount<StrengthPower>() ?? 0);
                // Recompute from the raw profile so a reduction that already
                // reached zero cannot be lost before the extra Strength.
                if (extra > 0m && HbrBattleStatsHelper.TryGetStatsWithAdditionalStrength(
                        dealer, extra, out HbrBattleStats upgraded))
                    attack = attack with { Strength = upgraded.Strength, Dexterity = upgraded.Dexterity };
            }
            // Cannon attacks use the summoner as dealer. The dealer's own
            // reduction is already present; apply the firing cannon's local
            // reduction once, without changing another cannon's attributes.
            decimal calamity = Math.Max(0, cannon.GetPowerAmount<CalamityPower>());
            attack = attack with
            {
                Strength = Adjust(attack.Strength, -calamity),
                Dexterity = Adjust(attack.Dexterity, -calamity),
                Intelligence = Adjust(attack.Intelligence, -calamity),
                Vitality = Adjust(attack.Vitality, -calamity),
                Spirit = Adjust(attack.Spirit, -calamity),
                Luck = Adjust(attack.Luck, -calamity)
            };
        }
        if (!EnemyStatScalingHelper.IsEnemy(target) || !HbrBattleStatsHelper.IsEnabled(target)) return attack;
        int layers = target.GetPowerAmount<DexterityPower>();
        if (layers == 0) return attack;
        decimal ignored = (decimal)layers * GetEnemyIgnorePerLayer(target);
        return attack with
        {
            Strength = Adjust(attack.Strength, -ignored),
            Dexterity = Adjust(attack.Dexterity, -ignored)
        };
    }

    private static decimal Adjust(decimal value, decimal delta) =>
        Math.Max(0m, DamageAmplificationLimits.AddSaturated(value, delta));

    // Each native layer is one percentage point of the unbuffed live attribute.
    // Flat Calamity applies afterward so one layer still removes one stat point.
    private static decimal ApplyPercent(decimal value, decimal layers, decimal calamity) =>
        Adjust(DamageAmplificationLimits.MultiplySaturated(Math.Max(0m, value),
            Math.Max(0m, 1m + layers / 100m)), -calamity);
}
