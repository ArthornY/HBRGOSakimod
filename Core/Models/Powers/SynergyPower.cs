using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SynergyPower : PowerModel
{
    private const int CardsPerTrigger = 3;
    private bool _absorbingLegacyCounters;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DualSwordsCount { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BowCount { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override int DisplayAmount => IsMutable ? DualSwordsCount : 0;

    public override Color AmountLabelColor => new("F8D36A");

    public override LocString Description => Describe(".description");

    internal LocString CompactDescription => Describe(".compactDescription");

    private LocString Describe(string suffix)
    {
        LocString description = LocString.GetIfExists("powers", Id.Entry + suffix) ?? base.Description;
        description.Add("DualSwordsCount", IsMutable ? DualSwordsCount : 0);
        description.Add("BowCount", IsMutable ? BowCount : 0);
        return description;
    }

    private static int AddCount(int current, int addition) =>
        (int)Math.Clamp((long)current + addition, 0L, BigCombatNumbers.NativeStatCap);

    private void NotifyCountersChanged()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, silent: true);
    }

    // The standard power Amount is just the single Synergy buff. Its two
    // saved counters follow the same snapshot rule as other hidden ledgers.
    internal Action SnapshotCounterDuplication()
    {
        int dual = DualSwordsCount, bow = BowCount;
        return () =>
        {
            AssertMutable();
            DualSwordsCount = AddCount(DualSwordsCount, dual);
            BowCount = AddCount(BowCount, bow);
            NotifyCountersChanged();
        };
    }

    // Old combat saves may still hold the two former power models. Transfer
    // their exact counters without treating a saved >=3 as a new card play.
    internal async Task AbsorbLegacyCounters()
    {
        if (_absorbingLegacyCounters || Owner == null) return;
        DualSwordsCounterPower dual = Owner.GetPower<DualSwordsCounterPower>();
        BowCounterPower bow = Owner.GetPower<BowCounterPower>();
        if (dual == null && bow == null) return;
        AssertMutable();
        _absorbingLegacyCounters = true;
        try
        {
            if (dual != null)
            {
                DualSwordsCount = AddCount(DualSwordsCount, Math.Max(0, dual.Amount));
                dual.SetAmount(0);
                await PowerCmd.Remove(dual);
            }
            if (bow != null)
            {
                BowCount = AddCount(BowCount, Math.Max(0, bow.Amount));
                bow.SetAmount(0);
                await PowerCmd.Remove(bow);
            }
            NotifyCountersChanged();
        }
        finally { _absorbingLegacyCounters = false; }
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource) => AbsorbLegacyCounters();

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState) =>
        side == Owner.Side && participants.Contains(Owner) ? AbsorbLegacyCounters() : Task.CompletedTask;

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay?.Card?.Owner?.Creature != Owner)
        {
            return;
        }

        await AbsorbLegacyCounters();

        ReptureCardMode mode =
            ModeTransitionHelper.GetCardMode(cardPlay.Card);
        switch (mode)
        {
            case ReptureCardMode.Bow:
                await AdvanceCounter(
                    choiceContext,
                    bow: true,
                    ReptureCardMode.DualSwords);
                break;
            case ReptureCardMode.DualSwords
                when !RollKeywordRules.IsCopiedCard(cardPlay.Card):
                await AdvanceCounter(
                    choiceContext,
                    bow: false,
                    ReptureCardMode.Bow);
                break;
        }
    }

    private async Task AdvanceCounter(
        PlayerChoiceContext choiceContext,
        bool bow,
        ReptureCardMode modeToDraw)
    {
        AssertMutable();
        if (bow) BowCount = AddCount(BowCount, 1);
        else DualSwordsCount = AddCount(DualSwordsCount, 1);
        int count = bow ? BowCount : DualSwordsCount;
        if (count < CardsPerTrigger)
        {
            NotifyCountersChanged();
            return;
        }

        if (bow) BowCount = 0;
        else DualSwordsCount = 0;
        NotifyCountersChanged();
        Flash();
        await ModeTransitionHelper.DrawModeRestrictedCard(
            choiceContext,
            Owner.Player,
            modeToDraw,
            this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _absorbingLegacyCounters = false;
    }
}
