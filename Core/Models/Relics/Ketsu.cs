using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class Ketsu : ElectronicSoldierManual
{
    public override int SixSenseRewardOptionCount => 4;

    protected override int CombatSixSensePointCount => base.CombatSixSensePointCount + 1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ConvertedSixSenseRewards { get; private set; }

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("relics/electronic_soldier_manual.png");

    public override bool TryModifyRewardsLate(
        Player player,
        List<Reward> rewards,
        AbstractRoom room)
    {
        bool modified = base.TryModifyRewardsLate(player, rewards, room);
        if (player != Owner || room is not CombatRoom)
        {
            return modified;
        }

        GrantSixSensePoints(ConvertedSixSenseRewards);
        SetConvertedSixSenseRewards(0);
        return modified;
    }

    internal void SetConvertedSixSenseRewards(int amount)
    {
        AssertMutable();
        ConvertedSixSenseRewards = Math.Max(0, amount);
    }

    internal Task OfferConvertedSixSenseRewardsImmediately()
    {
        GrantSixSensePoints(ConvertedSixSenseRewards);
        SetConvertedSixSenseRewards(0);
        return Task.CompletedTask;
    }
}
