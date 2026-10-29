using System;
using HBRGOSakiMod.Core.Models.Monsters;
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
    {
        if (!HbrBattleStatsHelper.IsEnabled(creature) || EnemyStatScalingHelper.IsEnemy(creature)) return stats;
        Creature source = creature?.Monster is HotaruCannonModel
            ? creature.PetOwner?.Creature ?? creature : creature;
        decimal strength = source?.GetPowerAmount<StrengthPower>() ?? 0;
        decimal dexterity = (source?.GetPowerAmount<DexterityPower>() ?? 0) +
            HotaruSeeYouAgainHelper.GetExtraDexterity(source?.Player);
        return stats with
        {
            Strength = Adjust(stats.Strength, strength),
            Dexterity = Adjust(stats.Dexterity, strength),
            Intelligence = Adjust(stats.Intelligence, dexterity)
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
        if (HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, card, props, out Creature cannon) &&
            HotaruCannonDamageUpgradeHelper.Has(cannon, HotaruCannonKind.Happiness, 3))
        {
            decimal extra = Math.Max(0, dealer?.GetPowerAmount<StrengthPower>() ?? 0);
            attack = attack with { Strength = Adjust(attack.Strength, extra), Dexterity = Adjust(attack.Dexterity, extra) };
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
}
