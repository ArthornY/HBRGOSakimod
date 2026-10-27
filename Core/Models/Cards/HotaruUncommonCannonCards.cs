using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_FINGERTIP_HABIT")]
public sealed class HotaruFingertipHabit : HotaruCannonCard
{
    public HotaruFingertipHabit() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.CannonGrowthBonus("BaseGain", 100m, divisor: 3m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruFingertipHabitPower>() };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        await PowerCmd.Apply<HotaruFingertipHabitPower>(context, Owner.Creature, DynamicVars["BaseGain"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["BaseGain"].UpgradeCannonGrowthByPercent(50m, divisor: 3m);
}

[CustomID("HOTARU_LIGHT_POINTS")]
public sealed class HotaruLightPoints : HotaruCannonCard
{
    public HotaruLightPoints() : base(1, CardType.Skill, CardRarity.Uncommon, HotaruTargetTypes.OwnedCannon) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("ResistanceLoss", 1m) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!Millsage.IsOwnedCannon(play.Target, Owner)) return;
        HotaruUncommonCannonHelper.QueueLight(play.Target, DynamicVars["ResistanceLoss"].IntValue);
        await Owner.GetRelic<Millsage>().TriggerAllEffects(play.Target, context, this, play);
    }
    protected override void OnUpgrade() => DynamicVars["ResistanceLoss"].UpgradeValueBy(1m);
}

[CustomID("HOTARU_AWAIT_REPLY")]
public sealed class HotaruAwaitReply : HotaruCannonCard
{
    public HotaruAwaitReply() : base(1, CardType.Skill, CardRarity.Uncommon, HotaruTargetTypes.OwnedCannon) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.Block(160m, ValueProp.Move), new CardsVar(2) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override bool GainsBlock => true;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        HotaruUncommonCannonHelper.QueueReply(play.Target, DynamicVars.Block.BaseValue, DynamicVars.Cards.IntValue);
        return Task.CompletedTask;
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeBlockByPercent(100m);
}

[CustomID("HOTARU_WAKING_COMMAND")]
public sealed class HotaruWakingCommand : HotaruCannonCard
{
    public HotaruWakingCommand() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("Activations", 1m), new EnergyVar(1) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruWakingCommandPower>() };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        await HotaruWakingCommandPower.Apply(context, Owner.Creature, DynamicVars.Energy.IntValue, DynamicVars["Activations"].IntValue, this);
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

[CustomID("HOTARU_CROSS_FRACTURE")]
public sealed class HotaruCrossFracture : HotaruCannonCard
{
    public HotaruCrossFracture() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.Damage(250m, ValueProp.Move) };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool broken = play.Target.HasPower<BreakPower>();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (broken) await HotaruCommonCannonHelper.SelectAndTrigger(context, this, play);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(350m, divisor: 3m);
}

[CustomID("HOTARU_ADD_LIGHT")]
public sealed class HotaruAddLight : HotaruCannonCard
{
    public HotaruAddLight() : base(1, CardType.Skill, CardRarity.Uncommon, HotaruTargetTypes.OwnedCannon) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.CannonGrowthBonus("BaseGain", 100m, divisor: 3m), HbrSkillVars.CannonGrowthBonus("CriticalGain", 100m, divisor: 3m) };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var roll = this.Capability<RollGlossaryCapability>();
        roll?.EnsureCurrentRollFor(this);
        decimal gain = DynamicVars["BaseGain"].BaseValue;
        if (RollKeywordRules.IsCriticalRoll(Owner, roll?.GetCurrentRollDetailsFor(this))) gain += DynamicVars["CriticalGain"].BaseValue;
        await HotaruUncommonCannonHelper.Grow(context, play.Target, gain, this, play);
    }
    protected override void OnUpgrade() => DynamicVars["BaseGain"].UpgradeCannonGrowthByPercent(50m, divisor: 3m);
}

[CustomID("HOTARU_FIRST_BEATS")]
public sealed class HotaruFirstBeats : HotaruCannonCard
{
    public HotaruFirstBeats() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.CannonGrowthBonus("BaseGain", 50m, divisor: 3m) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruFirstBeatsPower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruFirstBeatsPower.Apply(context, Owner.Creature, DynamicVars["BaseGain"].IntValue, this);
    protected override void OnUpgrade() => DynamicVars["BaseGain"].UpgradeCannonGrowthByPercent(50m, divisor: 3m);
}

[CustomID("HOTARU_TRUST_LIGHT")]
public sealed class HotaruTrustLight : HotaruCannonCard
{
    public HotaruTrustLight() : base(1, CardType.Skill, CardRarity.Uncommon, HotaruTargetTypes.OwnedCannon) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("RollBonus", 30m) };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        HotaruUncommonCannonHelper.QueueRoll(play.Target, DynamicVars["RollBonus"].IntValue);
        return Task.CompletedTask;
    }
    protected override void OnUpgrade() => DynamicVars["RollBonus"].UpgradeValueBy(10m);
}
