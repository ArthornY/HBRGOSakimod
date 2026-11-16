using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib.Scaffolding.Characters;

namespace HBRGOSakiMod.Core.Patches;

// Dread has its own screen and does not execute the ordinary character-select
// patches. Keep these bindings optional so one stable-built DLL works on both.
internal static class HbrgoDreadCharacterSelectCompatibility
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;
    internal static readonly Type ScreenType = typeof(CharacterModel).Assembly.GetType(
        "MegaCrit.Sts2.Core.Nodes.Screens.Dread.NDreadScreen", false);
    private static readonly ConditionalWeakTable<object, SelectorState> States = new();
    private static readonly FieldInfo ButtonCharacter = AccessTools.Field(typeof(NCharacterSelectButton), "_character");
    private static readonly FieldInfo SelectedButton = ScreenType?.GetField("_selectedButton", Flags);
    private static readonly FieldInfo FocusOverride = ScreenType?.GetField("_overriddenFocusedControl", Flags);
    private static readonly FieldInfo SkipSelection = ScreenType?.GetField("_skipNextCharSelectLogic", Flags);
    private static readonly FieldInfo CharacterButtons = ScreenType?.GetField("_charSelectButtons", Flags);
    private static readonly PropertyInfo Lobby = ScreenType?.GetProperty("Lobby", Flags);

    internal static MethodInfo Method(string name, params Type[] parameters) =>
        ScreenType?.GetMethod(name, Flags, null, parameters, null);

    internal static IEnumerable<CharacterModel> VisibleCharacters() =>
        ModelDb.AllCharacters.Where(character => character != null &&
            character.GetType() != typeof(HbrgoSaki) && character is not HbrgoSaki30A &&
            (character is not IModCharacterVanillaSelectionPolicy policy || !policy.HideFromVanillaCharacterSelect) &&
            (character is not CustomCharacterModel custom || !custom.HideFromVanillaCharacterSelect));

    internal static IEnumerable<CharacterModel> RotationCharacters() =>
        ModelDb.AllCharacters.Where(character => character != null &&
            character.GetType() != typeof(HbrgoSaki) && character is not HbrgoSaki30A &&
            (character is not IModCharacterVanillaSelectionPolicy policy || policy.AllowInVanillaRandomCharacterSelect) &&
            (character is not CustomCharacterModel custom || custom.AllowInVanillaRandomCharacterSelect));

    internal static ModelId NormalizeRotationStartingId(ModelId startingCharacter) =>
        startingCharacter == ModelDb.GetId<HbrgoSaki30A>() || startingCharacter == ModelDb.GetId<HbrgoSaki>()
            ? ModelDb.GetId<HbrgoSaki31O>() : startingCharacter;

    private static HbrgoSakiVariant PreferredVariant()
    {
        HbrgoSakiVariant variant = HbrgoSakiVariantPreference.GetPreferredVariant();
        return variant == HbrgoSakiVariant.ThirtyA && !HbrgoSakiVariantPreference.IsThirtyAUnlocked()
            ? HbrgoSakiVariant.ThirtyOneO : variant;
    }

    private sealed class SelectorState
    {
        internal HbrgoSakiVariant Variant = PreferredVariant();
        internal Button Switch;
    }

    private static SelectorState State(object screen) => States.GetValue(screen, _ => new SelectorState());
    private static IEnumerable<NCharacterSelectButton> Buttons(object screen) =>
        CharacterButtons?.GetValue(screen) as IEnumerable<NCharacterSelectButton> ?? Enumerable.Empty<NCharacterSelectButton>();
    private static NCharacterSelectButton SharedButton(object screen) =>
        Buttons(screen).FirstOrDefault(button => button.Character is HbrgoSaki31O);

    internal static void Attach(object screen)
    {
        if (screen is not Control control) return;
        SelectorState state = State(screen);
        if (state.Switch != null) return;
        // The native checkbox occupies bottom -299..-235 and the character row
        // -229..-75. This independent row is above both, not inside InfoPanel.
        Button toggle = new()
        {
            Name = "HBRGOSakiDreadVariantSwitchButton",
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = -210f, OffsetRight = 210f, OffsetTop = -360f, OffsetBottom = -314f,
            ClipText = true, FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            Visible = false
        };
        toggle.AddThemeFontSizeOverride("font_size", 18);
        control.AddChild(toggle);
        state.Switch = toggle;
        toggle.Pressed += () => Toggle(screen);
        if (SharedButton(screen) is { } shared)
        {
            toggle.FocusNeighborBottom = shared.GetPath();
            shared.FocusNeighborTop = toggle.GetPath();
        }
        Refresh(screen, state);
    }

    // The original screen chooses the preferred character, including vanilla
    // characters. Only remap an already-Saki preferred ID to the shared button.
    internal static void Open(object screen)
    {
        SelectorState state = State(screen);
        state.Variant = PreferredVariant();
        object lobby = Lobby?.GetValue(screen);
        object service = lobby?.GetType().GetProperty("NetService", Flags)?.GetValue(lobby);
        bool singleplayer = service?.GetType().GetProperty("Type", Flags)?.GetValue(service)?.ToString() == "Singleplayer";
        object progress = SaveManager.Instance.Progress;
        ModelId preferred = progress.GetType().GetProperty(singleplayer ? "PreferredDreadCharacter" :
            "PreferredMultiplayerDreadCharacter", Flags)?.GetValue(progress) as ModelId;
        bool legacy = preferred == ModelDb.GetId<HbrgoSaki>();
        if ((legacy || preferred == ModelDb.GetId<HbrgoSaki31O>() || preferred == ModelDb.GetId<HbrgoSaki30A>()) &&
            SharedButton(screen) is { } shared)
        {
            if (legacy) state.Variant = HbrgoSakiVariant.ThirtyOneO;
            FocusOverride?.SetValue(screen, shared);
            // OnSubmenuOpened has already queued native initial focus. It reads
            // the override when deferred, while this also handles controllers.
            shared.Select();
        }
        Refresh(screen, state);
    }

    internal sealed class SelectionIdentity : IDisposable
    {
        private NCharacterSelectButton _button;
        private readonly CharacterModel _original;
        internal readonly CharacterModel Actual;
        internal readonly bool Skipped;
        internal SelectionIdentity(NCharacterSelectButton button, CharacterModel actual, bool skipped)
        {
            _button = button; _original = button.Character; Actual = actual; Skipped = skipped;
            if (!skipped) ButtonCharacter.SetValue(button, actual);
        }
        public void Dispose()
        {
            NCharacterSelectButton button = _button;
            if (button == null) return;
            _button = null;
            ButtonCharacter.SetValue(button, _original);
        }
    }

    internal static SelectionIdentity BeginSelection(object screen, NCharacterSelectButton button)
    {
        if (button?.Character is not HbrgoSaki31O) return null;
        bool skipped = SkipSelection?.GetValue(screen) is true;
        SelectorState state = State(screen);
        HbrgoSakiVariant variant = state.Variant;
        if (button.IsLocked || variant == HbrgoSakiVariant.ThirtyA && !HbrgoSakiVariantPreference.IsThirtyAUnlocked())
            variant = HbrgoSakiVariant.ThirtyOneO;
        return new SelectionIdentity(button, HbrgoSakiVariantPreference.GetCharacter(variant), skipped);
    }

    internal static void AfterSelection(object screen, NCharacterSelectButton button, SelectionIdentity selection)
    {
        if (!States.TryGetValue(screen, out SelectorState state)) return;
        CharacterModel actual = selection?.Skipped == true ? button.Character : selection?.Actual ?? button.Character;
        if (actual is HbrgoSaki31O or HbrgoSaki30A)
        {
            state.Variant = HbrgoSakiVariantPreference.FromCharacter(actual);
            if (!button.IsLocked && selection?.Skipped != true)
                HbrgoSakiVariantPreference.SetPreferredVariant(state.Variant);
        }
        Refresh(screen, state);
    }

    internal static SelectionIdentity BeginRotationTickbox(object screen)
    {
        if (SelectedButton?.GetValue(screen) is not NCharacterSelectButton button ||
            button.Character is not HbrgoSaki31O) return null;
        // The shared catalog button has been restored to 31O after selection;
        // keep native checkbox callbacks on the actual local player's variant.
        object lobby = Lobby?.GetValue(screen);
        object local = lobby?.GetType().GetProperty("LocalPlayer", Flags)?.GetValue(lobby);
        CharacterModel actual = local?.GetType().GetField("character", Flags)?.GetValue(local) as CharacterModel;
        return actual is HbrgoSaki31O or HbrgoSaki30A ? new SelectionIdentity(button, actual, false) : null;
    }

    private static void Toggle(object screen)
    {
        if (SelectedButton?.GetValue(screen) is not NCharacterSelectButton selected ||
            selected.Character is not HbrgoSaki31O || selected.IsLocked) return;
        SelectorState state = State(screen);
        HbrgoSakiVariant next = state.Variant == HbrgoSakiVariant.ThirtyA
            ? HbrgoSakiVariant.ThirtyOneO : HbrgoSakiVariant.ThirtyA;
        if (next == HbrgoSakiVariant.ThirtyA && !HbrgoSakiVariantPreference.IsThirtyAUnlocked())
        { Refresh(screen, state); return; }
        state.Variant = next;
        // Unlike Button.Select(), the native screen method accepts a repeated
        // selection of the same shared button and updates the real lobby.
        Method("SelectCharacter", typeof(NCharacterSelectButton)).Invoke(screen, new object[] { selected });
    }

    private static void Refresh(object screen, SelectorState state)
    {
        if (state.Switch == null) return;
        NCharacterSelectButton selected = SelectedButton?.GetValue(screen) as NCharacterSelectButton;
        bool shared = selected?.Character is HbrgoSaki31O or HbrgoSaki30A;
        state.Switch.Visible = shared;
        bool unlocked = state.Variant == HbrgoSakiVariant.ThirtyA || HbrgoSakiVariantPreference.IsThirtyAUnlocked();
        state.Switch.Text = state.Variant == HbrgoSakiVariant.ThirtyA ? "切换至 丰川祥子（31O）" :
            unlocked ? "切换至 丰川祥子（30A）" : "丰川祥子（30A）：31O解锁骨龙后解锁";
        state.Switch.TooltipText = state.Switch.Text;
        state.Switch.Disabled = !shared || selected.IsLocked || !unlocked;
    }
}

[HarmonyPatch]
internal static class DreadCharacterCatalogCompatibilityPatch
{
    private static bool Prepare() => HbrgoDreadCharacterSelectCompatibility.Method("InitCharacterButtons") != null;
    private static IEnumerable<MethodBase> TargetMethods()
    {
        if (HbrgoDreadCharacterSelectCompatibility.Method("InitCharacterButtons") is { } method) yield return method;
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        DreadRotationCharacterCompatibilityPatch.ReplaceCatalog(instructions, nameof(HbrgoDreadCharacterSelectCompatibility.VisibleCharacters));
}

[HarmonyPatch]
internal static class DreadRotationCharacterCompatibilityPatch
{
    private static MethodInfo Target => typeof(CharacterModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.AscensionHelper", false)?
        .GetMethod("GetRotatingCharacterIdForStreak", new[] { typeof(ModelId), typeof(int) });
    private static bool Prepare() => Target != null;
    private static IEnumerable<MethodBase> TargetMethods() { if (Target != null) yield return Target; }
    private static bool Prefix(ref ModelId __0, int __1, ref ModelId __result)
    {
        // A chosen 30A is the real first run, not a second catalog slot. Later
        // rotations use the shared31O slot without a missing-index fallback.
        if (__1 == 0 && __0 == ModelDb.GetId<HbrgoSaki30A>()) { __result = __0; return false; }
        __0 = HbrgoDreadCharacterSelectCompatibility.NormalizeRotationStartingId(__0);
        return true;
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        ReplaceCatalog(instructions, nameof(HbrgoDreadCharacterSelectCompatibility.RotationCharacters));
    internal static IEnumerable<CodeInstruction> ReplaceCatalog(IEnumerable<CodeInstruction> instructions, string helper)
    {
        MethodInfo original = AccessTools.PropertyGetter(typeof(ModelDb), nameof(ModelDb.AllCharacters));
        MethodInfo replacement = AccessTools.Method(typeof(HbrgoDreadCharacterSelectCompatibility), helper);
        List<CodeInstruction> result = instructions.ToList();
        int count = 0;
        foreach (CodeInstruction instruction in result)
            if (instruction.Calls(original)) { instruction.opcode = OpCodes.Call; instruction.operand = replacement; count++; }
        if (count != 1) throw new InvalidOperationException("Dread character catalog expected one native catalog call; found " + count);
        return result;
    }
}

[HarmonyPatch]
internal static class DreadCharacterSelectReadyPatch
{
    private static bool Prepare() => HbrgoDreadCharacterSelectCompatibility.Method("_Ready") != null;
    private static IEnumerable<MethodBase> TargetMethods() { if (HbrgoDreadCharacterSelectCompatibility.Method("_Ready") is { } method) yield return method; }
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(object __instance) => HbrgoDreadCharacterSelectCompatibility.Attach(__instance);
}

[HarmonyPatch]
internal static class DreadCharacterSelectOpenPatch
{
    private static bool Prepare() => HbrgoDreadCharacterSelectCompatibility.Method("OnSubmenuOpened") != null;
    private static IEnumerable<MethodBase> TargetMethods() { if (HbrgoDreadCharacterSelectCompatibility.Method("OnSubmenuOpened") is { } method) yield return method; }
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(object __instance) => HbrgoDreadCharacterSelectCompatibility.Open(__instance);
}

[HarmonyPatch]
internal static class DreadCharacterSelectVariantPatch
{
    private static bool Prepare() => HbrgoDreadCharacterSelectCompatibility.Method("SelectCharacter", typeof(NCharacterSelectButton)) != null;
    private static IEnumerable<MethodBase> TargetMethods() { if (HbrgoDreadCharacterSelectCompatibility.Method("SelectCharacter", typeof(NCharacterSelectButton)) is { } method) yield return method; }
    private static void Prefix(object __instance, NCharacterSelectButton __0, out HbrgoDreadCharacterSelectCompatibility.SelectionIdentity __state) =>
        __state = HbrgoDreadCharacterSelectCompatibility.BeginSelection(__instance, __0);
    private static void Postfix(object __instance, NCharacterSelectButton __0, HbrgoDreadCharacterSelectCompatibility.SelectionIdentity __state) =>
        HbrgoDreadCharacterSelectCompatibility.AfterSelection(__instance, __0, __state);
    private static Exception Finalizer(Exception __exception, HbrgoDreadCharacterSelectCompatibility.SelectionIdentity __state)
    { __state?.Dispose(); return __exception; }
}

[HarmonyPatch]
internal static class DreadRotationTickboxVariantPatch
{
    private static bool Prepare() => HbrgoDreadCharacterSelectCompatibility.Method("BeginRotationTickboxChanged", typeof(bool)) != null;
    private static IEnumerable<MethodBase> TargetMethods() { if (HbrgoDreadCharacterSelectCompatibility.Method("BeginRotationTickboxChanged", typeof(bool)) is { } method) yield return method; }
    private static void Prefix(object __instance, out HbrgoDreadCharacterSelectCompatibility.SelectionIdentity __state) =>
        __state = HbrgoDreadCharacterSelectCompatibility.BeginRotationTickbox(__instance);
    private static Exception Finalizer(Exception __exception, HbrgoDreadCharacterSelectCompatibility.SelectionIdentity __state)
    { __state?.Dispose(); return __exception; }
}
