using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_TRIPLE_SCARS")]
public sealed class HotaruTripleScars : HotaruCannonCard
{
    public HotaruTripleScars() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(250m, ValueProp.Move, divisor: 3m), HbrSkillVars.DamageBonus("DebuffDamage", 50m) };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        // Count kinds, not stacks. Amount-dependent debuffs (negative Strength,
        // for example) are included alongside ordinary debuff powers.
        int kinds = play.Target.Powers
            .Where(power => power.GetTypeForAmount(power.Amount) == PowerType.Debuff)
            .Select(power => power.GetType()).Distinct().Count();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + kinds * DynamicVars["DebuffDamage"].BaseValue)
            .FromCardCompat(this, play).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(50m);
}

[CustomID("HOTARU_DAWN_GAP")]
public sealed class HotaruDawnGap : HotaruCannonCard
{
    public HotaruDawnGap() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<VulnerablePower>(2m), new PowerVar<FragilePower>(2m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<VulnerablePower>(), HoverTipFactory.FromPower<FragilePower>(),
        HoverTipFactory.FromPower<BreakPower>()
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool wasBroken = play.Target.HasPower<BreakPower>();
        await PowerCmd.Apply<VulnerablePower>(context, play.Target,
            DynamicVars["VulnerablePower"].BaseValue, Owner.Creature, this);
        if (wasBroken && play.Target.IsAlive)
            await PowerCmd.Apply<FragilePower>(context, play.Target,
                DynamicVars["FragilePower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["VulnerablePower"].UpgradeValueBy(1m);
}

[CustomID("HOTARU_COUNT_SCARS")]
public sealed class HotaruCountScars : HotaruCannonCard
{
    public HotaruCountScars() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(300m, ValueProp.Move), new EnergyVar(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<VulnerablePower>(), HoverTipFactory.FromPower<FragilePower>(),
        HoverTipFactory.FromPower<WeakPower>()
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (!play.Target.IsAlive || !HotaruCommonUtilityCardHelper.CanContinue(this)) return;

        // Removing a whole power is a fixed payment, unaffected by card-number
        // bonuses. Each successfully removed kind pays its own energy reward.
        PowerModel[][] powersByKind =
        {
            new PowerModel[] { play.Target.GetPower<VulnerablePower>() },
            HbrBattleStatsHelper.IsEnabled(play.Target)
                ? play.Target.GetPowerInstances<FragilePower>().Cast<PowerModel>().ToArray()
                : new PowerModel[] { play.Target.GetPower<FragilePower>() },
            new PowerModel[] { play.Target.GetPower<WeakPower>() }
        };
        foreach (PowerModel[] powers in powersByKind)
        {
            bool removedKind = false;
            foreach (PowerModel power in powers)
            {
                if (!play.Target.IsAlive || !HotaruCommonUtilityCardHelper.CanContinue(this)) return;
                if (power == null || !play.Target.Powers.Contains(power)) continue;
                await PowerCmd.Remove(power);
                removedKind |= !play.Target.Powers.Contains(power);
            }
            if (removedKind && HotaruCommonUtilityCardHelper.CanContinue(this))
                await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(350m, divisor: 3m);
}

[CustomID("HOTARU_HEART_WARMTH")]
public sealed class HotaruHeartWarmth : HotaruCannonCard
{
    public HotaruHeartWarmth() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<ChargePower>(2m), HbrSkillVars.Block(100m, ValueProp.Move) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<ChargePower>(), HoverTipFactory.Static(StaticHoverTip.Block)
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await PowerCmd.Apply<ChargePower>(context, Owner.Creature,
            DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
        if (HotaruCommonUtilityCardHelper.CanContinue(this))
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeBlockByPercent(80m);
}

[CustomID("HOTARU_HESITATION_GAP")]
public sealed class HotaruHesitationGap : HotaruCannonCard
{
    public HotaruHesitationGap() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(400m, ValueProp.Move, divisor: 3m), new PowerVar<ChargePower>(1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<ChargePower>(), HoverTipFactory.FromPower<WeakPower>()
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool wasWeak = play.Target.HasPower<WeakPower>();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (!HotaruCommonUtilityCardHelper.CanContinue(this)) return;
        await PowerCmd.Apply<ChargePower>(context, Owner.Creature,
            DynamicVars["ChargePower"].BaseValue * (wasWeak ? 2m : 1m), Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(200m, divisor: 3m);
}

[CustomID("HOTARU_LAST_BREATH")]
public sealed class HotaruLastBreath : HotaruCannonCard
{
    public HotaruLastBreath() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<ChargePower>(3m), new CardsVar(3) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<ChargePower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await PowerCmd.Apply<ChargePower>(context, Owner.Creature,
            DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
        if (HotaruCommonUtilityCardHelper.CanContinue(this))
            await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars["ChargePower"].UpgradeValueBy(3m);
}

[CustomID("HOTARU_LOW_ECHO")]
public sealed class HotaruLowEcho : HotaruCannonCard
{
    public HotaruLowEcho() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Block(200m, ValueProp.Move), new IntVar("RollBonus", 40m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.Static(StaticHoverTip.Block), HoverTipFactory.FromPower<HotaruLingeringEchoPower>()
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        RollGlossaryCapability roll = this.Capability<RollGlossaryCapability>();
        roll?.EnsureCurrentRollFor(this);
        bool grantsEcho = roll?.GetCurrentRollDetailsFor(this)?.BaseRoll <= 30;
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        if (grantsEcho && HotaruCommonUtilityCardHelper.CanContinue(this))
            await HotaruLingeringEchoPower.Apply(context, Owner.Creature,
                DynamicVars["RollBonus"].BaseValue, this);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeBlockByPercent(100m);
}

[CustomID("HOTARU_BENEATH_FRACTURE")]
public sealed class HotaruBeneathFracture : HotaruCannonCard
{
    public HotaruBeneathFracture() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(800m, ValueProp.Move, divisor: 3m), HbrSkillVars.DamageBonus("BreakDamage", 500m, divisor: 3m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<SixSenseResistancePower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        decimal damage = DynamicVars.Damage.BaseValue;
        if (SixSenseCombatHelper.TargetResistanceBreakCount(play.Target) > 0)
            damage += DynamicVars["BreakDamage"].BaseValue;
        await DamageCmd.Attack(damage).FromCardCompat(this, play).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(100m);
}

[CustomID("HOTARU_BLOODSTAINED_SCREEN")]
public sealed class HotaruBloodstainedScreen : HotaruCannonCard
{
    public HotaruBloodstainedScreen() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(1), HbrSkillVars.Block(100m, ValueProp.Move), new PowerVar<ChargePower>(1m), new EnergyVar(1)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.Static(StaticHoverTip.Block), HoverTipFactory.FromPower<ChargePower>()
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HotaruCommonUtilityCardHelper.CanContinue(this)) return;
        var combat = Owner.Creature.CombatState;
        CardModel selected = (await CardSelectCmd.FromHandForDiscard(context, Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
                card => !ReferenceEquals(card, this), this))
            .FirstOrDefault(card => HotaruCommonUtilityCardHelper.IsOtherHandCard(this, card));
        if (selected == null || !HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
        CardType discardedType = selected.Type;
        int discardedBefore = CombatManager.Instance.History.Entries.OfType<CardDiscardedEntry>()
            .Count(entry => ReferenceEquals(entry.Card, selected));
        // Use the native combined command to retain its discard/draw/Sly order.
        await CardCmd.DiscardAndDraw(context, new[] { selected }, DynamicVars.Cards.IntValue);
        bool paid = CombatManager.Instance.History.Entries.OfType<CardDiscardedEntry>()
            .Count(entry => ReferenceEquals(entry.Card, selected)) > discardedBefore;
        if (!paid || !HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
        switch (discardedType)
        {
            case CardType.Attack:
                await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
                break;
            case CardType.Skill:
                await PowerCmd.Apply<ChargePower>(context, Owner.Creature,
                    DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
                break;
            case CardType.Power:
                await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
                break;
        }
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

[CustomID("HOTARU_NEXT_PAGE")]
public sealed class HotaruNextPage : HotaruCannonCard
{
    public HotaruNextPage() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new CardsVar(3), new IntVar("CostReduction", 1m) };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HotaruCommonUtilityCardHelper.CanContinue(this)) return;
        var combat = Owner.Creature.CombatState;
        await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
        if (!HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
        bool IsEligible(CardModel card) => HotaruCommonUtilityCardHelper.IsOtherHandCard(this, card) &&
            !card.EnergyCost.CostsX && card.EnergyCost.GetWithModifiers(CostModifiers.None) >= 0;
        CardModel selected = (await CardSelectCmd.FromHand(context, Owner,
                new CardSelectorPrefs(new LocString("cards", "HOTARU_NEXT_PAGE.selectionPrompt"), 1),
                IsEligible, this)).FirstOrDefault(IsEligible);
        if (selected == null || !HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
        selected.EnergyCost.AddThisTurn(-Math.Max(0, DynamicVars["CostReduction"].IntValue));
        selected.InvokeEnergyCostChanged();
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
