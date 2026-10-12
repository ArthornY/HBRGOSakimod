using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NIntent), "UpdateVisuals")]
internal static class PhantomIntentPreviewPatch
{
    private static readonly FieldInfo OwnerField =
        AccessTools.Field(typeof(NIntent), "_owner");

    private static readonly FieldInfo IntentField =
        AccessTools.Field(typeof(NIntent), "_intent");

    [HarmonyPostfix]
    private static void Postfix(NIntent __instance)
    {
        if (OwnerField?.GetValue(__instance) is not Creature owner ||
            IntentField?.GetValue(__instance) is not AttackIntent intent ||
            !PhantomAttackIntentPreviewHelper.TryGetSegments(owner, intent, out var segments))
        {
            return;
        }

        MegaRichTextLabel valueLabel = __instance
            .GetNodeOrNull<MegaRichTextLabel>("%Value");
        if (valueLabel != null)
        {
            valueLabel.Text = FormatSegments(segments);
        }
    }

    private static string FormatSegments(
        IReadOnlyList<int> segments)
    {
        return string.Join(
            "[font_size=18] + [/font_size]",
            segments
                .GroupBy(damage => damage)
                .Select(group => group.Count() > 1
                    ? string.Concat(
                        group.Key.ToString(CultureInfo.InvariantCulture),
                        "[font_size=18]\u00D7",
                        group.Count().ToString(
                            CultureInfo.InvariantCulture),
                        "[/font_size]")
                    : group.Key.ToString(
                        CultureInfo.InvariantCulture)));
    }
}
