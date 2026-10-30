using System;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;

namespace HBRGOSakiMod.Core.Helpers;

internal static class LibraryInputCompatibility
{
    private static readonly FieldInfo ConfirmationField = ResolveConfirmationField(typeof(MegaInput));
    private static readonly Lazy<StringName> ConfirmationAction = new(() =>
        (StringName)ConfirmationField.GetValue(null));

    // Cache the native action, not a physical key. InputMap remapping remains
    // authoritative and neither lookup nor reflection runs for later presses.
    internal static bool IsAdvancePressed(InputEvent inputEvent) => inputEvent != null &&
        (inputEvent.IsActionPressed(ConfirmationAction.Value) || inputEvent.IsActionPressed(MegaInput.select));

    internal static FieldInfo ResolveConfirmationField(Type inputType)
    {
        foreach (string name in new[] { "confirm", "accept" })
        {
            FieldInfo field = inputType.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null) continue;
            if (field.FieldType == typeof(StringName)) return field;
            throw new MissingFieldException(inputType.FullName, name + " : Godot.StringName");
        }
        throw new MissingFieldException(inputType.FullName, "confirm or accept : Godot.StringName");
    }
}
