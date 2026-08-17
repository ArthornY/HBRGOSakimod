using System;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class HighDimensionalShellPower : PowerModel
{
    public const int BreakAmount = 3;

    private const int EnhancedShellAscensionLevel = 10;
    private const decimal IncomingDamageMultiplier = 0.75m;
    private const decimal BaseMaxHpRatio = 0.2m;
    private const decimal EnhancedEnemyMaxHpRatio = 0.3m;

    private bool _pendingBreak;
    private int _lastAbsorbedDamage;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    public static int CalculateAmount(Creature creature)
    {
        bool hasEnhancedEnemyShell =
            creature.Side == CombatSide.Enemy &&
            creature.CombatState?.RunState.AscensionLevel >=
                EnhancedShellAscensionLevel;
        decimal maxHpRatio = hasEnhancedEnemyShell
            ? EnhancedEnemyMaxHpRatio
            : BaseMaxHpRatio;
        return Math.Max(1, (int)(creature.MaxHp * maxHpRatio));
    }

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        return CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        return CombatPreviewRefreshHelper.RefreshOpponentIntents(oldOwner);
    }

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target != Owner || amount <= 0m)
        {
            return 1m;
        }

        return IncomingDamageMultiplier;
    }

    public bool TryAbsorb(decimal amount)
    {
        _lastAbsorbedDamage = 0;
        if (amount <= 0m || Amount <= 0 || _pendingBreak)
        {
            return false;
        }

        int shellToSpend = Math.Min(Amount, (int)amount);
        if (shellToSpend <= 0)
        {
            return false;
        }

        SetAmount(Amount - shellToSpend);
        _lastAbsorbedDamage = shellToSpend;
        Flash();
        _pendingBreak = Amount <= 0;
        return true;
    }

    internal int ConsumeLastAbsorbedDamage()
    {
        int absorbedDamage = _lastAbsorbedDamage;
        _lastAbsorbedDamage = 0;
        return absorbedDamage;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target != Owner || !_pendingBreak)
        {
            return;
        }

        _pendingBreak = false;
        await ResolveBreak(choiceContext, 0);
    }

    internal async Task BreakImmediately(
        PlayerChoiceContext choiceContext,
        int extraBreak)
    {
        if (Amount <= 0 && !_pendingBreak)
        {
            return;
        }

        SetAmount(0);
        _lastAbsorbedDamage = 0;
        _pendingBreak = false;
        await ResolveBreak(
            choiceContext,
            Math.Max(0, extraBreak));
    }

    private async Task ResolveBreak(
        PlayerChoiceContext choiceContext,
        int extraBreak)
    {
        if (!Owner.IsDead)
        {
            await PowerCmd.Apply<BreakPower>(
                choiceContext,
                Owner,
                BreakAmount + extraBreak,
                Owner,
                null);

            if (!Owner.HasPower<DestructionRatePower>())
            {
                await PowerCmd.Apply<DestructionRatePower>(
                    choiceContext,
                    Owner,
                    DestructionRatePower.BaselineAmount,
                    Owner,
                    null);
            }

            // Resume a registered interrupted intent after the stun instead
            // of falling back to the monster's last performed move.
            string interruptedMoveId = null;
            if (Owner.Monster?.NextMove is { } interruptedMove &&
                Owner.Monster.MoveStateMachine.States.ContainsKey(interruptedMove.Id))
            {
                interruptedMoveId = interruptedMove.Id;
            }

            await CreatureCmd.Stun(Owner, interruptedMoveId);
        }

        await PowerCmd.Remove(this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingBreak = false;
        _lastAbsorbedDamage = 0;
    }
}
