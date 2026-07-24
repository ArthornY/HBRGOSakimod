using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FeintMarkPower : PowerModel
{
    private static readonly object _dualSwordsEligibleStrikesLock = new();
    private static readonly HashSet<CardModel> _dualSwordsEligibleStrikes = new();

    private AttackCommand _activeAttackCommand;
    private bool _isIncomingDamageWindowOpen;
    private bool _shouldSpendSwordheart;
    private bool _canGrantExtraTurnForPendingDamage;
    private bool _didNegateDamage;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Godot.Color AmountLabelColor => new("F8D36A");

    public static bool IsDualSwordsEligibleStrike(CardModel card)
    {
        lock (_dualSwordsEligibleStrikesLock)
        {
            return _dualSwordsEligibleStrikes.Contains(card);
        }
    }

    public override async Task BeforeAttack(AttackCommand command)
    {
        Creature attacker = command.Attacker;
        if (Amount <= 0 ||
            _activeAttackCommand != null ||
            attacker == null ||
            attacker.Side == Owner.Side ||
            command.TargetSide != Owner.Side ||
            attacker.IsDead ||
            !command.DamageProps.IsPoweredAttack())
        {
            return;
        }

        if (Owner.Player == null || Owner.IsDead)
        {
            return;
        }

        _activeAttackCommand = command;
        _isIncomingDamageWindowOpen = false;
        _shouldSpendSwordheart = false;
        _canGrantExtraTurnForPendingDamage = false;
        _didNegateDamage = false;

        if (Owner.IsDead || attacker.IsDead)
        {
            return;
        }

        PlayerChoiceContext choiceContext = new BlockingPlayerChoiceContext();
        CardModel strike = Owner.CombatState.CreateCard<StrikeHbrgoSaki>(Owner.Player);
        strike.UpgradeInternal();
        strike.FinalizeUpgradeInternal();
        strike.ExhaustOnNextPlay = true;
        AddDualSwordsEligibleStrike(strike);
        try
        {
            await CardCmd.AutoPlay(choiceContext, strike, attacker, AutoPlayType.Default, skipCardPileVisuals: true);
            await CombatFlowHelper.FinishAutoPlayedCard(strike);
        }
        finally
        {
            RemoveDualSwordsEligibleStrike(strike);
        }

        _isIncomingDamageWindowOpen = !Owner.IsDead && !attacker.IsDead;
    }

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (!_isIncomingDamageWindowOpen ||
            _activeAttackCommand == null ||
            _shouldSpendSwordheart ||
            _didNegateDamage ||
            target != Owner ||
            dealer != _activeAttackCommand.Attacker ||
            !props.IsPoweredAttack())
        {
            return Task.CompletedTask;
        }

        _canGrantExtraTurnForPendingDamage = false;
        DodgePower dodge = Owner.GetPower<DodgePower>();
        if (dodge == null || amount >= dodge.Amount || props.HasFlag(ValueProp.Unblockable))
        {
            _shouldSpendSwordheart = true;
        }
        else
        {
            _canGrantExtraTurnForPendingDamage = true;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostBeforeOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (!_isIncomingDamageWindowOpen ||
            _activeAttackCommand == null ||
            _shouldSpendSwordheart ||
            _didNegateDamage ||
            amount <= 0m ||
            target == Owner ||
            target.Side != Owner.Side ||
            !Owner.HasPower<FeintDamageRedirectPower>() ||
            dealer != _activeAttackCommand.Attacker ||
            !props.IsPoweredAttack())
        {
            return amount;
        }

        _canGrantExtraTurnForPendingDamage = false;
        DodgePower dodge = Owner.GetPower<DodgePower>();
        if (dodge == null || amount >= dodge.Amount || props.HasFlag(ValueProp.Unblockable))
        {
            _shouldSpendSwordheart = true;
        }
        else
        {
            _canGrantExtraTurnForPendingDamage = true;
        }

        return amount;
    }

    internal bool TryNegateIncomingDamage(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer)
    {
        if (!_isIncomingDamageWindowOpen ||
            _activeAttackCommand == null ||
            _shouldSpendSwordheart ||
            amount <= 0m ||
            target == null ||
            target.Side != Owner.Side ||
            dealer != _activeAttackCommand.Attacker ||
            !props.IsPoweredAttack())
        {
            return false;
        }

        bool targetsOwner = target == Owner;
        bool redirectsAllyDamage =
            Owner.HasPower<FeintDamageRedirectPower>() &&
            target.Side == Owner.Side;
        if (!targetsOwner && !redirectsAllyDamage)
        {
            return false;
        }

        DodgePower dodge = Owner.GetPower<DodgePower>();
        if (dodge == null ||
            !IsDodgeNegationEligible(amount, dodge.Amount, props))
        {
            _shouldSpendSwordheart = true;
            _canGrantExtraTurnForPendingDamage = false;
            return false;
        }

        if (!DodgePower.TrySpend(Owner, amount))
        {
            _shouldSpendSwordheart = true;
            _canGrantExtraTurnForPendingDamage = false;
            return false;
        }

        _canGrantExtraTurnForPendingDamage = false;
        if (!_didNegateDamage)
        {
            _didNegateDamage = true;
            Flash();
            Owner.Player?.GetRelic<FeintLikeWind>()?.QueueExtraTurn();
        }

        return true;
    }

    internal static bool IsDodgeNegationEligible(
        decimal incomingDamage,
        int dodgeAmount,
        ValueProp props)
    {
        return incomingDamage > 0m &&
               incomingDamage < dodgeAmount &&
               !props.HasFlag(ValueProp.Unblockable);
    }

    internal void NotifyDamageNegatedByDodge(
        Creature target,
        ValueProp props,
        Creature dealer)
    {
        if (!_isIncomingDamageWindowOpen ||
            _activeAttackCommand == null ||
            _shouldSpendSwordheart ||
            !_canGrantExtraTurnForPendingDamage ||
            _didNegateDamage ||
            target != Owner ||
            dealer != _activeAttackCommand.Attacker ||
            !props.IsPoweredAttack())
        {
            return;
        }

        _canGrantExtraTurnForPendingDamage = false;
        _didNegateDamage = true;
        Owner.Player?.GetRelic<FeintLikeWind>()?.QueueExtraTurn();
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command != _activeAttackCommand)
        {
            return;
        }

        try
        {
            if (_shouldSpendSwordheart)
            {
                Flash();
                await SwordheartSpendHelper.TrySpend(choiceContext, Owner, 1m);
            }

            if (Amount > 0)
            {
                await PowerCmd.ModifyAmount(choiceContext, this, -1m, Owner, null);
            }
        }
        finally
        {
            _activeAttackCommand = null;
            _isIncomingDamageWindowOpen = false;
            _shouldSpendSwordheart = false;
            _canGrantExtraTurnForPendingDamage = false;
            _didNegateDamage = false;
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _activeAttackCommand = null;
        _isIncomingDamageWindowOpen = false;
        _shouldSpendSwordheart = false;
        _canGrantExtraTurnForPendingDamage = false;
        _didNegateDamage = false;
    }

    private static void AddDualSwordsEligibleStrike(CardModel card)
    {
        lock (_dualSwordsEligibleStrikesLock)
        {
            _dualSwordsEligibleStrikes.Add(card);
        }
    }

    private static void RemoveDualSwordsEligibleStrike(CardModel card)
    {
        lock (_dualSwordsEligibleStrikesLock)
        {
            _dualSwordsEligibleStrikes.Remove(card);
        }
    }
}
