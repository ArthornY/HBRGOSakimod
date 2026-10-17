using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_BEAT_APART")]
public sealed class HotaruBeatApart : HotaruCannonCard
{
    public HotaruBeatApart() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(4m, ValueProp.Move), new IntVar("Repeat", 2) };

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int hits = Math.Max(0, DynamicVars["Repeat"].IntValue);
        return hits == 0 ? Task.CompletedTask : DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, play).Targeting(play.Target).WithHitCount(hits)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1m);
}

[CustomID("HOTARU_HELD_WARMTH")]
public sealed class HotaruHeldWarmth : HotaruCannonCard
{
    public HotaruHeldWarmth() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(14m, ValueProp.Move) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<ChargePower>() };

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || originalCost <= 0m ||
            (Owner?.Creature?.GetPower<ChargePower>()?.Amount ?? 0) < 3) return false;
        modifiedCost = Math.Max(0m, originalCost - 1m);
        return true;
    }

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}

[CustomID("HOTARU_MOMENT_OF_CALM")]
public sealed class HotaruMomentOfCalm : HotaruCannonCard
{
    public HotaruMomentOfCalm() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(7m, ValueProp.Move), new CardsVar(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.Static(StaticHoverTip.Block), HoverTipFactory.FromPower<FragilePower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var combat = Owner.Creature.CombatState;
        bool fragile = (play.Target.GetPower<FragilePower>()?.Amount ?? 0) > 0;
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        if (fragile && HotaruCommonUtilityCardHelper.CanContinue(this, combat))
            await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

[CustomID("HOTARU_UNCONSCIOUS_AIM")]
public sealed class HotaruUnconsciousAim : HotaruCannonCard
{
    public HotaruUnconsciousAim() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(10m, ValueProp.Move), new CardsVar(1), new BlockVar(6m, ValueProp.Move)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.Static(StaticHoverTip.Block) };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var combat = Owner.Creature.CombatState;
        RollGlossaryCapability roll = this.Capability<RollGlossaryCapability>();
        roll?.EnsureCurrentRollFor(this);
        int openingFinal = HotaruUncommonCardHelper.TryGetOpeningRoll(this, out int firstRoll)
            ? firstRoll : roll?.GetCurrentRollDetailsFor(this)?.FinalRoll ?? 0;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (openingFinal >= 75 && HotaruCommonUtilityCardHelper.CanContinue(this, combat))
            await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
        if (openingFinal >= 91 && HotaruCommonUtilityCardHelper.CanContinue(this, combat))
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5m);
}

[CustomID("HOTARU_GENTLE_TOUCH")]
public sealed class HotaruGentleTouch : HotaruCannonCard
{
    public HotaruGentleTouch() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<SixSenseResistancePower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var combat = Owner.Creature.CombatState;
        await SixSenseCombatHelper.BreakLowestResistance(context, Owner, play.Target, this);
        if (HotaruCommonUtilityCardHelper.CanContinue(this, combat))
            await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
