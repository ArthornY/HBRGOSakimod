using HBRGOSakiMod.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MimicEvilEyeTidalSlashHbrgoSaki : CardModel
{
    private const string _destructionRatePercentVar =
        "DestructionRatePercent";

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/mimic_evil_eye_tidal_slash_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(700m, ValueProp.Move, divisor: 3m),
            new IntVar(_destructionRatePercentVar, 150m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<ChargePower>(),
            HoverTipFactory.FromPower<BreakPower>(),
            HoverTipFactory.FromPower<DestructionRatePower>()
        };

    public MimicEvilEyeTidalSlashHbrgoSaki()
        : base(
            2,
            CardType.Attack,
            CardRarity.Uncommon,
            TargetType.AnyEnemy)
    {
    }

    internal static decimal GetDestructionRateMultiplier(
        CardModel cardSource)
    {
        return cardSource is MimicEvilEyeTidalSlashHbrgoSaki card
            ? card.DynamicVars[_destructionRatePercentVar].BaseValue /
              100m
            : 1m;
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(250m, divisor: 3m);
        DynamicVars[_destructionRatePercentVar].UpgradeValueBy(50m);
    }
}
