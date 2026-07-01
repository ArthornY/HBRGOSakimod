using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ChihayaClassmateHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/chihaya_classmate_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<NormalStatePower>(),
        HoverTipFactory.FromPower<ReptureBowPower>(),
        HoverTipFactory.FromPower<ReptureDualSwordsPower>()
    };

    public ChihayaClassmateHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel selectedMode = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            new CardModel[]
            {
                CombatState.CreateCard<ReptureBowChoiceHbrgoSaki>(Owner),
                CombatState.CreateCard<ReptureDualSwordsChoiceHbrgoSaki>(Owner)
            },
            Owner);

        if (selectedMode == null)
        {
            return;
        }

        await EnterSelectedRepture(choiceContext, selectedMode);
        await ReplaceWithSwitchInHand(choiceContext);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }

    private async Task ReplaceWithSwitchInHand(PlayerChoiceContext choiceContext)
    {
        CardModel switchCard = CombatState.CreateCard<SwitchHbrgoSaki>(Owner);
        SyncUpgradeLevel(switchCard);

        await CardPileCmd.AddGeneratedCardToCombat(switchCard, PileType.Hand, Owner);
        await CardPileCmd.RemoveFromCombat(this);
    }

    private async Task EnterSelectedRepture(PlayerChoiceContext choiceContext, CardModel selectedMode)
    {
        if (Owner.Creature.HasPower<NormalStatePower>())
        {
            await PowerCmd.Remove(Owner.Creature.GetPower<NormalStatePower>());
        }

        if (Owner.Creature.HasPower<ReptureBowPower>())
        {
            await PowerCmd.Remove(Owner.Creature.GetPower<ReptureBowPower>());
        }

        if (Owner.Creature.HasPower<ReptureDualSwordsPower>())
        {
            await PowerCmd.Remove(Owner.Creature.GetPower<ReptureDualSwordsPower>());
        }

        if (selectedMode is ReptureDualSwordsChoiceHbrgoSaki)
        {
            await PowerCmd.Apply<ReptureDualSwordsPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            return;
        }

        await PowerCmd.Apply<ReptureBowPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    private void SyncUpgradeLevel(CardModel card)
    {
        while (card.CurrentUpgradeLevel < CurrentUpgradeLevel && card.IsUpgradable)
        {
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
        }
    }
}
