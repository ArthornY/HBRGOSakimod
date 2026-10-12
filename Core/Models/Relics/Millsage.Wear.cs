using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class Millsage
{
    public const int InitialCannonDamagePercent = 50;
    public const int CannonDamagePercentPerUpgrade = 10;
    private int _cannonDamagePercent = InitialCannonDamagePercent;
    // Missing fields in older saves retain their common multiplier and gain
    // the bonuses for already purchased levels. Reading never mutates a save.
    private int _happinessDamagePercent = -1;
    private int _coffinDamagePercent = -1;
    private int _contestDamagePercent = -1;
    private int _longingDamagePercent = -1;
    private int _absolutionDamagePercent = -1;
    private int _happinessMaximumDamagePercentBonus;
    private int _coffinMaximumDamagePercentBonus;
    private int _contestMaximumDamagePercentBonus;
    private int _longingMaximumDamagePercentBonus;
    private int _absolutionMaximumDamagePercentBonus;

    [SavedProperty]
    public int CannonDamagePercent
    {
        get => _cannonDamagePercent;
        private set
        {
            AssertMutable();
            _cannonDamagePercent = Math.Clamp(value, 0, InitialCannonDamagePercent);
            DynamicVars[nameof(CannonDamagePercent)].BaseValue = _cannonDamagePercent;
            InvokeDisplayAmountChanged();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LastCannonWearRoom { get; private set; } = string.Empty;

    // Preserve the old common field for loading previous runs. New saves
    // always include all five independent values, including an actual zero.
    [SavedProperty]
    public int HappinessDamagePercent
    {
        get => GetCannonDamagePercent(HotaruCannonKind.Happiness);
        private set => SetCannonDamagePercent(HotaruCannonKind.Happiness, value);
    }

    [SavedProperty]
    public int CoffinDamagePercent
    {
        get => GetCannonDamagePercent(HotaruCannonKind.Coffin);
        private set => SetCannonDamagePercent(HotaruCannonKind.Coffin, value);
    }

    [SavedProperty]
    public int ContestDamagePercent
    {
        get => GetCannonDamagePercent(HotaruCannonKind.Contest);
        private set => SetCannonDamagePercent(HotaruCannonKind.Contest, value);
    }

    [SavedProperty]
    public int LongingDamagePercent
    {
        get => GetCannonDamagePercent(HotaruCannonKind.Longing);
        private set => SetCannonDamagePercent(HotaruCannonKind.Longing, value);
    }

    [SavedProperty]
    public int AbsolutionDamagePercent
    {
        get => GetCannonDamagePercent(HotaruCannonKind.Absolution);
        private set => SetCannonDamagePercent(HotaruCannonKind.Absolution, value);
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HappinessMaximumDamagePercentBonus
    {
        get => _happinessMaximumDamagePercentBonus;
        private set => SetMaximumCannonDamagePercentBonus(HotaruCannonKind.Happiness, value);
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CoffinMaximumDamagePercentBonus
    {
        get => _coffinMaximumDamagePercentBonus;
        private set => SetMaximumCannonDamagePercentBonus(HotaruCannonKind.Coffin, value);
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ContestMaximumDamagePercentBonus
    {
        get => _contestMaximumDamagePercentBonus;
        private set => SetMaximumCannonDamagePercentBonus(HotaruCannonKind.Contest, value);
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LongingMaximumDamagePercentBonus
    {
        get => _longingMaximumDamagePercentBonus;
        private set => SetMaximumCannonDamagePercentBonus(HotaruCannonKind.Longing, value);
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int AbsolutionMaximumDamagePercentBonus
    {
        get => _absolutionMaximumDamagePercentBonus;
        private set => SetMaximumCannonDamagePercentBonus(HotaruCannonKind.Absolution, value);
    }

    public int GetMaximumCannonDamagePercent(HotaruCannonKind kind) =>
        (int)Math.Min(int.MaxValue, (long)InitialCannonDamagePercent +
            CannonDamagePercentPerUpgrade * GetCannonUpgradeLevel(kind) + GetMaximumCannonDamagePercentBonus(kind));

    private int GetMaximumCannonDamagePercentBonus(HotaruCannonKind kind) => kind switch
    {
        HotaruCannonKind.Happiness => _happinessMaximumDamagePercentBonus,
        HotaruCannonKind.Coffin => _coffinMaximumDamagePercentBonus,
        HotaruCannonKind.Contest => _contestMaximumDamagePercentBonus,
        HotaruCannonKind.Longing => _longingMaximumDamagePercentBonus,
        HotaruCannonKind.Absolution => _absolutionMaximumDamagePercentBonus,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private void SetMaximumCannonDamagePercentBonus(HotaruCannonKind kind, int value)
    {
        AssertMutable();
        value = Math.Max(0, value);
        switch (kind)
        {
            case HotaruCannonKind.Happiness: _happinessMaximumDamagePercentBonus = value; break;
            case HotaruCannonKind.Coffin: _coffinMaximumDamagePercentBonus = value; break;
            case HotaruCannonKind.Contest: _contestMaximumDamagePercentBonus = value; break;
            case HotaruCannonKind.Longing: _longingMaximumDamagePercentBonus = value; break;
            case HotaruCannonKind.Absolution: _absolutionMaximumDamagePercentBonus = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        InvokeDisplayAmountChanged();
    }

    internal void GainCannonSoulPower(HotaruCannonKind kind)
    {
        InitializeCannonDamagePercents();
        int current = GetCannonDamagePercent(kind);
        SetMaximumCannonDamagePercentBonus(kind,
            AddCannonPercent(GetMaximumCannonDamagePercentBonus(kind), 1));
        SetCannonDamagePercent(kind, AddCannonPercent(current, 1));
    }

    private static int AddCannonPercent(int value, int amount) =>
        (int)Math.Clamp((long)value + amount, 0L, int.MaxValue);

    public int GetCannonDamagePercent(HotaruCannonKind kind)
    {
        int stored = kind switch
        {
            HotaruCannonKind.Happiness => _happinessDamagePercent,
            HotaruCannonKind.Coffin => _coffinDamagePercent,
            HotaruCannonKind.Contest => _contestDamagePercent,
            HotaruCannonKind.Longing => _longingDamagePercent,
            HotaruCannonKind.Absolution => _absolutionDamagePercent,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return Math.Clamp(stored < 0
            ? CannonDamagePercent + CannonDamagePercentPerUpgrade * GetCannonUpgradeLevel(kind)
            : stored, 0, GetMaximumCannonDamagePercent(kind));
    }

    internal decimal GetCannonDamageMultiplier(HotaruCannonKind kind) => GetCannonDamagePercent(kind) / 100m;

    private void SetCannonDamagePercent(HotaruCannonKind kind, int value)
    {
        AssertMutable();
        // Save fields may load before their upgrade levels. Retain the value
        // until every field has loaded; the getter enforces the actual cap.
        StoreCannonDamagePercent(kind, Math.Max(0, value));
        InvokeDisplayAmountChanged();
    }

    private void StoreCannonDamagePercent(HotaruCannonKind kind, int value)
    {
        switch (kind)
        {
            case HotaruCannonKind.Happiness: _happinessDamagePercent = value; break;
            case HotaruCannonKind.Coffin: _coffinDamagePercent = value; break;
            case HotaruCannonKind.Contest: _contestDamagePercent = value; break;
            case HotaruCannonKind.Longing: _longingDamagePercent = value; break;
            case HotaruCannonKind.Absolution: _absolutionDamagePercent = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private void InitializeCannonDamagePercents()
    {
        AssertMutable();
        foreach (HotaruCannonKind kind in Enum.GetValues<HotaruCannonKind>())
            StoreCannonDamagePercent(kind, GetCannonDamagePercent(kind));
    }

    private void ShiftCannonDamagePercents(int amount)
    {
        InitializeCannonDamagePercents();
        foreach (HotaruCannonKind kind in Enum.GetValues<HotaruCannonKind>())
            StoreCannonDamagePercent(kind, Math.Clamp(AddCannonPercent(GetCannonDamagePercent(kind), amount),
                0, GetMaximumCannonDamagePercent(kind)));
        CannonDamagePercent += amount;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new DynamicVar(nameof(CannonDamagePercent), InitialCannonDamagePercent)
    };

    internal void ApplyCombatWear(CombatRoom room)
    {
        if (!IsMutable || room == null || !room.CombatState.Players.Contains(Owner)) return;
        int amount = HotaruCombatWearHelper.GetAmount(room);
        if (amount <= 0) return;
        string key = MemoryGardenUpgradePointsHelper.GetRoomKey(Owner.RunState, room);
        if (string.IsNullOrEmpty(key) || LastCannonWearRoom == key) return;
        LastCannonWearRoom = key;
        ShiftCannonDamagePercents(-amount);
    }

    public override IReadOnlyList<LocString> ModifyExtraRestSiteHealText(
        Player player, IReadOnlyList<LocString> currentExtraText)
    {
        return ReferenceEquals(player, Owner)
            ? currentExtraText.Concat(new[] { AdditionalRestSiteHealText }).ToArray()
            : currentExtraText;
    }
    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (!isMimicked && ReferenceEquals(player, Owner))
        {
            ShiftCannonDamagePercents(10);
            Flash();
        }
        return Task.CompletedTask;
    }
}
