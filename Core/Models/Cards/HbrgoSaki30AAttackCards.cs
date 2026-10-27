using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ShelterTheAfterimageHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/shelter_the_afterimage_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(100m, ValueProp.Move),
        new DynamicVar("Recovery", 4m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<PhantomPower>(),
        HoverTipFactory.FromPower<PhantomHighDimensionalShellPower>()
    };

    public ShelterTheAfterimageHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await PhantomHelper.RestoreDp(choiceContext,
            PhantomHelper.GetLivingPhantom(Owner), DynamicVars["Recovery"].IntValue, this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(50m);
}

public sealed class PainTransmissionHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override bool IsPlayable => PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(200m, ValueProp.Move),
        new HpLossVar(3m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<PhantomPower>() };

    public PainTransmissionHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom == null)
        {
            return;
        }

        await PhantomHelper.LoseHp(phantom, DynamicVars.HpLoss.BaseValue);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(200m, divisor: 3m);
}

public sealed class StandInFrontHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/stand_in_front_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(250m, ValueProp.Move, divisor: 3m),
        HbrSkillVars.Block(100m, ValueProp.Move)
    };

    public StandInFrontHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_flying_slash")
            .Execute(choiceContext);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(100m, divisor: 3m);
        DynamicVars.Block.UpgradeBlockByPercent(40m);
    }
}

public sealed class ActInYourSteadHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(200m, ValueProp.Move, divisor: 3m),
        HbrSkillVars.DamageBonus("BonusDamage", 50m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<PhantomPower>() };

    public ActInYourSteadHbrgoSaki()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        bool nonAttack = phantom?.Monster?.NextMove != null &&
            !PhantomHelper.HasCurrentAttackIntent(phantom);
        decimal bonus = nonAttack ? DynamicVars["BonusDamage"].BaseValue : 0m;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + bonus)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (nonAttack && phantom?.IsAlive == true &&
            !CombatManager.Instance.IsOverOrEnding &&
            PhantomHelper.GetLivingPhantom(Owner) == phantom)
        {
            await PhantomHelper.PerformCurrentIntent(phantom);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(100m, divisor: 3m);
}

public sealed class ArcSweepHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/arc_sweep_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(250m, ValueProp.Move, divisor: 3m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.FromPower<PhantomNextAttackAllEnemiesPower>() };

    public ArcSweepHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom != null && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<PhantomNextAttackAllEnemiesPower>(choiceContext,
                phantom, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(50m);
}

public sealed class LoneAssaultHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/lone_assault_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(400m, ValueProp.Move, divisor: 3m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<PhantomPower>() };

    public LoneAssaultHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (PhantomHelper.GetLivingPhantom(Owner) == null &&
            Owner.GetRelic<MomentOfReign>() is { } relic)
        {
            await relic.AdvancePhantomRevive();
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(50m);
}

public sealed class GatherWarmthHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/gather_warmth_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(400m, ValueProp.Move, divisor: 3m),
        new PowerVar<ChargePower>(3m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<PhantomPower>(),
        HoverTipFactory.FromPower<ChargePower>()
    };

    public GatherWarmthHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (Owner.GetRelic<MomentOfReign>()?.HasConsumedRosterThisTurn == true)
        {
            await PowerCmd.Apply<ChargePower>(choiceContext, Owner.Creature,
                DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(50m);
}
