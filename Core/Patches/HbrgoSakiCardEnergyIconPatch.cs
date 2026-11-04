using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

// Shared Saki cards sit in the base, 31O and 30A pools at once, so the card's own pool would
// mix 31O and 30A icons in one deck. In a run, Saki's cards show the owner's energy icon,
// as the text icons already do; outside a run they keep their own pool's icon.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.EnergyIcon), MethodType.Getter)]
internal static class HbrgoSakiCardEnergyIconPatch
{
    private static void Postfix(CardModel __instance, ref Texture2D __result)
    {
        if (!__instance.IsMutable ||
            __instance.Owner?.Character is not HbrgoSaki character ||
            !IsSakiPool(__instance.VisualCardPool) ||
            character.CardPool == __instance.VisualCardPool)
        {
            return;
        }

        __result = ResourceLoader.Load<Texture2D>(
            character.CardPool.EnergyIconPath,
            null,
            ResourceLoader.CacheMode.Reuse);
    }

    private static bool IsSakiPool(CardPoolModel pool) =>
        pool is HBRGOSakiCardPool
            or HBRGOSaki31OCardPool
            or HBRGOSaki30ACardPool
            or HBRGOSakiMemoryGardenCardPool;
}
