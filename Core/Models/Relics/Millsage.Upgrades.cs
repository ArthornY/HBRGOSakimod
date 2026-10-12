using System;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class Millsage
{
    public const int MaximumCannonUpgradeLevel = 7;
    private int _happinessUpgradeLevel;
    private int _coffinUpgradeLevel;
    private int _contestUpgradeLevel;
    private int _longingUpgradeLevel;
    private int _absolutionUpgradeLevel;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HappinessUpgradeLevel
    {
        get => _happinessUpgradeLevel;
        private set { AssertMutable(); _happinessUpgradeLevel = Math.Clamp(value, 0, MaximumCannonUpgradeLevel); }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CoffinUpgradeLevel
    {
        get => _coffinUpgradeLevel;
        private set { AssertMutable(); _coffinUpgradeLevel = Math.Clamp(value, 0, MaximumCannonUpgradeLevel); }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ContestUpgradeLevel
    {
        get => _contestUpgradeLevel;
        private set { AssertMutable(); _contestUpgradeLevel = Math.Clamp(value, 0, MaximumCannonUpgradeLevel); }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LongingUpgradeLevel
    {
        get => _longingUpgradeLevel;
        private set { AssertMutable(); _longingUpgradeLevel = Math.Clamp(value, 0, MaximumCannonUpgradeLevel); }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int AbsolutionUpgradeLevel
    {
        get => _absolutionUpgradeLevel;
        private set { AssertMutable(); _absolutionUpgradeLevel = Math.Clamp(value, 0, MaximumCannonUpgradeLevel); }
    }

    public int GetCannonUpgradeLevel(HotaruCannonKind kind) => kind switch
    {
        HotaruCannonKind.Happiness => HappinessUpgradeLevel,
        HotaruCannonKind.Coffin => CoffinUpgradeLevel,
        HotaruCannonKind.Contest => ContestUpgradeLevel,
        HotaruCannonKind.Longing => LongingUpgradeLevel,
        HotaruCannonKind.Absolution => AbsolutionUpgradeLevel,
        _ => 0
    };

    public bool HasCannonUpgrade(HotaruCannonKind kind, int tier) =>
        tier is >= 1 and <= MaximumCannonUpgradeLevel && GetCannonUpgradeLevel(kind) >= tier;

    internal bool IsCannonUnlocked(HotaruCannonKind kind) =>
        Enum.IsDefined(kind);

    internal bool TryUpgradeCannon(HotaruCannonKind kind, int expectedLevel)
    {
        if (!IsMutable || !IsCannonUnlocked(kind) || expectedLevel < 0 ||
            expectedLevel >= MaximumCannonUpgradeLevel || GetCannonUpgradeLevel(kind) != expectedLevel) return false;
        // Materialize every older shared value before changing a level, so
        // only this purchase adds its bonus and the other cannons stay intact.
        InitializeCannonDamagePercents();
        int currentDamagePercent = GetCannonDamagePercent(kind);
        int level = expectedLevel + 1;
        switch (kind)
        {
            case HotaruCannonKind.Happiness: HappinessUpgradeLevel = level; break;
            case HotaruCannonKind.Coffin: CoffinUpgradeLevel = level; break;
            case HotaruCannonKind.Contest: ContestUpgradeLevel = level; break;
            case HotaruCannonKind.Longing: LongingUpgradeLevel = level; break;
            case HotaruCannonKind.Absolution: AbsolutionUpgradeLevel = level; break;
        }
        SetCannonDamagePercent(kind, AddCannonPercent(currentDamagePercent, CannonDamagePercentPerUpgrade));
        Flash();
        return true;
    }
}
