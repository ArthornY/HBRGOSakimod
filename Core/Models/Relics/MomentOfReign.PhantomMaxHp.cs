using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

internal enum PhantomMaxHpChange
{
    Gain,
    Lose,
    Set,
    SetAndCurrent
}

public partial class MomentOfReign
{
    internal const decimal MaximumPhantomHp = BigCombatNumbers.MaximumCreatureStat;
    private static readonly AsyncLocal<Creature> UpdatingPhantomMaxHp = new();
    private int _phantomMaxHpAdjustment;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PhantomMaxHpAdjustment
    {
        get => _phantomMaxHpAdjustment;
        protected set { AssertMutable(); _phantomMaxHpAdjustment = Math.Clamp(value, -BigCombatNumbers.NativeStatCap, BigCombatNumbers.NativeStatCap); }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ActivePhantomMaxHpAdjustment { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PhantomMaxHpAdjustmentData { get; protected set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string ActivePhantomMaxHpAdjustmentData { get; protected set; } = string.Empty;

    internal decimal ExactPhantomMaxHpAdjustment
    {
        get => BigCombatNumbers.ReadSigned(PhantomMaxHpAdjustmentData, PhantomMaxHpAdjustment);
        private set
        {
            AssertMutable();
            decimal clamped = decimal.Truncate(Math.Clamp(value, -MaximumPhantomHp, MaximumPhantomHp));
            PhantomMaxHpAdjustmentData = BigCombatNumbers.Save(clamped);
            PhantomMaxHpAdjustment = BigCombatNumbers.ToNativeSigned(clamped);
        }
    }

    internal decimal ExactActivePhantomMaxHpAdjustment
    {
        get => BigCombatNumbers.ReadSigned(ActivePhantomMaxHpAdjustmentData, ActivePhantomMaxHpAdjustment);
        private set
        {
            AssertMutable();
            decimal clamped = decimal.Truncate(Math.Clamp(value, -MaximumPhantomHp, MaximumPhantomHp));
            ActivePhantomMaxHpAdjustmentData = BigCombatNumbers.Save(clamped);
            ActivePhantomMaxHpAdjustment = BigCombatNumbers.ToNativeSigned(clamped);
        }
    }
    // Last observed base HP is used for stored health and event max-HP payments.
    // Summoning still rolls the monster's normal HP and does not persist temporary effects.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PhantomBaseMaxHpData { get; protected set; } = string.Empty;

    internal decimal GetAdjustedPhantomMaxHp(decimal unadjustedMaxHp) =>
        AdjustPhantomMaxHp(unadjustedMaxHp, ExactPhantomMaxHpAdjustment);

    internal static decimal AdjustPhantomMaxHp(decimal unadjustedMaxHp, decimal adjustment) =>
        Math.Clamp(unadjustedMaxHp + adjustment, 1, MaximumPhantomHp);

    internal decimal GetEventMaxHpReference()
    {
        Dictionary<int, decimal> bases = ReadPhantomBaseMaxHp();
        Creature active = PhantomHelper.GetLivingPhantom(Owner);
        decimal highestMaxHp = 0;
        foreach (PhantomRosterEntry entry in GetRosterEntries())
        {
            // Prefer the actual rolled maximum for the active entry, excluding
            // temporary combat bonuses. Current HP and death do not rank entries.
            decimal unadjustedMaxHp = active != null && entry.InstanceId == ActiveRosterEntryId
                ? GetUnadjustedActiveMaxHp(active)
                : GetStoredUnadjustedMaxHp(entry.InstanceId, entry, bases);
            highestMaxHp = Math.Max(highestMaxHp, GetAdjustedPhantomMaxHp(unadjustedMaxHp));
        }

        if (active != null)
            highestMaxHp = Math.Max(highestMaxHp,
                GetAdjustedPhantomMaxHp(GetUnadjustedActiveMaxHp(active)));
        if (highestMaxHp == 0)
        {
            // Default summons have no roster entry, so their cached base already
            // includes initial level bonuses. Add those only before the first summon.
            PhantomRosterEntry? defaultEntry = bases.ContainsKey(0) ? null :
                new PhantomRosterEntry(0, ModelDb.Monster<SmallHopper>().Id, 0)
                    .AddLevels(NewlyObtainedPhantomLevelBonus);
            highestMaxHp = GetAdjustedPhantomMaxHp(GetStoredUnadjustedMaxHp(0, defaultEntry, bases));
        }

        // The saved net adjustment already includes all prior gains and losses.
        // Never subtract an event's previous payments from this result again.
        return highestMaxHp;
    }

    internal static decimal GetRequestedMaxHpChange(Creature creature, decimal amount,
        PhantomMaxHpChange change)
    {
        amount = Math.Clamp(amount, -MaximumPhantomHp, MaximumPhantomHp);
        decimal target = change switch
        {
            PhantomMaxHpChange.Gain => LargeCreatureHealthHelper.GetMaxHp(creature) + amount,
            PhantomMaxHpChange.Lose => LargeCreatureHealthHelper.GetMaxHp(creature) - amount,
            _ => Math.Max(0m, amount)
        };
        // Use the same integer truncation and HP ceiling as Creature.SetMaxHpInternal.
        // Loss remains signed even when it exceeds the summoner's own maximum HP.
        decimal delta = decimal.Floor(target) - LargeCreatureHealthHelper.GetMaxHp(creature);
        return Math.Clamp(delta, -MaximumPhantomHp, MaximumPhantomHp);
    }

    private Dictionary<int, decimal> ReadPhantomBaseMaxHp()
    {
        if (string.IsNullOrWhiteSpace(PhantomBaseMaxHpData)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, decimal>>(PhantomBaseMaxHpData) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private void SetPhantomBaseMaxHp(int instanceId, decimal maxHp)
    {
        Dictionary<int, decimal> values = ReadPhantomBaseMaxHp();
        maxHp = Math.Clamp(maxHp, 1, MaximumPhantomHp);
        if (values.TryGetValue(instanceId, out decimal previous) && previous == maxHp) return;
        values[instanceId] = maxHp;
        PhantomBaseMaxHpData = JsonSerializer.Serialize(values);
    }

    private void PrunePhantomBaseMaxHp(IReadOnlySet<int> retainedIds)
    {
        Dictionary<int, decimal> values = ReadPhantomBaseMaxHp();
        foreach (int id in values.Keys.Where(id => id != 0 && !retainedIds.Contains(id)).ToArray())
            values.Remove(id);
        PhantomBaseMaxHpData = values.Count == 0 ? string.Empty : JsonSerializer.Serialize(values);
    }

    private static decimal TemporaryPhantomHp(Creature phantom) =>
        phantom.GetPower<PhantomTemporaryLevelPower>()?.ExactAppliedBonusMaxHp ?? 0;

    private decimal GetUnadjustedActiveMaxHp(Creature phantom) =>
        Math.Clamp(LargeCreatureHealthHelper.GetMaxHp(phantom) - TemporaryPhantomHp(phantom) -
            ExactActivePhantomMaxHpAdjustment, 1, MaximumPhantomHp);

    private void StorePhantomBaseMaxHp(Creature phantom)
    {
        if (ReferenceEquals(UpdatingPhantomMaxHp.Value, phantom)) return;
        decimal bonus = BigCombatNumbers.ClampStat(GetActiveRosterEntry()?.BonusMaxHp ?? 0m);
        SetPhantomBaseMaxHp(ActiveRosterEntryId, GetUnadjustedActiveMaxHp(phantom) - bonus);
    }

    private decimal GetStoredUnadjustedMaxHp(int instanceId, Dictionary<int, decimal> bases)
    {
        PhantomRosterEntry? entry = GetRosterEntries()
            .Where(candidate => candidate.InstanceId == instanceId)
            .Select(candidate => (PhantomRosterEntry?)candidate).FirstOrDefault();
        return GetStoredUnadjustedMaxHp(instanceId, entry, bases);
    }

    private static decimal GetStoredUnadjustedMaxHp(int instanceId, PhantomRosterEntry? entry,
        Dictionary<int, decimal> bases)
    {
        if (!bases.TryGetValue(instanceId, out decimal baseHp))
        {
            // Older saves did not record the rolled maximum. Use its normal upper bound
            // until this phantom is next summoned, when the exact value is recorded.
            MonsterModel monster = entry is { } existing
                ? ModelDb.GetByIdOrNull<MonsterModel>(existing.MonsterId)
                : ModelDb.Monster<SmallHopper>();
            baseHp = entry?.GetBasePhantomHp(monster?.MaxInitialHp ?? 2,
                monster is RedCrimsonBossMonster) ?? Math.Max(1, (monster?.MaxInitialHp ?? 2) / 2);
        }
        return Math.Clamp((decimal)baseHp +
            decimal.Floor(Math.Max(0m, entry?.BonusMaxHp ?? 0m)), 1m, MaximumPhantomHp);
    }

    private void RestoreStoredHpFromRosterGrowth(IReadOnlyList<PhantomRosterEntry> previousEntries,
        IReadOnlyList<PhantomRosterEntry> nextEntries, Dictionary<int, decimal> health)
    {
        Dictionary<int, PhantomRosterEntry> previous = previousEntries
            .GroupBy(entry => entry.InstanceId).ToDictionary(group => group.Key, group => group.First());
        Dictionary<int, decimal> bases = ReadPhantomBaseMaxHp();
        foreach (PhantomRosterEntry entry in nextEntries)
        {
            // Unseen entries already summon at full HP. Growth must not revive
            // a defeated entry or carry healing across a replaced monster.
            if (!health.TryGetValue(entry.InstanceId, out decimal currentHp) || currentHp <= 0 ||
                !previous.TryGetValue(entry.InstanceId, out PhantomRosterEntry oldEntry) ||
                oldEntry.MonsterId != entry.MonsterId || entry.BonusMaxHp <= oldEntry.BonusMaxHp)
                continue;

            decimal oldMaxHp = GetAdjustedPhantomMaxHp(GetStoredUnadjustedMaxHp(entry.InstanceId, oldEntry, bases));
            decimal newMaxHp = GetAdjustedPhantomMaxHp(GetStoredUnadjustedMaxHp(entry.InstanceId, entry, bases));
            decimal gain = Math.Max(0, newMaxHp - oldMaxHp);
            if (gain > 0)
                health[entry.InstanceId] = Math.Min(newMaxHp, currentHp + gain);
        }
    }

    // Called only for this owner's main phantom. It keeps later level/fusion changes
    // consistent with a negative global modifier that has already reached the HP floor.
    internal void NormalizeActivePhantomMaxHp(Creature phantom, ref decimal amount)
    {
        if (ReferenceEquals(UpdatingPhantomMaxHp.Value, phantom) || amount <= 0m ||
            !PhantomHelper.IsOwnedMainPhantom(phantom, Owner)) return;
        if (ExactPhantomMaxHpAdjustment == 0 && ExactActivePhantomMaxHpAdjustment == 0) return;
        decimal temporaryHp = TemporaryPhantomHp(phantom);
        decimal requestedDelta = decimal.Floor(amount) - LargeCreatureHealthHelper.GetMaxHp(phantom);
        decimal unadjustedMaxHp = Math.Clamp(
            (decimal)LargeCreatureHealthHelper.GetMaxHp(phantom) - temporaryHp - ExactActivePhantomMaxHpAdjustment + requestedDelta,
            1m, MaximumPhantomHp);
        decimal permanentMaxHp = GetAdjustedPhantomMaxHp(unadjustedMaxHp);
        ExactActivePhantomMaxHpAdjustment = permanentMaxHp - unadjustedMaxHp;
        amount = Math.Min(MaximumPhantomHp, permanentMaxHp + temporaryHp);
    }

    internal async Task RedirectMaxHpChange(decimal delta, PhantomMaxHpChange change,
        PlayerChoiceContext choiceContext = null, bool isFromCard = false)
    {
        AssertMutable();
        if (delta == 0 && change is PhantomMaxHpChange.Gain or PhantomMaxHpChange.Lose) return;
        decimal previousAdjustment = ExactPhantomMaxHpAdjustment;
        decimal nextAdjustment = Math.Clamp(previousAdjustment + delta,
            -MaximumPhantomHp, MaximumPhantomHp);
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        decimal unadjustedMaxHp = phantom == null ? 0 : GetUnadjustedActiveMaxHp(phantom);
        decimal temporaryHp = phantom == null ? 0 : TemporaryPhantomHp(phantom);
        ExactPhantomMaxHpAdjustment = nextAdjustment;

        Dictionary<int, decimal> health = ReadPhantomHealth();
        Dictionary<int, decimal> bases = ReadPhantomBaseMaxHp();
        foreach (int id in health.Keys.ToArray())
        {
            if (phantom != null && id == ActiveRosterEntryId) continue;
            if (health[id] <= 0) continue; // Preserve pending revival when changing max HP.
            decimal originalMax = GetStoredUnadjustedMaxHp(id, bases);
            decimal oldMax = AdjustPhantomMaxHp(originalMax, previousAdjustment);
            decimal newMax = AdjustPhantomMaxHp(originalMax, nextAdjustment);
            health[id] = change switch
            {
                PhantomMaxHpChange.Gain or PhantomMaxHpChange.Set when newMax > oldMax =>
                    Math.Clamp(health[id] + newMax - oldMax, 0, newMax),
                PhantomMaxHpChange.SetAndCurrent => newMax,
                PhantomMaxHpChange.Lose => Math.Clamp(health[id], 0,
                    Math.Max(0L, Math.Min(MaximumPhantomHp, originalMax + nextAdjustment))),
                _ => Math.Min(health[id], newMax)
            };
        }
        PhantomHealthData = health.Count == 0 ? string.Empty : JsonSerializer.Serialize(health);
        if (phantom == null) return;

        decimal permanentMaxHp = GetAdjustedPhantomMaxHp(unadjustedMaxHp);
        decimal targetMaxHp = Math.Min(MaximumPhantomHp, permanentMaxHp + temporaryHp);
        Creature previousUpdating = UpdatingPhantomMaxHp.Value;
        UpdatingPhantomMaxHp.Value = phantom;
        try
        {
            switch (change)
            {
                case PhantomMaxHpChange.Gain or PhantomMaxHpChange.Set when targetMaxHp > LargeCreatureHealthHelper.GetMaxHp(phantom):
                    await CreatureCmd.GainMaxHp(phantom, targetMaxHp - LargeCreatureHealthHelper.GetMaxHp(phantom));
                    break;
                case PhantomMaxHpChange.Lose:
                    // Preserve the normal damage/death hooks for a max HP loss.
                    decimal rawTarget = Math.Min(MaximumPhantomHp,
                        LargeCreatureHealthHelper.GetMaxHp(phantom) + nextAdjustment - previousAdjustment);
                    decimal loss = Math.Max(0m, LargeCreatureHealthHelper.GetMaxHp(phantom) - rawTarget);
                    await CreatureCmd.LoseMaxHp(choiceContext ?? new ThrowingPlayerChoiceContext(),
                        phantom, loss, isFromCard);
                    if (LargeCreatureHealthHelper.GetMaxHp(phantom) != targetMaxHp)
                        await CreatureCmd.SetMaxHp(phantom, targetMaxHp);
                    break;
                case PhantomMaxHpChange.SetAndCurrent:
                    await CreatureCmd.SetMaxAndCurrentHp(phantom, targetMaxHp);
                    break;
                default:
                    if (LargeCreatureHealthHelper.GetMaxHp(phantom) != targetMaxHp)
                        await CreatureCmd.SetMaxHp(phantom, targetMaxHp);
                    break;
            }
        }
        finally
        {
            ExactActivePhantomMaxHpAdjustment = permanentMaxHp - unadjustedMaxHp;
            UpdatingPhantomMaxHp.Value = previousUpdating;
            StorePhantomHp(phantom);
        }
    }
}
