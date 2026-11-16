using System;
using System.Collections.Generic;
using System.Linq;

namespace HBRGOSakiMod.Core.Equipment;

internal enum HbrDropRoom { Normal, Elite, Boss }

// Pure equipment rules shared by the manual, network actions, UI and checks.
// Methods return a new state or null with a short Chinese reason.
internal static class HbrEquipmentRules
{
    // Original units per "+" level: one stat point, 20 DP or 0.1% critical rate.
    private const decimal PlusStatUnit = 1m;
    private const decimal PlusDpUnit = 20m;
    private const decimal PlusCriticalUnit = 0.1m;

    internal static int TierForAct(int actIndex) => HbrEquipmentCatalog.ClampTier(actIndex);

    // An item's original value in battle units: abilities and DP by the item's
    // grade scale, critical rate unchanged.
    internal static decimal Scaled(HbrEquipmentTemplate template, HbrEquipmentStat stat, decimal original) =>
        stat == HbrEquipmentStat.CriticalRate ? original : original * HbrEquipmentCatalog.ScaleOf(template);

    internal static decimal PlusOriginal(HbrPlusSlot slot) => slot.Stat switch
    {
        HbrEquipmentStat.Dp => PlusDpUnit * slot.Level,
        HbrEquipmentStat.CriticalRate => PlusCriticalUnit * slot.Level,
        _ => PlusStatUnit * slot.Level
    };

    internal static int PlusTotal(HbrEquipmentItem item) => item?.Plus.Sum(slot => slot.Level) ?? 0;

    // Missing or out-of-range indexes fall back to the nearest row, so every
    // item holds exactly one row per random table.
    internal static int[] NormalizeRolls(HbrEquipmentTemplate template, int[] rolls)
    {
        if (template == null || template.RollGroups.Count == 0) return Array.Empty<int>();
        return template.RollGroups.Select((group, index) =>
            Math.Clamp(rolls != null && index < rolls.Length ? rolls[index] : 0, 0, group.Rows.Count - 1)).ToArray();
    }

    // The rolled row of each random table, in table order.
    internal static IEnumerable<(HbrEquipmentRollGroup Group, HbrEquipmentRoll Row)> RolledRows(HbrEquipmentItem item)
    {
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
        if (template == null) yield break;
        for (int i = 0; i < template.RollGroups.Count && i < (item.Rolls?.Length ?? 0); i++)
        {
            HbrEquipmentRollGroup group = template.RollGroups[i];
            yield return (group, group.Rows[Math.Clamp(item.Rolls[i], 0, group.Rows.Count - 1)]);
        }
    }

    internal static int RollPlus(HbrEquipmentItem item) => RolledRows(item).Sum(rolled => rolled.Row.Plus);

    // The "+N" after a name: the random tables' grades plus every opened "+" level.
    internal static int DisplayPlus(HbrEquipmentItem item) => RollPlus(item) + PlusTotal(item);

    internal static HbrEquipmentBonus ItemBonus(HbrEquipmentItem item)
    {
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
        if (template == null) return default;
        HbrEquipmentBonus bonus = default;
        foreach (HbrEquipmentValue value in template.Values)
            bonus = bonus.With(value.Stat, Scaled(template, value.Stat, value.Original));
        foreach ((HbrEquipmentRollGroup group, HbrEquipmentRoll row) in RolledRows(item))
            if (group.Stat is HbrEquipmentStat stat) bonus = bonus.With(stat, Scaled(template, stat, row.Original));
        // "+" slots take the scale of the accessory they belong to.
        foreach (HbrPlusSlot slot in item.Plus)
            bonus = bonus.With(slot.Stat, Scaled(template, slot.Stat, PlusOriginal(slot)));
        return bonus;
    }

    // Fixed effects first, then rolled ones, in catalog order.
    internal static IEnumerable<HbrEquipmentEffectValue> ItemEffects(HbrEquipmentItem item)
    {
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
        if (template == null) yield break;
        foreach (HbrEquipmentEffectValue effect in template.Effects) yield return effect;
        foreach ((HbrEquipmentRollGroup group, HbrEquipmentRoll row) in RolledRows(item))
            if (group.Effect is HbrEquipmentEffect effect) yield return new(effect, row.Original);
    }

    internal static HbrEquipmentBonus Totals(HbrEquipmentState state)
    {
        HbrEquipmentBonus total = default;
        if (state == null) return total;
        foreach (int id in state.Equipped)
            if (id > 0) total += ItemBonus(state.FindItem(id));
        return total;
    }

    internal static HbrEquipmentEffectTotals EffectTotals(HbrEquipmentState state)
    {
        HbrEquipmentEffectTotals total = default;
        if (state == null) return total;
        foreach (int id in state.Equipped)
            if (id > 0)
                foreach (HbrEquipmentEffectValue effect in ItemEffects(state.FindItem(id)))
                    total = total.With(effect.Effect, effect.Value);
        return total;
    }

    // The original earring curves, by the hits of one attack clamped to 1..10:
    // Break/Attack fall from the full value at one hit to 5% at ten hits, and
    // Drive/Blast rise from 5% at one hit to the full value at ten.
    internal const decimal HitScaledFloor = 5m;
    internal const int HitScaledHits = 10;

    internal static decimal HitScaledPercent(HbrEquipmentEffect effect, decimal value, decimal hits)
    {
        if (value <= 0m) return 0m;
        decimal clamped = Math.Clamp(decimal.Ceiling(hits), 1m, HitScaledHits);
        decimal steps = effect is HbrEquipmentEffect.DpDamage or HbrEquipmentEffect.HpDamage
            ? clamped - 1m
            : HitScaledHits - clamped;
        // Multiply before dividing so the ends are exact.
        return value - (value - HitScaledFloor) * steps / (HitScaledHits - 1);
    }

    internal static int ChipSlotCount(HbrEquipmentState state) =>
        HbrEquipmentCatalog.Find(state.FindItem(state.At(HbrEquipmentSlot.Booster))?.Template)?.ChipSlots ?? 0;

    // Original accessory level: rarity sum plus half of every "+" value. Charms
    // are left out so their own slots cannot unlock themselves.
    internal static int AccessoryLevel(HbrEquipmentState state)
    {
        int rarity = 0, plus = 0;
        foreach (HbrEquipmentSlot slot in HbrEquipmentSlot.Accessories)
        {
            HbrEquipmentItem item = state.FindItem(state.At(slot));
            HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
            if (template == null) continue;
            rarity += template.Rarity;
            plus += DisplayPlus(item);
        }
        return rarity + plus / 2;
    }

    internal static int OpenCharmSlots(HbrEquipmentState state)
    {
        int level = AccessoryLevel(state);
        return HbrEquipmentCatalog.CharmSlotLevels.Count(required => level >= required);
    }

    internal static bool IsSlotOpen(HbrEquipmentState state, HbrEquipmentSlot slot) => slot.IsValid && slot.Kind switch
    {
        HbrEquipmentKind.Chip => slot.Index < ChipSlotCount(state),
        HbrEquipmentKind.Charm => slot.Index < OpenCharmSlots(state),
        _ => true
    };

    // Chips need open booster slots and charms need the accessory level. Any
    // item left in a closed slot returns to the inventory.
    internal static HbrEquipmentState Trim(HbrEquipmentState state)
    {
        int[] equipped = (int[])state.Equipped.Clone();
        bool changed;
        do
        {
            changed = false;
            var probe = state with { Equipped = equipped };
            for (int position = 0; position < equipped.Length; position++)
            {
                if (equipped[position] == 0 || IsSlotOpen(probe, HbrEquipmentSlot.FromPosition(position))) continue;
                equipped[position] = 0;
                changed = true;
            }
        }
        while (changed);
        return equipped.SequenceEqual(state.Equipped) ? state : state with { Equipped = equipped };
    }

    internal static HbrEquipmentState Equip(HbrEquipmentState state, int itemId, HbrEquipmentSlot slot,
        out string error)
    {
        error = null;
        HbrEquipmentItem item = state.FindItem(itemId);
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
        if (template == null) { error = "没有这件装备"; return null; }
        if (!slot.IsValid || template.Kind != slot.Kind) { error = "不能装在这一栏"; return null; }
        if (!IsSlotOpen(state, slot))
        {
            error = slot.Kind == HbrEquipmentKind.Chip ? "增幅器的芯片栏不够" : "饰品 Lv. 不足，吊饰栏未解锁";
            return null;
        }
        if (state.At(slot) == itemId) return state;
        int[] equipped = (int[])state.Equipped.Clone();
        int previous = Array.IndexOf(equipped, itemId);
        if (previous >= 0) equipped[previous] = 0;
        equipped[slot.Position] = itemId;
        return Commit(Trim(state with { Equipped = equipped }));
    }

    internal static HbrEquipmentState Unequip(HbrEquipmentState state, HbrEquipmentSlot slot, out string error)
    {
        error = null;
        if (!slot.IsValid || state.At(slot) == 0) { error = "这一栏是空的"; return null; }
        int[] equipped = (int[])state.Equipped.Clone();
        equipped[slot.Position] = 0;
        return Commit(Trim(state with { Equipped = equipped }));
    }

    internal static HbrEquipmentState AddItem(HbrEquipmentState state, string templateId, int[] rolls,
        HbrPlusSlot[] plus, out int itemId)
    {
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(templateId);
        itemId = 0;
        if (template == null) return state;
        itemId = state.NextItemId;
        var item = new HbrEquipmentItem
        {
            Id = itemId,
            Template = template.Id,
            Rolls = NormalizeRolls(template, rolls),
            Plus = (plus ?? Array.Empty<HbrPlusSlot>()).Take(template.PlusSlots).ToArray()
        };
        return Commit(state with { Items = state.Items.Append(item).ToArray(), NextItemId = itemId + 1 });
    }

    internal static HbrEquipmentState AddMaterials(HbrEquipmentState state, int plusRelease, int plusChange) =>
        plusRelease <= 0 && plusChange <= 0 ? state : Commit(state with
        {
            PlusRelease = (int)Math.Min(int.MaxValue, (long)state.PlusRelease + Math.Max(0, plusRelease)),
            PlusChange = (int)Math.Min(int.MaxValue, (long)state.PlusChange + Math.Max(0, plusChange))
        });

    internal static HbrEquipmentState RemoveItem(HbrEquipmentState state, int itemId, out string error)
    {
        error = null;
        if (state.FindItem(itemId) == null) { error = "没有这件装备"; return null; }
        if (state.IsEquipped(itemId)) { error = "请先卸下这件装备"; return null; }
        return Commit(state with { Items = state.Items.Where(item => item.Id != itemId).ToArray() });
    }

    internal static bool CanOpenPlus(HbrEquipmentState state, HbrEquipmentItem item) =>
        item != null && state.PlusRelease > 0 &&
        item.Plus.Length < (HbrEquipmentCatalog.Find(item.Template)?.PlusSlots ?? 0);

    internal static HbrEquipmentState OpenPlus(HbrEquipmentState state, int itemId, out string error)
    {
        error = null;
        HbrEquipmentItem item = state.FindItem(itemId);
        if (item == null) { error = "没有这件装备"; return null; }
        if ((HbrEquipmentCatalog.Find(item.Template)?.PlusSlots ?? 0) <= item.Plus.Length)
        { error = "这件饰品没有可解锁的 + 栏"; return null; }
        if (state.PlusRelease <= 0) { error = $"{HbrEquipmentCatalog.PlusReleaseName}不足"; return null; }
        var random = new HbrEquipmentRandom(state);
        HbrPlusSlot slot = HbrEquipmentGenerator.RollPlusSlot(random);
        HbrEquipmentItem changed = item with { Plus = item.Plus.Append(slot).ToArray() };
        return Commit(Trim(Replace(state, changed) with
        {
            PlusRelease = state.PlusRelease - 1,
            RandomCounter = random.Counter
        }));
    }

    internal static HbrEquipmentState ChangePlus(HbrEquipmentState state, int itemId, int index, out string error)
    {
        error = null;
        HbrEquipmentItem item = state.FindItem(itemId);
        if (item == null) { error = "没有这件装备"; return null; }
        if (index < 0 || index >= item.Plus.Length) { error = "这个 + 栏还没有解锁"; return null; }
        if (state.PlusChange <= 0) { error = $"{HbrEquipmentCatalog.PlusChangeName}不足"; return null; }
        var random = new HbrEquipmentRandom(state);
        HbrPlusSlot[] plus = (HbrPlusSlot[])item.Plus.Clone();
        plus[index] = HbrEquipmentGenerator.RollPlusSlot(random);
        return Commit(Trim(Replace(state, item with { Plus = plus }) with
        {
            PlusChange = state.PlusChange - 1,
            RandomCounter = random.Counter
        }));
    }

    private static HbrEquipmentState Replace(HbrEquipmentState state, HbrEquipmentItem item) =>
        state with { Items = state.Items.Select(row => row.Id == item.Id ? item : row).ToArray() };

    internal static HbrEquipmentState Commit(HbrEquipmentState state) =>
        state with { Revision = state.Revision == int.MaxValue ? 1 : state.Revision + 1 };
}

// Deterministic SplitMix64 stream keyed by the player's run seed. The saved
// counter advances only inside commands every peer executes in order.
internal sealed class HbrEquipmentRandom
{
    private readonly ulong _seed;

    internal int Counter { get; private set; }

    internal HbrEquipmentRandom(ulong seed, int counter)
    {
        _seed = seed;
        Counter = Math.Max(0, counter);
    }

    internal HbrEquipmentRandom(HbrEquipmentState state) : this(state.Seed, state.RandomCounter) { }

    // Drops use a stream keyed by the room instead of the saved counter, so
    // generating rewards never changes later crafting results.
    internal static HbrEquipmentRandom ForKey(ulong seed, string key)
    {
        ulong hash = 1469598103934665603UL;
        foreach (char c in key ?? string.Empty)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return new HbrEquipmentRandom(seed ^ hash, 0);
    }

    internal int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 1) { Counter++; return 0; }
        ulong z = _seed + 0x9E3779B97F4A7C15UL * (ulong)(++Counter);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (int)(z % (ulong)maxExclusive);
    }

    internal int Weighted(params int[] weights)
    {
        int total = weights.Sum(weight => Math.Max(0, weight));
        int roll = NextInt(Math.Max(1, total));
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= Math.Max(0, weights[i]);
            if (roll < 0) return i;
        }
        return weights.Length - 1;
    }
}
