using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class SwallowReversalHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/swallow_reversal_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DamageVar[] { new DamageVar(5m, ValueProp.Move) };

    protected override bool IsPlayable => CanUseDualSwordsCard();

    public SwallowReversalHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != this || CanUseDualSwordsCard();
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await CardPileCmd.Draw(choiceContext, 1m, Owner);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }

    private bool CanUseDualSwordsCard()
    {
        return Owner?.Creature?.HasPower<ReptureDualSwordsPower>() == true ||
               Owner?.Creature?.HasPower<MagicSwordModePower>() == true ||
               Owner?.Creature?.HasPower<CrimsonBladeFlowPower>() == true;
    }
}
