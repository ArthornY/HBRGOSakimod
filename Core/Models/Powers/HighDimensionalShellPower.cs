using System;
using System.Threading.Tasks;
using Godot;
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
    private const decimal MaxHpDivisor = 5m;

    private bool _pendingBreak;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    public static int CalculateAmount(Creature creature)
    {
        return Math.Max(1, (int)Math.Ceiling(creature.MaxHp / MaxHpDivisor));
    }

    public bool TryAbsorb(decimal amount)
    {
        if (amount <= 0m || Amount <= 0 || _pendingBreak)
        {
            return false;
        }

        int shellToSpend = Math.Min(Amount, Math.Max(1, (int)Math.Ceiling(amount)));
        SetAmount(Amount - shellToSpend);
        Flash();
        _pendingBreak = Amount <= 0;
        return true;
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
        if (!Owner.IsDead)
        {
            await PowerCmd.Apply<BreakPower>(
                choiceContext,
                Owner,
                BreakAmount,
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

            await CreatureCmd.Stun(Owner);
        }

        await PowerCmd.Remove(this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingBreak = false;
    }
}
