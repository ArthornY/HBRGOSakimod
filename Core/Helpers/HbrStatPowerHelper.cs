using System;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
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
        // Morale's fixed attribute points belong to the raw profile, before
        // percent stat buffs, Calamity and the final Crimson stance. Cannons
        // inherit their owner's level once; phantoms retain their own level.
        decimal morale = HbrMoralePower.GetAttributeBonus(source);
        if (morale != 0m) stats = stats with
        {
            Strength = DamageAmplificationLimits.AddSaturated(stats.Strength, morale),
            Dexterity = DamageAmplificationLimits.AddSaturated(stats.Dexterity, morale),
            Intelligence = DamageAmplificationLimits.AddSaturated(stats.Intelligence, morale),
            Vitality = DamageAmplificationLimits.AddSaturated(stats.Vitality, morale),
            Spirit = DamageAmplificationLimits.AddSaturated(stats.Spirit, morale),
            Luck = DamageAmplificationLimits.AddSaturated(stats.Luck, morale)
        };
        // 31A's Dodge adds its Strength and Dexterity here on read, as See You Again's
        // Dexterity does; neither is a stored stat power.
        int dodge = DodgeCombatHelper.GetStatBonus(source);
        decimal strength = DamageAmplificationLimits.AddSaturated(DamageAmplificationLimits.AddSaturated(
            source?.GetPowerAmount<StrengthPower>() ?? 0, dodge), additionalStrength);
        decimal dexterity = (source?.GetPowerAmount<DexterityPower>() ?? 0) + dodge +
            HotaruSeeYouAgainHelper.GetExtraDexterity(source?.Player);
        decimal calamity = CalamityPower.GetTotalReduction(creature);
        // Cannons inherit their summoner's live attributes. A debuff applied to
        // the cannon itself is local; phantoms retain their independent buffs.
        if (source != creature)
            calamity += CalamityPower.GetTotalReduction(source);
        decimal wisdomFactor = HBRGOSakiMod.Core.Models.Powers.HbrWisdomDownPower.GetWisdomFactor(creature);
        decimal spiritFactor = HBRGOSakiMod.Core.Models.Powers.HbrSpiritDownPower.GetSpiritFactor(creature);
        if (source != creature)
        {
            // Owner-sourced cannons inherit the owner's percentage stat
            // reductions as well as their own. Add the independent reductions
            // in one bucket; multiplying factors would incorrectly compound.
            wisdomFactor = DamageAmplificationLimits.AddSaturated(wisdomFactor,
                HBRGOSakiMod.Core.Models.Powers.HbrWisdomDownPower.GetWisdomFactor(source) - 1m);
            spiritFactor = DamageAmplificationLimits.AddSaturated(spiritFactor,
                HBRGOSakiMod.Core.Models.Powers.HbrSpiritDownPower.GetSpiritFactor(source) - 1m);
        }
        decimal sakuraDefense = DamageAmplificationLimits.AddSaturated(
            HBRGOSakiMod.Core.Models.Powers.SakuraRainPower.GetDefenseBonusPercent(source),
            ThirtyOneBBondHelper.GetCharmDefenseBonusPercent(source));
        HbrBattleStats adjusted = stats with
        {
            Strength = ApplyPercent(stats.Strength, DamageAmplificationLimits.MultiplySaturated(strength, 2m), calamity),
            Dexterity = ApplyPercent(stats.Dexterity, DamageAmplificationLimits.MultiplySaturated(strength, 2m), calamity),
            // Wisdom Down (original WisdomDownRate) takes its share last, as
            // Spirit Down does.
            Intelligence = DamageAmplificationLimits.MultiplySaturated(ApplyPercent(stats.Intelligence,
                DamageAmplificationLimits.MultiplySaturated(dexterity, 2m), calamity),
                wisdomFactor),
            Vitality = DamageAmplificationLimits.AddSaturated(ApplyPercent(stats.Vitality, sakuraDefense, calamity),
                IchikoEldestCombatHelper.GetVitalityBonus(source)),
            // Spirit Down (original SpiritDownRate) is a share of the
            // remaining live attribute, read here like Calamity.
            Spirit = DamageAmplificationLimits.MultiplySaturated(ApplyPercent(stats.Spirit, sakuraDefense, calamity),
                spiritFactor),
            Luck = Adjust(stats.Luck, -calamity)
        };
        // Crimson is a final attribute stance, after percentages and flat
        // reductions. It cannot bring Spirit/Vitality back through Sakura.
        if (!CrimsonBladeFlowPower.IsActive(source)) return adjusted;
        decimal crimsonDefense = HisameCloseSlashCombatHelper.HasMastersAdvice(source?.Player ?? source?.PetOwner) ? .5m : 0m;
        return adjusted with
        {
            Strength = DamageAmplificationLimits.MultiplySaturated(adjusted.Strength, 2m),
            Dexterity = DamageAmplificationLimits.MultiplySaturated(adjusted.Dexterity, 2m),
            Vitality = DamageAmplificationLimits.MultiplySaturated(adjusted.Vitality, crimsonDefense),
            Spirit = DamageAmplificationLimits.MultiplySaturated(adjusted.Spirit, crimsonDefense)
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
            decimal calamity = CalamityPower.GetTotalReduction(cannon);
            // The cannon's own Calamity belongs before the inherited final
            // Crimson doubling, just like the same cannon's displayed stats.
            decimal attackCalamity = DamageAmplificationLimits.MultiplySaturated(calamity,
                CrimsonBladeFlowPower.IsActive(dealer) ? 2m : 1m);
            attack = attack with
            {
                Strength = Adjust(attack.Strength, -attackCalamity),
                Dexterity = Adjust(attack.Dexterity, -attackCalamity),
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

    // This helper receives percentage points. Native allied Strength/Dexterity
    // contribute two per layer; Sakura already supplies its real percentage.
    private static decimal ApplyPercent(decimal value, decimal percent, decimal calamity) =>
        Adjust(DamageAmplificationLimits.MultiplySaturated(Math.Max(0m, value),
            Math.Max(0m, 1m + percent / 100m)), -calamity);
}
