using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_RESONANT_FIRE")]
public sealed class HotaruResonantFire : HotaruCannonCard
{
    public HotaruResonantFire() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(8m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        // The attack target may have died. The cannon choice only depends on
        // whether this combat and the acting player are still active.
        await HotaruCommonCannonHelper.SelectAndTrigger(context, this, play);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}

[CustomID("HOTARU_HANDOFF")]
public sealed class HotaruHandoff : HotaruCannonCard
{
    public HotaruHandoff() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
        { new DamageVar(8m, ValueProp.Move), new PowerVar<ChargePower>(1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<ChargePower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool handoff = HotaruCommonCannonHelper.HasManualCannonTargetThisTurn(Owner);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (handoff)
            await PowerCmd.Apply<ChargePower>(context, Owner.Creature,
                DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["ChargePower"].UpgradeValueBy(1m);
}

[CustomID("HOTARU_LEANING_ON")]
public sealed class HotaruLeaningOn : HotaruCannonCard
{
    public HotaruLeaningOn() : base(2, CardType.Skill, CardRarity.Common, HotaruTargetTypes.OwnedCannon) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
        { new BlockVar(8m, ValueProp.Move), new IntVar("ExtraTriggers", 1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.Static(StaticHoverTip.Block) };
    public override bool GainsBlock => true;

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        if (Owner.GetRelic<Millsage>() is { } relic)
            await relic.ChargeFromCard(context, play.Target, DynamicVars["ExtraTriggers"].IntValue, this);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4m);
}

[CustomID("HOTARU_BURN_WARMTH")]
public sealed class HotaruBurnWarmth : HotaruCannonCard
{
    public HotaruBurnWarmth() : base(0, CardType.Skill, CardRarity.Common, HotaruTargetTypes.OwnedCannon) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("ExtraTriggers", 1m) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<ChargePower>() };
    private bool HasCharge => Owner?.Creature?.GetPower<ChargePower>()?.Amount >= 1;
    protected override bool IsPlayable => base.IsPlayable && HasCharge;
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) => card != this || IsPlayable;

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (Owner?.GetRelic<Millsage>() is not { } relic ||
            !Millsage.IsOwnedCannon(play.Target, Owner) ||
            Owner.Creature.GetPower<ChargePower>() is not { Amount: >= 1 } charge) return;
        // One charge is a fixed payment, not an effect value amplified by
        // My Happiness's card-number overlay.
        int before = charge.Amount;
        await PowerCmd.ModifyAmount(context, charge, -1m, Owner.Creature, this);
        if (charge.Amount < before)
            await relic.ChargeFromCard(context, play.Target, DynamicVars["ExtraTriggers"].IntValue, this);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

[CustomID("HOTARU_WAIT_OPPORTUNITY")]
public sealed class HotaruWaitOpportunity : HotaruCannonCard
{
    public HotaruWaitOpportunity() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
        { new IntVar("ActivationCount", 1m), new BlockVar(0m, ValueProp.Move) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
        { HoverTipFactory.FromPower<HotaruWaitOpportunityPower>(), HoverTipFactory.Static(StaticHoverTip.Block) };
    public override bool GainsBlock => IsUpgraded;

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        // Queue before block callbacks: if a callback starts an inserted turn,
        // that genuinely is the next player turn.
        await HotaruWaitOpportunityPower.Apply(context, Owner.Creature,
            DynamicVars["ActivationCount"].IntValue, this);
        if (IsUpgraded)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4m);
}

[CustomID("HOTARU_SIDE_BY_SIDE")]
public sealed class HotaruSideBySide : HotaruCannonCard
{
    public HotaruSideBySide() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
        { new BlockVar(5m, ValueProp.Move), new IntVar("DamagePercent", 10m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
        { HoverTipFactory.Static(StaticHoverTip.Block), HoverTipFactory.FromPower<HotaruSideBySidePower>() };
    public override bool GainsBlock => true;

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await HotaruSideBySidePower.Apply(context, Owner.Creature,
            DynamicVars["DamagePercent"].IntValue, this);
    }

    protected override void OnUpgrade() => DynamicVars["DamagePercent"].UpgradeValueBy(5m);
}

// A selection display only. Instances never enter a deck or combat pile.
[CustomID("HOTARU_CANNON_CHOICE_TOKEN")]
public sealed class HotaruCannonChoiceToken : HotaruCannonCard
{
    private string _choiceTitle;
    private LocString _choiceDescription;
    public HotaruCannonChoiceToken() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self) { }
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override string Title => _choiceTitle ?? base.Title;
    internal void Configure(string title, LocString description)
    {
        AssertMutable();
        _choiceTitle = title;
        _choiceDescription = description;
    }
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("CannonDescription", _choiceDescription);
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
    protected override void OnUpgrade() { }
}
