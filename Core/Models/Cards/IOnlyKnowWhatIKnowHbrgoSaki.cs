using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class IOnlyKnowWhatIKnowHbrgoSaki : CardModel
{
    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/i_only_know_what_i_know_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[]
    {
        CardKeyword.Exhaust
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(3)
    };

    public IOnlyKnowWhatIKnowHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardPile discardPile =
            Owner.PlayerCombatState.DiscardPile;
        int selectCount = System.Math.Min(
            (int)DynamicVars.Cards.BaseValue,
            discardPile.Cards.Count);
        if (selectCount <= 0)
        {
            return;
        }

        // The native multi-select screen returns a set, not click order.
        // Select one at a time, leaving the originals in discard until confirmed.
        List<CardModel> selectedCards = new(selectCount);
        for (int selectionIndex = 0; selectionIndex < selectCount; selectionIndex++)
        {
            LocString prompt = new(
                "cards",
                "I_ONLY_KNOW_WHAT_I_KNOW_HBRGO_SAKI.selectionPrompt");
            prompt.Add("Index", selectionIndex + 1);
            prompt.Add("Total", selectCount);
            CardSelectorPrefs prefs = new(prompt, 1);
            CardModel selected = (await CardSelectCmd.FromCombatPile(
                choiceContext,
                discardPile,
                Owner,
                prefs,
                card => !selectedCards.Contains(card))).FirstOrDefault();
            if (selected == null)
            {
                break;
            }

            selectedCards.Add(selected);
        }

        await CardPileCmd.Add(
            selectedCards.AsEnumerable().Reverse(),
            PileType.Draw,
            CardPilePosition.Top,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(2m);
    }
}
