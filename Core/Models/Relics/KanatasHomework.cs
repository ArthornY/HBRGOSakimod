using System.Threading.Tasks;
using System.Linq;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class KanatasHomework : HotaruClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (!IsMelted && HotaruClassRelicCombatHelper.IsEnabled(Owner) && room != null &&
            room.CombatState.Players.Contains(Owner) && room.RoomType is RoomType.Elite or RoomType.Boss &&
            Owner.GetRelic<ElectronicSoldierManual>() is { } manual &&
            manual.ClaimMemoryGardenRoomPoints(MemoryGardenUpgradePointsHelper.GetRoomKey(Owner.RunState, room) + ":kanatas-homework", 1)) Flash();
        return Task.CompletedTask;
    }
}
