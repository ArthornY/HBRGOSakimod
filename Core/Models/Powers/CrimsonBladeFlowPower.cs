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

    private bool _putSwitchInHandAfterRemoved;

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
        await ModeTransitionHelper.RemoveChihayaAndSwitchFromCombat(Owner);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (_putSwitchInHandAfterRemoved)
        {
            oldOwner.GetPower<MagicSwordModePower>()?.MarkRestoredSwitchFree();
        }

        await ModeTransitionHelper.EnterDualSwordsAfterModeExit(oldOwner);
        await ModeTransitionHelper.RestoreSwitchToHandAfterModeExit(oldOwner, _putSwitchInHandAfterRemoved);
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
            await ModeTransitionHelper.RemoveChihayaAndSwitchFromCombat(Owner);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) ||
            Amount <= 0 ||
            ExtraTurnDurationHelper.ShouldSuppressDurationTick(Owner))
        {
            return;
        }

        bool willExpire = Amount <= 1;
        Creature owner = Owner;
        _putSwitchInHandAfterRemoved |= willExpire;
        int lostDp = owner.Player?.GetRelic<DeflectorProtection>()?.LoseDpByCurrentPercent(_currentDpLossPercent) ?? 0;
        if (lostDp > 0)
        {
            Flash();
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1m, owner, null);
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (amount <= 0m || target == null)
        {
            return 1m;
        }

        return dealer == Owner || target == Owner ? 2m : 1m;
    }

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (canonicalPower is not DodgePower || target != Owner || amount <= 0m)
        {
            return false;
        }

        modifiedAmount = amount * 2m;
        return true;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _putSwitchInHandAfterRemoved = false;
    }

}
