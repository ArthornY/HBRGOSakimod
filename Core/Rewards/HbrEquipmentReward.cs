using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BaseLib.Patches.Content;
using HBRGOSakiMod.Core.Equipment;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Combat.Rewards;

namespace HBRGOSakiMod.Core.Rewards;

// A battle drop: one accessory or chip, or the "+" materials. The rolled rows
// are stored in the reward so a restored reward screen shows the same goods.
public sealed class HbrEquipmentReward : ModCustomReward
{
    [CustomEnum(null)]
    public static RewardType HbrEquipment;

    private readonly ElectronicSoldierManual _manual;
    private readonly HbrEquipmentClaim _claim;

    public override RewardType ModRewardType => HbrEquipment;

    public override int RewardsSetIndex => 5;

    public override LocString Description
    {
        get
        {
            LocString description = new("relics", "HBR_EQUIPMENT_REWARD.description");
            description.Add("Name", Label());
            return description;
        }
    }

    public override bool IsPopulated => _manual != null && _claim != null;

    protected override string RewardIconPath => HbrEquipmentArt.Icon(IconFile());

    internal HbrEquipmentReward(Player player, ElectronicSoldierManual manual, HbrEquipmentClaim claim)
        : base(player)
    {
        _manual = manual;
        _claim = claim;
    }

    public override void Populate()
    {
    }

    protected override Task<bool> OnSelect() =>
        Task.FromResult(HbrEquipmentNetActions.RequestClaim(_manual, _claim));

    public override void MarkContentAsSeen()
    {
    }

    public override string ToModRewardJson() => JsonSerializer.Serialize(_claim);

    public static HbrEquipmentReward CreateFromSerializable(SerializableReward save, Player player, string json)
    {
        HbrEquipmentClaim claim = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { claim = JsonSerializer.Deserialize<HbrEquipmentClaim>(json); }
            catch (JsonException) { }
        }
        return new HbrEquipmentReward(player, player.GetRelic<ElectronicSoldierManual>(), claim);
    }

    private HbrEquipmentClaimItem FirstItem => _claim?.Items?.FirstOrDefault();

    private string Label()
    {
        if (FirstItem is { } row && HbrEquipmentCatalog.Find(row.Template) is { } template)
        {
            var item = new HbrEquipmentItem
            {
                Template = row.Template,
                Rolls = HbrEquipmentRules.NormalizeRolls(template, row.Rolls),
                Plus = row.Plus ?? Array.Empty<HbrPlusSlot>()
            };
            return $"{HbrEquipmentText.KindName(template.Kind)}：{HbrEquipmentText.Name(item)}（{HbrEquipmentText.Stars(template.Rarity)}）";
        }
        string release = _claim?.PlusRelease > 0 ? $"{HbrEquipmentCatalog.PlusReleaseName} ×{_claim.PlusRelease}" : null;
        string change = _claim?.PlusChange > 0 ? $"{HbrEquipmentCatalog.PlusChangeName} ×{_claim.PlusChange}" : null;
        return string.Join("、", new[] { release, change }.Where(part => part != null));
    }

    private string IconFile()
    {
        if (FirstItem is { } row && HbrEquipmentCatalog.Find(row.Template) is { } template) return template.Icon;
        return _claim?.PlusRelease > 0 ? HbrEquipmentCatalog.PlusReleaseIcon : HbrEquipmentCatalog.PlusChangeIcon;
    }
}
