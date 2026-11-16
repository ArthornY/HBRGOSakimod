using System.Text.Json;
using System.Threading.Tasks;
using BaseLib.Patches.Content;
using HBRGOSakiMod.Core.FateDice;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Combat.Rewards;

namespace HBRGOSakiMod.Core.Rewards;

// One fragment of an elite's or a boss's three-choice set (design v3, section 4).
// The set shares one receipt, so claiming one fragment uses the whole set up.
public sealed class HbrFateFragmentReward : ModCustomReward
{
    [CustomEnum(null)]
    public static RewardType HbrFateFragment;

    private readonly ElectronicSoldierManual _manual;
    private readonly FateFragmentClaim _claim;

    public override RewardType ModRewardType => HbrFateFragment;

    public override int RewardsSetIndex => 6;

    public override LocString Description
    {
        get
        {
            HbrFateFragment fragment = HbrFateDiceCatalog.Find(_claim?.Fragment);
            LocString description = new("relics", "HBR_FATE_FRAGMENT_REWARD.description");
            description.Add("Name", fragment?.Name ?? string.Empty);
            description.Add("Category", fragment == null ? string.Empty : HbrFateDiceCatalog.CategoryName(fragment.Category));
            description.Add("Effect", HbrFateDiceCatalog.Describe(fragment, _manual?.IsHbrNumericsEnabled == true));
            return description;
        }
    }

    public override bool IsPopulated => _manual != null && HbrFateDiceCatalog.Exists(_claim?.Fragment);

    // The fragment placeholder until the final icon is drawn (「出图前，先用程序画的占位图」).
    protected override string RewardIconPath => HbrMagicSwordArt.FateFragmentIcon;

    internal string Fragment => _claim?.Fragment;

    internal HbrFateFragmentReward(Player player, ElectronicSoldierManual manual, string receipt, string fragment)
        : this(player, manual, new FateFragmentClaim { Receipt = receipt, Fragment = fragment })
    {
    }

    private HbrFateFragmentReward(Player player, ElectronicSoldierManual manual, FateFragmentClaim claim)
        : base(player)
    {
        _manual = manual;
        _claim = claim;
    }

    public override void Populate()
    {
    }

    protected override Task<bool> OnSelect() =>
        Task.FromResult(HbrFateDiceNetActions.RequestClaim(_manual, _claim?.Receipt, _claim?.Fragment));

    public override void MarkContentAsSeen()
    {
    }

    public override string ToModRewardJson() => JsonSerializer.Serialize(_claim);

    public static HbrFateFragmentReward CreateFromSerializable(SerializableReward save, Player player, string json)
    {
        FateFragmentClaim claim = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { claim = JsonSerializer.Deserialize<FateFragmentClaim>(json); }
            catch (JsonException) { }
        }
        return new HbrFateFragmentReward(player, player.GetRelic<ElectronicSoldierManual>(), claim);
    }

    private sealed class FateFragmentClaim
    {
        public string Receipt { get; init; }
        public string Fragment { get; init; }
    }
}
