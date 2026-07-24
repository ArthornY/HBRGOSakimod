using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.Description), MethodType.Getter)]
internal static class CardDescriptionLocPatch
{
    private static void Postfix(CardModel __instance, ref LocString __result)
    {
        string locKey = GetReplacementLocKey(__instance);
        if (locKey == null)
        {
            return;
        }

        LocString replacement = new("cards", locKey);
        replacement.AddVariablesFrom(__result);
        __result = replacement;
    }

    private static string GetReplacementLocKey(CardModel card)
    {
        bool isUpgraded = card.IsUpgraded || card.DeckVersion?.IsUpgraded == true;
        return card switch
        {
            FeintHbrgoSaki when !CombatModeHelper.IsMultiplayer(card.Owner?.Creature) =>
                "FEINT_HBRGO_SAKI.singleplayerDescription",
            NoSelfInDreamHbrgoSaki when isUpgraded =>
                "NO_SELF_IN_DREAM_HBRGO_SAKI.upgradedDescription",
            ImitateHbrgoSaki when isUpgraded =>
                "IMITATE_HBRGO_SAKI.upgradedDescription",
            SakuraRainHbrgoSaki when isUpgraded =>
                "SAKURA_RAIN_HBRGO_SAKI.upgradedDescription",
            MagicSwordEnhancementHbrgoSaki when isUpgraded =>
                "MAGIC_SWORD_ENHANCEMENT_HBRGO_SAKI.upgradedDescription",
            FragileHbrgoSaki when isUpgraded =>
                "FRAGILE_HBRGO_SAKI.upgradedDescription",
            MagicSwordLuckySevenHbrgoSaki when isUpgraded =>
                "MAGIC_SWORD_LUCKY_SEVEN_HBRGO_SAKI.upgradedDescription",
            _ => null
        };
    }
}
