using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NararaNullificationPower : PowerModel, ICustomPower
{
    // One shared selection for the entire player side, using synchronized RNG.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PenalizedAttributes { get; private set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override LocString Description =>
        HbrgoSakiEnergyIconHelper.Add31OPrefix(base.Description);
    public string CustomPackedIconPath => "res://images/powers/six_sense_resistance_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal bool IsActive => IsMutable && Amount > 0 && Owner?.IsAlive == true &&
        Owner.Side == CombatSide.Enemy && Owner.Monster is NararaShibaBossMonster;

    public bool IsPenalized(CardModel card) => card?.Owner != null &&
        SixSenseCardCapability.GetAttributes(card).Any(attribute => IsAttributeLocked(card.Owner, attribute));

    internal bool IsResistanceLocked(SixSenseAttribute attribute) => IsActive &&
        (PenalizedAttributes & (1 << (int)attribute)) != 0;

    internal bool IsAttributeLocked(Player player, SixSenseAttribute attribute)
    {
        if (!IsActive || player?.Creature.IsAlive != true ||
            !ReferenceEquals(player.Creature.CombatState, Owner.CombatState)) return false;
        return player.Creature.Side != Owner.Side &&
            (PenalizedAttributes & (1 << (int)attribute)) != 0;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player && IsActive && ReferenceEquals(combatState, Owner.CombatState))
            SelectForPlayers(combatState.Players.Where(player => participants.Contains(player.Creature)).ToArray());
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !IsActive) return Task.CompletedTask;
        PenalizedAttributes = 0;
        RefreshCards();
        return Task.CompletedTask;
    }

    internal void SelectForPlayers(IReadOnlyList<Player> participants)
    {
        if (!IsActive || !participants.Any(player => player.Creature.IsAlive)) return;
        AssertMutable();
        var combat = Owner.CombatState;
        // Draw exactly twice, irrespective of party size, without replacement.
        var remaining = Enum.GetValues<SixSenseAttribute>().ToList();
        SixSenseAttribute first = combat.RunState.Rng.Niche.NextItem(remaining);
        remaining.Remove(first);
        SixSenseAttribute second = combat.RunState.Rng.Niche.NextItem(remaining);
        PenalizedAttributes = (1 << (int)first) | (1 << (int)second);
        RefreshCards();
    }

    internal int CaptureSelections() => PenalizedAttributes;

    internal void RestoreSelections(int selections)
    {
        AssertMutable();
        PenalizedAttributes = selections;
        RefreshCards();
    }

    private void RefreshCards()
    {
        InvokeDisplayAmountChanged();
        CombatPreviewRefreshHelper.RefreshCombatState(Owner, nameof(NararaNullificationPower));
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        NararaNullificationHelper.RecordPlay(this, cardPlay);
        return Task.CompletedTask;
    }

    public decimal ModifyDamageCap(Creature target, ValueProp props, Creature dealer, CardModel cardSource) =>
        GetNullificationDamageCap(target, cardSource);

    // Give the compatibility bridge its own nonvirtual entry point. Calling an
    // override directly still encodes the old AbstractModel signature in IL.
    internal decimal GetNullificationDamageCap(Creature target, CardModel cardSource) =>
        target == Owner && IsActive && NararaNullificationHelper.IsCardNullified(Owner, cardSource)
            ? 0m : decimal.MaxValue;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsActive || !NararaNullificationHelper.WasNullified(this, cardPlay)) return;
        Flash();
        // Native HP uses integer truncation. Preserve the same rounding rule and
        // heal exactly the actual maximum-HP increase, including the native cap.
        await CreatureCmd.GainMaxHp(Owner, LargeCreatureHealthHelper.GetMaxHp(Owner) * 0.02m);
    }

}
