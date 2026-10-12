using System;
using System.Collections.Generic;
using System.Linq;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class SoulRollHistoryEntry
{
    public SoulRollBreakdown WinningRoll { get; }

    public IReadOnlyList<SoulRollBreakdown> DiscardedRollDetails { get; }

    public int Roll => WinningRoll.FinalRoll;

    public IReadOnlyList<int> DiscardedRolls =>
        DiscardedRollDetails.Select(roll => roll.FinalRoll).ToArray();

    public int? DiscardedRoll => DiscardedRolls.Count > 0
        ? DiscardedRolls[0]
        : null;

    public string Source { get; }

    public SoulRollHistoryEntry(int roll, int? discardedRoll, string source)
        : this(
            roll,
            discardedRoll.HasValue
                ? new[] { discardedRoll.Value }
                : Array.Empty<int>(),
            source)
    {
    }

    public SoulRollHistoryEntry(
        int roll,
        IEnumerable<int> discardedRolls,
        string source)
        : this(
            SoulRollBreakdown.FromFinalRoll(roll),
            discardedRolls?.Select(
                discardedRoll =>
                    SoulRollBreakdown.FromFinalRoll(discardedRoll)),
            source)
    {
    }

    public SoulRollHistoryEntry(
        SoulRollBreakdown winningRoll,
        IEnumerable<SoulRollBreakdown> discardedRolls,
        string source)
    {
        WinningRoll = winningRoll ?? SoulRollBreakdown.FromFinalRoll(0);
        DiscardedRollDetails =
            discardedRolls?.ToArray() ?? Array.Empty<SoulRollBreakdown>();
        Source = source;
    }
}

public sealed class SoulRollBreakdown
{
    public int BaseRoll { get; }

    public int FinalRoll { get; }

    public bool IsForcedCritical { get; init; }

    public SoulRollTier? ResolvedTier { get; init; }

    public string DiceSource { get; }

    public IReadOnlyList<SoulRollBonusDetail> Bonuses { get; }

    public int TotalBonus => FinalRoll - BaseRoll;

    public SoulRollBreakdown(
        int baseRoll,
        int finalRoll,
        string diceSource,
        IEnumerable<SoulRollBonusDetail> bonuses = null)
    {
        BaseRoll = baseRoll;
        FinalRoll = finalRoll;
        DiceSource = string.IsNullOrWhiteSpace(diceSource)
            ? "基础骰"
            : diceSource;
        Bonuses = bonuses?.ToArray() ?? Array.Empty<SoulRollBonusDetail>();
    }

    public static SoulRollBreakdown FromFinalRoll(
        int roll,
        string diceSource = "基础骰")
    {
        return new SoulRollBreakdown(roll, roll, diceSource);
    }
}

public sealed class SoulRollBonusDetail
{
    public int Amount { get; }

    public string Source { get; }

    public bool IsPercentage { get; }

    public SoulRollBonusDetail(int amount, string source, bool isPercentage = false)
    {
        Amount = amount;
        Source = string.IsNullOrWhiteSpace(source) ? "未知加成" : source;
        IsPercentage = isPercentage;
    }
}
