using HarmonyLib;
using HBRGOSakiMod.Core.Audio;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NCharacterSelectButton), "OnPress")]
internal static class CharacterSelectButtonOnPressPatch
{
    private static void Postfix(NCharacterSelectButton __instance)
    {
        __instance.Select();
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class CharacterSelectScreenSelectCharacterPatch
{
    private static void Postfix(
        NCharacterSelectScreen __instance,
        NCharacterSelectButton charSelectButton,
        CharacterModel characterModel)
    {
        if (charSelectButton.IsRandom)
        {
            HbrgoSakiCharacterSelectVoice.Stop();
            return;
        }

        if (charSelectButton.IsLocked)
        {
            HbrgoSakiCharacterSelectVoice.Stop();
            return;
        }

        if (characterModel is HbrgoSaki)
        {
            HbrgoSakiCharacterSelectVoice.Play();
        }
        else
        {
            HbrgoSakiCharacterSelectVoice.Stop();
        }
    }
}
