using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>Formats static reference text without creating a creature or evaluating a live power.</summary>
internal static class HbrManualGlossaryPresentation
{
    private const string EnergyIcon = "[img=28x28]res://images/packed/sprite_fonts/colorless_energy_icon.png[/img]";
    private static readonly Regex Variable = new(@"\{([^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex EnergyCount = new(@"energyIcons\((\d+)\)", RegexOptions.Compiled);
    private static readonly Regex SimpleEnergy = new(@"\[(E{1,3})\]", RegexOptions.Compiled);

    internal static string FormatDescription(string raw, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "暂无说明。";
        string text = Regex.Replace(raw.Trim(), @"(?m)^\s*#", string.Empty).Trim();
        var notes = new List<string>();
        SixSenseAttribute? sense = SenseFromId(sourceId);

        // These templates are shared with combat hover tips. The manual explains the rule,
        // so use formulas or named amounts instead of inventing a player's current values.
        text = SimpleEnergy.Replace(text, match => RepeatEnergy(match.Groups[1].Length));
        text = Variable.Replace(text, match =>
        {
            string expression = match.Groups[1].Value;
            int separator = expression.IndexOf(':');
            string name = (separator < 0 ? expression : expression[..separator]).Trim();
            Match energy = EnergyCount.Match(expression);
            if (energy.Success && int.TryParse(energy.Groups[1].Value, out int count))
                return RepeatEnergy(count);
            if (name == "singleStarIcon")
                return "[img=28x28]res://images/packed/sprite_fonts/star_icon.png[/img]";
            if (name == "singleEnergyIcon") return EnergyIcon;
            if (sense is { } attribute)
            {
                if (name is "normal" or "enhanced")
                {
                    if (attribute != SixSenseAttribute.Talent) AddNote("L 表示该项六感的等级。");
                    return SenseFormula(attribute, name == "enhanced");
                }
                if (name == "loss") return Number(SixSenseCombatHelper.ResistanceLoss(attribute));
            }
            switch (name)
            {
                case "RollBonus" when sourceId.EndsWith("HOTARU_IRIDESCENCE", StringComparison.Ordinal):
                    AddNote("C 表示虹光自身的永久充能计数，初始 1 层，上限 6 层。");
                    return "（30×C）";
                case "EnergyIcons" when sourceId.EndsWith("HOTARU_IRIDESCENCE", StringComparison.Ordinal):
                    AddNote("C 表示虹光自身的永久充能计数，初始 1 层，上限 6 层。");
                    return "（C−2）" + EnergyIcon;
                case "StartingStats" when sourceId.EndsWith("HOTARU_IRIDESCENCE", StringComparison.Ordinal):
                    AddNote("C 表示虹光自身的永久充能计数，初始 1 层，上限 6 层。");
                    return "C²";
                case "Amount":
                case "value":
                    AddNote("N 表示该能力的层数或累计数值。");
                    return "N";
                case "threshold":
                case "absoluteThreshold":
                    AddNote("C 表示充能层数，门槛最低为 1。");
                    return $"max（1，{RollKeywordRules.GetAbsoluteCriticalMinimumForCharge(0)}−{RollKeywordRules.GetAbsoluteCriticalMinimumForCharge(0) - RollKeywordRules.GetAbsoluteCriticalMinimumForCharge(1)}×C）";
                case "criticalThreshold":
                    AddNote("C 表示充能层数，普通大成功的原始出目下限最低为 1。");
                    return $"max（1，{RollKeywordRules.GetCriticalMinimumForCharge(0)}−{RollKeywordRules.GetCriticalMinimumForCharge(0) - RollKeywordRules.GetCriticalMinimumForCharge(1)}×C）";
                case "damagePerStack":
                    AddNote("B 表示该目标已被击破的六感抗性种类数（0～6）。");
                    return $"（{Number(SixSenseCombatHelper.DestructionConversion(0))}×1.1^B）";
                case "conversion":
                    AddNote("B 表示该目标已被击破的六感抗性种类数（0～6）。");
                    return $"（{Number(SixSenseCombatHelper.DestructionConversion(0) * 100m)}×1.1^B）";
                case "resistances":
                    return "六感抗性：领袖魅力、才能、温柔、精神力、单纯、疯狂。";
                case "InheritedDamageBonus":
                    AddNote("D 表示本场战斗已经继承的伤害增幅。");
                    return "D";
                case "DamagePercent":
                    return "（此能力累计转化DP的一半）";
                case "TemporaryMaxHp":
                    return "（每次转化DP的一半向下取整后累加）";
                case "DpToReturn":
                    return "（本场被此牌清除的DP总量）";
                case "CopyLevel":
                    AddNote("L 表示该效果所指定的复制幻影等级。");
                    return "L";
                case "StrengthGain":
                    AddNote("S 表示施加此能力时设定的力量收益。");
                    return "S";
                case "leadership": return "（累计领袖魅力加值）";
                case "gentleness": return "（累计温柔加值）";
                case "mentalStrength": return "（累计精神力减伤）";
                case "primary": return "卡牌的固有六感";
                case "attributes": return "固有属性及兼具属性";
                case "bonus": return "（固有属性等级）";
                case "Times":
                    AddNote("N 表示效果指定的额外次数。");
                    return "N";
                default:
                    AddNote("具体数值由施加此效果的卡牌或能力决定。");
                    return "（对应数值）";
            }
        });

        // Raw description entries normally have no conditions; tolerate an added nested
        // placeholder without sending incomplete SmartFormat text into LocManager.
        while (Variable.IsMatch(text)) text = Variable.Replace(text, "（对应数值）");
        if (notes.Count > 0)
            text += "\n\n[color=#b9bbc5]" + string.Join("\n", notes) + "[/color]";
        return text;

        void AddNote(string note)
        {
            if (!notes.Contains(note, StringComparer.Ordinal)) notes.Add(note);
        }
    }

    private static SixSenseAttribute? SenseFromId(string sourceId)
    {
        const string prefix = "card_keywords:HBRGO_SIX_SENSE.";
        return sourceId?.StartsWith(prefix, StringComparison.Ordinal) == true &&
            Enum.TryParse(sourceId[prefix.Length..], out SixSenseAttribute sense) && Enum.IsDefined(sense)
                ? sense : null;
    }

    private static string SenseFormula(SixSenseAttribute sense, bool enhanced)
    {
        decimal scale = sense == SixSenseAttribute.Madness ? 100m : 1m;
        decimal baseline = SixSenseCombatHelper.EffectValue(sense, 0m, enhanced) * scale;
        decimal perLevel = SixSenseCombatHelper.EffectValue(sense, 1m, enhanced) * scale - baseline;
        if (perLevel == 0m) return Number(baseline);
        string levelTerm = perLevel == 1m ? "L" : Number(perLevel) + "×L";
        return baseline == 0m ? "（" + levelTerm + "）" : $"（{Number(baseline)}＋{levelTerm}）";
    }

    private static string Number(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string RepeatEnergy(int count) => count is >= 1 and <= 3
        ? string.Concat(Enumerable.Repeat(EnergyIcon, count)) : count.ToString(CultureInfo.InvariantCulture) + EnergyIcon;
}
