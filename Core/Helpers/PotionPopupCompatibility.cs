using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PotionPopupCompatibility
{
    private static readonly Func<NPotionPopup, bool> ReadRemovalState = BindRemovalState();

    internal static bool IsMarkedForRemoval(NPotionPopup popup) => ReadRemovalState(popup);

    private static Func<NPotionPopup, bool> BindRemovalState()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        Type type = typeof(NPotionPopup);
        PropertyInfo property = type.GetProperty("IsMarkedForRemoval", flags);
        MethodInfo getter = property?.GetGetMethod(nonPublic: true);
        if (getter != null && !getter.IsStatic && getter.ReturnType == typeof(bool) &&
            getter.GetParameters().Length == 0)
            return getter.CreateDelegate<Func<NPotionPopup, bool>>();
        FieldInfo field = type.GetField("IsMarkedForRemoval", flags);
        if (field?.FieldType == typeof(bool))
        {
            AccessTools.FieldRef<NPotionPopup, bool> read = AccessTools.FieldRefAccess<NPotionPopup, bool>(field);
            return popup => read(popup);
        }
        throw new MissingMemberException(type.FullName, "bool IsMarkedForRemoval field or getter");
    }
}
