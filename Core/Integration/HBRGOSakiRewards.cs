using HBRGOSakiMod.Core.Rewards;
using STS2RitsuLib.Combat.Rewards;

namespace HBRGOSakiMod.Core.Integration;

internal static class HBRGOSakiRewards
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        ModRewardRegistry.Register(
            SixSenseReward.SixSense,
            SixSenseReward.CreateFromSerializable);
        ModRewardRegistry.Register(
            HbrEquipmentReward.HbrEquipment,
            HbrEquipmentReward.CreateFromSerializable);
        ModRewardRegistry.Register(
            HbrFateFragmentReward.HbrFateFragment,
            HbrFateFragmentReward.CreateFromSerializable);
    }
}
