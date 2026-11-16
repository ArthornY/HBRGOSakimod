using System;
using System.Collections.Generic;
using System.Globalization;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Manual-only wording for power entries whose shared hover template reads one live instance:
/// amounts set by the applying card, counters that exist only in combat, and text assembled at
/// hover time. The manual states the rule instead; combat hovers keep reading these templates.
/// </summary>
/// <remarks>
/// Card amounts are written as "base（升级后 upgraded）". validation/hbr-effect-skill-damage
/// compares every amount here with the real card models, base and upgraded.
/// </remarks>
internal static class HbrManualGlossaryStaticText
{
    internal const string LayerNote = "N 表示该能力的层数或累计数值。";
    private const string StackNote = "多次打出时，数值相加。";
    private const string TurnStackNote = "同一回合多次获得时，数值相加。";
    private const string SaviorCard = "SAVIOR_GRAND_ENTRANCE_HBRGO_SAKI";

    internal static string Rewrite(string tableName, string prefix, string raw, bool hbr, ICollection<string> notes)
    {
        if (tableName != "powers" || string.IsNullOrEmpty(raw)) return raw;
        const string aliasPrefix = "HBRGOSAKIMOD-";
        if (prefix.StartsWith(aliasPrefix, StringComparison.Ordinal)) prefix = prefix[aliasPrefix.Length..];
        LocTable powers = LocManager.Instance.GetTable("powers");
        switch (prefix)
        {
            // Their canonical hover already reads a static rule text instead of the owned template.
            case "FRAGILE_POWER" when hbr:
            case "MINDS_EYE_POWER":
            case "HBR_ATTACK_DOWN_POWER":
            case "HBR_DEFENSE_DOWN_POWER":
                return Text(powers, prefix + ".hbrReferenceDescription", raw);
            case "HBR_ATTACK_UP_POWER":
            case "HBR_DEFENSE_UP_POWER":
            case "HBR_SPIRIT_DOWN_POWER":
            case "HBR_WISDOM_DOWN_POWER":
            case "HBR_CRITICAL_RATE_UP_POWER":
            case "HBR_CRITICAL_DAMAGE_UP_POWER":
                return Text(powers, prefix + (hbr ? ".hbrReferenceDescription" : ".referenceDescription"), raw);
            case "HBR_ENEMY_CHARGE_POWER":
            case "HBR_IMPRISON_POWER":
            case "HBR_BIND_POWER":
            case "HBR_STUN_POWER":
            case "HBR_CONTINUOUS_DP_DAMAGE_POWER":
            case "HBR_VIRUS_POWER":
            case "HBR_ILLUSION_POWER":
            case "HBR_CONFUSION_POWER":
            case "HBR_CONTINUOUS_DAMAGE_POWER":
            case "HBR_ENERGY_DRAIN_POWER":
                return Text(powers, prefix + (hbr ? ".hbrReferenceDescription" : ".referenceDescription"), raw);
            case "AOI_INVINCIBLE_SHIELD_POWER":
            case "FAINTNESS_STORM_COUNTDOWN_POWER":
            case "PILLAR_OVERDRIVE_DETECTION_POWER":
            case "BARREL_DEMISE_COUNTDOWN_POWER":
            case "DIAMOND_DP_CHECK_POWER":
            case "SHRIMP_RUNAWAY_POWER":
            case "HBR_PROVOKE_POWER":
                return Text(powers, prefix + ".referenceDescription", raw);
            case "ICHIGO_NEXT_TURN_ENERGY_PENALTY_POWER":
                return Fill(raw, ("EnergyIcons", "[E]×N"));
            case "BOOST_UP_FORM_DAMAGE_POWER":
                notes.Add("增幅来自Boost Up的「新星肅清」（每次+50%）和「祈禱之花，託付風中」（每次+25%），多次获得时相加。");
                return Text(powers, prefix + ".canonicalDescription", raw);

            // Amounts set by the applying card. The HBR texts of Breathing Shelter and Six Sense
            // Remnants state native units for an HBR-converted settlement, so they stay as they are,
            // as does My Happiness, whose redemption text omits the per-stack multiplier.
            case "HOTARU_BREATHING_SHELTER_POWER" when !hbr:
                notes.Add(StackNote);
                return Fill(raw, ("ExtraActions", "1"), ("Block", "12"));
            case "HOTARU_SIX_SENSE_REMNANTS_POWER" when !hbr:
                notes.Add(StackNote);
                return Fill(raw, ("Charge", "1"), ("BaseGain", "1（升级后2）"));
            case "HOTARU_UNFADING_SCARS_POWER":
                notes.Add(StackNote);
                return Fill(raw, ("EffectPercent", "0（升级后25）"));
            case "HOTARU_UNTIL_DAWN_POWER":
                notes.Add(StackNote);
                return Fill(raw, ("EnergyIcons", "[EE]（升级后[EEE]）"));
            case "HOTARU_WAKING_COMMAND_POWER":
                notes.Add(StackNote);
                return Fill(raw, ("EnergyIcons", "[E]"), ("Activations", "1"));
            case "HOTARU_TRAILING_SILENCE_POWER":
                notes.Add(TurnStackNote);
                return Fill(raw, ("ActiveAmount", "1"));
            case "HOTARU_ONLY_THIS_BEAT_POWER":
                notes.Add(TurnStackNote);
                return Fill(raw, ("ActiveAmount", "20"));
            case "HOTARU_BREATHING_RHYTHM_POWER":
                notes.Add("呼吸间的节拍每次给予3次。");
                if (hbr) return Fill(raw, ("Remaining", "（剩余次数）"));
                notes.Add("同一回合多次获得时，格挡相加，次数分别计算。");
                return Fill(raw, ("Remaining", "（剩余次数）"), ("ActiveAmount", "4（升级后6）"));
            case "HOTARU_TRACE_OLD_SCAR_POWER":
                notes.Add("同一牌型多次被选中时相加。");
                return Fill(raw, ("Discounts", "（循着同一道刻痕所选的牌型，每次降低1）"));
            case "HOTARU_LINGERING_ECHO_POWER":
                notes.Add("点数来自低处的余响：其原始出目不高于30时给予40点，多次获得时相加。");
                return Fill(raw, ("PendingPoints", "（待结算的点数）"));
            case "HOTARU_SEE_YOU_AGAIN_POWER":
                notes.Add(LayerNote);
                notes.Add("额外敏捷来自思念炮「战斗开始时获得2层敏捷」的升级，未解锁时为0。");
                return Fill(raw, ("Multiplier", "2^N"), ("ExtraDexterity", "2×(2^N−1)"));

            // Text assembled at hover time, without the live parts.
            case "HOTARU_FAREWELL_INERTIA_POWER":
                return Fill(raw, ("RewardsText", Fill(Text(powers, prefix + ".rewardLine", string.Empty),
                    ("Copies", "每"), ("PrimaryValue", "1"), ("Limit", "2（升级后3）"), ("SecondaryValue", "1"))));
            case "HOTARU_BREATH_AFTER_MISS_POWER" when !hbr:
                return Fill(raw, ("RewardsText", Fill(Text(powers, prefix + ".rewardLine", string.Empty),
                    ("Copies", "每"), ("Limit", "2（升级后3）"), ("SecondaryValue", "1"),
                    ("PrimaryValue", "4（升级后6）"), ("Draw", "1"))));
            case "HOTARU_MY_COFFIN_POWER":
                notes.Add(LayerNote);
                notes.Add("牺牲、救赎的百分比按层相加。");
                return Fill(raw, ("CoffinDescription",
                    Fill(Text(powers, prefix + ".normalDescription", string.Empty), ("ExtraActivations", "N"),
                        ("NormalDamagePercent", "（100×N）"), ("UpgradedCopies", "（升级版的层数）")) +
                    "\n\n[b]选择牺牲后[/b]\n" +
                    Fill(Text(powers, prefix + ".sacrificeDescription", string.Empty), ("SacrificePercent", "100（升级后200）")) +
                    "\n\n[b]选择救赎后[/b]\n" +
                    Fill(Text(powers, prefix + ".redemptionDescription", string.Empty), ("RedemptionPercent", "50（升级后100）"))));
            // Native Sea Breeze doubles Vulnerable's bonus when damage resolves, not the applied base
            // value its shared text describes, so the native entry stays as it is.
            case "SAVIOR_GRAND_ENTRANCE_POWER" when hbr:
                return Text(powers, prefix + ".hbrReferenceDescription",
                    Fill(raw, ("UpgradeEffects", SaviorUpgradeEffects(hbr))));

            case "HIGH_DIMENSIONAL_SHELL_POWER":
                return Fill(raw, ("breakAmount", hbr ? "2（精英3，Boss4）" : "3（精英4，Boss5）"));

            // Fixed values, or values converted by mode.
            case "GHOST_STATE_POWER":
                return Fill(raw, ("BonusPermille",
                    GhostStatePower.DefenseDownBonusPermille.ToString("0.#", CultureInfo.InvariantCulture)));
            case "REPTURE_BOW_POWER":
                return Fill(raw, ("ResourceFollowup", hbr ? HbrNativeEffectSkillHelper.FormatPercent(1m) : "1"));
            case "FATE_POWER":
                return Fill(raw, ("ResourceLossPerStack", "3"));
            case "UNBROKEN_REMNANT_POWER":
                return Fill(raw, ("ResourceAmount", "（层数）"));

            // State that exists only during combat.
            case "HBR_CALAMITY_POWER":
                return Fill(raw, ("RemainingTurns", "（剩余回合数）"));
            case "FEAR_OF_DEVIL_POWER":
                return "每层使敌方状态值降低30点。31C羁绊未满6种时只保留1层，重复施加刷新至敌方回合结束；满6种时每次再增加1层，持续至战斗结束。已有的永久层数不会被较低档位的施加削减。";
            case "NARARA_LAYERED_DP_POWER":
                return Fill(raw, ("Layers", "（剩余条数）"));
            case "DESTRUCTION_RATE_POWER":
                return Fill(raw, ("CurrentDestruction", "（当前破坏率）"), ("MaximumDestruction", "（当前上限）"));
            default:
                return raw;
        }
    }

    // Every memory-garden effect the power can carry. Effect 2 only lowers the card's cost,
    // and the power's hover leaves it out too.
    private static string SaviorUpgradeEffects(bool hbr)
    {
        LocTable cards = LocManager.Instance.GetTable("cards");
        var lines = new List<string>();
        for (int id = 0; cards.HasEntry(SaviorCard + ".upgrade_effect." + id); id++)
        {
            if (id == 2) continue;
            string key = SaviorCard + ".upgrade_effect." + id;
            if (hbr && cards.HasEntry(key + ".hbr")) key += ".hbr";
            string title = Text(cards, SaviorCard + ".upgrade_effect_title." + id, string.Empty);
            lines.Add((title.Length > 0 ? title + "：" : string.Empty) + cards.GetRawText(key));
        }
        return lines.Count == 0 ? string.Empty
            : "\n\n[b]记忆之庭升级效果[/b]（升级时选中的才生效）\n" + string.Join("\n", lines);
    }

    private static string Text(LocTable table, string key, string fallback) =>
        table.HasEntry(key) ? table.GetRawText(key) : fallback;

    private static string Fill(string text, params (string Name, string Value)[] values)
    {
        foreach ((string name, string value) in values)
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        return text;
    }
}
