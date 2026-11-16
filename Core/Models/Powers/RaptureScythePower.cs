using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class RaptureScythePower : PowerModel
{
    private const int WeakenedCounterLossPerTurn = 2;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("E55353");

    public override async Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        using (ModeTransitionHelper.SuppressDualSwordsOnModeExit())
        {
            await PowerCmd.Remove(Owner.GetPower<NormalStatePower>());
            await PowerCmd.Remove(Owner.GetPower<ReptureBowPower>());
            await PowerCmd.Remove(Owner.GetPower<ReptureDualSwordsPower>());
        }

        await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
        await ModeTransitionHelper.RecordModeSwitch(Owner);
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay?.Card?.Owner?.Creature != Owner ||
            Owner.CombatState == null)
        {
            return;
        }

        RedCrimson redCrimson = Owner.CombatState.Allies
            .Where(creature =>
                creature.IsAlive &&
                creature.PetOwner == Owner.Player)
            .Select(creature => creature.Monster)
            .OfType<RedCrimson>()
            .FirstOrDefault();
        if (redCrimson == null)
        {
            return;
        }

        Flash();
        await redCrimson.AddActionGauge(
            choiceContext,
            1);
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side ||
            !participants.Contains(Owner) ||
            !ModeTransitionHelper.IsWeaponModeWeakened(Owner))
        {
            return Task.CompletedTask;
        }

        RestorationMoment restorationMoment =
            Owner.Player?.GetRelic<RestorationMoment>();
        if (restorationMoment == null || restorationMoment.Counter <= 0)
        {
            return Task.CompletedTask;
        }

        Flash();
        restorationMoment.SpendCounter(WeakenedCounterLossPerTurn);
        return Task.CompletedTask;
    }
}
