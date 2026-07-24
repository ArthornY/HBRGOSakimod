using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class FeintHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/feint_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public FeintHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false)
    {
    }

    protected override bool IsPlayable =>
        Owner?.Creature?.HasPower<MagicSwordModePower>() == true &&
        CombatModeHelper.HasEnemyAttackIntent(Owner?.Creature);

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card != this)
        {
            return true;
        }

        return IsPlayable;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsPlayable)
        {
            return;
        }

        await PowerCmd.Apply<FeintMarkPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
        if (CombatModeHelper.IsMultiplayer(Owner.Creature))
        {
            await PowerCmd.Apply<FeintDamageRedirectPower>(
                choiceContext,
                Owner.Creature,
                1m,
                Owner.Creature,
                this);
        }

        await CardPileCmd.Draw(choiceContext, 2m, Owner);
    }

    protected override PileType GetResultPileTypeForCardPlay()
    {
        return Owner.Creature.HasPower<MagicSwordModePower>() ? base.GetResultPileTypeForCardPlay() : PileType.None;
    }
}
