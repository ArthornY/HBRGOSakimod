using System.Globalization;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Coefficients describe the skill, while native preview variables describe its
// current result. Keep both views separate: a library card has no combat result.
internal static class HbrSkillCardDescription
{
    internal static bool IsEnabled(CardModel card) => card != null &&
        card.GetType().Assembly == typeof(HbrSkillCardDescription).Assembly &&
        (!card.IsMutable || card.Owner == null || HbrBattleStatsHelper.IsEnabled(card.Owner));

    internal static string FormatPercent(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    internal static void AddParameters(CardModel card, LocString description)
    {
        if (description == null) return;
        HbrFixedResourceDescriptionHelper.AddParameters(card, description);
        bool combatPreview = card.IsMutable && card.Owner != null && card.CombatState != null &&
            (card.Pile?.Type is PileType.Hand or PileType.Play ||
             card.UpgradePreviewType == CardUpgradePreviewType.Combat);
        foreach (DynamicVar variable in card.DynamicVars.Values)
        {
            if (variable is not IHbrSkillPowerVar skill) continue;
            description.Add(variable.Name + "Skill", FormatPercent(skill.SkillPowerPercent));
            if (skill.SkillPowerKind == HbrSkillPowerKind.DamageAndBlock)
                description.Add(variable.Name + "BlockSkill",
                    FormatPercent(HbrSkillPower.BlockPercent(variable.BaseValue)));
            if (combatPreview && variable is DamageVar or BlockVar or CalculatedDamageVar or CalculatedBlockVar)
            {
                LocString preview = new("cards", "HBR_SKILL_POWER.preview");
                preview.AddObj("Value", variable);
                description.Add(variable.Name + "SkillPreview", preview);
            }
            else description.Add(variable.Name + "SkillPreview", string.Empty);
        }
    }
}
