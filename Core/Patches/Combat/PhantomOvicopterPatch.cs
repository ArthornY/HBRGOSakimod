using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Ovicopter), "CanLay", MethodType.Getter)]
internal static class PhantomOvicopterCanLayPatch
{
    private static bool Prefix(Ovicopter __instance, ref bool __result)
    {
        if (!PhantomOvicopterHelper.IsPhantom(__instance.Creature))
            return true;

        __result = PhantomOvicopterHelper.CanLay(__instance.Creature);
        return false;
    }
}

[HarmonyPatch(typeof(Ovicopter), "LayEggsMove")]
internal static class PhantomOvicopterLayEggsPatch
{
    private static bool Prefix(Ovicopter __instance, ref Task __result)
    {
        if (!PhantomOvicopterHelper.IsPhantom(__instance.Creature))
            return true;

        __result = PhantomOvicopterHelper.LayEggs(__instance.Creature);
        return false;
    }
}
