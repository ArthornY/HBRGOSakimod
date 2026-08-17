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
        string specialKey = card switch
        {
            FeintHbrgoSaki when !CombatModeHelper.IsMultiplayer(card.Owner?.Creature) =>
                "FEINT_HBRGO_SAKI.singleplayerDescription",
            ShadowDanceSongHbrgoSaki shadowDance =>
                $"SHADOW_DANCE_SONG_HBRGO_SAKI.level{shadowDance.SongLevel}Description",
            BlueSkySongHbrgoSaki blueSky =>
                $"BLUE_SKY_SONG_HBRGO_SAKI.level{blueSky.SongLevel}Description",
            LostDaysSongHbrgoSaki lostDays =>
                $"LOST_DAYS_SONG_HBRGO_SAKI.level{lostDays.SongLevel}Description",
            _ => null
        };

        if (specialKey != null || !isUpgraded)
        {
            return specialKey;
        }

        string upgradedKey = $"{card.Id.Entry}.upgradedDescription";
        return new LocString("cards", upgradedKey).Exists()
            ? upgradedKey
            : null;
    }
}
