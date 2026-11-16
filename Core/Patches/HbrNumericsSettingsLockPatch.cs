using System;
using System.Reflection;
using BaseLib.Config;
using BaseLib.Config.UI;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using STS2RitsuLib.Settings;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrNumericsSettingsLock
{
    internal static bool IsForcedProperty(ModConfig config, PropertyInfo property) =>
        config is HBRGOSakiConfig && property?.DeclaringType == typeof(HBRGOSakiConfig) &&
        property.Name == nameof(HBRGOSakiConfig.HbrNumerics);

    internal static bool IsForcedEntry(ModSettingsEntryDefinition entry) =>
        entry is ToggleModSettingsEntryDefinition toggle &&
        string.Equals(toggle.Binding.ModId, "HBRGOSakiMod", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(toggle.Id, "bl_HBR_NUMERICS", StringComparison.Ordinal);
}

[HarmonyPatch(typeof(NConfigTickbox), nameof(NConfigTickbox._Ready))]
internal static class HbrNumericsBaseLibSettingsLockPatch
{
    private static void Postfix(NConfigTickbox __instance, ModConfig ____config, PropertyInfo ____property)
    {
        if (HbrNumericsSettingsLock.IsForcedProperty(____config, ____property)) __instance.Disable();
    }
}

// Ritsu's BaseLib mirror uses the same config property. Its native enabled
// predicate supplies the disabled style and blocks mouse/controller editing,
// including when a settings control is recycled from its node pool.
[HarmonyPatch(typeof(ModSettingsEntryDefinition), nameof(ModSettingsEntryDefinition.EnabledPredicate), MethodType.Getter)]
internal static class HbrNumericsRitsuSettingsLockPatch
{
    private static readonly Func<bool> Disabled = static () => false;

    private static void Postfix(ModSettingsEntryDefinition __instance, ref Func<bool> __result)
    {
        if (HbrNumericsSettingsLock.IsForcedEntry(__instance)) __result = Disabled;
    }
}
