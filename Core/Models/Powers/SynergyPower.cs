using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SynergyPower : PowerModel
{
    private const int CardsPerTrigger = 3;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("6ED7FF");

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay?.Card?.Owner?.Creature != Owner)
        {
            return;
        }

        ReptureCardMode mode =
            ModeTransitionHelper.GetCardMode(cardPlay.Card);
        switch (mode)
        {
            case ReptureCardMode.Bow:
                await AdvanceCounter<BowCounterPower>(
                    choiceContext,
                    cardPlay.Card,
                    ReptureCardMode.DualSwords);
                break;
            case ReptureCardMode.DualSwords
                when !RollKeywordRules.IsCopiedCard(cardPlay.Card):
                await AdvanceCounter<DualSwordsCounterPower>(
                    choiceContext,
                    cardPlay.Card,
                    ReptureCardMode.Bow);
                break;
        }
    }

    private async Task AdvanceCounter<TCounter>(
        PlayerChoiceContext choiceContext,
        CardModel cardSource,
        ReptureCardMode modeToDraw)
        where TCounter : PowerModel
    {
        TCounter counter = await PowerCmd.Apply<TCounter>(
            choiceContext,
            Owner,
            1m,
            Owner,
            cardSource);
        if (counter == null || counter.Amount < CardsPerTrigger)
        {
            return;
        }

        Flash();
        await PowerCmd.Remove(counter);
        await ModeTransitionHelper.DrawModeRestrictedCard(
            choiceContext,
            Owner.Player,
            modeToDraw,
            this);
    }
}
