using System.Collections.Generic;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrRelicCollection
{
    internal static RelicModel[] Entries(IEnumerable<RelicModel> relics,
        IEnumerable<RelicModel> existing = null)
    {
        var ids = new HashSet<ModelId>();
        if (existing != null)
            foreach (RelicModel relic in existing)
                if (relic != null) ids.Add(relic.Id);

        var entries = new List<RelicModel>();
        foreach (RelicModel relic in relics)
        {
            // These models only store HUD history/OD state. Actual collection
            // relics and differently functioning shared-icon variants remain.
            if (relic == null || relic is SoulOfAnchor or Overclock) continue;
            RelicModel canonical = relic.CanonicalInstance ?? relic;
            if (ids.Add(canonical.Id)) entries.Add(canonical);
        }
        return entries.ToArray();
    }
}

[HarmonyPatch(typeof(NRelicCollectionCategory), "LoadSubcategory")]
internal static class HbrRelicCollectionSubcategoryPatch
{
    private static void Prefix(ref IEnumerable<RelicModel> relics) =>
        // Materialize once: Orobas may return a fresh mutable fallback relic
        // on each enumeration. Rendering and detail navigation share this list.
        relics = HbrRelicCollection.Entries(relics);
}

[HarmonyPatch(typeof(NRelicCollectionCategory), "LoadRelicNodes")]
internal static class HbrRelicCollectionNodesPatch
{
    private static void Prefix(ref IEnumerable<RelicModel> relics) =>
        relics = HbrRelicCollection.Entries(relics);
}

[HarmonyPatch(typeof(NRelicCollection), nameof(NRelicCollection.AddRelics))]
internal static class HbrRelicCollectionNavigationPatch
{
    private static void Prefix(NRelicCollection __instance, ref IEnumerable<RelicModel> relics) =>
        relics = HbrRelicCollection.Entries(relics, __instance.Relics);
}
