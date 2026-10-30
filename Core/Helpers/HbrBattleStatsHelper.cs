using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Helpers;

// Test-branch bridge values, not the original game's character stat profiles.
internal static class HbrBattleStatsHelper
{
    internal const int PlayerHealthScale = 100;
    internal const decimal InitialStat = 100m;
    internal const decimal StatGainPerLevel = 4m;
    internal const decimal SenseStatGainPerLevel = 2m;

    internal static string GetBattleStatTitle(SixSenseAttribute attribute) => attribute switch
    {
        SixSenseAttribute.Madness => "力",
        SixSenseAttribute.LeadershipCharm => "器用",
        SixSenseAttribute.Talent => "知性",
        SixSenseAttribute.Gentleness => "体力",
        SixSenseAttribute.MentalStrength => "精神",
        SixSenseAttribute.Simplicity => "运",
        _ => string.Empty
    };

    internal static HbrBattleStats GetSixSenseBonuses(ElectronicSoldierManual manual)
    {
        if (manual?.IsHbrNumericsEnabled != true) return default;
        // Resolve the two live sense modifiers once for all six stats. Derived
        // bonuses are never saved, so allocations, form changes and refunds
        // cannot apply the same levels twice or retain a stale stat increment.
        Creature creature = manual.Owner?.Creature;
        decimal multiplier = creature?.GetPower<AllRounderPower>()?.SenseMultiplier ?? 1m;
        LoliFormPower form = creature?.GetPower<LoliFormPower>();
        decimal Bonus(SixSenseAttribute attribute) => Math.Max(0m,
            ElectronicSoldierManual.CalculateEffectiveSenseAmount(attribute,
                manual.GetSenseAmount(attribute), multiplier, form)) * SenseStatGainPerLevel;
        return new(Bonus(SixSenseAttribute.Madness), Bonus(SixSenseAttribute.LeadershipCharm),
            Bonus(SixSenseAttribute.Talent), Bonus(SixSenseAttribute.Gentleness),
            Bonus(SixSenseAttribute.MentalStrength), Bonus(SixSenseAttribute.Simplicity));
    }

    internal static bool IsEnabled(Player player) =>
        player?.GetRelic<ElectronicSoldierManual>()?.IsHbrNumericsEnabled == true;

    internal static bool IsEnabled(Creature creature)
    {
        if (creature == null) return false;
        Player owner = creature.Player ?? creature.PetOwner;
        if (owner != null) return IsEnabled(owner);
        return EnemyStatScalingHelper.IsEnemy(creature) &&
            creature.CombatState?.RunState?.Players.Any(IsEnabled) == true;
    }

    internal static bool TryGetStats(Creature creature, out HbrBattleStats stats)
        => TryGetStatsWithAdditionalStrength(creature, 0m, out stats);

    internal static bool TryGetStatsWithAdditionalStrength(Creature creature,
        decimal additionalStrength, out HbrBattleStats stats)
    {
        stats = default;
        if (!IsEnabled(creature)) return false;
        Player owner = creature.Player ?? creature.PetOwner;
        if (owner != null)
        {
            stats = owner.GetRelic<ElectronicSoldierManual>().GetHbrBattleStats();
            // Pet cannons inherit the owner exactly. Phantom levels retain
            // their existing effects and add a small explicit stat bonus.
            if (creature.GetPower<PhantomPower>() is { } phantom)
            {
                int level = phantom.IsSummonedMinion ? phantom.SummonedMinionLevel :
                    owner.GetRelic<MomentOfReign>()?.GetActivePhantomBaseLevel() ?? 1;
                decimal bonus = Math.Max(0m, (decimal)level - 1m) * 2m;
                stats = new(stats.Strength + bonus, stats.Dexterity + bonus,
                    stats.Intelligence + bonus, stats.Vitality + bonus,
                    stats.Spirit + bonus, stats.Luck + bonus);
            }
            stats = HbrStatPowerHelper.ApplyWithAdditionalStrength(creature, stats, additionalStrength);
            return true;
        }

        // Original HBR enemy rows have one enemy parameter, HP and DP, not a
        // second set of player stats. Never synthesize six copies of that value.
        return false;
    }

    internal static decimal GetEnemyParameter(Creature creature) =>
        HbrEnemyProfileHelper.GetEnemyParameter(creature);

    // Called only by CharacterModel's fresh-player maximum, never on load or on
    // arbitrary max-HP writes. Native saved HP is therefore never re-scaled.
    internal static int GetInitialPlayerHp(int legacyHp) =>
        (int)Math.Min(BigCombatNumbers.NativeStatCap, (long)Math.Max(1, legacyHp) * PlayerHealthScale);

    // Callers supply only raw, newly-created allied monster HP. Enemy room HP
    // and stored cross-battle phantom health must never enter this adapter.
    internal static decimal GetPhantomBaseHp(Player owner, decimal rawBaseHp) =>
        IsEnabled(owner) ? BigCombatNumbers.ScaleHealth(rawBaseHp, PlayerHealthScale) : rawBaseHp;
}
