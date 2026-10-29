using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MimicIceFlowerThousandArrowsHbrgoSaki : CardModel
{
    private const string DamagePerBowCardVar = "DamagePerBowCard";
    private const string BowCardThresholdVar = "BowCardThreshold";
    private const string ThresholdVulnerableVar =
        "ThresholdVulnerablePower";
    private const string ThresholdFragileVar =
        "ThresholdFragilePower";
    private const string ThresholdWeakVar = "ThresholdWeakPower";

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/mimic_ice_flower_thousand_arrows_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(350m, ValueProp.Move),
        new PowerVar<VulnerablePower>(3m),
        new PowerVar<FragilePower>(3m),
        HbrSkillVars.DamageBonus(DamagePerBowCardVar, 100m, divisor: 3m),
        new IntVar(BowCardThresholdVar, 5m),
        new PowerVar<VulnerablePower>(ThresholdVulnerableVar, 3m),
        new PowerVar<FragilePower>(ThresholdFragileVar, 3m),
        new PowerVar<WeakPower>(ThresholdWeakVar, 0m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<FragilePower>(),
        HoverTipFactory.FromPower<WeakPower>()
    };

    public MimicIceFlowerThousandArrowsHbrgoSaki()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != this ||
               ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        int bowCardCount = ReptureBowPower.GetBowCardCount(
            Owner.Creature);
        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            DynamicVars.Damage.BaseValue +
            bowCardCount * DynamicVars[DamagePerBowCardVar].BaseValue);
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        Creature[] targets = CombatState
            .GetOpponentsOf(Owner.Creature)
            .Where(creature => creature.IsAlive)
            .ToArray();
        if (targets.Length == 0 || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            targets,
            ModeTransitionHelper.GetAdjustedEffectAmount(
                this,
                DynamicVars["VulnerablePower"].BaseValue),
            Owner.Creature,
            this);
        bool meetsThreshold = bowCardCount >= DynamicVars[BowCardThresholdVar].BaseValue;
        bool hbr = HbrBattleStatsHelper.IsEnabled(Owner);
        // One grant uses one modifier coefficient: 3 + 3 means B(6), not
        // two independent B(3) grants. Legacy retains its original two grants.
        decimal fragile = DynamicVars["FragilePower"].BaseValue +
            (hbr && meetsThreshold ? DynamicVars[ThresholdFragileVar].BaseValue : 0m);
        await PowerCmd.Apply<FragilePower>(
            choiceContext,
            targets,
            ModeTransitionHelper.GetAdjustedEffectAmount(
                this,
                fragile),
            Owner.Creature,
            this);

        if (!meetsThreshold)
        {
            return;
        }

        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            targets,
            ModeTransitionHelper.GetAdjustedEffectAmount(
                this,
                DynamicVars[ThresholdVulnerableVar].BaseValue),
            Owner.Creature,
            this);
        if (!hbr)
            await PowerCmd.Apply<FragilePower>(
                choiceContext,
                targets,
                ModeTransitionHelper.GetAdjustedEffectAmount(
                    this,
                    DynamicVars[ThresholdFragileVar].BaseValue),
                Owner.Creature,
                this);

        decimal weak = DynamicVars[ThresholdWeakVar].BaseValue;
        if (weak > 0m)
        {
            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                targets,
                ModeTransitionHelper.GetAdjustedEffectAmount(
                    this,
                    weak),
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["VulnerablePower"].UpgradeValueBy(2m);
        DynamicVars["FragilePower"].UpgradeValueBy(2m);
        DynamicVars[DamagePerBowCardVar].UpgradeDamageByPercent(50m, divisor: 3m);
        DynamicVars[BowCardThresholdVar].UpgradeValueBy(-2m);
        DynamicVars[ThresholdVulnerableVar].UpgradeValueBy(1m);
        DynamicVars[ThresholdFragileVar].UpgradeValueBy(1m);
        DynamicVars[ThresholdWeakVar].UpgradeValueBy(2m);
    }
}
