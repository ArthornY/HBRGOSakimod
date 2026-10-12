using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using STS2RitsuLib.Settings;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class HbrgoRitsuSettingsValuePatch
{
    // RitsuLib 0.6.x moved this control into STS2-RitsuLib.Ui while the
    // settings registry lives in STS2-RitsuLib.Settings. Resolve across loaded
    // assemblies so both the split runtime and the older single DLL work.
    private static readonly Type SliderControlType = AccessTools.TypeByName(
        "STS2RitsuLib.Settings.ModSettingsSliderControl");
    private static readonly FieldInfo OnChangedField = SliderControlType == null
        ? null
        : AccessTools.Field(SliderControlType, "_onChanged");
    private static readonly MethodInfo SubmitMethod = SliderControlType == null
        ? null
        : AccessTools.DeclaredMethod(SliderControlType, "TryApplyTypedValue", new[] { typeof(string) });
    private static readonly HashSet<string> PercentageSliderIds = new(StringComparer.Ordinal)
    {
        "bl_ENEMY_HIGH_DIMENSIONAL_SHELL_PERCENT",
        "bl_ENEMY_DP_GROWTH_PER_ACT_PERCENT",
        "bl_ENEMY_MAX_HP_PERCENT",
        "bl_ENEMY_MAX_HP_GROWTH_PER_ACT_PERCENT",
        "bl_ENEMY_DAMAGE_PERCENT",
        "bl_ENEMY_DAMAGE_GROWTH_PER_ACT_PERCENT"
    };

    // An empty TargetMethods enumeration still makes Harmony fall back to an
    // undefined target. Prepare must skip this optional compatibility patch
    // before target discovery when a future RitsuLib changes its private API.
    private static bool Prepare() => SubmitMethod is { IsStatic: false }
        && SubmitMethod.ReturnType == typeof(void)
        && OnChangedField is { IsStatic: false }
        && typeof(Delegate).IsAssignableFrom(OnChangedField.FieldType);

    private static MethodBase TargetMethod() => SubmitMethod;

    private static void Prefix(object __instance, ref string __0)
    {
        // The native factory captures its entry in the change callback. Check both
        // the binding owner and entry id so other mods' numeric inputs stay native.
        if (OnChangedField.GetValue(__instance) is not Delegate { Target: { } target })
        {
            return;
        }

        FieldInfo entryField = AccessTools.Field(target.GetType(), "entry");
        if (entryField?.GetValue(target) is not IntSliderModSettingsEntryDefinition entry
            || !string.Equals(entry.Binding.ModId, "HBRGOSakiMod", StringComparison.OrdinalIgnoreCase)
            || !PercentageSliderIds.Contains(entry.Id))
        {
            return;
        }

        // Share parsing with the BaseLib editor, including finite-value checks.
        // An empty string takes the native invalid-input path and restores the
        // current formatted value without changing the binding.
        __0 = HbrgoConfigSliderValuePatch.TryParseValue(
            __0, entry.MinValue, entry.MaxValue, entry.Step, out double value)
            ? value.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }
}
