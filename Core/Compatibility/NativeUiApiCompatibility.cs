using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace HBRGOSakiMod.Core.Compatibility;

internal static class NativeUiApiCompatibility
{
    internal static void ToggleIsInteractableCompat(this NCreature node, bool on)
    {
        MethodInfo method = CreatureInteraction.Method;
        CompatibilityReflection.Invoke(method, node,
            CreatureInteraction.TakesVisibilityFlag ? [on, true] : [on]);
    }

    internal static void SetCreatureIsInteractableCompat(
        this NCombatRoom room, Creature creature, bool on)
    {
        if (RoomInteraction.LegacyMethod is { } method)
        {
            CompatibilityReflection.Invoke(method, room, [creature, on]);
            return;
        }

        NCreature node = room.GetCreatureNode(creature);
        if (node != null)
        {
            // v0.113 SetIsInteractable updates visibility immediately when true,
            // matching the old Toggle method, and refreshes room navigation even
            // when the interactable value is unchanged (after dragging a pet).
            node.ToggleIsInteractableCompat(on);
        }
        else
        {
            // The old room wrapper refreshed navigation even for a missing node.
            CompatibilityReflection.Invoke(RoomInteraction.RefreshNavigation,
                room, Array.Empty<object>());
        }
    }

    internal static void PushHotkeyPressedBindingCompat(
        this NHotkeyManager manager, string hotkey, Action action)
    {
        MethodInfo method = Hotkeys.Push;
        CompatibilityReflection.Invoke(method, manager,
            Hotkeys.TakesSequence
                ? [hotkey, action, Hotkeys.DefaultSequence]
                : [hotkey, action]);
    }

    internal static void RemoveHotkeyPressedBindingCompat(
        this NHotkeyManager manager, string hotkey, Action action)
    {
        // v0.113 returns the removed binding's sequence. These callers only
        // unregister their own handler, as before; they do not reinsert it.
        CompatibilityReflection.Invoke(Hotkeys.Remove, manager, [hotkey, action]);
    }

    internal static void SelectCharacterCompat(
        this NCharacterSelectScreen screen,
        NCharacterSelectButton button,
        CharacterModel character)
    {
        MethodInfo method = CharacterSelection.Select;
        if (CharacterSelection.TakesCharacter)
        {
            CompatibilityReflection.Invoke(method, screen, [button, character]);
            return;
        }

        // v0.113 captures button.Character at method entry. Temporarily supply
        // the requested variant, then restore the shared 31O catalog button's
        // identity. The native UI/lobby and our postfix see the correct variant,
        // while FindSharedButton keeps recognizing the same button afterwards.
        object original = CharacterSelection.ButtonCharacter.GetValue(button);
        try
        {
            CharacterSelection.ButtonCharacter.SetValue(button, character);
            CompatibilityReflection.Invoke(method, screen, [button]);
        }
        finally
        {
            CharacterSelection.ButtonCharacter.SetValue(button, original);
        }
    }

    internal static CharacterModel SelectedCharacterFromArguments(object[] arguments)
    {
        foreach (object argument in arguments)
        {
            if (argument is CharacterModel character) return character;
        }
        foreach (object argument in arguments)
        {
            if (argument is NCharacterSelectButton button) return button.Character;
        }

        throw new ArgumentException("Character selection supplied neither a character nor a button.",
            nameof(arguments));
    }

    private static class CreatureInteraction
    {
        internal static readonly MethodInfo Method =
            typeof(NCreature).GetMethod("ToggleIsInteractable", [typeof(bool)]) ??
            typeof(NCreature).GetMethod("SetIsInteractable", [typeof(bool), typeof(bool)]) ??
            throw new MissingMethodException(typeof(NCreature).FullName, "SetIsInteractable");

        internal static readonly bool TakesVisibilityFlag = Method.GetParameters().Length == 2;
    }

    private static class RoomInteraction
    {
        internal static readonly MethodInfo LegacyMethod = typeof(NCombatRoom)
            .GetMethod("SetCreatureIsInteractable", [typeof(Creature), typeof(bool)]);

        internal static readonly MethodInfo RefreshNavigation = typeof(NCombatRoom)
            .GetMethod("UpdateCreatureNavigation", BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) ??
            throw new MissingMethodException(typeof(NCombatRoom).FullName, "UpdateCreatureNavigation");
    }

    private static class Hotkeys
    {
        internal static readonly MethodInfo Push =
            typeof(NHotkeyManager).GetMethod("PushHotkeyPressedBinding", [typeof(string), typeof(Action)]) ??
            typeof(NHotkeyManager).GetMethod("PushHotkeyPressedBinding",
                [typeof(string), typeof(Action), typeof(long)]) ??
            throw new MissingMethodException(typeof(NHotkeyManager).FullName, "PushHotkeyPressedBinding");

        internal static readonly MethodInfo Remove =
            typeof(NHotkeyManager).GetMethod("RemoveHotkeyPressedBinding", [typeof(string), typeof(Action)]) ??
            throw new MissingMethodException(typeof(NHotkeyManager).FullName, "RemoveHotkeyPressedBinding");

        internal static readonly bool TakesSequence = Push.GetParameters().Length == 3;

        internal static readonly object DefaultSequence = GetDefaultSequence();

        private static object GetDefaultSequence()
        {
            ParameterInfo[] parameters = Push.GetParameters();
            if (parameters.Length == 2) return null;
            if (parameters[2].HasDefaultValue) return parameters[2].DefaultValue;
            throw new MissingMethodException(typeof(NHotkeyManager).FullName,
                "PushHotkeyPressedBinding optional sequence");
        }
    }

    private static class CharacterSelection
    {
        internal static readonly MethodInfo Select =
            typeof(NCharacterSelectScreen).GetMethod("SelectCharacter",
                [typeof(NCharacterSelectButton), typeof(CharacterModel)]) ??
            typeof(NCharacterSelectScreen).GetMethod("SelectCharacter", [typeof(NCharacterSelectButton)]) ??
            throw new MissingMethodException(typeof(NCharacterSelectScreen).FullName, "SelectCharacter");

        internal static readonly bool TakesCharacter = Select.GetParameters().Length == 2;

        internal static readonly FieldInfo ButtonCharacter = typeof(NCharacterSelectButton)
            .GetField("_character", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingFieldException(typeof(NCharacterSelectButton).FullName, "_character");
    }
}
