using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ReapHbrgoSaki : CardModel
{
    private const string _counterGainVar = "CounterGain";

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/reap_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(_counterGainVar, 10m)
        };

    public ReapHbrgoSaki()
        : base(
            2,
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

        RestorationMoment restorationMoment =
            Owner.GetRelic<RestorationMoment>();
        decimal damage =
            (restorationMoment?.Counter ?? 0) / 3m;
        bool targetWasAlive = cardPlay.Target.IsAlive;

        await DamageCmd.Attack(damage)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (targetWasAlive &&
            !cardPlay.Target.IsAlive)
        {
            restorationMoment?.GainCounter(
                (int)DynamicVars[_counterGainVar].BaseValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[_counterGainVar]
            .UpgradeValueBy(8m);
    }
}
