using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class HellfireBurnHbrgoSaki : ThirtyOneBGardenCard
{
    internal const int Flash = 0, KillerShot = 1, SelfEncouragement = 2, NoisyEntrustment = 3,
        MilitaryMerit = 4, BasesLoadedHomerun = 5, SunshineTrigger = 6;
    protected override int FlashEffectId => Flash;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(200m, divisor: 3m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrComboSmallPower>(),
      HoverTipFactory.FromPower<HbrComboLargePower>(), HoverTipFactory.FromPower<HbrMoralePower>() };
    public HellfireBurnHbrgoSaki() : base(1, CardType.Attack, TargetType.AllEnemies) { }
    internal decimal MeritPercent => ThirtyOneBPhysicalCardState.GetMerit(this);
    internal int BaseHitCount => HasUpgradeEffect(BasesLoadedHomerun) && Owner.Creature.GetPowerAmount<HbrMoralePower>() >= 5 ? 4 : 2;
    public override Task BeforeCombatStart() { ThirtyOneBPhysicalCardState.Reset(this); return Task.CompletedTask; }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("IchigoHits", IsMutable && Owner != null ? BaseHitCount : 2);
        text.Add("IchigoNoDpBonus", HasUpgradeEffect(NoisyEntrustment) ? 50 : 30);
        decimal basis = DynamicVars.Damage.BaseValue + HbrSkillPower.DamageUnits(MeritPercent);
        HbrSkillCardDescription.AddDamageParameters(this, text, "Damage", basis, previewUnits: basis);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (HasUpgradeEffect(SelfEncouragement))
        {
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, Owner.Creature, 30m, Owner.Creature, this);
            await HbrComboPower.Grant<HbrComboSmallPower>(context, Owner.Creature, Owner.Creature, this);
        }
        if (HasUpgradeEffect(NoisyEntrustment))
            await HbrComboPower.Grant<HbrComboLargePower>(context, Owner.Creature, Owner.Creature, this);
        var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue + HbrSkillPower.DamageUnits(MeritPercent))
            .FromCardCompat(this, play).TargetingAllOpponents(CombatState).WithHitCount(BaseHitCount)
            .WithHitFx("vfx/vfx_attack_slash");
        await attack.Execute(context);
        var hit = attack.Results.SelectMany(results => results).Select(result => result.Receiver)
            .Where(target => target != null && target.Side != Owner.Creature.Side).Distinct().ToArray();
        if (HasUpgradeEffect(MilitaryMerit)) ThirtyOneBPhysicalCardState.AddMerit(this, 10m * hit.Length);
        if (HasUpgradeEffect(SunshineTrigger))
            foreach (var dead in hit.Where(target => target.IsDead))
                await PowerCmd.Apply<HbrMoralePower>(context, Owner.Creature, 1m, Owner.Creature, this);
        if (HasUpgradeEffect(NoisyEntrustment))
            await PowerCmd.Apply<IchigoNextTurnEnergyPenaltyPower>(context, Owner.Creature, 1m, Owner.Creature, this);
    }
}
