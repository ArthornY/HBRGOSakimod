using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ExtraGaugeStrikeHbrgoSaki : CardModel
{
    public override IEnumerable<CardKeyword>
        CanonicalKeywords =>
            new[] { CardKeyword.Exhaust };

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/extra_gauge_strike_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayScytheRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DamageVar[]
        {
            new DamageVar(6m, ValueProp.Move)
        };

    public ExtraGaugeStrikeHbrgoSaki()
        : base(
            1,
            CardType.Attack,
            CardRarity.Common,
            TargetType.AnyEnemy)
    {
    }

    public override bool ShouldPlay(
        CardModel card,
        AutoPlayType autoPlayType)
    {
        return card != this ||
            ModeTransitionHelper.CanPlayScytheRestrictedCard(
                Owner?.Creature);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        await DamageCmd.Attack(
                ModeTransitionHelper.GetAdjustedEffectAmount(
                    this,
                    DynamicVars.Damage.BaseValue))
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        RedCrimson redCrimson = Owner.Creature.CombatState?.Allies
            .Where(creature =>
                creature.IsAlive &&
                creature.PetOwner == Owner)
            .Select(creature => creature.Monster)
            .OfType<RedCrimson>()
            .FirstOrDefault();
        if (redCrimson != null)
        {
            await redCrimson.ActImmediately();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
