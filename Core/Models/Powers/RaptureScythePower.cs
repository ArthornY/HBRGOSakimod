using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class RaptureScythePower : PowerModel
{
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
}
