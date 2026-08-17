using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class RecallHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/recall_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public RecallHbrgoSaki()
        : base(
            1,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        CardModel[] choices = CombatManager.Instance.History
            .CardPlaysFinished
            .Where(entry => entry.CardPlay.Card.Owner == Owner)
            .Where(entry => entry.CardPlay.IsFirstInSeries)
            .Select(entry => entry.CardPlay.Card)
            .Where(card => card is not RecallHbrgoSaki)
            .GroupBy(card => card.Id)
            .Select(group => CreateOwnedCopy(group.Last()))
            .ToArray();
        if (choices.Length == 0)
        {
            return;
        }

        CardModel selected = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                choices,
                Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 1)))
            .FirstOrDefault();
        foreach (CardModel choice in choices)
        {
            if (choice != selected)
            {
                DualSwordsCopyHelper.Clear(choice);
                choice.RemoveFromState();
            }
        }

        if (selected != null)
        {
            await CardPileCmd.AddGeneratedCardToCombat(
                selected,
                PileType.Hand,
                Owner);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    private CardModel CreateOwnedCopy(CardModel source)
    {
        CardModel copy = CardModel.FromSerializable(
            source.ToSerializable());
        Owner.Creature.CombatState.AddCard(copy, Owner);
        DualSwordsCopyHelper.RecordCopy(copy);
        copy.AfterCreated();
        return copy;
    }
}
