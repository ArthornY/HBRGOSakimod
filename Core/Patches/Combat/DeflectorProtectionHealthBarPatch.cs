using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.SetCreature))]
internal static class NHealthBarSetCreaturePatch
{
    private const string DpBarName = "DeflectorProtectionDpBar";

    private static void Postfix(NHealthBar __instance, Creature creature)
    {
        DeflectorProtection relic = creature.Player?.GetRelic<DeflectorProtection>();
        if (relic == null || __instance.GetNodeOrNull<NDeflectorProtectionDpBar>(DpBarName) != null)
        {
            return;
        }

        NDeflectorProtectionDpBar dpBar = new()
        {
            Name = DpBarName
        };
        __instance.AddChildSafely(dpBar);
        dpBar.SetCreature(creature, __instance.HpBarContainer);
    }
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.RefreshValues))]
internal static class NHealthBarRefreshValuesPatch
{
    private const string DpBarName = "DeflectorProtectionDpBar";

    private static void Postfix(NHealthBar __instance)
    {
        __instance.GetNodeOrNull<NDeflectorProtectionDpBar>(DpBarName)?.Refresh();
    }
}
