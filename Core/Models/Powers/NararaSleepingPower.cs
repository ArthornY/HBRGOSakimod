using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NararaSleepingPower :
    PowerModel,
    IHighDimensionalShellObserver,
    IHighDimensionalShellBreakOverride
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    bool IHighDimensionalShellBreakOverride
        .ShouldSuppressDefaultHighDimensionalShellBreak =>
            Owner?.IsAlive == true && Amount > 0;

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        Owner?.GetPower<NararaPressurePower>()?.ResetPressure();
        Owner?.GetPower<NararaGiveYouPower>()?.RefreshCountdown();
        Owner?.GetPower<NararaOverworkPower>()?.RefreshCountdown();
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Amount <= 0 || !participants.Contains(Owner))
        {
            return;
        }

        Flash();
        await PowerCmd.Decrement(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner?.IsAlive == true &&
            oldOwner.Monster is NararaShibaBossMonster boss)
        {
            oldOwner.GetPower<NararaPressurePower>()?.ResetPressure();
            // Reset even if no cards were played during this sleep.
            oldOwner.GetPower<NararaGiveYouPower>()?.ResetAwakeCounters();
            oldOwner.GetPower<NararaOverworkPower>()?.RefreshCountdown();
            boss.ForceAwakeCycle();
        }

        return Task.CompletedTask;
    }

    public void OnHighDimensionalShellApplied(
        HighDimensionalShellPower shell)
    {
    }

    public void OnHighDimensionalShellAmountChanged(
        decimal previousAmount,
        decimal currentAmount,
        decimal maximumAmount)
    {
    }

    public async Task AfterHighDimensionalShellBreak(
        PlayerChoiceContext choiceContext)
    {
        // Only exhaustion of the last DP layer reaches this hook. Earlier
        // layers are finite reserves, not instant shell restoration by sleep.
        await PowerCmd.Remove(this);
    }

    Task IHighDimensionalShellBreakOverride
        .AfterHighDimensionalShellRemoved(
            PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

}
