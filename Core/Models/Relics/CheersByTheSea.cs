using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class CheersByTheSea : HotaruClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    [SavedProperty]
    public bool UsedThisCombat { get; private set; }
    internal void ResetBattle() => UsedThisCombat = false;
    internal bool ClaimActivation()
    {
        if (UsedThisCombat) return false;
        UsedThisCombat = true;
        return true;
    }
}
