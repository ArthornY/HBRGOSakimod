using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class TranscendSelfHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/transcend_self_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantoms(Owner).Any();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("StrengthDivisor", 2m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>(),
            HoverTipFactory.FromPower<PhantomDamageInheritancePower>(),
            HoverTipFactory.FromPower<StrengthPower>()
        };

    public TranscendSelfHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        var phantom = PhantomHelper
            .GetLivingPhantoms(Owner)
            .FirstOrDefault();
        if (phantom == null)
        {
            return;
        }

        decimal damageBonus = DamageAmplificationLimits.Clamp(Math.Floor(
            PhantomHelper.GetDamageBonusPercent(phantom)));
        if (damageBonus > 0)
        {
            await PowerCmd.Apply<PhantomDamageInheritancePower>(
                choiceContext,
                Owner.Creature,
                damageBonus,
                Owner.Creature,
                null);
        }

        int phantomStrength = Math.Max(
            0,
            phantom.GetPower<StrengthPower>()?.Amount ?? 0);
        int divisor = Math.Max(
            1,
            DynamicVars["StrengthDivisor"].IntValue);
        int inheritedStrength = phantomStrength / divisor;
        if (inheritedStrength > 0)
        {
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                Owner.Creature,
                inheritedStrength,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrengthDivisor"].UpgradeValueBy(-1m);
    }
}
