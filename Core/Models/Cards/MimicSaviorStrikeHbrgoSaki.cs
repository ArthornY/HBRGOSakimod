using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MimicSaviorStrikeHbrgoSaki : CardModel
{
    private const int _stunRollThreshold = 75;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/mimic_savior_strike_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(9m, ValueProp.Move)
    };

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayDualSwordsRestrictedCard(
            Owner?.Creature);

    public MimicSaviorStrikeHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override bool ShouldPlay(
        CardModel card,
        AutoPlayType autoPlayType)
    {
        return card != this ||
            ModeTransitionHelper.CanPlayDualSwordsRestrictedCard(
                Owner?.Creature);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(
                ModeTransitionHelper.GetAdjustedEffectAmount(
                    this,
                    DynamicVars.Damage.BaseValue))
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        int roll = Owner.Relics
            .OfType<SoulOfAnchor>()
            .Select(soul => soul.GetCurrentRollFor(this))
            .DefaultIfEmpty(0)
            .Max();
        if (roll < _stunRollThreshold ||
            !cardPlay.Target.IsAlive)
        {
            return;
        }

        await CreatureCmd.Stun(cardPlay.Target);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        AddKeyword(CardKeyword.Retain);
    }
}
