#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(SavedProperties), nameof(SavedProperties.FillInternal))]
internal static class SavedPropertiesReadOnlyCompatibilityPatch
{
    private const BindingFlags MemberFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly HashSet<string> LoggedActions = [];
    private static readonly object LogLock = new();

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(SavedProperties __instance, object model)
    {
        if (model == null)
        {
            return;
        }

        RestoreReadOnlyProperties(
            __instance.ints,
            model,
            static (property, value) =>
                property.PropertyType.IsEnum
                    ? Enum.ToObject(property.PropertyType, value)
                    : value);
        RestoreReadOnlyProperties(
            __instance.intArrays,
            model,
            static (property, value) => ConvertIntArray(property.PropertyType, value));
        RestoreReadOnlyProperties(
            __instance.bools,
            model,
            static (_, value) => value);
        RestoreReadOnlyProperties(
            __instance.modelIds,
            model,
            static (_, value) => value);
        RestoreReadOnlyProperties(
            __instance.cards,
            model,
            static (_, value) => value);
        RestoreReadOnlyProperties(
            __instance.cardArrays,
            model,
            static (_, value) => value.ToList());
        RestoreReadOnlyProperties(
            __instance.strings,
            model,
            static (_, value) => value);
    }

    private static void RestoreReadOnlyProperties<T>(
        List<SavedProperties.SavedProperty<T>>? entries,
        object model,
        Func<PropertyInfo, T, object?> convertValue)
    {
        if (entries == null)
        {
            return;
        }

        Type modelType = model.GetType();
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            SavedProperties.SavedProperty<T> entry = entries[index];
            PropertyInfo? property = modelType.GetProperty(entry.name, MemberFlags);
            if (property == null || property.GetSetMethod(nonPublic: true) != null)
            {
                continue;
            }

            FieldInfo? backingField = FindBackingField(modelType, property.Name);
            bool restored = false;
            string detail;

            if (backingField == null)
            {
                detail = "no compatible backing field was found";
            }
            else if (backingField.IsInitOnly)
            {
                detail = $"backing field {backingField.Name} is readonly";
            }
            else
            {
                try
                {
                    object? value = convertValue(property, entry.value);
                    if (!TryConvertForField(value, backingField.FieldType, out object? converted))
                    {
                        detail =
                            $"value type {value?.GetType().FullName ?? "<null>"} is incompatible with " +
                            $"backing field {backingField.Name} ({backingField.FieldType.FullName})";
                    }
                    else
                    {
                        backingField.SetValue(model, converted);
                        restored = true;
                        detail = $"restored through backing field {backingField.Name}";
                    }
                }
                catch (Exception exception)
                {
                    detail = $"backing-field restore failed: {exception.GetType().Name}: {exception.Message}";
                }
            }

            // The base implementation throws on a property without a setter. The value has
            // either been restored above or is discarded so multiplayer sync can continue.
            entries.RemoveAt(index);
            LogOnce(modelType, property.Name, restored, detail);
        }
    }

    private static object ConvertIntArray(Type propertyType, int[] values)
    {
        Type? elementType = propertyType.GetElementType();
        if (elementType?.IsEnum != true)
        {
            return values;
        }

        Array converted = Array.CreateInstance(elementType, values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            converted.SetValue(Enum.ToObject(elementType, values[index]), index);
        }

        return converted;
    }

    private static FieldInfo? FindBackingField(Type modelType, string propertyName)
    {
        string lowerCamelName = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        string[] candidates =
        [
            $"<{propertyName}>k__BackingField",
            $"_{lowerCamelName}",
            lowerCamelName,
        ];

        for (Type? currentType = modelType; currentType != null; currentType = currentType.BaseType)
        {
            foreach (string candidate in candidates)
            {
                FieldInfo? field = currentType.GetField(
                    candidate,
                    MemberFlags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }
        }

        return null;
    }

    private static bool TryConvertForField(
        object? value,
        Type fieldType,
        out object? converted)
    {
        if (value == null)
        {
            converted = null;
            return !fieldType.IsValueType || Nullable.GetUnderlyingType(fieldType) != null;
        }

        if (fieldType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        Type targetType = Nullable.GetUnderlyingType(fieldType) ?? fieldType;
        try
        {
            if (targetType.IsEnum)
            {
                converted = Enum.ToObject(targetType, value);
                return true;
            }

            converted = Convert.ChangeType(value, targetType);
            return true;
        }
        catch
        {
            converted = null;
            return false;
        }
    }

    private static void LogOnce(
        Type modelType,
        string propertyName,
        bool restored,
        string detail)
    {
        string key = $"{modelType.AssemblyQualifiedName}:{propertyName}:{restored}:{detail}";
        lock (LogLock)
        {
            if (!LoggedActions.Add(key))
            {
                return;
            }
        }

        string action = restored ? "Recovered" : "Skipped";
        Log.Warn(
            $"[HBRGOSakiMod] {action} read-only saved property " +
            $"{modelType.FullName}.{propertyName}: {detail}. " +
            "This usually means multiplayer clients have different SavedProperty tables.");
    }
}
