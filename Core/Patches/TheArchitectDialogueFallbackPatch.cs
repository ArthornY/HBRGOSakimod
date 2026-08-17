using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Events;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches;

internal static class TheArchitectDialogueFallback
{
    private static readonly FieldInfo DialogueField =
        PrivateAccess.Field<TheArchitect>("_dialogue");

    internal static void EnsureDialogue(TheArchitect architect)
    {
        if (DialogueField.GetValue(architect) is AncientDialogue ||
            architect.Owner?.Character is not HbrgoSaki)
        {
            return;
        }

        AncientDialogue dialogue =
            new(string.Empty, string.Empty)
            {
                EndAttackers = ArchitectAttackers.Both
            };
        dialogue.Lines[0].LineText =
            new LocString(
                "ancients",
                "HBRGO_SAKI_ARCHITECT_GREETING.char");
        dialogue.Lines[0].Speaker =
            AncientDialogueSpeaker.Character;
        dialogue.Lines[1].LineText =
            new LocString(
                "ancients",
                "HBRGO_SAKI_ARCHITECT_GREETING.ancient");
        dialogue.Lines[1].Speaker =
            AncientDialogueSpeaker.Ancient;
        DialogueField.SetValue(architect, dialogue);
        Log.Info("[HBRGOSakiMod] Added HBRGO Saki's Architect finale dialogue.");
    }
}

[HarmonyPatch(typeof(TheArchitect), "LoadDialogue")]
internal static class TheArchitectLoadDialoguePatch
{
    private static bool Prefix(TheArchitect __instance)
    {
        if (__instance.Owner?.Character is not HbrgoSaki)
        {
            return true;
        }

        TheArchitectDialogueFallback.EnsureDialogue(
            __instance);
        return false;
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
