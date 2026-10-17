using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_CONVERGING_FRAGMENTS")]
public sealed class HotaruConvergingFragments : HotaruCannonCard
{
    public HotaruConvergingFragments() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruConvergingFragmentsPower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<HotaruConvergingFragmentsPower>(context, Owner.Creature, DynamicVars.Energy.BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_TRAILING_SILENCE")]
public sealed class HotaruTrailingSilence : HotaruCannonCard
{
    public HotaruTrailingSilence() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8m, ValueProp.Move), new IntVar("Repeat", 2), new IntVar("CostReduction", 1)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruTrailingSilencePower>() };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash")
            .WithHitCount(DynamicVars["Repeat"].IntValue).Execute(context);
        await HotaruTurnLedgerPower.Grant<HotaruTrailingSilencePower>(context, Owner.Creature,
            DynamicVars["CostReduction"].IntValue, 1, this);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

[CustomID("HOTARU_BREATHING_RHYTHM")]
public sealed class HotaruBreathingRhythm : HotaruCannonCard
{
    public HotaruBreathingRhythm() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(4m, ValueProp.Unpowered), new IntVar("Attacks", 3)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruBreathingRhythmPower>() };
    public override bool GainsBlock => true;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruTurnLedgerPower.Grant<HotaruBreathingRhythmPower>(context, Owner.Creature,
            DynamicVars.Block.IntValue, DynamicVars["Attacks"].IntValue, this);
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}

[CustomID("HOTARU_LUCKY_FAVOR")]
public sealed class HotaruLuckyFavor : HotaruCannonCard
{
    public HotaruLuckyFavor() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruLuckyFavorPower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<HotaruLuckyFavorPower>(context, Owner.Creature, DynamicVars.Energy.BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_STILL_COVERED")]
public sealed class HotaruStillCovered : HotaruCannonCard
{
    public HotaruStillCovered() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(6m, ValueProp.Unpowered) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruStillCoveredPower>() };
    public override bool GainsBlock => true;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<HotaruStillCoveredPower>(context, Owner.Creature, DynamicVars.Block.BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4m);
}

[CustomID("HOTARU_THREE_BREATHS")]
public sealed class HotaruThreeBreaths : HotaruCannonCard
{
    public HotaruThreeBreaths() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(1), new PowerVar<ChargePower>(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HotaruThreeBreathsPower>(), HoverTipFactory.FromPower<ChargePower>()
    };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<HotaruThreeBreathsPower>(context, Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
