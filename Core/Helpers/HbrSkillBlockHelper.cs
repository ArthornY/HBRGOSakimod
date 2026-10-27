using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// Only explicitly identified fixed skill-block grants call this bridge.
// Unpowered still controls native modifier eligibility; it does not identify
// units. Deferred block and percentages of real damage must not call it.
internal static class HbrSkillBlockHelper
{
    internal static decimal ResolveUnpowered(Creature recipient, decimal units) =>
        units > 0m && HbrBattleStatsHelper.TryGetStats(recipient, out HbrBattleStats stats)
            ? HbrSkillPower.EvaluateBlockFromUnits(units, stats.Intelligence)
            : units;
}