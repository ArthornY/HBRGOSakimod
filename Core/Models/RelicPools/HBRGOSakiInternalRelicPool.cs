using BaseLib.Abstracts;

namespace HBRGOSakiMod.Core.Models.RelicPools;

// Registers generated multiplayer and collection relics without normal reward drops.
public sealed class HBRGOSakiInternalRelicPool : CustomRelicPoolModel
{
    // RelicModel.Pool resolves through ModelDb.AllRelicPools, including for tooltips.
    // Event/Starter rarity keeps these generated relics out of normal reward rolls.
    public override bool IsShared => true;
}
