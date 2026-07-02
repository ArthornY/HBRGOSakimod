using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class FeintHbrgoSaki : CardModel
{
    public override string PortraitPath => MissingPortraitPath;

    public override string BetaPortraitPath => MissingPortraitPath;

    public FeintHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false)
    {
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card != this)
        {
            return true;
        }

        MagicSwordModePower magicSwordMode = Owner.Creature.GetPower<MagicSwordModePower>();
        SwordheartPower swordheart = Owner.Creature.GetPower<SwordheartPower>();
        return magicSwordMode != null
            && swordheart != null
            && swordheart.Amount >= SwordheartClarityPower.GetSwordheartConsumptionAmount(Owner.Creature, 1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SwordheartPower swordheart = Owner.Creature.GetPower<SwordheartPower>();
        MagicSwordModePower magicSwordMode = Owner.Creature.GetPower<MagicSwordModePower>();
        if (magicSwordMode == null
            || swordheart == null
            || swordheart.Amount < SwordheartClarityPower.GetSwordheartConsumptionAmount(Owner.Creature, 1m))
        {
            return;
        }

        await PowerCmd.Apply<FeintMarkPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
        await CardPileCmd.Draw(choiceContext, 2m, Owner);
        await magicSwordMode.SpendSwordheart(choiceContext, 1m, this);
    }

    protected override PileType GetResultPileTypeForCardPlay()
    {
        return Owner.Creature.HasPower<MagicSwordModePower>() ? base.GetResultPileTypeForCardPlay() : PileType.None;
    }
}
