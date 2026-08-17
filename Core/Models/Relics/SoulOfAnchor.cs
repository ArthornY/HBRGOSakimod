using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Relics.Visibility;

namespace HBRGOSakiMod.Core.Models.Relics;

/// <summary>
/// Hidden compatibility model that stores and exposes roll history.
/// Roll gameplay is implemented by the RitsuLib roll card capability.
/// </summary>
public class SoulOfAnchor : RelicModel, IModRelicVisibility
{
    private int _lastRoll;
    private List<SoulRollHistoryEntry> _rollHistory = new();

    public event Action HistoryChanged;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => false;

    public bool IsRelicVisible => false;

    public override int DisplayAmount => LastRoll;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("relics/soul_of_anchor.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    public IReadOnlyList<SoulRollHistoryEntry> RollHistory => _rollHistory;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastRoll
    {
        get => _lastRoll;
        private set
        {
            AssertMutable();
            _lastRoll = Math.Max(0, value);
        }
    }

    public override Task BeforeCombatStart()
    {
        ClearHistory();
        return Task.CompletedTask;
    }

    public void ClearHistory()
    {
        AssertMutable();
        _rollHistory.Clear();
        LastRoll = 0;
        NotifyHistoryChanged();
    }

    public void RecordRoll(
        SoulRollBreakdown winningRoll,
        IReadOnlyList<SoulRollBreakdown> discardedRolls,
        string source)
    {
        AssertMutable();
        SoulRollBreakdown resolvedWinningRoll =
            winningRoll ?? SoulRollBreakdown.FromFinalRoll(0);
        _rollHistory.Add(new SoulRollHistoryEntry(
            resolvedWinningRoll,
            discardedRolls ?? Array.Empty<SoulRollBreakdown>(),
            source));
        LastRoll = resolvedWinningRoll.FinalRoll;
        NotifyHistoryChanged();
    }

    public void RecordAbilityRoll(
        int roll,
        int? discardedRoll,
        string source)
    {
        RecordRoll(
            SoulRollBreakdown.FromFinalRoll(roll),
            discardedRoll.HasValue
                ? new[]
                {
                    SoulRollBreakdown.FromFinalRoll(
                        discardedRoll.Value,
                        "优势骰")
                }
                : Array.Empty<SoulRollBreakdown>(),
            source);
    }

    public void RecordAbilityRoll(
        int roll,
        IReadOnlyList<int> discardedRolls,
        string source)
    {
        RecordRoll(
            SoulRollBreakdown.FromFinalRoll(roll),
            discardedRolls?
                .Select(discardedRoll =>
                    SoulRollBreakdown.FromFinalRoll(
                        discardedRoll,
                        "优势骰"))
                .ToArray() ?? Array.Empty<SoulRollBreakdown>(),
            source);
    }

    public void RecordAbilityRoll(
        SoulRollBreakdown winningRoll,
        IReadOnlyList<SoulRollBreakdown> discardedRolls,
        string source)
    {
        RecordRoll(winningRoll, discardedRolls, source);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _lastRoll = 0;
        _rollHistory = new List<SoulRollHistoryEntry>();
        HistoryChanged = null;
    }

    private void NotifyHistoryChanged()
    {
        InvokeDisplayAmountChanged();
        HistoryChanged?.Invoke();
    }
}
