using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_ECHOING_WOUNDS")]
public sealed class HotaruEchoingWounds : HotaruCannonCard
{
    public HotaruEchoingWounds() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new IntVar("ResistanceLoss", 2m), HbrSkillVars.Damage(500m, ValueProp.Move, divisor: 3m), HbrSkillVars.Block(200m, ValueProp.Move)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<WeakPower>(), HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<FragilePower>(), HoverTipFactory.FromPower<SixSenseResistancePower>(),
        HoverTipFactory.Static(StaticHoverTip.Block)
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        // All three conditions describe the target when this card resolves;
        // a break/debuff caused by the first branch cannot enable another one.
        bool weak = play.Target.HasPower<WeakPower>();
        bool vulnerable = play.Target.HasPower<VulnerablePower>();
        bool fragile = play.Target.HasPower<FragilePower>();
        if (weak)
            await SixSenseCombatHelper.ReduceHighestResistance(context, Owner, play.Target,
                this, DynamicVars["ResistanceLoss"].IntValue);
        if (vulnerable && play.Target.IsAlive && HotaruCommonUtilityCardHelper.CanContinue(this))
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
                .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (fragile && HotaruCommonUtilityCardHelper.CanContinue(this))
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ResistanceLoss"].UpgradeValueBy(1m);
        DynamicVars.Damage.UpgradeDamageByPercent(200m, divisor: 3m);
        DynamicVars.Block.UpgradeBlockByPercent(80m);
    }
}

[CustomID("HOTARU_SCARS_POLYPHONY")]
public sealed class HotaruScarsPolyphony : HotaruCannonCard
{
    public HotaruScarsPolyphony() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(200m, ValueProp.Move, divisor: 3m), new IntVar("MaxHits", 4m) };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int kinds = HotaruRareCombatCardHelper.DebuffKinds(play.Target.Powers);
        int hits = Math.Min(Math.Max(0, DynamicVars["MaxHits"].IntValue), kinds + 1);
        if (hits <= 0) return;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitCount(hits).WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(100m, divisor: 3m);
        DynamicVars["MaxHits"].UpgradeValueBy(2m);
    }
}

[CustomID("HOTARU_REPLAY_PAIN")]
public sealed class HotaruReplayPain : HotaruCannonCard
{
    public HotaruReplayPain() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HotaruCommonUtilityCardHelper.CanContinue(this)) return;
        var combat = Owner.Creature.CombatState;
        // A single global snapshot prevents a reaction from adding a new
        // debuff to a later creature and having it doubled by the same play.
        var snapshot = combat.Creatures.Where(creature => creature.IsAlive)
            .SelectMany(creature => creature.Powers
                .Where(power => power.Amount != 0 && power.GetTypeForAmount(power.Amount) == PowerType.Debuff)
                .Select(power => (Creature: creature, Power: power, Amount: power.Amount)))
            .ToArray();
        foreach (var entry in snapshot)
        {
            if (!HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
            if (!entry.Creature.IsAlive || !ReferenceEquals(entry.Creature.CombatState, combat) ||
                !entry.Creature.Powers.Contains(entry.Power)) continue;
            // Adding the signed amount also doubles negative Strength and
            // Dexterity. Native power modification retains prevention hooks.
            await PowerCmd.ModifyAmount(context, entry.Power, entry.Amount, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

[CustomID("HOTARU_SILENCE_HERE")]
public sealed class HotaruSilenceHere : HotaruCannonCard
{
    public HotaruSilenceHere() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(500m, ValueProp.Move), HbrSkillVars.DamageBonus("DebuffDamage", 250m, divisor: 3m), new IntVar("CannonBoost", 50m)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<BreakPower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool broken = play.Target.HasPower<BreakPower>();
        int kinds = HotaruRareCombatCardHelper.DebuffKinds(play.Target.Powers);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + kinds * DynamicVars["DebuffDamage"].BaseValue)
            .FromCardCompat(this, play).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if ((broken || play.Target.HasPower<BreakPower>()) && HotaruCommonUtilityCardHelper.CanContinue(this))
            HotaruRareCannonHelper.AddTurnActivationBoost(Owner, DynamicVars["CannonBoost"].BaseValue);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["DebuffDamage"].UpgradeDamageByPercent(100m, divisor: 3m);
    }
}

[CustomID("HOTARU_MISSING_OUTLINE")]
public sealed class HotaruMissingOutline : HotaruCannonCard
{
    public HotaruMissingOutline() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(50m, ValueProp.Move, divisor: 3m), HbrSkillVars.DamageBonus("BreakDamage", 50m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<SixSenseResistancePower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var combat = Owner.Creature.CombatState;
        int broken = SixSenseCombatHelper.TargetResistanceBreakCount(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + broken * DynamicVars["BreakDamage"].BaseValue)
            .FromCardCompat(this, play).Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
        for (int i = 0; i < broken; i++)
        {
            if (!HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
            var cannons = HotaruCommonCannonHelper.GetCannons(Owner);
            if (cannons.Length == 0) return;
            var cannon = cannons[Owner.RunState.Rng.Niche.NextInt(cannons.Length)];
            await Owner.GetRelic<Millsage>().TriggerAllEffects(cannon, context, this, play);
        }
    }

    protected override void OnUpgrade() => DynamicVars["BreakDamage"].UpgradeDamageByPercent(100m, divisor: 3m);
}

[CustomID("HOTARU_WARM_EMPTY_SEAT")]
public sealed class HotaruWarmEmptySeat : HotaruCannonCard
{
    public HotaruWarmEmptySeat() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new CardsVar(1), new PowerVar<ChargePower>(1m), new IntVar("ExhaustCards", 0m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<ChargePower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HotaruCommonUtilityCardHelper.CanContinue(this)) return;
        var combat = Owner.Creature.CombatState;
        int retired = Owner.GetRelic<Millsage>()?.RetiredCannonCount ?? 0;
        decimal cards = DynamicVars.Cards.BaseValue + retired;
        decimal charge = DynamicVars["ChargePower"].BaseValue + retired;
        await CardPileCmd.Draw(context, cards, Owner);
        if (HotaruCommonUtilityCardHelper.CanContinue(this, combat))
            await PowerCmd.Apply<ChargePower>(context, Owner.Creature, charge, Owner.Creature, this);
        if (!IsUpgraded || !HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;

        int count = Math.Min(Math.Max(0, DynamicVars["ExhaustCards"].IntValue), PileType.Hand.GetPile(Owner).Cards
            .Count(card => HotaruCommonUtilityCardHelper.IsOtherHandCard(this, card)));
        if (count == 0) return;
        CardModel[] selected = (await CardSelectCmd.FromHand(context, Owner,
                new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, count),
                card => HotaruCommonUtilityCardHelper.IsOtherHandCard(this, card), this))
            .Distinct().Take(count).ToArray();
        foreach (CardModel card in selected)
        {
            if (!HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
            if (HotaruCommonUtilityCardHelper.IsOtherHandCard(this, card))
                await CardExhaustRuntimeCompatibility.Exhaust(context, card);
        }
    }

    protected override void OnUpgrade() => DynamicVars["ExhaustCards"].UpgradeValueBy(2m);
}

internal static class HotaruRareCombatCardHelper
{
    internal static int DebuffKinds(IEnumerable<PowerModel> powers) => powers
        .Where(power => power.GetTypeForAmount(power.Amount) == PowerType.Debuff)
        .Select(power => power.GetType()).Distinct().Count();
}
