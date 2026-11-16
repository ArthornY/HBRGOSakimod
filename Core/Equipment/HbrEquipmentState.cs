using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HBRGOSakiMod.Core.Equipment;

// Scaled bonuses in the units the battle layer reads: six stats, max DP and
// critical rate percentage points.
internal readonly record struct HbrEquipmentBonus(
    decimal Strength, decimal Dexterity, decimal Intelligence, decimal Vitality,
    decimal Spirit, decimal Luck, decimal Dp, decimal CriticalRate)
{
    internal decimal this[HbrEquipmentStat stat] => stat switch
    {
        HbrEquipmentStat.Strength => Strength,
        HbrEquipmentStat.Dexterity => Dexterity,
        HbrEquipmentStat.Intelligence => Intelligence,
        HbrEquipmentStat.Vitality => Vitality,
        HbrEquipmentStat.Spirit => Spirit,
        HbrEquipmentStat.Luck => Luck,
        HbrEquipmentStat.Dp => Dp,
        _ => CriticalRate
    };

    internal HbrEquipmentBonus With(HbrEquipmentStat stat, decimal amount) => stat switch
    {
        HbrEquipmentStat.Strength => this with { Strength = Strength + amount },
        HbrEquipmentStat.Dexterity => this with { Dexterity = Dexterity + amount },
        HbrEquipmentStat.Intelligence => this with { Intelligence = Intelligence + amount },
        HbrEquipmentStat.Vitality => this with { Vitality = Vitality + amount },
        HbrEquipmentStat.Spirit => this with { Spirit = Spirit + amount },
        HbrEquipmentStat.Luck => this with { Luck = Luck + amount },
        HbrEquipmentStat.Dp => this with { Dp = Dp + amount },
        _ => this with { CriticalRate = CriticalRate + amount }
    };

    public static HbrEquipmentBonus operator +(HbrEquipmentBonus a, HbrEquipmentBonus b) => new(
        a.Strength + b.Strength, a.Dexterity + b.Dexterity, a.Intelligence + b.Intelligence,
        a.Vitality + b.Vitality, a.Spirit + b.Spirit, a.Luck + b.Luck, a.Dp + b.Dp,
        a.CriticalRate + b.CriticalRate);
}

// Summed effect values of the worn items, in the units of HbrEquipmentEffect.
internal readonly struct HbrEquipmentEffectTotals
{
    private static readonly int Count = Enum.GetValues<HbrEquipmentEffect>().Length;
    private readonly decimal[] _values;

    private HbrEquipmentEffectTotals(decimal[] values) => _values = values;

    internal decimal this[HbrEquipmentEffect effect] => _values?[(int)effect] ?? 0m;

    internal bool Has(HbrEquipmentEffect effect) => this[effect] > 0m;

    internal HbrEquipmentEffectTotals With(HbrEquipmentEffect effect, decimal amount)
    {
        decimal[] values = (decimal[])(_values?.Clone() ?? new decimal[Count]);
        values[(int)effect] += amount;
        return new HbrEquipmentEffectTotals(values);
    }
}

// One opened "+" slot. Level 1..3 multiplies the stat's base unit.
internal sealed record HbrPlusSlot(HbrEquipmentStat Stat, int Level);

internal sealed record HbrEquipmentItem
{
    public int Id { get; init; }
    public string Template { get; init; } = string.Empty;
    // One row index per random table of the template.
    public int[] Rolls { get; init; } = Array.Empty<int>();
    public HbrPlusSlot[] Plus { get; init; } = Array.Empty<HbrPlusSlot>();
}

internal readonly record struct HbrEquipmentSlot(HbrEquipmentKind Kind, int Index)
{
    internal const int Count = 15;
    private const int FirstChip = 5;
    private const int FirstCharm = FirstChip + HbrEquipmentCatalog.MaxChipSlots;

    internal static readonly HbrEquipmentSlot Booster = new(HbrEquipmentKind.Booster, 0);
    internal static readonly HbrEquipmentSlot Ring = new(HbrEquipmentKind.Ring, 0);
    internal static readonly HbrEquipmentSlot Earring = new(HbrEquipmentKind.Earring, 0);
    internal static readonly HbrEquipmentSlot Bracelet = new(HbrEquipmentKind.Bracelet, 0);
    internal static readonly HbrEquipmentSlot Necklace = new(HbrEquipmentKind.Necklace, 0);

    internal static readonly HbrEquipmentSlot[] Accessories = [Ring, Earring, Bracelet, Necklace];

    internal static HbrEquipmentSlot Chip(int index) => new(HbrEquipmentKind.Chip, index);
    internal static HbrEquipmentSlot Charm(int index) => new(HbrEquipmentKind.Charm, index);

    internal bool IsValid => Kind switch
    {
        HbrEquipmentKind.Chip => Index >= 0 && Index < HbrEquipmentCatalog.MaxChipSlots,
        HbrEquipmentKind.Charm => Index >= 0 && Index < HbrEquipmentCatalog.CharmSlots,
        _ => Index == 0
    };

    internal int Position => Kind switch
    {
        HbrEquipmentKind.Booster => 0,
        HbrEquipmentKind.Ring => 1,
        HbrEquipmentKind.Earring => 2,
        HbrEquipmentKind.Bracelet => 3,
        HbrEquipmentKind.Necklace => 4,
        HbrEquipmentKind.Chip => FirstChip + Index,
        _ => FirstCharm + Index
    };

    internal static HbrEquipmentSlot FromPosition(int position) => position switch
    {
        0 => Booster,
        1 => Ring,
        2 => Earring,
        3 => Bracelet,
        4 => Necklace,
        < FirstCharm => Chip(position - FirstChip),
        _ => Charm(position - FirstCharm)
    };
}

// The saved, immutable per-player equipment record. Every change produces a
// new instance with a higher revision, so stale network requests are refused.
internal sealed record HbrEquipmentState
{
    internal const int CurrentSchema = 1;
    private const int MaxReceipts = 64;

    public int Schema { get; init; } = CurrentSchema;
    public int Revision { get; init; }
    public int NextItemId { get; init; } = 1;
    // The owner's run seed, copied once so every peer rolls the same stream.
    public ulong Seed { get; init; }
    public int RandomCounter { get; init; }
    public HbrEquipmentItem[] Items { get; init; } = Array.Empty<HbrEquipmentItem>();
    public int[] Equipped { get; init; } = new int[HbrEquipmentSlot.Count];
    public int PlusRelease { get; init; }
    public int PlusChange { get; init; }
    public string[] Receipts { get; init; } = Array.Empty<string>();

    public int[] PurchasedActs { get; init; } = Array.Empty<int>();
    public int[] AlchemyActs { get; init; } = Array.Empty<int>();
    public HbrAlchemyTransaction LastAlchemy { get; init; }

    internal static readonly HbrEquipmentState Empty = new();

    internal static HbrEquipmentState Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return Empty;
        HbrEquipmentState state = null;
        try { state = JsonSerializer.Deserialize<HbrEquipmentState>(data); }
        catch (JsonException) { }
        catch (NotSupportedException) { }
        return Normalize(state);
    }

    internal string Serialize() => JsonSerializer.Serialize(this);

    internal HbrEquipmentItem FindItem(int id) => id <= 0 ? null : Items.FirstOrDefault(item => item.Id == id);

    internal int At(HbrEquipmentSlot slot) => slot.IsValid ? Equipped[slot.Position] : 0;

    internal HbrEquipmentSlot? SlotOf(int itemId)
    {
        if (itemId <= 0) return null;
        int position = Array.IndexOf(Equipped, itemId);
        return position < 0 ? null : HbrEquipmentSlot.FromPosition(position);
    }

    internal bool IsEquipped(int itemId) => itemId > 0 && Array.IndexOf(Equipped, itemId) >= 0;

    internal bool HasReceipt(string receipt) =>
        !string.IsNullOrEmpty(receipt) && Receipts.Contains(receipt, StringComparer.Ordinal);

    internal HbrEquipmentState WithReceipt(string receipt)
    {
        if (string.IsNullOrEmpty(receipt) || HasReceipt(receipt)) return this;
        // Receipts only guard the most recent rooms and reward claims.
        return this with { Receipts = Receipts.Append(receipt).TakeLast(MaxReceipts).ToArray() };
    }

    // Saved data may come from a later edit or a damaged file. Drop rows that
    // no longer resolve instead of letting a bad reference break the manual.
    private static HbrEquipmentState Normalize(HbrEquipmentState state)
    {
        if (state == null || state.Schema != CurrentSchema) return Empty;
        var seen = new HashSet<int>();
        HbrEquipmentItem[] items = (state.Items ?? Array.Empty<HbrEquipmentItem>())
            .Where(item => item != null && item.Id > 0 && HbrEquipmentCatalog.Find(item.Template) != null &&
                seen.Add(item.Id))
            .Select(NormalizeItem).ToArray();
        int[] equipped = new int[HbrEquipmentSlot.Count];
        int[] saved = state.Equipped ?? Array.Empty<int>();
        var used = new HashSet<int>();
        for (int position = 0; position < Math.Min(saved.Length, equipped.Length); position++)
        {
            HbrEquipmentItem item = items.FirstOrDefault(row => row.Id == saved[position]);
            HbrEquipmentSlot slot = HbrEquipmentSlot.FromPosition(position);
            if (item != null && HbrEquipmentCatalog.Find(item.Template).Kind == slot.Kind && used.Add(item.Id))
                equipped[position] = item.Id;
        }
        int nextId = Math.Max(state.NextItemId, items.Length == 0 ? 1 : items.Max(item => item.Id) + 1);
        return HbrEquipmentRules.Trim(state with
        {
            Items = items,
            Equipped = equipped,
            NextItemId = nextId,
            RandomCounter = Math.Max(0, state.RandomCounter),
            PurchasedActs = (state.PurchasedActs ?? Array.Empty<int>()).Where(act => act >= 0).Distinct().ToArray(),
            AlchemyActs = (state.AlchemyActs ?? Array.Empty<int>()).Where(act => act >= 0).Distinct().ToArray(),
            LastAlchemy = NormalizeTransaction(state.LastAlchemy, items),
            PlusRelease = Math.Max(0, state.PlusRelease),
            PlusChange = Math.Max(0, state.PlusChange),
            Receipts = (state.Receipts ?? Array.Empty<string>()).Where(receipt => !string.IsNullOrEmpty(receipt))
                .Distinct(StringComparer.Ordinal).TakeLast(MaxReceipts).ToArray()
        });
    }

    private static HbrAlchemyTransaction NormalizeTransaction(HbrAlchemyTransaction transaction, HbrEquipmentItem[] items)
    {
        if (transaction == null || string.IsNullOrEmpty(transaction.Id) || transaction.PaidPrice < 1 ||
            items.FirstOrDefault(item => item.Id == transaction.OriginalItemId) is not { } original) return null;
        HbrEquipmentClaimItem second = transaction.Second;
        if (second != null)
        {
            HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(second.Template);
            if (template == null || template.Kind != HbrEquipmentCatalog.Find(original.Template).Kind) return null;
            HbrEquipmentItem normalized = NormalizeItem(new HbrEquipmentItem
            { Id = original.Id, Template = second.Template, Rolls = second.Rolls, Plus = second.Plus });
            second = new(normalized.Template, normalized.Rolls, normalized.Plus);
        }
        return transaction with { Second = second, Act = Math.Max(0, transaction.Act),
            Tier = HbrEquipmentCatalog.ClampTier(transaction.Tier), Refund = Math.Clamp(transaction.Refund, 0, transaction.PaidPrice / 2) };
    }

    private static HbrEquipmentItem NormalizeItem(HbrEquipmentItem item)
    {
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item.Template);
        HbrPlusSlot[] plus = (item.Plus ?? Array.Empty<HbrPlusSlot>())
            .Where(slot => slot != null && Enum.IsDefined(slot.Stat))
            .Select(slot => slot with { Level = Math.Clamp(slot.Level, 1, HbrEquipmentCatalog.MaxPlusLevel) })
            .Take(template.PlusSlots).ToArray();
        return item with { Rolls = HbrEquipmentRules.NormalizeRolls(template, item.Rolls), Plus = plus };
    }
}
