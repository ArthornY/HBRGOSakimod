using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class ElectronicSoldierManual
{
    // Logical map-node kinds live in the existing run-relic save payload. Keeping
    // MapPointType.Unknown on the map preserves native save and 8-bit packet data.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MemoryFragmentMapData { get; protected set; } = string.Empty;

    internal void SetMemoryFragmentMapData(string data)
    {
        AssertMutable();
        MemoryFragmentMapData = data ?? string.Empty;
    }
}
