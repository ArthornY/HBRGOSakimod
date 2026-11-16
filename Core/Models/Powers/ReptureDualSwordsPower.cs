using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ReptureDualSwordsPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool NextAttackHasChase { get; private set; }

    public override MegaCrit.Sts2.Core.Localization.LocString Description =>
        HbrgoSakiEnergyIconHelper.Add31OPrefix(base.Description);
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Color AmountLabelColor => new("F8D36A");

    // Chase is scoped to one physical card, not a blanket form penalty.
    internal static decimal GetProjectedDamageMultiplier(Creature owner) => 1m;

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        using (ModeTransitionHelper.SuppressDualSwordsOnModeExit())
        {
            await PowerCmd.Remove(Owner.GetPower<NormalStatePower>());
            await PowerCmd.Remove(Owner.GetPower<ReptureBowPower>());
            await PowerCmd.Remove(Owner.GetPower<RaptureScythePower>());
        }
        await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
        await ModeTransitionHelper.RecordModeSwitch(Owner);
        DualSwordsChaseHelper.Refresh(Owner);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        DualSwordsChaseHelper.Refresh(oldOwner);
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombatLate(CardModel card,
        decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card?.Owner?.Creature != Owner || originalCost <= 0m ||
            !DualSwordsChaseHelper.IsChaseCost(card)) return false;
        modifiedCost = decimal.Floor(originalCost / 2m);
        return modifiedCost != originalCost;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        DualSwordsChaseHelper.Commit(cardPlay, this);
        return Task.CompletedTask;
    }

    internal void SetNextAttackHasChase(bool value)
    {
        if (NextAttackHasChase == value) return;
        AssertMutable();
        NextAttackHasChase = value;
        DualSwordsChaseHelper.Refresh(Owner);
    }

    public override decimal ModifyBlockMultiplicative(Creature target,
        decimal block, ValueProp props, CardModel cardSource, CardPlay cardPlay) =>
        target == Owner && block > 0m && HasVirtualCrimsonBladeFlow() ? 2m : 1m;

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        // Preserve the independent rune's virtual Crimson Flow. HBR's version
        // lives in its final stat transform, not in this legacy damage bucket.
        if (!HbrBattleStatsHelper.IsEnabled(Owner) && amount > 0m &&
            HasVirtualCrimsonBladeFlow() && (dealer == Owner || target == Owner))
            HbrgoSakiDamageMultiplierHelper.Add(2m, this);
        return 1m;
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!HasVirtualCrimsonBladeFlow() || side != Owner.Side || !participants.Contains(Owner)) return;
        Flash();
        await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (!HasVirtualCrimsonBladeFlow() || side != Owner.Side || !participants.Contains(Owner) ||
            ExtraTurnDurationHelper.ShouldSuppressDurationTick(Owner)) return;
        DeflectorProtection protection = Owner.Player?.GetRelic<DeflectorProtection>();
        int lostDp = protection?.LoseDpByCurrentPercent(.1m) ?? 0;
        if (lostDp > 0) Flash();
        if (protection != null) await protection.ResolveMentalStrengthDpLossRewards(choiceContext);
    }

    private bool HasVirtualCrimsonBladeFlow() => !Owner.HasPower<CrimsonBladeFlowPower>() &&
        HbrgoSakiHextechCompatLoader.HasDoubleSwordsDoubleRune(Owner);
}
