using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class DestinedArrowHbrgoSaki : CardModel
{
    private const int ChoiceCount = 3;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/destined_arrow_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DamageVar[]
        {
            HbrSkillVars.Damage(350m, ValueProp.Move, divisor: 3m)
        };

    public DestinedArrowHbrgoSaki()
        : base(
            1,
            CardType.Attack,
            CardRarity.Common,
            TargetType.AllEnemies)
    {
    }

    public override bool ShouldPlay(
        CardModel card,
        AutoPlayType autoPlayType)
    {
        return card != this ||
            ModeTransitionHelper.CanPlayBowRestrictedCard(
                Owner?.Creature);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        CardModel[] choices = CardFactory.GetDistinctForCombat(
                Owner,
                Owner.Character.CardPool
                    .GetUnlockedCards(
                        Owner.UnlockState,
                        Owner.RunState.CardMultiplayerConstraint)
                    .Where(card =>
                        card is not DestinedArrowHbrgoSaki &&
                        ModeTransitionHelper.IsBowRestrictedCard(card)),
                ChoiceCount,
                Owner.RunState.Rng.CombatCardGeneration)
            .ToArray();

        if (IsUpgraded)
        {
            CardCmd.Upgrade(choices, CardPreviewStyle.None);
        }

        CardModel selected =
            await CardSelectCmd.FromChooseACardScreen(
                choiceContext,
                choices,
                Owner);
        if (selected != null)
        {
            await CardPileCmd.AddGeneratedCardToCombat(
                selected,
                PileType.Hand,
                Owner);
        }

        if (CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            DynamicVars.Damage.BaseValue);
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
    }
}
