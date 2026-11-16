using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;

namespace HBRGOSakiMod.Core.Patches;

// The retired 人造救世主 is never an unlocked character (see RetiredArtificialSaviorPatches).
[HarmonyPatch(typeof(UnlockState), "get_Characters")]
[HarmonyPriority(Priority.Last)]
internal static class HiddenArtificialSaviorUnlockPatch
{
    private static void Postfix(
        ref IEnumerable<CharacterModel> __result)
    {
        __result = HbrgoRetiredCharacters.WithoutRetired(__result);
    }
}
