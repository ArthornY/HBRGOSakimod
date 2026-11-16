using System.Text.Json.Nodes;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Capabilities;

/// <summary>
/// Card six-sense attributes were removed (2026-11-09); only the level points
/// remain. The capability stays registered so cards saved with an attribute still
/// load: the saved value is read and ignored, nothing is written back, and new
/// cards no longer receive this capability.
/// </summary>
public sealed class SixSenseCardCapability : CardCapability
{
    protected override JsonNode SaveAdditionalState() => null;

    protected override void LoadAdditionalState(JsonNode state, int schemaVersion)
    {
    }
}
