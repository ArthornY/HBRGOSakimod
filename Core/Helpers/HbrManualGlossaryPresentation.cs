using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HBRGOSakiMod.Core.Models.Capabilities;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>Formats static reference text without creating a creature or evaluating a live power.</summary>
internal static class HbrManualGlossaryPresentation
{
    private const string EnergyIcon = "[img=28x28]res://images/packed/sprite_fonts/colorless_energy_icon.png[/img]";
    private static readonly Regex Variable = new(@"\{([^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex EnergyCount = new(@"energyIcons\((\d+)\)", RegexOptions.Compiled);
    private static readonly Regex SimpleEnergy = new(@"\[(E{1,3})\]", RegexOptions.Compiled);

    internal static string FormatDescription(string raw, string sourceId) => Format(raw, sourceId, null);

    // A manual entry, with the notes its static wording supplies after the formatter's own.
    internal static string FormatEntry(string raw, string sourceId, IEnumerable<string> extraNotes) =>
        Format(raw, sourceId, extraNotes);

    private static string Format(string raw, string sourceId, IEnumerable<string> extraNotes)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "暂无说明。";
        string text = Regex.Replace(raw.Trim(), @"(?m)^\s*#", string.Empty).Trim();
        var notes = new List<string>();

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
            switch (name)
            {
                case "DualSwordsCount" when sourceId.EndsWith("SYNERGY_POWER", StringComparison.Ordinal):
                    AddNote("D 为双剑计数，B 为弓计数；分别计数，到 3 抽另一形态牌并清零。");
                    return "D";
                case "BowCount" when sourceId.EndsWith("SYNERGY_POWER", StringComparison.Ordinal):
                    return "B";
                case "Value" when sourceId.EndsWith("ROLL_BONUS_POWER", StringComparison.Ordinal) ||
                                       sourceId.EndsWith("ADVANTAGE_DICE_POWER", StringComparison.Ordinal):
                    AddNote("V 表示本份能力的数值；不同数值分别显示，同数值叠加剩余次数。");
                    return "V";
                case "RemainingUses" when sourceId.EndsWith("ROLL_BONUS_POWER", StringComparison.Ordinal) ||
                                               sourceId.EndsWith("ADVANTAGE_DICE_POWER", StringComparison.Ordinal):
                    AddNote("N 表示本份能力剩余的掷骰次数，显示在图标右上角；每次完整掷骰消耗 1 次，包含最强之盾判定。");
                    return "N";
                case "amountSkill":
                    AddNote("B 表示本次生效的格挡原值：能力层数、当前生效账本值，或每份奖励的原值。");
                    return "（20×B）";
                case "amountBlock":
                    AddNote("B 表示本次生效的格挡原值，K(I) 为获得者智慧的格挡加成系数。");
                    return "向下取整（B×100×K(I)）";
                case "blockPreview":
                    AddNote("实际格挡为向下取整（B×100×K(I)）；K(I) 根据获得者智慧计算。");
                    return "（按获得者智慧结算）";
                case "BlockRewardsText" when sourceId.EndsWith("HOTARU_BREATH_AFTER_MISS_POWER", StringComparison.Ordinal):
                    AddNote("每份效果分别保存触发次数、充能、格挡原值与抽牌数；格挡原值 B 对应 20×B% 倍率，实际按智慧结算。");
                    return "每份效果：每回合指定次数内未大成功，获得该份效果的充能和（20×B）%倍率格挡；首次大成功时，抽取该份效果指定的张数。";
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
                case "DamageSkill":
                    // A damage power's HBR skill from its stacks, written as its card-hover tip writes it.
                    return $"（层数×{HbrNativeEffectSkillHelper.FormatPercent(1m)}）";
                case "threshold":
                case "absoluteThreshold":
                    AddNote("C 表示充能层数，门槛最低为 1。");
                    return $"max（1，{RollKeywordRules.GetAbsoluteCriticalMinimumForCharge(0)}−{RollKeywordRules.GetAbsoluteCriticalMinimumForCharge(0) - RollKeywordRules.GetAbsoluteCriticalMinimumForCharge(1)}×C）";
                case "criticalThreshold":
                    AddNote("C 表示充能层数，普通大成功的原始出目下限最低为 1。");
                    return $"max（1，{RollKeywordRules.GetCriticalMinimumForCharge(0)}−{RollKeywordRules.GetCriticalMinimumForCharge(0) - RollKeywordRules.GetCriticalMinimumForCharge(1)}×C）";
                case "damagePerStack":
                    // Native numerics: a flat 0.5% per stack.
                    return "0.5";
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
        foreach (string note in extraNotes ?? Array.Empty<string>()) AddNote(note);
        if (notes.Count > 0)
            text += "\n\n[color=#b9bbc5]" + string.Join("\n", notes) + "[/color]";
        return text;

        void AddNote(string note)
        {
            if (!notes.Contains(note, StringComparer.Ordinal)) notes.Add(note);
        }
    }

    private static string RepeatEnergy(int count) => count is >= 1 and <= 3
        ? string.Concat(Enumerable.Repeat(EnergyIcon, count)) : count.ToString(CultureInfo.InvariantCulture) + EnergyIcon;
}
