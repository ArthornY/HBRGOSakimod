using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Patches.Content;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Integration;
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
    private const int LegacyOptionCountMask = 0x3;
    private const int OptionBits = 3;
    private const int LegacyFirstOptionShift = 2;
    private const int LegacyFixedAttributeFlag = 1 << 11;
    private const int LegacyFixedAttributeShift = 12;
    private const int ExtendedFormatFlag = 1 << 20;
    private const int ExtendedOptionCountMask = 0x7;
    private const int ExtendedFirstOptionShift = 3;
    private const int ExtendedFixedAttributeFlag = 1 << 15;
    private const int ExtendedFixedAttributeShift = 16;
    private const int ExceedMaximumFlag = 1 << 19;

    [CustomEnum(null)]
    public static RewardType SixSense;

    private readonly ElectronicSoldierManual _manual;
    private readonly SixSenseAttribute? _fixedAttribute;
    private readonly bool _canExceedMaximum;
    private IReadOnlyList<SixSenseAttribute> _options = new List<SixSenseAttribute>();

    public override RewardType ModRewardType => SixSense;

    public override int RewardsSetIndex => 4;

    public override LocString Description => new("relics", "SIX_SENSE_POINT_REWARD.description");

    public override bool IsPopulated => _manual != null;

    protected override string RewardIconPath =>
        _manual?.PackedIconPath ??
        ModelDb.Relic<ElectronicSoldierManual>().PackedIconPath;

    public bool IsDefaultReward => !_fixedAttribute.HasValue;

    public SixSenseReward(
        Player player,
        ElectronicSoldierManual manual,
        SixSenseAttribute? fixedAttribute = null,
        IReadOnlyList<SixSenseAttribute> restoredOptions = null,
        bool canExceedMaximum = false)
        : base(player)
    {
        _manual = manual;
        _fixedAttribute = fixedAttribute;
        _canExceedMaximum = canExceedMaximum;
        if (restoredOptions != null)
        {
            _options = restoredOptions.ToArray();
        }
    }

    public override void Populate()
    {
        // Legacy saves may still contain these entries; keep their payload without rolling new choices.
    }

    protected override Task<bool> OnSelect() => Task.FromResult(SixSenseNetActions.ClaimLegacyPoint(_manual));

    public override void MarkContentAsSeen()
    {
    }

    public override string ToModRewardJson()
    {
        Populate();
        return PackState(
                _fixedAttribute,
                _options,
                _canExceedMaximum)
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
            out IReadOnlyList<SixSenseAttribute> options,
            out bool canExceedMaximum);

        return new SixSenseReward(
            player,
            player.GetRelic<ElectronicSoldierManual>(),
            fixedAttribute,
            options,
            canExceedMaximum);
    }

    private static int PackState(
        SixSenseAttribute? fixedAttribute,
        IReadOnlyList<SixSenseAttribute> options,
        bool canExceedMaximum)
    {
        int count = System.Math.Min(options.Count, 4);
        int payload = ExtendedFormatFlag | count;
        for (int i = 0; i < count; i++)
        {
            payload |= (((int)options[i] + 1) & 0x7)
                << (ExtendedFirstOptionShift + i * OptionBits);
        }

        if (fixedAttribute.HasValue)
        {
            payload |= ExtendedFixedAttributeFlag;
            payload |= (((int)fixedAttribute.Value + 1) & 0x7)
                << ExtendedFixedAttributeShift;
        }

        if (canExceedMaximum)
        {
            payload |= ExceedMaximumFlag;
        }

        return payload;
    }

    private static void UnpackState(
        int payload,
        out SixSenseAttribute? fixedAttribute,
        out IReadOnlyList<SixSenseAttribute> options,
        out bool canExceedMaximum)
    {
        bool isExtended = (payload & ExtendedFormatFlag) != 0;
        int fixedAttributeFlag = isExtended
            ? ExtendedFixedAttributeFlag
            : LegacyFixedAttributeFlag;
        int fixedAttributeShift = isExtended
            ? ExtendedFixedAttributeShift
            : LegacyFixedAttributeShift;
        int optionCountMask = isExtended
            ? ExtendedOptionCountMask
            : LegacyOptionCountMask;
        int firstOptionShift = isExtended
            ? ExtendedFirstOptionShift
            : LegacyFirstOptionShift;
        canExceedMaximum = isExtended &&
            (payload & ExceedMaximumFlag) != 0;

        fixedAttribute = null;
        if ((payload & fixedAttributeFlag) != 0)
        {
            int fixedValue =
                ((payload >> fixedAttributeShift) & 0x7) - 1;
            if (System.Enum.IsDefined(
                    typeof(SixSenseAttribute),
                    fixedValue))
            {
                fixedAttribute = (SixSenseAttribute)fixedValue;
            }
        }

        int count = payload & optionCountMask;
        List<SixSenseAttribute> restoredOptions = new(count);
        for (int i = 0; i < count; i++)
        {
            int optionValue =
                ((payload >> (firstOptionShift + i * OptionBits)) & 0x7) - 1;
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
