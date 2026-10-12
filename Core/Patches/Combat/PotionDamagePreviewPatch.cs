using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.DynamicDescription), MethodType.Getter)]
internal static class PotionDamageDescriptionPatch
{
    private static void Postfix(PotionModel __instance, LocString __result) =>
        PotionDamagePreviewHelper.UpdateDescription(__instance, __result,
            PotionDamagePreviewUiHelper.GetSelectedTarget(__instance));
}

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.HoverTip), MethodType.Getter)]
internal static class PotionDamageHoverTipPatch
{
    private static void Postfix(PotionModel __instance, ref HoverTip __result)
    {
        string details = PotionDamagePreviewHelper.GetTargetDetails(__instance);
        if (string.IsNullOrEmpty(details)) return;
        HoverTip updated = new(__instance.Title, __result.Description + "\n" + details, __result.Icon)
        {
            Id = __result.Id,
            IsSmart = __result.IsSmart,
            IsDebuff = __result.IsDebuff,
            IsInstanced = __result.IsInstanced,
            ShouldOverrideTextOverflow = __result.ShouldOverrideTextOverflow
        };
        if (__result.CanonicalModel != null) updated.SetCanonicalModel(__result.CanonicalModel);
        __result = updated;
    }
}

[HarmonyPatch(typeof(NPotionHolder), "TargetNode")]
internal static class PotionDamageTargetingPatch
{
    private static void Postfix(NPotionHolder __instance, ref Task __result) =>
        __result = PotionDamagePreviewUiHelper.TrackTargeting(__instance, __result);
}

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.CreateAndShow),
    new[] { typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment) })]
internal static class PotionDamageHoverTipRefreshPatch
{
    private static void Postfix(
        NHoverTipSet __result, Control owner, HoverTipAlignment alignment) =>
        PotionDamagePreviewUiHelper.TrackHoverTips(__result, owner, alignment);
}
