using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
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
    private bool _isReplicatingSkyAspectPower;

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
        }

        await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
        SwiftPower swift = Owner.GetPower<SwiftPower>();
        if (swift != null)
        {
            await swift.RecordModeSwitch();
        }
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
        SniperModePower sniperMode =
            Owner.GetPower<SniperModePower>();
        if (sniperMode != null)
        {
            wasAllTarget = sniperMode.WasNaturallyAllTarget(
                command,
                wasAllTarget);
        }

        _activeAttacks.Add(command);
        if (wasAllTarget)
        {
            _amplifiedAllTargetAttacks.Add(command);
        }

        if (sniperMode?.TryConcentrateBowAttack(
                command,
                wasAllTarget) == true)
        {
            return Task.CompletedTask;
        }

        if (wasAllTarget)
        {
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

    public override async Task BeforePowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature target,
        Creature applier,
        CardModel cardSource)
    {
        if (_isReplicatingSkyAspectPower ||
            amount == 0m ||
            applier != Owner ||
            target == null ||
            target.Side == Owner.Side ||
            !HbrgoSakiHextechCompatLoader
                .HasChangingSkyAspectRune(Owner))
        {
            return;
        }

        PowerModel canonicalPower = power.Id == ModelId.none
            ? null
            : ModelDb.GetByIdOrNull<PowerModel>(power.Id);
        if (canonicalPower == null)
        {
            return;
        }

        Creature[] extraTargets = Owner.CombatState
            .GetOpponentsOf(Owner)
            .Where(creature =>
                creature != target &&
                creature.IsHittable)
            .ToArray();
        if (extraTargets.Length == 0)
        {
            return;
        }

        _isReplicatingSkyAspectPower = true;
        try
        {
            foreach (Creature extraTarget in extraTargets)
            {
                await PowerCmd.Apply(
                    new ThrowingPlayerChoiceContext(),
                    canonicalPower.ToMutable(),
                    extraTarget,
                    amount,
                    applier,
                    cardSource);
            }
        }
        finally
        {
            _isReplicatingSkyAspectPower = false;
        }
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
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

        return _destructionRateMultiplier +
               GetEffectiveBowCardCount(Owner) *
               _deckBonusPerBowCard;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _amplifiedAllTargetAttacks = new HashSet<AttackCommand>();
        _singleOpponentAttacks = new HashSet<AttackCommand>();
        _activeAttacks = new List<AttackCommand>();
        _isReplicatingSkyAspectPower = false;
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
        return 1m + GetEffectiveBowCardCount(Owner) * _deckBonusPerBowCard;
    }

    internal static decimal GetProjectedDamageMultiplier(
        Creature owner,
        CardModel card)
    {
        decimal multiplier =
            1m +
            GetEffectiveBowCardCount(owner) *
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

    internal static decimal CalculateExtraDpDamage(
        Creature target,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        ICombatState combatState = target?.CombatState;
        if (combatState == null ||
            dealer?.CombatState != combatState ||
            target.Side == dealer.Side ||
            dealer.GetPower<ReptureBowPower>() == null ||
            !props.IsPoweredAttack())
        {
            return 0m;
        }

        IRunState runState = IRunState.GetFrom(
            new Creature[]
            {
                target,
                dealer
            });
        using (HighDimensionalShellPower.SuppressDamageReduction())
        {
            return HookCompatibility.ModifyDamage(
                runState,
                combatState,
                target,
                dealer,
                1m,
                props,
                cardSource,
                ModifyDamageHookType.All,
                CardPreviewMode.None,
                out _);
        }
    }

    internal static int GetBowCardCount(Creature owner)
    {
        return owner?.Player?.Deck?.Cards.Count(
            ModeTransitionHelper.IsBowRestrictedCard) ?? 0;
    }

    internal static int GetEnemyPowerAmount<TPower>(
        Creature owner,
        Creature primaryTarget,
        bool useDisplayAmount = false)
        where TPower : PowerModel
    {
        if (primaryTarget == null)
        {
            return 0;
        }

        IEnumerable<Creature> targets =
            HbrgoSakiHextechCompatLoader
                .HasChangingSkyAspectRune(owner) &&
            owner?.HasPower<ReptureBowPower>() == true
                ? owner.CombatState
                    .GetOpponentsOf(owner)
                    .Where(creature => creature.IsHittable)
                : new[] { primaryTarget };

        long total = targets.Sum(creature =>
        {
            TPower power = creature.GetPower<TPower>();
            return power == null
                ? 0L
                : useDisplayAmount
                    ? (long)power.DisplayAmount
                    : (long)power.Amount;
        });
        return (int)Math.Clamp(total, int.MinValue, int.MaxValue);
    }

    internal static bool AnyEnemyHasPower<TPower>(
        Creature owner,
        Creature primaryTarget)
        where TPower : PowerModel
    {
        if (primaryTarget == null)
        {
            return false;
        }

        if (!HbrgoSakiHextechCompatLoader
                .HasChangingSkyAspectRune(owner) ||
            owner?.HasPower<ReptureBowPower>() != true)
        {
            return primaryTarget.HasPower<TPower>();
        }

        return owner.CombatState
            .GetOpponentsOf(owner)
            .Any(creature =>
                creature.IsHittable &&
                creature.HasPower<TPower>());
    }

    private static int GetEffectiveBowCardCount(Creature owner)
    {
        return ModeTransitionHelper.IsWeaponModeWeakened(owner)
            ? 0
            : GetBowCardCount(owner);
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
