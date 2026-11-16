using System;
using System.Linq;
using System.Text.Json;
using HBRGOSakiMod.Core.Equipment;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class ElectronicSoldierManual
{
    private const int HbrBattleSchema = 1;
    private string _hbrBattleProgressData = string.Empty;
    private HbrBattleProgress _hbrBattleProgress;

    // A single immutable string is authoritative. Cached records and receipt
    // arrays are never mutated, so native relic clones cannot share live state.
    private sealed record HbrBattleProgress
    {
        public HbrBattleProgress() { }
        public int Schema { get; init; } = HbrBattleSchema;
        public bool Enabled { get; init; }
        public bool HealthScaleApplied { get; init; }
        public int Level { get; init; } = 1;
        public string[] ClaimedVictories { get; init; } = Array.Empty<string>();
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HbrBattleProgressData
    {
        get => _hbrBattleProgressData;
        protected set
        {
            AssertMutable();
            _hbrBattleProgressData = value ?? string.Empty;
            _hbrBattleProgress = null;
        }
    }

    public bool IsHbrNumericsEnabled => ReadHbrBattleProgress() is
        { Schema: HbrBattleSchema, Enabled: true };

    public int HbrBattleLevel => IsHbrNumericsEnabled ? ReadHbrBattleProgress().Level : 0;

    internal HbrBattleStats GetHbrBattleStats()
    {
        if (!IsHbrNumericsEnabled) return default;
        decimal amount = HbrBattleStatsHelper.InitialStat +
            Math.Max(0m, (decimal)HbrBattleLevel - 1m) * HbrBattleStatsHelper.StatGainPerLevel;
        HbrBattleStats senses = HbrBattleStatsHelper.GetSixSenseBonuses(this);
        HbrEquipmentBonus gear = GetEquipmentBonus();
        return new(amount + senses.Strength + gear.Strength, amount + senses.Dexterity + gear.Dexterity,
            amount + senses.Intelligence + gear.Intelligence, amount + senses.Vitality + gear.Vitality,
            amount + senses.Spirit + gear.Spirit, amount + senses.Luck + gear.Luck);
    }

    internal void InitializeHbrBattleNumerics()
    {
        AssertMutable();
        // Only fresh-run entry points call this. Missing old-save data is not
        // interpreted as permission to change that run's health or attributes.
        if (!string.IsNullOrEmpty(HbrBattleProgressData)) return;
        bool nativeStartingHpIsScaled = Owner?.Character is HbrgoSaki or HbrgoHotaru;
        int maximum = Owner?.Creature?.MaxHp ?? 0;
        int current = Owner?.Creature?.CurrentHp ?? 0;
        // HBR CharacterModel.StartingHp already supplies its scaled maximum.
        // Existing multiplayer infrastructure also provides the manual to
        // native teammates; scale their fresh-run HP exactly once here.
        StoreHbrBattleProgress(new HbrBattleProgress
        {
            Enabled = true,
            HealthScaleApplied = true
        });
        if (!nativeStartingHpIsScaled && Owner?.Creature is { } creature && maximum > 0)
        {
            creature.SetMaxHpInternal(HbrBattleStatsHelper.GetInitialPlayerHp(maximum));
            creature.SetCurrentHpInternal(Math.Min(creature.MaxHp,
                Math.Max(0L, (long)current * HbrBattleStatsHelper.PlayerHealthScale)));
        }
    }

    internal void GrantHbrBattleVictory(CombatRoom room)
    {
        if (!IsMutable || !IsHbrNumericsEnabled || room == null ||
            !room.CombatState.Players.Contains(Owner)) return;
        string receipt = MemoryGardenUpgradePointsHelper.GetRoomKey(Owner.RunState, room);
        if (string.IsNullOrEmpty(receipt)) return;
        HbrBattleProgress progress = ReadHbrBattleProgress();
        if (progress.ClaimedVictories.Contains(receipt, StringComparer.Ordinal)) return;
        int levels = room.RoomType switch { RoomType.Boss => 3, RoomType.Elite => 2, _ => 1 };
        // Commit the receipt together with the level before UI observers run.
        StoreHbrBattleProgress(progress with
        {
            Level = (int)Math.Min(int.MaxValue, (long)progress.Level + levels),
            ClaimedVictories = progress.ClaimedVictories.Append(receipt)
                .OrderBy(key => key, StringComparer.Ordinal).ToArray()
        });
    }

    private HbrBattleProgress ReadHbrBattleProgress()
    {
        if (_hbrBattleProgress != null) return _hbrBattleProgress;
        HbrBattleProgress progress = null;
        if (!string.IsNullOrWhiteSpace(HbrBattleProgressData))
        {
            try { progress = JsonSerializer.Deserialize<HbrBattleProgress>(HbrBattleProgressData); }
            catch (JsonException) { }
        }
        progress ??= new HbrBattleProgress();
        _hbrBattleProgress = progress with
        {
            Level = Math.Max(1, progress.Level),
            ClaimedVictories = (progress.ClaimedVictories ?? Array.Empty<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal).ToArray()
        };
        return _hbrBattleProgress;
    }

    private void StoreHbrBattleProgress(HbrBattleProgress progress)
    {
        AssertMutable();
        HbrBattleProgressData = JsonSerializer.Serialize(progress);
        _hbrBattleProgress = progress;
    }
}
