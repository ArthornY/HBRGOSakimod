using System;
using System.Collections.Generic;
using System.Linq;
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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_BREATHING_SHELTER")]
public sealed class HotaruBreathingShelter : HotaruCannonCard
{
    public HotaruBreathingShelter() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.BlockBonus("BlockReward", 240m), new IntVar("ExtraActions", 1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruBreathingShelterPower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruBreathingShelterPower.Apply(context, Owner.Creature, DynamicVars["BlockReward"].IntValue, DynamicVars["ExtraActions"].IntValue, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_SIX_SENSE_REMNANTS")]
public sealed class HotaruSixSenseRemnants : HotaruCannonCard
{
    public HotaruSixSenseRemnants() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.CannonGrowthBonus("BaseGain", 50m, divisor: 3m), new IntVar("Charge", 1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<HotaruSixSenseRemnantsPower>(), HoverTipFactory.FromPower<ChargePower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruSixSenseRemnantsPower.Apply(context, Owner.Creature, DynamicVars["BaseGain"].IntValue, DynamicVars["Charge"].IntValue, this);
    protected override void OnUpgrade() => DynamicVars["BaseGain"].UpgradeCannonGrowthByPercent(50m, divisor: 3m);
}

[CustomID("HOTARU_GIVE_YOU_CHANCE")]
public sealed class HotaruGiveYouChance : HotaruCannonCard
{
    public HotaruGiveYouChance() : base(2, CardType.Skill, CardRarity.Rare, HotaruTargetTypes.OwnedCannon) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        Owner.GetRelic<Millsage>().TriggerAllEffects(play.Target, context, this, play,
            modifier: new HotaruCannonActionModifier { AdvantageRoll = true, RollAmplificationMultiplier = 2m });
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_GIVE_YOU_WARMTH")]
public sealed class HotaruGiveYouWarmth : HotaruCannonCard
{
    public HotaruGiveYouWarmth() : base(1, CardType.Skill, CardRarity.Rare, HotaruTargetTypes.OwnedCannon) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("Charge", 3m) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<ChargePower>() };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!Millsage.IsOwnedCannon(play.Target, Owner)) return;
        var charge = Owner.Creature.GetPower<ChargePower>();
        int before = Math.Max(0, charge?.Amount ?? 0);
        if (before == 0)
        {
            await PowerCmd.Apply<ChargePower>(context, Owner.Creature, DynamicVars["Charge"].BaseValue, Owner.Creature, this);
            return;
        }
        int spent = Math.Min(before, DynamicVars["Charge"].IntValue);
        await PowerCmd.ModifyAmount(context, charge, -spent, Owner.Creature, this);
        int actual = Math.Max(0, before - Math.Max(0, charge.Amount));
        await Owner.GetRelic<Millsage>().ChargeFromCard(context, play.Target, actual, this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_FOLLOW_TRAJECTORIES")]
public sealed class HotaruFollowTrajectories : HotaruCannonCard
{
    public HotaruFollowTrajectories() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.Damage(800m, ValueProp.Move, divisor: 3m), HbrSkillVars.DamageBonus("EchoDamage", 200m, divisor: 3m) };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int repeats = Math.Min(6, HotaruRareCannonHelper.ActionCount(Owner));
        using var comboAttack = HbrComboHelper.BeginCardAttack(this);
        try
        {
            IReadOnlyList<decimal> segments = new[] { DynamicVars.Damage.BaseValue }
                .Concat(Enumerable.Repeat(DynamicVars["EchoDamage"].BaseValue, Math.Max(0, repeats))).ToArray();
            if (comboAttack != null) segments = HbrComboHelper.TransformSegments(Owner.Creature, segments);
            foreach (decimal damage in segments)
            {
                if (!Millsage.IsEnemy(play.Target, Owner)) break;
                var attack = await DamageCmd.Attack(damage).FromCardCompat(this, play)
                    .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
                comboAttack?.Record(attack);
            }
        }
        finally
        {
            if (comboAttack != null) await comboAttack.Complete(context);
        }
    }
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeDamageByPercent(100m); DynamicVars["EchoDamage"].UpgradeDamageByPercent(100m, divisor: 3m); }
}

[CustomID("HOTARU_ONE_VOICE_ENOUGH")]
public sealed class HotaruOneVoiceEnough : HotaruCannonCard
{
    public HotaruOneVoiceEnough() : base(2, CardType.Skill, CardRarity.Uncommon, HotaruTargetTypes.OwnedCannon) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("BonusPercent", 50m) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var modifier = HotaruCommonCannonHelper.GetCannons(Owner).Length == 1
            ? new HotaruCannonActionModifier { FinalMultiplier = 2m }
            : new HotaruCannonActionModifier { BonusPercent = DynamicVars["BonusPercent"].BaseValue };
        return Owner.GetRelic<Millsage>().TriggerAllEffects(play.Target, context, this, play, modifier: modifier);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_EXTENDED_CODA")]
public sealed class HotaruExtendedCoda : HotaruCannonCard
{
    public HotaruExtendedCoda() : base(0, CardType.Skill, CardRarity.Rare, HotaruTargetTypes.OwnedCannon) { }
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("ExtraActions", 1m) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        EnergyCost.CapturedXValue = DualSwordsCopyHelper.PeekXEnergySpent(this, EnergyCost.CapturedXValue);
        return Owner.GetRelic<Millsage>().ChargeFromCard(context, play.Target,
            (int)Math.Min(int.MaxValue, (long)Math.Max(0, ResolveEnergyXValue()) + DynamicVars["ExtraActions"].IntValue), this);
    }
    protected override void OnUpgrade() => DynamicVars["ExtraActions"].UpgradeValueBy(1m);
}
