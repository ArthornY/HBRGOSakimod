namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class DeflectorProtection
{
    internal void RestoreDpAfterHalfBodyRevival()
    {
        AssertMutable();
        EnsureDpInitialized();
        // A life-saving reset fills DP exactly; ordinary recovery penalties do
        // not reduce the reset promised by this consumed memory.
        SetDpClamped(MaxDp);
        Flash();
    }
}
