using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NHealNumVfx), nameof(NHealNumVfx.Create))]
internal static class LargeHealNumberPatch
{
    private static readonly FieldInfo Text = AccessTools.Field(typeof(NHealNumVfx), "_text");

    private static void Prefix(ref decimal amount, out decimal __state)
    {
        __state = amount;
        // Only the VFX factory receives this bounded compatibility value.
        // The healing command retains its original exact amount.
        amount = Math.Clamp(amount, int.MinValue, int.MaxValue);
    }

    private static void Postfix(NHealNumVfx __result, decimal __state)
    {
        if (__result != null && (__state > int.MaxValue || __state < int.MinValue))
            Text.SetValue(__result, decimal.Truncate(__state).ToString("0", CultureInfo.InvariantCulture));
    }
}
