using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_UNFADING_SCARS_POWER")]
public sealed class HotaruUnfadingScarsPower : HotaruUncommonCardPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int EffectPercent { get; private set; }
    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("EffectPercent", EffectPercent);
            return text;
        }
    }

    internal static async Task Apply(PlayerChoiceContext context, Creature owner, int percent, CardModel source)
    {
        if (!HotaruCommonCannonHelper.CanAct(owner?.Player)) return;
        HotaruUnfadingScarsPower power = owner.GetPower<HotaruUnfadingScarsPower>();
        int before = power?.Amount ?? 0;
        if (power == null)
        {
            power = (HotaruUnfadingScarsPower)ModelDb.Power<HotaruUnfadingScarsPower>().ToMutable();
            await PowerCmd.Apply(context, power, owner, 1, owner, source);
        }
        else await PowerCmd.ModifyAmount(context, power, 1, owner, source);
        int actual = Math.Max(0, power.Amount - before);
        power.EffectPercent = (int)Math.Min(int.MaxValue, (long)power.EffectPercent + (long)Math.Max(0, percent) * actual);
        owner.InvokePowerModified(power, 0, true);
    }

    private static IEnumerable<HotaruUnfadingScarsPower> Sources(Creature affected) =>
        affected?.Side == CombatSide.Enemy && affected.CombatState != null
            ? affected.CombatState.Allies.Where(c => c.IsAlive && c.Player != null)
                .Select(c => c.GetPower<HotaruUnfadingScarsPower>()).Where(p => p?.Amount > 0)
            : Enumerable.Empty<HotaruUnfadingScarsPower>();

    internal static bool PreventsNaturalDecay(PowerModel power) =>
        (power is VulnerablePower or WeakPower or FragilePower) && Sources(power.Owner).Any();

    internal static decimal AdditionalEffect(Creature affected) =>
        Sources(affected).Sum(power => (decimal)power.EffectPercent) / 100m;
}

[CustomID("HOTARU_TRACE_OLD_SCAR_POWER")]
public sealed class HotaruTraceOldScarPower : HotaruTurnLedgerPower
{
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card?.IsMutable != true || card.Owner?.Creature != Owner || card.EnergyCost.CostsX) return false;
        int discount = SixSenseCardCapability.GetAttributes(card).Sum(attribute => ActiveAmountForTag((int)attribute));
        if (discount <= 0) return false;
        modifiedCost = Math.Max(0m, originalCost - discount);
        return modifiedCost != originalCost;
    }

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            string[] active = IsMutable ? ElectronicSoldierManual.AllSixSenseAttributes
                .Where(attribute => ActiveAmountForTag((int)attribute) > 0)
                .Select(attribute => $"{ElectronicSoldierManual.GetSenseTitle(attribute)}：-{ActiveAmountForTag((int)attribute)}")
                .ToArray() : Array.Empty<string>();
            text.Add("Discounts", string.Join("、", active));
            return text;
        }
    }

}

[CustomID("HOTARU_ONLY_THIS_BEAT_POWER")]
public sealed class HotaruOnlyThisBeatPower : HotaruTurnLedgerPower { }

[CustomID("HOTARU_CERTAIN_VOICE_POWER")]
public sealed class HotaruCertainVoicePower : HotaruUncommonCardPower
{
    private HashSet<object> _reservations = new(ReferenceEqualityComparer.Instance);
    internal bool Reserve(object token)
    {
        if (token == null || Amount <= _reservations.Count) return false;
        return _reservations.Add(token);
    }
    internal async Task Consume(PlayerChoiceContext context, object token)
    {
        if (!_reservations.Remove(token) || Amount <= 0) return;
        await PowerCmd.ModifyAmount(context, this, -1, Owner, null);
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservations = new(ReferenceEqualityComparer.Instance);
    }
}

public abstract class HotaruRollProgressPower : HotaruUncommonCardPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ObservedTurn { get; internal set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ObservedCritical { get; internal set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ObservedMisses { get; internal set; }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel source)
    {
        if (ReferenceEquals(power, this) && amount > 0m) HotaruRareRollHelper.SyncProgress(this);
        return Task.CompletedTask;
    }
}

public abstract class HotaruConfiguredRollPower : HotaruRollProgressPower
{
    public sealed class RewardEntry
    {
        public int Limit { get; set; }
        public int Value { get; set; }
        public int SecondaryValue { get; set; } = 1;
        public int Draw { get; set; } = 1;
        public int Copies { get; set; }
    }
    private List<RewardEntry> _entries = new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string RewardEntries
    {
        get => JsonSerializer.Serialize(_entries);
        private set
        {
            try { _entries = JsonSerializer.Deserialize<List<RewardEntry>>(value ?? "") ?? new(); }
            catch (JsonException) { _entries = new(); }
            _entries = _entries.Where(e => e != null && e.Limit > 0 && e.Value > 0 && e.Copies > 0).ToList();
        }
    }

    internal RewardEntry[] Snapshot() => Amount <= 0 ? Array.Empty<RewardEntry>() : _entries
        .Select(e => new RewardEntry { Limit = e.Limit, Value = e.Value, SecondaryValue = e.SecondaryValue,
            Draw = e.Draw, Copies = e.Copies }).ToArray();

    internal static int Reward(IEnumerable<RewardEntry> entries, int ordinal, bool useValue) =>
        (int)Math.Min(int.MaxValue, entries.Where(e => ordinal > 0 && ordinal <= e.Limit)
            .Sum(e => (long)e.Copies * (useValue ? e.Value : 1)));

    protected static async Task ApplyConfigured<T>(PlayerChoiceContext context, Creature owner,
        int limit, int value, int secondaryValue, int draw, CardModel source) where T : HotaruConfiguredRollPower, new()
    {
        if (!HotaruCommonCannonHelper.CanAct(owner?.Player) || limit <= 0 || value <= 0) return;
        T power = owner.GetPower<T>();
        int before = power?.Amount ?? 0;
        if (power == null)
        {
            power = (T)ModelDb.Power<T>().ToMutable();
            await PowerCmd.Apply(context, power, owner, 1, owner, source);
        }
        else await PowerCmd.ModifyAmount(context, power, 1, owner, source);
        int actual = Math.Max(0, power.Amount - before);
        if (actual <= 0 || power.Owner != owner) return;
        RewardEntry existing = power._entries.FirstOrDefault(e => e.Limit == limit && e.Value == value &&
            e.SecondaryValue == secondaryValue && e.Draw == draw);
        if (existing == null) power._entries.Add(new RewardEntry { Limit = limit, Value = value,
            SecondaryValue = secondaryValue, Draw = draw, Copies = actual });
        else existing.Copies = (int)Math.Min(int.MaxValue, (long)existing.Copies + actual);
        HotaruRareRollHelper.SyncProgress(power);
        owner.InvokePowerModified(power, 0, true);
    }

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("TriggerLimit", _entries.Select(e => e.Limit).DefaultIfEmpty(2).Max());
            text.Add("RewardValue", _entries.Sum(e => (long)e.Value * e.Copies));
            return text;
        }
    }

    protected string FormatRewardLines(string key, int fallbackLimit, int fallbackValue, int fallbackDraw,
        bool skillBlock = false)
    {
        RewardEntry[] entries = IsMutable ? Snapshot() : new[]
        {
            new RewardEntry { Limit = fallbackLimit, Value = fallbackValue, SecondaryValue = 1, Draw = fallbackDraw, Copies = 1 }
        };
        return string.Join("\n", entries.Select(entry =>
        {
            LocString line = new("powers", key);
            line.Add("Copies", entry.Copies);
            line.Add("Limit", entry.Limit);
            line.Add("PrimaryValue", entry.Value);
            line.Add("SecondaryValue", entry.SecondaryValue);
            line.Add("Draw", entry.Draw);
            if (skillBlock) HbrSkillBlockDescription.AddParameters(line, IsMutable ? Owner : null, entry.Value);
            return line.GetFormattedText();
        }));
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _entries = _entries.Select(e => new RewardEntry { Limit = e.Limit, Value = e.Value,
            SecondaryValue = e.SecondaryValue, Draw = e.Draw, Copies = e.Copies }).ToList();
    }
}

[CustomID("HOTARU_BREATH_AFTER_MISS_POWER")]
public sealed class HotaruBreathAfterMissPower : HotaruConfiguredRollPower
{
    internal static Task Apply(PlayerChoiceContext context, Creature owner, int limit, int block,
        int charge, int draw, CardModel source) =>
        ApplyConfigured<HotaruBreathAfterMissPower>(context, owner, limit, block, charge, draw, source);
    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            if (HbrSkillBlockDescription.IsEnabled(this))
            {
                LocString replacement = new("powers", "HOTARU_BREATH_AFTER_MISS_POWER.hbrDescription");
                replacement.AddVariablesFrom(text);
                replacement.Add("BlockRewardsText", FormatRewardLines(
                    "HOTARU_BREATH_AFTER_MISS_POWER.hbrRewardLine", 2, 4, 1, skillBlock: true));
                return replacement;
            }
            text.Add("RewardsText", FormatRewardLines("HOTARU_BREATH_AFTER_MISS_POWER.rewardLine", 2, 4, 1));
            return text;
        }
    }
}

[CustomID("HOTARU_INHERIT_ANSWER_POWER")]
public sealed class HotaruInheritAnswerPower : HotaruRollProgressPower { }

[CustomID("HOTARU_FAREWELL_INERTIA_POWER")]
public sealed class HotaruFarewellInertiaPower : HotaruConfiguredRollPower
{
    internal static Task Apply(PlayerChoiceContext context, Creature owner, int limit, int baseCharge,
        int extraActions, CardModel source) =>
        ApplyConfigured<HotaruFarewellInertiaPower>(context, owner, limit, baseCharge, extraActions, 0, source);
    internal int StartingCharge(int retiredCount) => (int)Math.Min(int.MaxValue,
        Snapshot().Sum(e => ((long)e.Value + Math.Max(0, retiredCount)) * e.Copies));
    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("RewardsText", FormatRewardLines("HOTARU_FAREWELL_INERTIA_POWER.rewardLine", 2, 1, 0));
            return text;
        }
    }
}
