using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NGameOverScreen), "InitializeBannerAndQuote")]
internal static class HbrgoSakiGameOverTextPatch
{
    private static readonly FieldInfo HistoryField =
        AccessTools.Field(typeof(NGameOverScreen), "_history");

    private static readonly FieldInfo LocalPlayerField =
        AccessTools.Field(typeof(NGameOverScreen), "_localPlayer");

    private static readonly FieldInfo DeathQuoteField =
        AccessTools.Field(typeof(NGameOverScreen), "_deathQuote");

    private static readonly FieldInfo EncounterQuoteField =
        AccessTools.Field(typeof(NGameOverScreen), "_encounterQuote");

    private static void Postfix(NGameOverScreen __instance)
    {
        if (HistoryField.GetValue(__instance) is not RunHistory { Win: false } ||
            LocalPlayerField.GetValue(__instance) is not Player
            {
                Character: HbrgoSaki
            })
        {
            return;
        }

        if (DeathQuoteField.GetValue(__instance) is MegaRichTextLabel deathQuote)
        {
            deathQuote.Text = "hidesuwa";
        }

        EncounterQuoteField.SetValue(__instance, "兵败如山倒啊desuwa");
    }
}
