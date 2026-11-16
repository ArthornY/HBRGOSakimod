using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Helpers;

// HBR texts that replace a whole native description apply only where the
// current language's own table defines them: a fallback table is another language.
internal static class HbrLocalizedText
{
    internal static string Local(string tableName, string key)
    {
        LocTable table;
        try { table = LocManager.Instance?.GetTable(tableName); }
        catch (LocException) { return null; }
        return table?.IsLocalKey(key) == true ? table.GetRawText(key) : null;
    }
}
