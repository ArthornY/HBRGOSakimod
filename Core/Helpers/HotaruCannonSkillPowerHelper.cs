using System.Globalization;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Helpers;

// Author cannon skill powers as explicit percentages while retaining the old
// internal/save units. Damage 100% = strike 6; block 100% = defend 5.
internal static class HotaruCannonSkillPowerHelper
{
    internal static readonly decimal HappinessGrowthStep = HbrSkillPower.DamageUnits(50m, 3m);
    internal static readonly decimal UpgradedHappinessGrowth = HbrSkillPower.DamageUnits(50m);
    internal static readonly decimal HappinessHitGrowth = HbrSkillPower.DamageUnits(100m, 3m);
    internal static readonly decimal ContestResistanceDamage = HbrSkillPower.DamageUnits(50m, 3m);
    internal static readonly decimal CoffinStartBlock = HbrSkillPower.BlockUnits(80m);
    internal static readonly decimal LongingStartBlock = HbrSkillPower.BlockUnits(60m);
    internal static readonly decimal ContestEndDamage = HbrSkillPower.DamageUnits(100m, 3m);
    internal static readonly decimal LongingEndDamage = HbrSkillPower.DamageUnits(50m);
    internal static readonly decimal AbsolutionStartDamage = HbrSkillPower.DamageUnits(50m, 3m);
    internal static readonly decimal AbsolutionEndDamage = HbrSkillPower.DamageUnits(100m, 3m);

    internal static decimal StartBlock(HotaruCannonKind kind) => kind switch
    {
        HotaruCannonKind.Coffin => CoffinStartBlock,
        HotaruCannonKind.Longing => LongingStartBlock,
        _ => 0m
    };

    internal static decimal EndDamage(HotaruCannonKind kind) => kind switch
    {
        HotaruCannonKind.Contest => ContestEndDamage,
        HotaruCannonKind.Longing => LongingEndDamage,
        HotaruCannonKind.Absolution => AbsolutionEndDamage,
        _ => 0m
    };

    internal static string DamagePercent(decimal units) =>
        HbrSkillPower.DamagePercent(units).ToString("0.###", CultureInfo.InvariantCulture);

    internal static string BlockPercent(decimal units) =>
        HbrSkillPower.BlockPercent(units).ToString("0.###", CultureInfo.InvariantCulture);

    internal static LocString Description(string table, string key, Player owner)
    {
        LocString hbr = new(table, key + ".hbr");
        return HbrBattleStatsHelper.IsEnabled(owner) && hbr.Exists()
            ? hbr : new LocString(table, key);
    }

    internal static void AddDescriptionVariables(LocString text, HotaruCannonKind kind,
        decimal? damage = null, decimal? growth = null, decimal? block = null, decimal? startDamage = null, decimal? endDamage = null)
    {
        text.Add("DamageSkillPercent", DamagePercent(damage ?? EndDamage(kind)));
        text.Add("BaseDamageSkillPercent", DamagePercent(damage ?? EndDamage(kind)));
        text.Add("BlockSkillPercent", BlockPercent(block ?? StartBlock(kind)));
        text.Add("StartDamageSkillPercent", DamagePercent(startDamage ?? AbsolutionStartDamage));
        text.Add("EndDamageSkillPercent", DamagePercent(endDamage ?? AbsolutionEndDamage));
        text.Add("GrowthSkillPercent", DamagePercent(growth ?? HappinessGrowthStep));
        text.Add("GrowthStepSkillPercent", DamagePercent(HappinessGrowthStep));
        text.Add("UpgradedGrowthSkillPercent", DamagePercent(UpgradedHappinessGrowth));
        text.Add("HitGrowthSkillPercent", DamagePercent(HappinessHitGrowth));
        text.Add("ResistanceSkillPercent", DamagePercent(ContestResistanceDamage));
    }
}