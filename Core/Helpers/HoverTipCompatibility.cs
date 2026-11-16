using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Newer runtimes make tooltip identity/flags immutable. Keep native identities
// there and translate only our own keyword lookups; existing tooltips copy by value.
internal static class HoverTipCompatibility
{
    private static readonly MethodInfo IdSetter =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Id))?.SetMethod;
    private static readonly MethodInfo DebuffSetter =
        typeof(HoverTip).GetProperty(nameof(HoverTip.IsDebuff))?.SetMethod;
    private static readonly MethodInfo SmartSetter =
        typeof(HoverTip).GetProperty(nameof(HoverTip.IsSmart))?.SetMethod;
    private static readonly MethodInfo InstancedSetter =
        typeof(HoverTip).GetProperty(nameof(HoverTip.IsInstanced))?.SetMethod;
    private static readonly MethodInfo OverflowSetter =
        typeof(HoverTip).GetProperty(nameof(HoverTip.ShouldOverrideTextOverflow))?.SetMethod;
    private static readonly MethodInfo TitleSetter =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Title))?.SetMethod;
    private static readonly MethodInfo DescriptionSetter =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Description))?.SetMethod;
    private static readonly ConcurrentDictionary<string, string> KeywordIds = new(StringComparer.Ordinal);
    private static string _penaltyNativeId;

    // A card references a relic concept, independently of inventory visibility.
    // Hidden relics intentionally have an empty HoverTips list. Use the native
    // single tip to retain its description and canonical model on the card.
    internal static IHoverTip CreateRelicReference<T>() where T : RelicModel =>
        ModelDb.Relic<T>().HoverTip;

    internal static HoverTip CreateKeyword(LocString title, LocString description, string keywordId)
    {
        HoverTip tip = new(title, description);
        if (IdSetter != null)
        {
            object boxed = tip;
            IdSetter.Invoke(boxed, new object[] { keywordId });
            return (HoverTip)boxed;
        }

        KeywordIds[tip.Id] = keywordId;
        return tip;
    }

    internal static string KeywordId(HoverTip tip) =>
        tip.Id != null && KeywordIds.TryGetValue(tip.Id, out string keywordId) ? keywordId : tip.Id;

    internal static HoverTip Copy(HoverTip tip, string title, string description)
    {
        if (TitleSetter != null && DescriptionSetter != null)
        {
            // New runtimes expose text setters but immutable identity/flags.
            object boxed = tip;
            TitleSetter.Invoke(boxed, new object[] { title });
            DescriptionSetter.Invoke(boxed, new object[] { description });
            return (HoverTip)boxed;
        }

        // Older runtimes have immutable text and writable identity/flags.
        HoverTip copy = title == null
            ? new HoverTip(HbrCompactText.Literal("text", description), tip.Icon)
            : new HoverTip(HbrCompactText.Literal("title", title), description, tip.Icon);
        object legacy = copy;
        IdSetter?.Invoke(legacy, new object[] { tip.Id });
        SmartSetter?.Invoke(legacy, new object[] { tip.IsSmart });
        DebuffSetter?.Invoke(legacy, new object[] { tip.IsDebuff });
        InstancedSetter?.Invoke(legacy, new object[] { tip.IsInstanced });
        OverflowSetter?.Invoke(legacy, new object[] { tip.ShouldOverrideTextOverflow });
        copy = (HoverTip)legacy;
        if (tip.CanonicalModel != null) copy.SetCanonicalModel(tip.CanonicalModel);
        return copy;
    }

    internal static HoverTip CreateCardPenalty(LocString title, LocString description)
    {
        HoverTip tip = CreateKeyword(title, description, "NARARA_NULLIFICATION_CARD_PENALTY");
        if (DebuffSetter != null)
        {
            object boxed = tip;
            DebuffSetter.Invoke(boxed, new object[] { true });
            return (HoverTip)boxed;
        }

        // This exact LocString tooltip is a card penalty. The real Narara power
        // remains a Buff, with a different native identity.
        _penaltyNativeId = tip.Id;
        return tip;
    }

    internal static bool IsCardPenalty(HoverTip tip) =>
        _penaltyNativeId != null && string.Equals(tip.Id, _penaltyNativeId, StringComparison.Ordinal);
}

[HarmonyPatch]
internal static class ImmutableCardPenaltyHoverTipPatch
{
    private static bool Prepare() =>
        typeof(HoverTip).GetProperty(nameof(HoverTip.IsDebuff))?.SetMethod == null;

    private static MethodBase TargetMethod() =>
        typeof(HoverTip).GetProperty(nameof(HoverTip.IsDebuff)).GetMethod;

    private static void Postfix(HoverTip __instance, ref bool __result)
    {
        if (HoverTipCompatibility.IsCardPenalty(__instance)) __result = true;
    }
}
