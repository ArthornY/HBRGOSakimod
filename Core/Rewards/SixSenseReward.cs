using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rewards;

namespace HBRGOSakiMod.Core.Rewards;

public sealed class SixSenseReward : Reward
{
    private readonly ElectronicSoldierManual _manual;
    private IReadOnlyList<SixSenseAttribute> _options = new List<SixSenseAttribute>();

    protected override RewardType RewardType => RewardType.None;

    public override int RewardsSetIndex => 4;

    public override LocString Description => new("relics", "SIX_SENSE_REWARD.description");

    public override bool IsPopulated => _options.Count > 0;

    protected override string IconPath => _manual.PackedIconPath;

    public SixSenseReward(Player player, ElectronicSoldierManual manual)
        : base(player)
    {
        _manual = manual;
    }

    public override void Populate()
    {
        if (_options.Count > 0)
        {
            return;
        }

        _options = _manual.RollSixSenseRewardOptions();
    }

    protected override async Task<bool> OnSelect()
    {
        Populate();
        return await _manual.ChooseAndIncreaseSense(_options);
    }

    public override void MarkContentAsSeen()
    {
    }
}
