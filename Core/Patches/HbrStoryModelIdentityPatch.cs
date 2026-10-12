using System;
using HarmonyLib;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrStoryModelIdentityPatch
{
    internal static void Install(Harmony harmony)
    {
        // Install before content registration: Ritsu computes public entries while
        // registering owned types, before the main assembly's PatchAll call.
        harmony.Patch(AccessTools.Method(typeof(ModelDb), nameof(ModelDb.GetEntry)),
            postfix: new HarmonyMethod(typeof(HbrStoryModelIdentityPatch), nameof(Postfix)));
    }

    // Ritsu uses Priority.Last (0). Harmony reserves -1 for "unspecified", so use
    // -2 to apply the JSON identity after Ritsu's fixed-entry assignment.
    [HarmonyPriority(Priority.Last - 2)]
    private static void Postfix(Type type, ref string __result)
    {
        if (type.IsAbstract || !typeof(HbrStoryEvent).IsAssignableFrom(type)) return;

        // Editor-generated CLR names include "Story_". The JSON id is the stable
        // identity shared by localization, saves and both event pools.
        __result = HbrStoryDefinition.EventIdPrefix + HbrMemoryFragmentPool.GetDefinition(type).Id;
    }
}

[HarmonyPatch(typeof(ModelId), nameof(ModelId.Deserialize))]
internal static class HbrStoryLegacyModelIdPatch
{
    private static void Postfix(ref ModelId __result)
    {
        // 1.2.0.6 briefly wrote these two IDs without Ritsu's namespace.
        // Normalize during deserialization so saved rooms and visit history agree.
        if (__result.Category == "EVENT"
            && __result.Entry is "LIBRARY_HBRGO_SAKI" or "NARVY_HBRGO_SAKI")
        {
            __result = new ModelId("EVENT", HbrStoryDefinition.EventIdPrefix + __result.Entry);
        }
    }
}
