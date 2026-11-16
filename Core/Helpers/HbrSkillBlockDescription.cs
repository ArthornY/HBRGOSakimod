using System.Globalization;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// A presentation view over the existing counters/turn ledgers. Reading a tooltip
// must never rewrite Power.Amount or save a previously resolved block amount.
internal static class HbrSkillBlockDescription
{
    internal static bool IsEnabled(PowerModel power) => power != null &&
        (!power.IsMutable || power.Owner == null || HbrBattleStatsHelper.IsEnabled(power.Owner));

    internal static LocString ForPower(PowerModel power, LocString legacy, string key,
        decimal nativeUnits, string referenceUnits = "层数")
    {
        if (!IsEnabled(power)) return legacy;
        LocString description = new("powers", key);
        description.AddVariablesFrom(legacy);
        AddParameters(description, power.IsMutable ? power.Owner : null, nativeUnits,
            knownUnits: power.IsMutable, referenceUnits: referenceUnits);
        return description;
    }

    internal static void AddParameters(LocString description, Creature recipient, decimal nativeUnits,
        bool knownUnits = true, string referenceUnits = "格挡原值")
    {
        description.Add("amountSkill", knownUnits
            ? HbrSkillPower.BlockPercent(nativeUnits).ToString("0.##", CultureInfo.InvariantCulture)
            : $"（{referenceUnits}×20）");
        bool live = knownUnits && HbrBattleStatsHelper.TryGetStats(recipient, out _);
        decimal block = live ? HbrSkillBlockHelper.ResolveUnpowered(recipient, nativeUnits) : 0m;
        description.Add("amountBlock", block);
        description.Add("blockPreview", live
            ? $"（按当前智慧预计{block.ToString("0.##", CultureInfo.InvariantCulture)}点）"
            : "（实际格挡按获得者智慧结算）");
    }
}
