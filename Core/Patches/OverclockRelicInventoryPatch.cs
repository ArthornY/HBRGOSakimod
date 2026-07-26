using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NRelicInventory), "Add")]
internal static class HideOverclockFromRelicInventoryAddPatch
{
    private static void Postfix(NRelicInventory __instance, RelicModel relic)
    {
        if (relic is Overclock)
        {
            OverclockRelicInventoryVisibility.HideHolder(__instance);
        }
    }
}

internal static class OverclockRelicInventoryVisibility
{
    internal static void HideHolder(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is NRelicInventoryHolder
                {
                    Relic.Model: Overclock
                } holder)
            {
                holder.Visible = false;
                holder.MouseFilter = Control.MouseFilterEnum.Ignore;
                holder.ProcessMode = Node.ProcessModeEnum.Disabled;
            }

            HideHolder(child);
        }
    }
}

[HarmonyPatch(typeof(NRelicInventory), nameof(NRelicInventory.AnimateRelic))]
internal static class HideOverclockFromRelicInventoryAnimationPatch
{
    private static bool Prefix(RelicModel relic, Vector2? startPosition, Vector2? startScale)
    {
        return relic is not Overclock;
    }
}
