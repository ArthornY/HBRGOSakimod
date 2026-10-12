using System.Globalization;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CardDamageNumberFormatter
{
    internal static string Format(decimal damage) =>
        damage.ToString("#,0.############################", CultureInfo.InvariantCulture);
}
