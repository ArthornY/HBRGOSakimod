using System;

namespace HBRGOSakiMod.Core.Integration;

internal static class HbrgoSakiCardPortraitContext
{
    private static WeakReference<object> _library;
    private static HbrgoSakiVariant _variant;

    // Library cards and their upgrade copies have no player owner.
    internal static HbrgoSakiVariant? LibraryVariant =>
        _library?.TryGetTarget(out _) == true ? _variant : null;

    internal static void SetLibraryVariant(object library, HbrgoSakiVariant variant)
    {
        _library = new WeakReference<object>(library);
        _variant = variant;
    }

    internal static void ClearLibrary(object library)
    {
        if (_library?.TryGetTarget(out object current) == true &&
            ReferenceEquals(current, library))
        {
            _library = null;
        }
    }
}
