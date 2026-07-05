using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class SwitchHbrgoSaki : CardModel
{
    public override string PortraitPath => MissingPortraitPath;

    public override string BetaPortraitPath => MissingPortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<ReptureBowPower>(),
        HoverTipFactory.FromPower<ReptureDualSwordsPower>(),
        HoverTipFactory.FromPower<CrimsonBladeFlowPower>()
    };

    public SwitchHbrgoSaki()
        : base(3, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool switchToDualSwords = Owner.Creature.HasPower<ReptureBowPower>();
        await RemoveAllModes();

        if (switchToDualSwords)
        {
            await PowerCmd.Apply<ReptureDualSwordsPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            return;
        }

        await PowerCmd.Apply<ReptureBowPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    public void ReduceCostUntilPlayed()
    {
        EnergyCost.AddUntilPlayed(-1);
    }

    public void MakeNextPlayFree()
    {
        EnergyCost.AddUntilPlayed(-99);
    }

    public override Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner && Pile?.Type == PileType.Hand)
        {
            ReduceCostUntilPlayed();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && IsModeRestrictedCard(cardPlay.Card))
        {
            ReduceCostUntilPlayed();
        }

        return Task.CompletedTask;
    }

    protected override PileType GetResultPileTypeForCardPlay()
    {
        return Owner.Creature.HasPower<MagicSwordModePower>() || Owner.Creature.HasPower<CrimsonBladeFlowPower>()
            ? base.GetResultPileTypeForCardPlay()
            : PileType.Hand;
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    private async Task RemoveAllModes()
    {
        using (ModeTransitionHelper.SuppressDualSwordsOnModeExit())
        {
            await PowerCmd.Remove(Owner.Creature.GetPower<NormalStatePower>());
            await PowerCmd.Remove(Owner.Creature.GetPower<ReptureBowPower>());
            await PowerCmd.Remove(Owner.Creature.GetPower<ReptureDualSwordsPower>());
            await PowerCmd.Remove(Owner.Creature.GetPower<CrimsonBladeFlowPower>());
        }
    }

    private static bool IsModeRestrictedCard(CardModel card)
    {
        return card is ArrowRainHbrgoSaki
            or CrossSlashHbrgoSaki
            or SwallowReversalHbrgoSaki
            or SlideStepHbrgoSaki
            or BladeDanceHbrgoSaki
            or StrongShotHbrgoSaki;
    }
}
