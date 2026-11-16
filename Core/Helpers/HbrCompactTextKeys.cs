using System;
using System.Collections.Generic;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Localization entries that have a shorter hover version, written next to the
/// full entry under <see cref="CompactKey"/>. Pure data: the validation suite
/// compiles this file without the game.
/// </summary>
internal static class HbrCompactTextKeys
{
    internal const string ModPrefix = "HBRGOSAKIMOD-";

    /// <summary>
    /// Full entries swapped while hover tips are built in compact mode. Keys
    /// are listed without the mod prefix; prefixed copies resolve to the same
    /// short text.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string[]> Entries =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["card_keywords"] = new[]
            {
                "HBRGO_SAKI_MOD_KEYWORD_ROLL.description",
                "HBRGO_SAKI_MOD_KEYWORD_ROLL.attackSkillDescription",
                "HBRGO_SAKI_MOD_KEYWORD_ROLL.powerDescription",
                "HBRGO_SAKI_MOD_KEYWORD_ROLL.sakiAttackSkillDescription",
                "HBRGO_SAKI_MOD_KEYWORD_ROLL.sakiPowerDescription",
                "HBRGO_SAKI_MOD_KEYWORD_CRITICAL_SUCCESS.description",
                "HBRGO_SAKI_MOD_KEYWORD_CRITICAL_SUCCESS.sakiDescription",
                "HBRGO_SAKI_MOD_KEYWORD_CRITICAL_SUCCESS.hotaruDescription",
                "HBRGO_SAKI_MOD_KEYWORD_ABSOLUTE_CRITICAL_SUCCESS.description",
                "HBRGO_SAKI_MOD_KEYWORD_ABSOLUTE_CRITICAL_SUCCESS.hotaruDescription"
            },
            ["powers"] = new[]
            {
                "CHARGE_POWER.description",
                "ROLL_BONUS_POWER.description",
                "ADVANTAGE_DICE_POWER.description",
                "SYNERGY_POWER.description",
                "BEYOND_PROTECTION_POWER.description",
                "HBR_STAT_STRENGTH_ALLY.description",
                "HBR_STAT_DEXTERITY_ALLY.description",
                "HBR_STAT_STRENGTH_ENEMY.description",
                "HBR_STAT_DEXTERITY_ENEMY.description",
                "HBR_DEFENSE_DOWN_POWER.description",
                "HBR_DEFENSE_DOWN_POWER.hbrReferenceDescription",
                "HBR_ATTACK_DOWN_POWER.description",
                "HBR_ATTACK_DOWN_POWER.hbrReferenceDescription",
                "FRAGILE_POWER.hbrDescription",
                "FRAGILE_POWER.hbrReferenceDescription",
                "NORMAL_STATE_POWER.description",
                "MAGIC_SWORD_MODE_POWER.description",
                "ITS_MYGO_PROTECTION_POWER.description",
                "LOST_STAR_SONG_SUPPORT_POWER.description",
                "HOTARU_CANNON_DAMAGE_MULTIPLIER.description",
                "HOTARU_MY_HAPPINESS_POWER.normalDescription",
                "HOTARU_MY_HAPPINESS_POWER.sacrificeDescription",
                "HOTARU_MY_HAPPINESS_POWER.redemptionDescription",
                "HOTARU_FINAL_CANNON_SOUL.description",
                "NARARA_GIVE_YOU_POWER.description",
                "NARARA_NULLIFICATION_POWER.description",
                "NARARA_NULLIFICATION_POWER.hbrDescription",
                "NARARA_PRESSURE_POWER.description",
                "NARARA_PRESSURE_POWER.hbrDescription",
                "NARARA_LAYERED_DP_POWER.description",
                "NARARA_LAYERED_HP_POWER.description",
                "SKULL_FEATHER_POWER.description",
                "RAPTURE_SCYTHE_POWER.description",
                "KILL_KISS_FUSION_POWER.description",
                "PROTECTIVE_CHARM_POWER.description",
                "AOI_INVINCIBLE_SHIELD_POWER.description",
                "AOI_MIRACLE_SHIELD_POWER.description",
                "ICHIGO_NEXT_TURN_ENERGY_PENALTY_POWER.description",
                "KOZUE_SPIRIT_POWER.description",
                "KOZUE_SPIRIT_SHELL_POWER.description",
                "FEAR_OF_DEVIL_POWER.description"
            },
            ["relics"] = new[]
            {
                "ELECTRONIC_SOLDIER_MANUAL.description",
                "ELECTRONIC_SOLDIER_MANUAL.hbrDescription",
                "ELECTRONIC_SOLDIER_MANUAL_31O.description",
                "ELECTRONIC_SOLDIER_MANUAL_31O.hbrDescription",
                "ELECTRONIC_SOLDIER_MANUAL_31_O.description",
                "ELECTRONIC_SOLDIER_MANUAL_31_O.hbrDescription",
                "KETSU.description",
                "KETSU.hbrDescription",
                "HOTARU_ELECTRONIC_SOLDIER_MANUAL.description",
                "HOTARU_ELECTRONIC_SOLDIER_MANUAL.hbrDescription",
                "OVERCLOCK.description",
                "HOTARU_IRIDESCENCE.description",
                "DEFLECTOR_PROTECTION.description",
                "THIRTY_ONE_A_COLLECTION.description",
                "THIRTY_ONE_A_COLLECTION.hbrDescription",
                "THIRTY_ONE_B_COLLECTION.description",
                "THIRTY_ONE_C_COLLECTION.description",
                "TIGERS_MOUTH_SNACK_BOND.description",
                "FAMILY_IS_LIKE_THIS_BOND.description",
                "DEMON_KING_AND_FAMILIAR_BOND.description",
                "THIRTY_A_COLLECTION.description",
                "MOMENT_OF_REIGN.description",
                "RESTORATION_MOMENT.description"
            }
        };

    /// <summary>
    /// Variables supplied by the owning model/hover hook beyond the visible
    /// full template, keyed by "table:entry".
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string[]> ExtraVariables =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["relics:ELECTRONIC_SOLDIER_MANUAL.description"] = new[] { GardenPoints },
            ["relics:ELECTRONIC_SOLDIER_MANUAL.hbrDescription"] = new[] { GardenPoints },
            ["relics:ELECTRONIC_SOLDIER_MANUAL_31O.description"] = new[] { GardenPoints },
            ["relics:ELECTRONIC_SOLDIER_MANUAL_31O.hbrDescription"] = new[] { GardenPoints },
            ["relics:ELECTRONIC_SOLDIER_MANUAL_31_O.description"] = new[] { GardenPoints },
            ["relics:ELECTRONIC_SOLDIER_MANUAL_31_O.hbrDescription"] = new[] { GardenPoints },
            ["relics:KETSU.description"] = new[] { GardenPoints },
            ["relics:KETSU.hbrDescription"] = new[] { GardenPoints },
            ["relics:HOTARU_ELECTRONIC_SOLDIER_MANUAL.description"] = new[] { GardenPoints },
            ["relics:HOTARU_ELECTRONIC_SOLDIER_MANUAL.hbrDescription"] = new[] { GardenPoints },
            ["relics:OVERCLOCK.description"] = new[] { RemainingLevels },
            ["relics:HOTARU_IRIDESCENCE.description"] = new[] { Layers },
            ["relics:THIRTY_ONE_A_COLLECTION.description"] = new[] { Count },
            ["relics:THIRTY_ONE_A_COLLECTION.hbrDescription"] = new[] { "Count", "CutinCount", "NextTier", "LivePending" },
            ["relics:THIRTY_ONE_B_COLLECTION.description"] = new[] { "Kinds", "ActiveTiers", "NextTier" },
            ["relics:THIRTY_ONE_C_COLLECTION.description"] = new[] { "Servants", "RemainingUses", "MaximumUses", "TotalPercent" },
            ["relics:TIGERS_MOUTH_SNACK_BOND.description"] = new[] { "ActiveText", "RanaBonus", "ByakkoBonus" },
            ["relics:FAMILY_IS_LIKE_THIS_BOND.description"] = new[] { "ActiveText", "UsedText" },
            ["relics:DEMON_KING_AND_FAMILIAR_BOND.description"] = new[] { "ActiveText" },
            ["powers:HBR_ATTACK_DOWN_POWER.description"] = new[] { "Duration" },
            ["powers:HBR_DEFENSE_DOWN_POWER.description"] = new[] { "Duration" },
            ["powers:FRAGILE_POWER.hbrDescription"] = new[] { "Duration" },
            ["relics:THIRTY_A_COLLECTION.description"] = new[] { Count }
        };

    internal const string GardenPoints = "hbrGardenPoints";
    internal const string RemainingLevels = "hbrRemaining";
    internal const string Layers = "hbrLayers";
    internal const string Count = "hbrCount";

    /// <summary>Interface text of the compact hover, all in card_keywords.</summary>
    internal static readonly IReadOnlyDictionary<string, string[]> UiEntries =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [UiPrefix + "title"] = new[] { "title" },
            [UiPrefix + "text"] = new[] { "text" },
            [UiPrefix + "line"] = new[] { "title", "text" },
            [UiPrefix + "titleSeparator"] = Array.Empty<string>(),
            [UiPrefix + "debuffFooter"] = Array.Empty<string>(),
            [UiPrefix + "debuffFooterMany"] = Array.Empty<string>(),
            [UiPrefix + "holdKeyboard"] = Array.Empty<string>(),
            [UiPrefix + "holdController"] = Array.Empty<string>()
        };

    internal const string UiPrefix = "HBRGO_COMPACT_HOVER.";

    /// <summary>"description" becomes "compactDescription"; "xDescription" becomes "xCompactDescription".</summary>
    internal static string CompactKey(string key)
    {
        int dot = key.LastIndexOf('.');
        string entry = key.Substring(0, dot + 1);
        string field = key.Substring(dot + 1);
        const string suffix = "Description";
        if (field == "description") return entry + "compactDescription";
        return field.EndsWith(suffix, StringComparison.Ordinal)
            ? entry + field.Substring(0, field.Length - suffix.Length) + "Compact" + suffix
            : entry + field + "Compact";
    }

    internal static string Normalize(string key) =>
        key.StartsWith(ModPrefix, StringComparison.Ordinal) ? key.Substring(ModPrefix.Length) : key;
}
