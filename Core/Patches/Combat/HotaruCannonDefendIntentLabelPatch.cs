using System.Collections.Generic;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NIntent), "UpdateVisuals")]
internal static class HotaruCannonDefendIntentLabelPatch
{
    private static void Postfix(AbstractIntent ____intent, MegaRichTextLabel ____valueLabel,
        IEnumerable<Creature> ____targets, Creature ____owner)
    {
        // Native intent labels cover attacks and statuses only. Keep its blue
        // shield and existing value label, and opt in just our cannon defense.
        if (____intent is HotaruCannonModel.CannonDefendIntent && ____valueLabel != null)
            ____valueLabel.Text = ____intent.GetIntentLabel(____targets, ____owner).GetFormattedText();
    }
}
