using System;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(
    typeof(DamageVar),
    nameof(DamageVar.UpdateCardPreview))]
internal static class ModeTransitionDamagePreviewPatch
{
    private static void Prefix(
        CardModel card,
        bool runGlobalHooks,
        out IDisposable __state)
    {
        __state = ModeTransitionHelper.BeginDamagePreview(
            card,
            runGlobalHooks);
    }

    private static Exception Finalizer(
        Exception __exception,
        IDisposable __state)
    {
        __state?.Dispose();
        return __exception;
    }
}
