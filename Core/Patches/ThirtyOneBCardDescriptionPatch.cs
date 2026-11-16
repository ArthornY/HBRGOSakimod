using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
namespace HBRGOSakiMod.Core.Patches;
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Description), MethodType.Getter)]
internal static class ThirtyOneBCardDescriptionPatch
{
    // Runs after the common skill parameters so a card's per-physical growth
    // is reflected identically on the face and in the inspect coefficient.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel __instance, LocString __result)
    {
        if (__instance is not HellfireBurnHbrgoSaki card) return;
        decimal basis = card.DynamicVars.Damage.BaseValue + HbrSkillPower.DamageUnits(card.MeritPercent);
        HbrSkillCardDescription.AddDamageParameters(card, __result, "Damage", basis, previewUnits: basis);
    }
}
