using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Patches.Content;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Combat.Rewards;

namespace HBRGOSakiMod.Core.Rewards;

public sealed class SixSenseReward : ModCustomReward
{
    private const int OptionCountMask = 0x3;
    private const int OptionBits = 3;
    private const int FirstOptionShift = 2;
    private const int FixedAttributeFlag = 1 << 11;
    private const int FixedAttributeShift = 12;

    [CustomEnum(null)]
    public static RewardType SixSense;

    private readonly ElectronicSoldierManual _manual;
    private readonly SixSenseAttribute? _fixedAttribute;
    private IReadOnlyList<SixSenseAttribute> _options = new List<SixSenseAttribute>();

    public override RewardType ModRewardType => SixSense;

    public override int RewardsSetIndex => 4;

    public override LocString Description => new(
        "relics",
        _fixedAttribute.HasValue
            ? ElectronicSoldierManual.GetSenseRewardDescriptionKey(_fixedAttribute.Value)
            : "SIX_SENSE_REWARD.description");

    public override bool IsPopulated => _options.Count > 0;

    protected override string RewardIconPath =>
        _manual?.PackedIconPath ??
        ModelDb.Relic<ElectronicSoldierManual>().PackedIconPath;

    public bool IsDefaultReward => !_fixedAttribute.HasValue;

    public SixSenseReward(
        Player player,
        ElectronicSoldierManual manual,
        SixSenseAttribute? fixedAttribute = null,
        IReadOnlyList<SixSenseAttribute> restoredOptions = null)
        : base(player)
    {
        _manual = manual;
        _fixedAttribute = fixedAttribute;
        if (restoredOptions != null)
        {
            _options = restoredOptions.ToArray();
        }
    }

    public override void Populate()
    {
        if (_options.Count > 0)
        {
            return;
        }

        _options = _fixedAttribute.HasValue
            ? new[] { _fixedAttribute.Value }
            : _manual?.RollSixSenseRewardOptions() ??
              new List<SixSenseAttribute>();
    }

    protected override async Task<bool> OnSelect()
    {
        if (_manual == null)
        {
            return false;
        }

        Populate();
        if (_fixedAttribute.HasValue)
        {
            await _manual.TryIncreaseSense(_fixedAttribute.Value);
            return true;
        }

        return await _manual.ChooseAndIncreaseSense(_options);
    }

    public override void MarkContentAsSeen()
    {
    }

    public override string ToModRewardJson()
    {
        Populate();
        return PackState(_fixedAttribute, _options)
            .ToString(CultureInfo.InvariantCulture);
    }

    public static SixSenseReward CreateFromSerializable(
        SerializableReward save,
        Player player,
        string json)
    {
        int payload = save.GoldAmount;
        if (!string.IsNullOrWhiteSpace(json) &&
            int.TryParse(
                json,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int restoredPayload))
        {
            payload = restoredPayload;
        }

        UnpackState(
            payload,
            out SixSenseAttribute? fixedAttribute,
            out IReadOnlyList<SixSenseAttribute> options);

        return new SixSenseReward(
            player,
            player.GetRelic<ElectronicSoldierManual>(),
            fixedAttribute,
            options);
    }

    private static int PackState(
        SixSenseAttribute? fixedAttribute,
        IReadOnlyList<SixSenseAttribute> options)
    {
        int count = System.Math.Min(options.Count, 3);
        int payload = count;
        for (int i = 0; i < count; i++)
        {
            payload |= (((int)options[i] + 1) & 0x7)
                << (FirstOptionShift + i * OptionBits);
        }

        if (fixedAttribute.HasValue)
        {
            payload |= FixedAttributeFlag;
            payload |= (((int)fixedAttribute.Value + 1) & 0x7)
                << FixedAttributeShift;
        }

        return payload;
    }

    private static void UnpackState(
        int payload,
        out SixSenseAttribute? fixedAttribute,
        out IReadOnlyList<SixSenseAttribute> options)
    {
        fixedAttribute = null;
        if ((payload & FixedAttributeFlag) != 0)
        {
            int fixedValue =
                ((payload >> FixedAttributeShift) & 0x7) - 1;
            if (System.Enum.IsDefined(
                    typeof(SixSenseAttribute),
                    fixedValue))
            {
                fixedAttribute = (SixSenseAttribute)fixedValue;
            }
        }

        int count = payload & OptionCountMask;
        List<SixSenseAttribute> restoredOptions = new(count);
        for (int i = 0; i < count; i++)
        {
            int optionValue =
                ((payload >> (FirstOptionShift + i * OptionBits)) & 0x7) - 1;
            if (System.Enum.IsDefined(
                    typeof(SixSenseAttribute),
                    optionValue))
            {
                restoredOptions.Add((SixSenseAttribute)optionValue);
            }
        }

        if (restoredOptions.Count == 0 && fixedAttribute.HasValue)
        {
            restoredOptions.Add(fixedAttribute.Value);
        }

        options = restoredOptions;
    }
}
