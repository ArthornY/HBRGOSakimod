using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruCombatWearHelper
{
    internal static int GetAmount(CombatRoom room) => room?.RoomType switch
    {
        RoomType.Monster => 1,
        RoomType.Elite => 2,
        RoomType.Boss => 4,
        _ => 0
    };
}