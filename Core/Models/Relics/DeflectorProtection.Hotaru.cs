using System;
using System.Linq;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Relics.Visibility;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class DeflectorProtection : IModRelicVisibility
{
    // Hotaru's manual explains DP; the existing DP bar remains visible.
    public bool IsRelicVisible => !IsMutable || Owner?.Character is not HbrgoHotaru;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LastHotaruCombatWearRoom { get; private set; } = string.Empty;

    internal void ApplyHotaruCombatWear(CombatRoom room)
    {
        if (!IsMutable || Owner?.Character is not HbrgoHotaru || room == null ||
            !room.CombatState.Players.Contains(Owner)) return;
        int amount = HbrResourceValueHelper.FixedInt(Owner.Creature, HotaruCombatWearHelper.GetAmount(room));
        if (amount <= 0) return;
        string key = MemoryGardenUpgradePointsHelper.GetRoomKey(Owner.RunState, room);
        if (string.IsNullOrEmpty(key) || LastHotaruCombatWearRoom == key) return;
        LastHotaruCombatWearRoom = key;
        if (Owner.GetRelic<HotaruIridescence>()?.Layers >= 3)
        {
            GainMaxDp(amount);
        }
        else
        {
            LoseMaxDp(amount);
        }
    }
}
