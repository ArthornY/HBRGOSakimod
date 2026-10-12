using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ChihayaClassmateHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/chihaya_classmate_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            yield return HoverTipFactory.FromPower<NormalStatePower>();
            yield return HoverTipFactory.FromPower<ReptureBowPower>();
            yield return HoverTipFactory.FromPower<ReptureDualSwordsPower>();
            if (!IsMutable || Owner?.Character is not HbrgoSaki31O)
            {
                yield return HoverTipFactory.FromPower<RaptureScythePower>();
            }
        }
    }

    public ChihayaClassmateHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel[] modeChoices = Owner.Character is HbrgoSaki31O
            ? new CardModel[]
            {
                CombatState.CreateCard<ReptureBowChoiceHbrgoSaki>(Owner),
                CombatState.CreateCard<ReptureDualSwordsChoiceHbrgoSaki>(Owner)
            }
            : new CardModel[]
            {
                CombatState.CreateCard<ReptureBowChoiceHbrgoSaki>(Owner),
                CombatState.CreateCard<ReptureDualSwordsChoiceHbrgoSaki>(Owner),
                CombatState.CreateCard<RaptureScytheChoiceHbrgoSaki>(Owner)
            };

        CardModel selectedMode = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            modeChoices,
            Owner);

        if (selectedMode == null)
        {
            return;
        }

        await EnterSelectedRepture(choiceContext, selectedMode);
        await DrawModeRestrictedCard(choiceContext, selectedMode);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }

    private async Task EnterSelectedRepture(PlayerChoiceContext choiceContext, CardModel selectedMode)
    {
        using (ModeTransitionHelper.SuppressDualSwordsOnModeExit())
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

            if (Owner.Creature.HasPower<RaptureScythePower>())
            {
                await PowerCmd.Remove(Owner.Creature.GetPower<RaptureScythePower>());
            }
        }

        if (selectedMode is ReptureDualSwordsChoiceHbrgoSaki)
        {
            await PowerCmd.Apply<ReptureDualSwordsPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            return;
        }

        if (selectedMode is RaptureScytheChoiceHbrgoSaki)
        {
            await PowerCmd.Apply<RaptureScythePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            return;
        }

        await PowerCmd.Apply<ReptureBowPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    private async Task DrawModeRestrictedCard(PlayerChoiceContext choiceContext, CardModel selectedMode)
    {
        CardPile drawPile = Owner.PlayerCombatState.DrawPile;
        CardModel restrictedCard = drawPile.Cards.FirstOrDefault(card =>
            IsRestrictedToSelectedMode(card, selectedMode));
        if (restrictedCard == null)
        {
            return;
        }

        await CardPileCmd.Add(
            restrictedCard,
            drawPile,
            CardPilePosition.Top,
            this,
            skipVisuals: true);
        await CardPileCmd.Draw(choiceContext, Owner);
    }

    private static bool IsRestrictedToSelectedMode(CardModel card, CardModel selectedMode)
    {
        return selectedMode switch
        {
            ReptureBowChoiceHbrgoSaki =>
                ModeTransitionHelper.IsBowRestrictedCard(card),
            ReptureDualSwordsChoiceHbrgoSaki =>
                ModeTransitionHelper.IsDualSwordsRestrictedCard(card),
            RaptureScytheChoiceHbrgoSaki =>
                ModeTransitionHelper.IsScytheRestrictedCard(card),
            _ => false
        };
    }

}

public sealed class AnokoHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/anoko_hbrgo_saki.jpg");

    public override string BetaPortraitPath => PortraitPath;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<BufferPower>(2m),
            new PowerVar<StrongestShieldPower>(1m),
            new PowerVar<SwordheartClarityPower>(1m),
            new PowerVar<ProtagonistPrivilegePower>(1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<BufferPower>(),
            HoverTipFactory.FromPower<StrongestShieldPower>(),
            HoverTipFactory.FromPower<SwordheartClarityPower>(),
            HoverTipFactory.FromPower<SwordheartPower>(),
            HoverTipFactory.FromPower<ProtagonistPrivilegePower>(),
            HoverTipFactory.FromCard<MeditationHbrgoSaki>()
        };

    public AnokoHbrgoSaki()
        : base(0, CardType.Power, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<BufferPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["BufferPower"].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<StrongestShieldPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["StrongestShieldPower"].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<SwordheartClarityPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["SwordheartClarityPower"].BaseValue,
            Owner.Creature,
            this);

        int swordheartToGain = Math.Max(
            0,
            SwordheartPower.GetMaxAmount(Owner.Creature) -
            (Owner.Creature.GetPower<SwordheartPower>()?.Amount ?? 0));
        if (swordheartToGain > 0)
        {
            MagicSwordModePower magicSwordMode =
                Owner.Creature.GetPower<MagicSwordModePower>();
            if (magicSwordMode != null)
            {
                await magicSwordMode
                    .GrantSwordheartIgnoringMagicSwordRestriction(
                        choiceContext,
                        swordheartToGain,
                        this);
            }
            else
            {
                await PowerCmd.Apply<SwordheartPower>(
                    choiceContext,
                    Owner.Creature,
                    swordheartToGain,
                    Owner.Creature,
                    this);
            }
        }

        await PowerCmd.Apply<ProtagonistPrivilegePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ProtagonistPrivilegePower"].BaseValue,
            Owner.Creature,
            this);

        MeditationHbrgoSaki meditation =
            CombatState.CreateCard<MeditationHbrgoSaki>(Owner);
        if (IsUpgraded || DeckVersion?.IsUpgraded == true)
        {
            CardCmd.Upgrade(meditation, CardPreviewStyle.None);
        }

        await CardPileCmd.AddGeneratedCardToCombat(
            meditation,
            PileType.Draw,
            Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
