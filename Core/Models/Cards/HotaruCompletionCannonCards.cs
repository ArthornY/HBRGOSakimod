using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_VOICE_FROM_LIGHT")]
public sealed class HotaruVoiceFromLight : HotaruCannonCard
{
    public HotaruVoiceFromLight() : base(1, CardType.Skill, CardRarity.Uncommon, HotaruTargetTypes.OwnedCannon) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
        { new PowerVar<VulnerablePower>(1), new PowerVar<FragilePower>(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
        { HoverTipFactory.FromPower<VulnerablePower>(), HoverTipFactory.FromPower<FragilePower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        HotaruUncommonCannonHelper.QueueLightDebuffs(play.Target,
            DynamicVars["VulnerablePower"].IntValue, DynamicVars["FragilePower"].IntValue);
        return Task.CompletedTask;
    }
    protected override void OnUpgrade() => DynamicVars["FragilePower"].UpgradeValueBy(1);
}

[CustomID("HOTARU_STEP_UPWARD")]
public sealed class HotaruStepUpward : HotaruCannonCard
{
    public HotaruStepUpward() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
        { HbrSkillVars.Damage(400m, ValueProp.Move, divisor: 3m), HbrSkillVars.DamageBonus("ExtraActionDamage", 50m) };
    private decimal ExtraActions => IsMutable ? HotaruRareCannonHelper.ExecutedExtraActions(Owner) : 0m;
    private decimal CurrentDamage => DamageAmplificationLimits.AddSaturated(DynamicVars.Damage.BaseValue,
        DamageAmplificationLimits.MultiplySaturated(ExtraActions, DynamicVars["ExtraActionDamage"].BaseValue));
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("ExecutedExtraActions", ExtraActions);
        description.Add("CurrentDamage", CurrentDamage);
        description.Add("CurrentDamageSkill", HbrSkillCardDescription.FormatPercent(HbrSkillPower.DamagePercent(CurrentDamage)));
        HbrSkillCardDescription.AddDamageParameters(this, description, "CurrentDamage", CurrentDamage);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(CurrentDamage).FromCardCompat(this, play).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(200m, divisor: 3m);
}
