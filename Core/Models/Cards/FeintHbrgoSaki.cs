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

        SwordheartPower swordheart = Owner.Creature.GetPower<SwordheartPower>();
        return Owner.Creature.HasPower<MagicSwordModePower>() && swordheart != null && swordheart.Amount > 0;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SwordheartPower swordheart = Owner.Creature.GetPower<SwordheartPower>();
        if (!Owner.Creature.HasPower<MagicSwordModePower>() || swordheart == null || swordheart.Amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<FeintMarkPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
        await CardPileCmd.Draw(choiceContext, 2m, Owner);
        await PowerCmd.ModifyAmount(choiceContext, swordheart, -1m, Owner.Creature, this);
    }

    protected override PileType GetResultPileTypeForCardPlay()
    {
        return Owner.Creature.HasPower<MagicSwordModePower>() ? base.GetResultPileTypeForCardPlay() : PileType.None;
    }
}
