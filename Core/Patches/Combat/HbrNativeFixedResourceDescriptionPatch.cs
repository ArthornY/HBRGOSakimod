using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
using BeatingRemnant = MegaCrit.Sts2.Core.Models.Relics.BeatingRemnant;
using Corrupted = MegaCrit.Sts2.Core.Models.Enchantments.Corrupted;
using CrimsonMantle = MegaCrit.Sts2.Core.Models.Cards.CrimsonMantle;
using Inferno = MegaCrit.Sts2.Core.Models.Cards.Inferno;
using Regret = MegaCrit.Sts2.Core.Models.Cards.Regret;
using SereTalon = MegaCrit.Sts2.Core.Models.Relics.SereTalon;

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
    internal sealed record Display(Creature Owner, int NativeAmount, string HbrTextKey);
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
    // A text that words the amount without its number names a static_hover_tips
    // replacement instead, used where the current language defines it.
    internal static void Tag(LocString text, Creature owner, int nativeAmount, string hbrTextKey = null)
    {
        if (text == null) return;
        Displays.Remove(text);
        Displays.Add(text, new Display(owner, nativeAmount, hbrTextKey));
    }
}

// Native card, relic and enchantment texts that write a fixed HP amount
// literally, beside a value the registry or a command patch converts.
[HarmonyPatch]
internal static class HbrNativeFixedLiteralDescriptionPatch
{
    // Simplified Chinese words Regret's loss as the hand size, without its 1 HP.
    internal const string RegretTextKey = "HBR_NATIVE_REGRET.description";

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Description));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicEventDescription));
        yield return AccessTools.PropertyGetter(typeof(EnchantmentModel), nameof(EnchantmentModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(EnchantmentModel), nameof(EnchantmentModel.DynamicExtraCardText));
    }

    private static void Postfix(AbstractModel __instance, LocString __result)
    {
        // Canonical templates cannot read Owner; they keep native text.
        if (__result == null || __instance?.IsMutable != true) return;
        switch (__instance)
        {
            case CrimsonMantle or Inferno:
                HbrNativeFixedModifierDescriptionPatch.Tag(__result, ((CardModel)__instance).Owner?.Creature, 1);
                break;
            case Regret regret:
                HbrNativeFixedModifierDescriptionPatch.Tag(__result, regret.Owner?.Creature, 1, RegretTextKey);
                break;
            case BeatingRemnant remnant:
                HbrNativeFixedModifierDescriptionPatch.Tag(__result, remnant.Owner?.Creature, 20);
                break;
            // v0.111.0 writes Sere Talon's 9 max HP literally; stable's text has no 9.
            case SereTalon talon:
                HbrNativeFixedModifierDescriptionPatch.Tag(__result, talon.Owner?.Creature, 9);
                break;
            case Corrupted corrupted:
                HbrNativeFixedModifierDescriptionPatch.Tag(__result,
                    corrupted.Card is { IsMutable: true } card ? card.Owner?.Creature : null, 2);
                break;
        }
    }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class HbrNativeFixedModifierTextPatch
{
    // Terminal/NightTerrors, a few event options (Colossal Flower's digs,
    // Trial's Noble Guilty) and the native models in
    // HbrNativeFixedLiteralDescriptionPatch encode their fixed HP amount
    // literally in native translation text. Only instances tagged by those
    // exact descriptions are adapted, under HbrFixedAmountText's single
    // standalone amount rule.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(LocString __instance, ref string __result)
    {
        if (!HbrNativeFixedModifierDescriptionPatch.Displays.TryGetValue(__instance, out var display) ||
            !HbrBattleStatsHelper.IsEnabled(display.Owner) || string.IsNullOrEmpty(__result)) return;
        string replacement = display.HbrTextKey == null ? null :
            HbrLocalizedText.Local("static_hover_tips", display.HbrTextKey);
        if (replacement == null && HbrFixedAmountText.FindSingle(__result, display.NativeAmount) < 0) return;
        __instance.Add("HbrFixedHp", HbrResourceValueHelper.Fixed(display.Owner, display.NativeAmount));
        __result = replacement ?? HbrFixedAmountText.Replace(__result, display.NativeAmount, "{HbrFixedHp}");
    }
}
