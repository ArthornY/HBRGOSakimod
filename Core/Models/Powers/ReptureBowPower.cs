using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ReptureBowPower : PowerModel
{
    private static readonly FieldInfo SingleTargetField = AccessTools.Field(typeof(AttackCommand), "_singleTarget");
    private static readonly FieldInfo CombatStateField = AccessTools.Field(typeof(AttackCommand), "_combatState");
    private static readonly FieldInfo IsRandomlyTargetedField = AccessTools.Field(typeof(AttackCommand), "<IsRandomlyTargeted>k__BackingField");

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
            await PowerCmd.Remove(Owner.GetPower<MagicSwordModePower>());
            await PowerCmd.Remove(Owner.GetPower<CrimsonBladeFlowPower>());
        }

        await ModeTransitionHelper.ReplaceChihayaInLibraryWithSwitch(Owner, cardSource);
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

        bool wasAllTarget = command.IsMultiTargeted && !command.IsRandomlyTargeted;
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
        if (dealer != Owner || target == null || target.Side == Owner.Side || !props.IsPoweredAttack())
        {
            return 1m;
        }

        decimal multiplier = 1m;
        AttackCommand activeAttack = _activeAttacks.LastOrDefault();
        if (activeAttack != null && _amplifiedAllTargetAttacks.Contains(activeAttack))
        {
            multiplier *= 1.5m;
        }

        if (activeAttack != null && _singleOpponentAttacks.Contains(activeAttack))
        {
            multiplier *= 1.5m;
        }

        return multiplier;
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

    private static void RetargetToAllOpponents(AttackCommand command, ICombatState combatState)
    {
        SingleTargetField.SetValue(command, null);
        CombatStateField.SetValue(command, null);
        IsRandomlyTargetedField.SetValue(command, false);
        command.TargetingAllOpponents(combatState);
    }
}
