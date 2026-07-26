using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Events;

namespace HBRGOSakiMod.Core.Patches;

internal static class TheArchitectDialogueFallback
{
    private static readonly FieldInfo DialogueField = AccessTools.Field(
        typeof(TheArchitect),
        "_dialogue");

    internal static void EnsureDialogue(TheArchitect architect)
    {
        if (DialogueField.GetValue(architect) is AncientDialogue ||
            architect.Owner?.Character is not HbrgoSaki)
        {
            return;
        }

        DialogueField.SetValue(
            architect,
            new AncientDialogue(string.Empty)
            {
                EndAttackers = ArchitectAttackers.Both
            });
        Log.Info("[HBRGOSakiMod] Added an empty Architect finale with both attack animations for HBRGO Saki.");
    }
}

[HarmonyPatch(typeof(TheArchitect), "WinRun")]
internal static class TheArchitectWinRunPatch
{
    private static void Prefix(TheArchitect __instance)
    {
        TheArchitectDialogueFallback.EnsureDialogue(__instance);
    }
}
