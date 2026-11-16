using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrgoRandomCharacterSelectCompatibility
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;
    internal static readonly MethodInfo RandomInitializer =
        typeof(NCharacterSelectButton).GetMethod("InitAsRandom", Flags);
    private static readonly FieldInfo ButtonDelegate =
        typeof(NCharacterSelectButton).GetField("_delegate", Flags);
    private static readonly MethodInfo LegacyInitializer =
        typeof(NCharacterSelectButton).GetMethod("Init", Flags);
    private static readonly Type PolicyScope = Type.GetType(
        "STS2RitsuLib.Scaffolding.Characters.Patches.CharacterVanillaSelectionPolicyScope, STS2-RitsuLib.Runtime", false);
    private static readonly MethodInfo EnterPolicyScope = PolicyScope?.GetMethod("Enter", Flags);
    private static readonly MethodInfo ExitPolicyScope = PolicyScope?.GetMethod("Exit", Flags);

    internal static bool EnterRandomScope()
    {
        if (LegacyInitializer == null || EnterPolicyScope == null || ExitPolicyScope == null)
        {
            Log.Warn("[HBRGOSakiMod] Random character policy scope unavailable; retaining explicit eligibility filtering.");
            return false;
        }
        // Ritsu 0.6.14 knows the old Init entry but not beta's InitAsRandom.
        // Use its existing RandomEligible scope for the whole synchronous body:
        // UnlockState.Characters also reads the catalog while checking locks.
        EnterPolicyScope.Invoke(null, new object[] { LegacyInitializer });
        return true;
    }

    internal static void ExitRandomScope(bool entered)
    {
        if (entered) ExitPolicyScope.Invoke(null, new object[] { LegacyInitializer });
    }

    internal static bool RefreshLockedRandom(NCharacterSelectButton button)
    {
        if (RandomInitializer == null || ButtonDelegate == null ||
            ButtonDelegate.GetValue(button) is not { } callback) return false;
        // Reuse the native initializer and the original screen delegate rather
        // than setting unlock progress or manufacturing a RandomCharacter unlock.
        RandomInitializer.Invoke(button, new[] { callback });
        if (!button.IsLocked) button.Enable();
        return true;
    }
}

[HarmonyPatch]
internal static class RandomCharacterEligibilityCompatibilityPatch
{
    private static bool Prepare() => HbrgoRandomCharacterSelectCompatibility.RandomInitializer != null;
    private static IEnumerable<MethodBase> TargetMethods()
    {
        if (HbrgoRandomCharacterSelectCompatibility.RandomInitializer is { } method) yield return method;
    }
    private static void Prefix(out bool __state) =>
        __state = HbrgoRandomCharacterSelectCompatibility.EnterRandomScope();
    private static Exception Finalizer(Exception __exception, bool __state)
    {
        HbrgoRandomCharacterSelectCompatibility.ExitRandomScope(__state);
        return __exception;
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        DreadRotationCharacterCompatibilityPatch.ReplaceCatalog(instructions,
            nameof(HbrgoDreadCharacterSelectCompatibility.RotationCharacters));
}

[HarmonyPatch]
internal static class RandomCharacterCachedUnlockCompatibilityPatch
{
    private static bool Prepare() => HbrgoRandomCharacterSelectCompatibility.RandomInitializer != null;
    private static IEnumerable<MethodBase> TargetMethods()
    {
        if (HbrgoRandomCharacterSelectCompatibility.RandomInitializer != null &&
            typeof(NCharacterSelectButton).GetMethod("UnlockIfPossible") is { } method) yield return method;
    }
    private static bool Prefix(NCharacterSelectButton __instance, ref bool ____isLocked)
    {
        // RotatingCharacter also sets IsRandom on beta; its state is unrelated.
        if (!____isLocked || !__instance.IsRandom ||
            __instance.Character?.GetType() != typeof(RandomCharacter)) return true;
        try
        {
            return !HbrgoRandomCharacterSelectCompatibility.RefreshLockedRandom(__instance);
        }
        catch (Exception error)
        {
            ____isLocked = true;
            Log.Warn($"[HBRGOSakiMod] Random character unlock refresh failed: {error.GetBaseException().Message}");
            return true;
        }
    }
}
