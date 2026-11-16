using System;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Compatibility;

[HarmonyPatch]
internal static class CannonVisibilityCompatibility
{
    private static readonly bool UsesInteractability =
        AccessTools.PropertyGetter(typeof(MonsterModel), "IsInteractable") != null;
    private static MethodBase TargetMethod() =>
        AccessTools.PropertyGetter(typeof(MonsterModel), "IsInteractable") ??
        AccessTools.PropertyGetter(typeof(MonsterModel), "IsHealthBarVisible") ??
        throw new MissingMemberException(typeof(MonsterModel).FullName,
            "IsInteractable / IsHealthBarVisible");

    private static bool Prefix(MonsterModel __instance, ref bool __result)
    {
        if (__instance is not HotaruCannonModel) return true;
        // v0.113 uses this getter to guard SetIsInteractable as well. Cannons
        // must remain targetable by their own cards; their layout hides HP.
        __result = UsesInteractability;
        return false;
    }
}
