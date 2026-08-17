using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class CrimsonBladeFlowPower : PowerModel
{
    public const int SoulOfAnchorEffectMultiplier = 2;

    private const decimal _currentDpLossPercent = 0.1m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F85C5C");

    public static int GetSoulOfAnchorEffectMultiplier(Creature creature)
    {
        return creature?.HasPower<CrimsonBladeFlowPower>() == true ? SoulOfAnchorEffectMultiplier : 1;
    }

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        await PowerCmd.Remove(Owner.GetPower<NormalStatePower>());
        await PowerCmd.Remove(Owner.GetPower<ReptureBowPower>());
        await PowerCmd.Remove(Owner.GetPower<ReptureDualSwordsPower>());
        await PowerCmd.Remove(Owner.GetPower<RaptureScythePower>());
        await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
        if (await ModeTransitionHelper.DiscardIncompatibleCardsFromHandAndRedraw(Owner) > 0)
        {
            Flash();
        }

        await CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await ModeTransitionHelper.EnterDualSwordsAfterModeExit(oldOwner);
        await CombatPreviewRefreshHelper.RefreshOpponentIntents(oldOwner);
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel clonedBy)
    {
        if (card is ChihayaClassmateHbrgoSaki or SwitchHbrgoSaki &&
            card.Owner?.Creature == Owner &&
            card.Pile?.IsCombatPile == true &&
            card.Pile.Type != PileType.Play)
        {
            await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
        }

        await ModeTransitionHelper.HandleIncompatibleCardEnteredHand(
            Owner,
            card,
            oldPileType);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) ||
            Amount <= 0 ||
            ExtraTurnDurationHelper.ShouldSuppressDurationTick(Owner))
        {
            return;
        }

        Creature owner = Owner;
        int lostDp = owner.Player?.GetRelic<DeflectorProtection>()?.LoseDpByCurrentPercent(_currentDpLossPercent) ?? 0;
        if (lostDp > 0)
        {
            Flash();
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1m, owner, null);
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (amount <= 0m)
        {
            return 1m;
        }

        decimal multiplier =
            dealer == Owner || target == Owner ? 2m : 1m;
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
    }

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        if (target != Owner || block <= 0m)
        {
            return 1m;
        }

        return 2m;
    }
}
