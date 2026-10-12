using System;
using System.Collections.Generic;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Creature), nameof(Creature.HoverTips), MethodType.Getter)]
internal static class HotaruCannonHoverPatch
{
    private static bool Prefix(Creature __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance.Monster is not HotaruCannonModel cannon) return true;
        if (!CombatManager.Instance.IsInProgress)
        {
            __result = Array.Empty<IHoverTip>();
            return false;
        }

        // Native creature tips repeat every intent. A cannon instead has one
        // complete ability tip, followed by its normal power/status tips.
        List<IHoverTip> tips = new() { cannon.CreateAbilityHoverTip() };
        foreach (var power in __instance.Powers)
            foreach (IHoverTip tip in power.HoverTips)
                tips.MegaTryAddingTip(tip);
        __result = tips;
        return false;
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.RefreshIntents))]
internal static class HotaruCannonHoverRefreshPatch
{
    private static void Postfix(NCreature __instance)
    {
        // Cannon state can change without a native Creature value event (charge
        // or Happiness growth). Existing phase code already refreshes intents.
        // Reuse the focused entity's native owner, guards and removal lifecycle.
        if (__instance.Entity?.Monster is not HotaruCannonModel || !__instance.IsFocused ||
            !CombatManager.Instance.IsInProgress || NCombatRoom.Instance == null ||
            NTargetManager.Instance?.IsInSelection == true) return;
        __instance.ShowHoverTips(__instance.Entity.HoverTips);
    }
}
