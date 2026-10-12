using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class ElectronicSoldierManual
{
    // Temporary lost effects are saved with the run's shared garden progress.
    // Loading a pre-combat save must not leave a combat-only downgrade behind.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MemoryGardenDampenRestores { get; protected set; } = string.Empty;

    internal void SetMemoryGardenDampenRestores(string pending)
    {
        AssertMutable();
        MemoryGardenDampenRestores = pending;
    }
}
