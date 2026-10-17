using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_UNFADING_SCARS")]
public sealed class HotaruUnfadingScars : HotaruCannonCard
{
    public HotaruUnfadingScars() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("EffectPercent", 0) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HotaruUnfadingScarsPower>(),
        HoverTipFactory.FromPower<MegaCrit.Sts2.Core.Models.Powers.VulnerablePower>(),
        HoverTipFactory.FromPower<MegaCrit.Sts2.Core.Models.Powers.WeakPower>(),
        HoverTipFactory.FromPower<FragilePower>()
    };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruUnfadingScarsPower.Apply(context, Owner.Creature, IsUpgraded ? DynamicVars["EffectPercent"].IntValue : 0, this);
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["EffectPercent"].UpgradeValueBy(25);
    }
}

[CustomID("HOTARU_TRACE_OLD_SCAR")]
public sealed class HotaruTraceOldScar : HotaruCannonCard
{
    public HotaruTraceOldScar() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new IntVar("ResistanceLoss", 2), new IntVar("CostReduction", 1)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<SixSenseResistancePower>() };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await HotaruRareRollHelper.ChooseResistanceAndDiscount(context, this, play.Target,
            DynamicVars["ResistanceLoss"].IntValue, DynamicVars["CostReduction"].IntValue);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_ONLY_THIS_BEAT")]
public sealed class HotaruOnlyThisBeat : HotaruCannonCard
{
    public HotaruOnlyThisBeat() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<ChargePower>(8), new IntVar("RollPercent", 20)
    };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<ChargePower>(), HoverTipFactory.FromPower<HotaruOnlyThisBeatPower>()
    };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await PowerCmd.Apply<ChargePower>(context, Owner.Creature, DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
        await HotaruTurnLedgerPower.Grant<HotaruOnlyThisBeatPower>(context, Owner.Creature,
            DynamicVars["RollPercent"].IntValue, int.MaxValue, this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_CERTAIN_VOICE")]
public sealed class HotaruCertainVoice : HotaruCannonCard
{
    public HotaruCertainVoice() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(1) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruCertainVoicePower>() };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        // Grant before drawing: automatically played cards never consume it.
        await PowerCmd.Apply<HotaruCertainVoicePower>(context, Owner.Creature, 1, Owner.Creature, this);
        await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

[CustomID("HOTARU_BREATH_AFTER_MISS")]
public sealed class HotaruBreathAfterMiss : HotaruCannonCard
{
    public HotaruBreathAfterMiss() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new IntVar("MissLimit", 2), new BlockVar(4m, ValueProp.Unpowered),
        new PowerVar<ChargePower>(1), new CardsVar(1)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HotaruBreathAfterMissPower>(), HoverTipFactory.FromPower<ChargePower>()
    };
    public override bool GainsBlock => true;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruBreathAfterMissPower.Apply(context, Owner.Creature, DynamicVars["MissLimit"].IntValue,
            DynamicVars.Block.IntValue, DynamicVars["ChargePower"].IntValue, DynamicVars.Cards.IntValue, this);
    protected override void OnUpgrade()
    {
        DynamicVars["MissLimit"].UpgradeValueBy(1);
        DynamicVars.Block.UpgradeValueBy(2);
    }
}

[CustomID("HOTARU_INHERIT_ANSWER")]
public sealed class HotaruInheritAnswer : HotaruCannonCard
{
    public HotaruInheritAnswer() : base(3, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruInheritAnswerPower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<HotaruInheritAnswerPower>(context, Owner.Creature, 1, Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_FAREWELL_INERTIA")]
public sealed class HotaruFarewellInertia : HotaruCannonCard
{
    public HotaruFarewellInertia() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new IntVar("CriticalLimit", 2), new IntVar("BaseCharge", 1), new IntVar("ExtraActions", 1)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HotaruFarewellInertiaPower>(), HoverTipFactory.FromPower<ChargePower>()
    };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruFarewellInertiaPower.Apply(context, Owner.Creature, DynamicVars["CriticalLimit"].IntValue,
            DynamicVars["BaseCharge"].IntValue, DynamicVars["ExtraActions"].IntValue, this);
    protected override void OnUpgrade() => DynamicVars["CriticalLimit"].UpgradeValueBy(1);
}
