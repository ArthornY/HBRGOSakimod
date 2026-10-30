using HBRGOSakiMod.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_DETONATION")]
public sealed class HotaruDetonation : HotaruCannonCard
{
    public HotaruDetonation() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<VulnerablePower>(1m),
            new PowerVar<FragilePower>(1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<VulnerablePower>(),
            HoverTipFactory.FromPower<FragilePower>()
        };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await HbrPercentDebuffApplication.ApplyDefenseDown(choiceContext, cardPlay.Target,
            DynamicVars["VulnerablePower"].BaseValue, Owner.Creature, this);
        if (cardPlay.Target.IsAlive)
            await PowerCmd.Apply<FragilePower>(choiceContext, cardPlay.Target,
                DynamicVars["FragilePower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["VulnerablePower"].UpgradeValueBy(1m);
        DynamicVars["FragilePower"].UpgradeValueBy(1m);
    }
}

[CustomID("HOTARU_RIFT_LIGHT")]
public sealed class HotaruRiftLight : HotaruCannonCard
{
    public HotaruRiftLight() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(350m, ValueProp.Move, divisor: 3m), new CardsVar(1) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<FragilePower>() };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        // A debuff added by this hit cannot retroactively satisfy its draw condition.
        bool wasFragile = cardPlay.Target.HasPower<FragilePower>();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (wasFragile)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(50m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

[CustomID("HOTARU_KNOCK")]
public sealed class HotaruKnock : HotaruCannonCard
{
    public HotaruKnock() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(700m, ValueProp.Move, divisor: 3m),
            HbrSkillVars.DamageBonus("BreakDamage", 400m, divisor: 3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<BreakPower>() };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal damage = DynamicVars.Damage.BaseValue;
        if (cardPlay.Target.HasPower<BreakPower>())
            damage += DynamicVars["BreakDamage"].BaseValue;
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(200m, divisor: 3m);
}

[CustomID("HOTARU_REKINDLE")]
public sealed class HotaruRekindle : HotaruCannonCard
{
    public HotaruRekindle() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(100m, ValueProp.Move), new EnergyVar(1) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        RollGlossaryCapability roll = this.Capability<RollGlossaryCapability>();
        roll?.EnsureCurrentRollFor(this);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (RollKeywordRules.IsCriticalRoll(Owner, roll?.GetCurrentRollDetailsFor(this)))
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(50m);
}

[CustomID("HOTARU_WARM_PALMS")]
public sealed class HotaruWarmPalms : HotaruCannonCard
{
    public HotaruWarmPalms() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Block(120m, ValueProp.Move), new PowerVar<ChargePower>(1m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.Static(StaticHoverTip.Block),
            HoverTipFactory.FromPower<ChargePower>()
        };

    public override bool GainsBlock => true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<ChargePower>(choiceContext, Owner.Creature,
            DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeBlockByPercent(60m);
}

[CustomID("HOTARU_COOLING_CYCLE")]
public sealed class HotaruCoolingCycle : HotaruCannonCard
{
    public HotaruCoolingCycle() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new CardsVar(1), new PowerVar<ChargePower>(1m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<ChargePower>() };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        await PowerCmd.Apply<ChargePower>(choiceContext, Owner.Creature,
            DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
