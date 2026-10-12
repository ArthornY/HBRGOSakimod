using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(UnlockState), "get_Characters")]
[HarmonyPriority(Priority.Last)]
internal static class HiddenArtificialSaviorUnlockPatch
{
    private static void Postfix(
        ref IEnumerable<CharacterModel> __result)
    {
        __result = __result.Where(character =>
            character?.GetType() != typeof(HbrgoSaki));
    }
}
