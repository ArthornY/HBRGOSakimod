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

public sealed class ChargeBurstHbrgoSaki : CardModel
{
    private const string _damagePerChargeVar =
        "DamagePerCharge";

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/charge_burst_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.DamageBonus(_damagePerChargeVar, 200m, divisor: 3m),
            new PowerVar<ChargePower>(1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<ChargePower>()
        };

    public ChargeBurstHbrgoSaki()
        : base(
            1,
            CardType.Attack,
            CardRarity.Rare,
            TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));

        if (!IsUpgraded)
        {
            await GainCharge(choiceContext);
        }

        ChargePower charge = Owner.Creature.GetPower<ChargePower>();
        decimal chargeAmount = charge?.Amount ?? 0m;
        decimal damage =
            chargeAmount *
            DynamicVars[_damagePerChargeVar].BaseValue;
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (IsUpgraded)
        {
            await GainCharge(choiceContext);
        }
    }

    private Task GainCharge(PlayerChoiceContext choiceContext)
    {
        return PowerCmd.Apply<ChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ChargePower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[_damagePerChargeVar].UpgradeDamageByPercent(50m, divisor: 3m);
    }
}
