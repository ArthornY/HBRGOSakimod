using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerModel), "AddDumbVariablesToDescription")]
internal static class HbrNativeFixedPowerDescriptionPatch
{
    private static void Postfix(PowerModel __instance, LocString __0, int? __1)
    {
        if (__instance is not (RegenPower or PaperCutsPower) || !__instance.IsMutable ||
            !HbrBattleStatsHelper.IsEnabled(__instance.Owner)) return;
        __0.Add("Amount", HbrResourceValueHelper.Fixed(__instance.Owner, __1 ?? __instance.Amount));
    }
}

[HarmonyPatch(typeof(CookRestSiteOption), nameof(CookRestSiteOption.Description), MethodType.Getter)]
internal static class HbrNativeFixedCookDescriptionPatch
{
    private static readonly AccessTools.FieldRef<RestSiteOption, Player> Owner =
        AccessTools.FieldRefAccess<RestSiteOption, Player>("<Owner>k__BackingField");
    private static void Postfix(CookRestSiteOption __instance, LocString __result)
    {
        if (__result?.Variables.ContainsKey("MaxHp") != true) return;
        __result.Add("MaxHp", HbrResourceValueHelper.Fixed(Owner(__instance)?.Creature, 9m));
    }
}

[HarmonyPatch(typeof(ModifierModel), nameof(ModifierModel.Description), MethodType.Getter)]
internal static class HbrNativeFixedModifierDescriptionPatch
{
    internal sealed record Display(Creature Owner, int NativeAmount);
    internal static readonly ConditionalWeakTable<LocString, Display> Displays = new();
    private static readonly AccessTools.FieldRef<ModifierModel, RunState> Run =
        AccessTools.FieldRefAccess<ModifierModel, RunState>("_runState");

    private static void Postfix(ModifierModel __instance, LocString __result)
    {
        int amount = __instance switch { Terminal => 1, NightTerrors => 5, _ => 0 };
        if (amount == 0 || __result == null) return;
        Creature owner = Run(__instance)?.Players.FirstOrDefault(HbrBattleStatsHelper.IsEnabled)?.Creature;
        if (owner == null) return;
        Tag(__result, owner, amount);
    }

    // Event options whose text writes a fixed HP amount literally tag themselves too.
    internal static void Tag(LocString text, Creature owner, int nativeAmount)
    {
        if (text == null) return;
        Displays.Remove(text);
        Displays.Add(text, new Display(owner, nativeAmount));
    }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class HbrNativeFixedModifierTextPatch
{
    // Terminal/NightTerrors and a few event options (Colossal Flower's digs,
    // Trial's Noble Guilty) encode their fixed HP amount literally in native
    // translation text. Only instances tagged by those exact descriptions are
    // adapted. Skip all BBCode tags, colors and IDs, and require exactly one
    // standalone occurrence of the audited native amount; otherwise retain it.
    private static readonly Regex Number = new(@"(?<![0-9A-Za-z#.])\d+(?![0-9A-Za-z.])", RegexOptions.CultureInvariant);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(LocString __instance, ref string __result)
    {
        if (!HbrNativeFixedModifierDescriptionPatch.Displays.TryGetValue(__instance, out var display) ||
            !HbrBattleStatsHelper.IsEnabled(display.Owner) || string.IsNullOrEmpty(__result)) return;
        string raw = __result;
        int found = -1;
        bool insideTag = false;
        int start = 0;
        for (int cursor = 0; cursor <= __result.Length; cursor++)
        {
            if (cursor < __result.Length && __result[cursor] == '[')
            {
                if (!insideTag && !Find(start, cursor - start)) return;
                insideTag = true;
            }
            else if (cursor < __result.Length && __result[cursor] == ']')
            { insideTag = false; start = cursor + 1; }
            else if (cursor == __result.Length && !insideTag && !Find(start, cursor - start)) return;
        }
        if (found < 0) return;
        __instance.Add("HbrFixedHp", HbrResourceValueHelper.Fixed(display.Owner, display.NativeAmount));
        string digits = display.NativeAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        __result = __result[..found] + "{HbrFixedHp}" + __result[(found + digits.Length)..];

        bool Find(int offset, int length)
        {
            string digits = display.NativeAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (Match match in Number.Matches(raw.Substring(offset, length)))
            {
                if (match.Value != digits) continue;
                if (found >= 0) return false;
                found = offset + match.Index;
            }
            return true;
        }
    }
}
