using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class FatePointsHbrgoSaki : CardModel
{
    private const decimal _criticalDamage = 77m;
    private const decimal _criticalGold = 77m;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/fate_points_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[]
        {
            CardKeyword.Ethereal,
            CardKeyword.Exhaust
        };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new DamageVar(1m, ValueProp.Move),
            new PowerVar<FragilePower>(5m),
            new PowerVar<VulnerablePower>(5m),
            new PowerVar<WeakPower>(5m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<FragilePower>(),
            HoverTipFactory.FromPower<VulnerablePower>(),
            HoverTipFactory.FromPower<WeakPower>()
        };

    public FatePointsHbrgoSaki()
        : base(
            3,
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

        RollGlossaryCapability rollCapability =
            this.Capability<RollGlossaryCapability>();
        int roll = rollCapability?.EnsureCurrentRollFor(this) ?? 0;
        bool isCritical =
            rollCapability?.GetRollTier(roll) ==
            SoulRollTier.Critical;
        decimal damage = isCritical
            ? _criticalDamage
            : DynamicVars.Damage.BaseValue;

        await DamageCmd.Attack(damage)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (!isCritical)
        {
            return;
        }

        if (cardPlay.Target.IsAlive)
        {
            await PowerCmd.Apply<FragilePower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars["FragilePower"].BaseValue,
                Owner.Creature,
                this);
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars["VulnerablePower"].BaseValue,
                Owner.Creature,
                this);
            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars["WeakPower"].BaseValue,
                Owner.Creature,
                this);
        }

        Owner.GetRelic<Overclock>()?
            .GainCharge(Overclock.ChargePerLevel);
        if (IsUpgraded)
        {
            await PlayerCmd.GainGold(
                _criticalGold,
                Owner);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }
}
