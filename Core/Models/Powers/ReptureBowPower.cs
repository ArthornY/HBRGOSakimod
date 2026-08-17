using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ReptureBowPower : PowerModel
{
    private const decimal _damageMultiplier = 1.25m;
    private const decimal _destructionRateMultiplier = 1.5m;
    private const decimal _deckBonusPerBowCard = 0.05m;

    private static readonly FieldInfo SingleTargetField =
        PrivateAccess.Field<AttackCommand>("_singleTarget");
    private static readonly FieldInfo CombatStateField =
        PrivateAccess.Field<AttackCommand>("_combatState");
    private static readonly FieldInfo IsRandomlyTargetedField =
        PrivateAccess.Field<AttackCommand>("<IsRandomlyTargeted>k__BackingField");

    private HashSet<AttackCommand> _amplifiedAllTargetAttacks = new();
    private HashSet<AttackCommand> _singleOpponentAttacks = new();
    private List<AttackCommand> _activeAttacks = new();

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        using (ModeTransitionHelper.SuppressDualSwordsOnModeExit())
        {
            await PowerCmd.Remove(Owner.GetPower<NormalStatePower>());
            await PowerCmd.Remove(Owner.GetPower<ReptureDualSwordsPower>());
            await PowerCmd.Remove(Owner.GetPower<RaptureScythePower>());
            MagicSwordModePower magicSwordMode =
                Owner.GetPower<MagicSwordModePower>();
            if (magicSwordMode != null)
            {
                await magicSwordMode.RemoveForModeTransition();
            }

            await PowerCmd.Remove(Owner.GetPower<CrimsonBladeFlowPower>());
        }

        await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
    }

    public override Task BeforeAttack(AttackCommand command)
    {
        if (!IsOwnerAttack(command))
        {
            return Task.CompletedTask;
        }

        if (HasExactlyOneHittableOpponent())
        {
            _singleOpponentAttacks.Add(command);
        }

        bool wasAllTarget =
            command.IsMultiTargeted && !command.IsRandomlyTargeted ||
            command.ModelSource is PiercingShotHbrgoSaki ||
            command.ModelSource is CardModel card && NotOverYetPower.IsAllTargetGeneratedAttack(card);
        _activeAttacks.Add(command);
        if (wasAllTarget)
        {
            _amplifiedAllTargetAttacks.Add(command);
            return Task.CompletedTask;
        }

        if (command.IsSingleTargeted || command.IsRandomlyTargeted)
        {
            RetargetToAllOpponents(command, Owner.CombatState);
        }

        return Task.CompletedTask;
    }

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        _amplifiedAllTargetAttacks.Remove(command);
        _singleOpponentAttacks.Remove(command);
        _activeAttacks.Remove(command);
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (dealer != Owner ||
            target?.Side == Owner.Side ||
            !props.IsPoweredAttack())
        {
            return 1m;
        }

        decimal multiplier = GetDeckDamageMultiplier();
        AttackCommand activeAttack = _activeAttacks.LastOrDefault();
        if (activeAttack != null && _amplifiedAllTargetAttacks.Contains(activeAttack))
        {
            multiplier *= _damageMultiplier;
        }

        if (activeAttack != null && _singleOpponentAttacks.Contains(activeAttack))
        {
            multiplier *= _damageMultiplier;
        }

        if (activeAttack == null && cardSource != null)
        {
            if (IsProjectedAllTargetAttack(cardSource))
            {
                multiplier *= _damageMultiplier;
            }

            if (HasExactlyOneHittableOpponent())
            {
                multiplier *= _damageMultiplier;
            }
        }

        return multiplier;
    }

    public override decimal ModifyPowerAmountGivenMultiplicative(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature target,
        CardModel cardSource)
    {
        if (power is not DestructionRatePower ||
            giver != Owner ||
            target == null ||
            target.Side == Owner.Side ||
            amount <= 0m)
        {
            return 1m;
        }

        return _destructionRateMultiplier + GetBowCardCount() * _deckBonusPerBowCard;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _amplifiedAllTargetAttacks = new HashSet<AttackCommand>();
        _singleOpponentAttacks = new HashSet<AttackCommand>();
        _activeAttacks = new List<AttackCommand>();
    }

    private bool IsOwnerAttack(AttackCommand command)
    {
        return command.Attacker == Owner;
    }

    private bool HasExactlyOneHittableOpponent()
    {
        ICombatState combatState = Owner.CombatState;
        return combatState != null && combatState.GetOpponentsOf(Owner).Count(creature => creature.IsHittable) == 1;
    }

    private decimal GetDeckDamageMultiplier()
    {
        return 1m + GetBowCardCount() * _deckBonusPerBowCard;
    }

    internal static decimal GetProjectedDamageMultiplier(
        Creature owner,
        CardModel card)
    {
        decimal multiplier =
            1m +
            GetBowCardCount(owner) *
            _deckBonusPerBowCard;
        if (IsProjectedAllTargetAttack(card))
        {
            multiplier *= _damageMultiplier;
        }

        if (HasExactlyOneHittableOpponent(owner))
        {
            multiplier *= _damageMultiplier;
        }

        return multiplier;
    }

    private int GetBowCardCount()
    {
        return GetBowCardCount(Owner);
    }

    private static int GetBowCardCount(Creature owner)
    {
        return owner?.Player?.Deck?.Cards.Count(
            ModeTransitionHelper.IsBowRestrictedCard) ?? 0;
    }

    private static bool IsProjectedAllTargetAttack(
        CardModel card)
    {
        return card?.TargetType == TargetType.AllEnemies ||
            card is PiercingShotHbrgoSaki;
    }

    private static bool HasExactlyOneHittableOpponent(
        Creature owner)
    {
        ICombatState combatState = owner?.CombatState;
        return combatState != null &&
            combatState
                .GetOpponentsOf(owner)
                .Count(creature => creature.IsHittable) == 1;
    }

    private static void RetargetToAllOpponents(AttackCommand command, ICombatState combatState)
    {
        SingleTargetField.SetValue(command, null);
        CombatStateField.SetValue(command, null);
        IsRandomlyTargetedField.SetValue(command, false);
        command.TargetingAllOpponents(combatState);
    }
}
