using System;
using System.Linq;
using System.Text.Json;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class MomentOfReign
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastSummonedRosterEntryId { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LastSummonedTemporaryData { get; protected set; } = string.Empty;
    private sealed record TemporarySummon(string Monster, int Upgrade, int Experience, int Value,
        decimal Damage, decimal Reduction, decimal Hp, string Fusion, decimal Roll);
    private void RememberSummonedForClassRelics(PhantomRosterEntry entry)
    {
        LastSummonedRosterEntryId = entry.InstanceId;
        LastSummonedTemporaryData = entry.InstanceId > 0 ? string.Empty : JsonSerializer.Serialize(new TemporarySummon(
            entry.MonsterId.ToString(), entry.UpgradeLevel, entry.Experience, entry.ExperienceValue,
            entry.InheritedLevelDamageBonusPercent, entry.InheritedLevelDamageReductionPercent,
            entry.InheritedLevelBonusMaxHp, entry.FusionEffectData, entry.InheritedLevelSummonerRollBonus));
    }
    internal bool GrantLastSummonedOrdinaryExperience()
    {
        if (!IsMutable || !HbrBattleStatsHelper.IsEnabled(Owner)) return false;
        var entries = GetRosterEntries().ToList();
        int index = entries.FindIndex(entry => entry.InstanceId == LastSummonedRosterEntryId && LastSummonedRosterEntryId > 0);
        if (index < 0)
        {
            // A consumed physical entry must not resurrect as another monster
            // of the same type. Only the actually summoned temporary fallback
            // can acquire its first persistent instance here.
            if (LastSummonedRosterEntryId > 0 || string.IsNullOrEmpty(LastSummonedTemporaryData)) return false;
            TemporarySummon temporary = JsonSerializer.Deserialize<TemporarySummon>(LastSummonedTemporaryData);
            if (temporary == null) return false;
            ModelId monsterId = ModelId.Deserialize(temporary.Monster);
            if (!IsAllowedRosterMonster(monsterId)) return false;
            entries.Add(new PhantomRosterEntry(AllocateRosterInstanceId(), monsterId, temporary.Upgrade,
                temporary.Experience, temporary.Value, temporary.Damage, temporary.Reduction,
                temporary.Hp, temporary.Fusion, temporary.Roll));
            index = entries.Count - 1;
            LastSummonedRosterEntryId = entries[index].InstanceId;
            LastSummonedTemporaryData = string.Empty;
            // Adopting the fallback must preserve injury/death. Otherwise the
            // new ID would look unseen and the next summon would heal it.
            if (GetStoredPhantomHp(0) is { } carriedHp) SetStoredPhantomHp(LastSummonedRosterEntryId, carriedHp);
            if (ReadPhantomBaseMaxHp().TryGetValue(0, out decimal baseHp)) SetPhantomBaseMaxHp(LastSummonedRosterEntryId, baseHp);
        }
        entries[index] = entries[index].AddExperience(ThirtyAClassRelicsHelper.TicketExperience(Owner, GetExperienceValue(RoomType.Monster)));
        SetRosterEntries(entries);
        return true;
    }
}
