using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PressOnThroughPainHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override bool GainsBlock => true;
    protected override bool IsPlayable => PhantomHelper.GetLivingPhantom(Owner) != null;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new HpLossVar(2m), HbrSkillVars.Block(320m, ValueProp.Move)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<PhantomPower>() };
    public PressOnThroughPainHbrgoSaki() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom == null) return;
        await PhantomHelper.LoseHp(phantom, DynamicVars.HpLoss.BaseValue);
        if (!CombatManager.Instance.IsOverOrEnding)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeBlockByPercent(100m);
}

public sealed class ChargeInReserveHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<ChargePower>(2m), new PowerVar<ChargeInReserveNextTurnPower>(2m)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<ChargePower>() };
    public ChargeInReserveHbrgoSaki() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ChargePower>(choiceContext, Owner.Creature, DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<ChargeInReserveNextTurnPower>(choiceContext, Owner.Creature,
            DynamicVars["ChargeInReserveNextTurnPower"].BaseValue, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars["ChargeInReserveNextTurnPower"].UpgradeValueBy(1m);
}

public sealed class PierceTheSilenceHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/pierce_the_silence_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(150m, ValueProp.Move), new PowerVar<ChargePower>(2m)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<PhantomPower>(), HoverTipFactory.FromPower<ChargePower>() };
    public PierceTheSilenceHbrgoSaki() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        bool hasNonAttackIntent = phantom?.Monster?.NextMove?.Intents.Any(intent => intent is not AttackIntent) == true;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target).WithHitCount(hasNonAttackIntent ? 2 : 1)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (hasNonAttackIntent && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<ChargePower>(choiceContext, Owner.Creature, DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(50m);
}

public sealed class BorrowedEdgeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override bool IsPlayable => PhantomHelper.GetLivingPhantom(Owner) != null;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    { HbrSkillVars.Damage(250m, ValueProp.Move, divisor: 3m), new DynamicVar("DpLoss", 6m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<PhantomPower>(), HoverTipFactory.FromPower<PhantomHighDimensionalShellPower>() };
    public BorrowedEdgeHbrgoSaki() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom == null) return;
        var shell = phantom.GetPower<PhantomHighDimensionalShellPower>();
        if (shell?.CurrentDp > 0m)
            await PowerCmd.ModifyAmount(choiceContext, shell, -Math.Min(shell.CurrentDp, HbrResourceValueHelper.Fixed(Owner.Creature, DynamicVars["DpLoss"].BaseValue)), Owner.Creature, this);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target).WithHitCount(2).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(50m);
}

public sealed class RefuseToLeaveHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/refuse_to_leave_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { HbrSkillVars.BlockPower<RefuseToLeavePower>(200m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<PhantomPower>(), HoverTipFactory.FromPower<ChargePower>() };
    public RefuseToLeaveHbrgoSaki() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<RefuseToLeavePower>(choiceContext, Owner.Creature, DynamicVars["RefuseToLeavePower"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["RefuseToLeavePower"].UpgradeBlockByPercent(80m);
}

public sealed class SilentOfferingHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/silent_offering_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SilentOfferingPower>(2m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<PhantomPower>(), HoverTipFactory.FromPower<ChargePower>() };
    public SilentOfferingHbrgoSaki() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<SilentOfferingPower>(choiceContext, Owner.Creature, DynamicVars["SilentOfferingPower"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["SilentOfferingPower"].UpgradeValueBy(1m);
}

public sealed class AwaitingReturnHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<AwaitingReturnPower>(2m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<PhantomPower>(), HoverTipFactory.FromPower<ChargePower>() };
    public AwaitingReturnHbrgoSaki() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<AwaitingReturnPower>(choiceContext, Owner.Creature, DynamicVars["AwaitingReturnPower"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["AwaitingReturnPower"].UpgradeValueBy(2m);
}

public sealed class InMyNameHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<InMyNamePower>(1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<PhantomPower>(), HoverTipFactory.FromPower<StrengthPower>() };
    public InMyNameHbrgoSaki() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<InMyNamePower>(choiceContext, Owner.Creature, DynamicVars["InMyNamePower"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["InMyNamePower"].UpgradeValueBy(1m);
}

public sealed class UnbrokenRemnantHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/unbroken_remnant_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    { new PowerVar<UnbrokenRemnantPower>(10m), new PowerVar<StrengthPower>(2m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<PhantomHighDimensionalShellPower>(), HoverTipFactory.FromPower<StrengthPower>() };
    public UnbrokenRemnantHbrgoSaki() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var power = await PowerCmd.Apply<UnbrokenRemnantPower>(choiceContext, Owner.Creature,
            DynamicVars["UnbrokenRemnantPower"].BaseValue, Owner.Creature, this);
        power?.AddStrengthGain(DynamicVars["StrengthPower"].IntValue);
    }
    protected override void OnUpgrade()
    {
        DynamicVars["UnbrokenRemnantPower"].UpgradeValueBy(4m);
        DynamicVars["StrengthPower"].UpgradeValueBy(2m);
    }
}

public sealed class BorrowLifeTogetherHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override bool IsPlayable => PhantomHelper.GetLivingPhantom(Owner) != null;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new HpLossVar(1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<PhantomHighDimensionalShellPower>() };
    public BorrowLifeTogetherHbrgoSaki() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom == null) return;
        // This is the player's HP cost, not damage that can be redirected to the phantom or DP.
        await CreatureCmd.SetCurrentHp(Owner.Creature,
            Math.Max(0m, Owner.Creature.CurrentHp - HbrResourceValueHelper.Fixed(Owner.Creature, DynamicVars.HpLoss.BaseValue)));
        if (!phantom.IsAlive || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding) return;
        await PhantomHelper.RestoreDp(choiceContext, phantom, PhantomHighDimensionalShellPower.CalculateInitialAmount(phantom), this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class ScarLedgerHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.DamageCalculationBase(200m), HbrSkillVars.ExtraDamage(250m, divisor: 3m), HbrSkillVars.DamageBonus("BonusCap", 2500m, divisor: 3m),
        HbrSkillVars.CalculatedDamage(ValueProp.Move).WithMultiplier(GetDamageTakenMultiplier)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<PhantomPower>() };
    public ScarLedgerHbrgoSaki() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        Owner.GetRelic<MomentOfReign>()?.ResetPhantomDamageTakenCount();
    }
    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeDamageByPercent(200m, divisor: 3m);
        DynamicVars.ExtraDamage.UpgradeDamageByPercent(50m);
        DynamicVars["BonusCap"].UpgradeDamageByPercent(500m);
    }
    private static decimal GetDamageTakenMultiplier(CardModel card, Creature target)
    {
        if (card?.IsMutable != true) return 0m;
        return Math.Min(
            card.Owner?.GetRelic<MomentOfReign>()?.PhantomDamageTakenCount ?? 0,
            card.DynamicVars["BonusCap"].BaseValue / card.DynamicVars.ExtraDamage.BaseValue);
    }
}

public sealed class BuryYesterdayHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.DamageCalculationBase(200m), HbrSkillVars.ExtraDamage(100m),
        HbrSkillVars.CalculatedDamage(ValueProp.Move).WithMultiplier(GetHighestPhantomLevel)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<PhantomPower>() };
    public BuryYesterdayHbrgoSaki() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCardCompat(this, cardPlay).TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeDamageByPercent(100m);
        DynamicVars.ExtraDamage.UpgradeDamageByPercent(50m);
    }
    private static decimal GetHighestPhantomLevel(CardModel card, Creature target)
    {
        if (card?.IsMutable != true ||
            card.Owner?.GetRelic<MomentOfReign>() is not { } relic)
            return 0m;

        decimal highestLevel = relic.GetRosterEntries()
            .Select(entry => (decimal)entry.Level)
            .DefaultIfEmpty(0m)
            .Max();
        Creature phantom = PhantomHelper.GetLivingPhantom(card.Owner);
        if (phantom != null)
        {
            int baseLevel = relic.GetActivePhantomBaseLevel();
            decimal activeLevel = phantom.GetPower<PhantomTemporaryLevelPower>()?
                .GetEffectiveLevel(baseLevel) ?? baseLevel;
            highestLevel = Math.Max(highestLevel, activeLevel);
        }
        return highestLevel;
    }
}

public sealed class DefyFateStrikeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    { HbrSkillVars.Damage(350m, ValueProp.Move, divisor: 3m), new DynamicVar("PhantomHits", 1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<PhantomPower>() };
    public DefyFateStrikeHbrgoSaki() : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int x = DualSwordsCopyHelper.TakeXEnergySpent(this, cardPlay.Resources.EnergySpent);
        if (x <= 0) return;
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target).WithHitCount(x).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        long phantomHits = (long)x * DynamicVars["PhantomHits"].IntValue;
        for (long i = 0; i < phantomHits && phantom?.IsAlive == true && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            await PhantomHelper.LoseHp(phantom, 1m);
        }
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(50m);
        DynamicVars["PhantomHits"].UpgradeValueBy(1m);
    }
}

public sealed class EnduringAfterimageHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<PhantomPower>() };
    public EnduringAfterimageHbrgoSaki() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<EnduringAfterimagePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class DevourRemainingLifeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<PhantomPower>() };
    public DevourRemainingLifeHbrgoSaki() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<DevourRemainingLifePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class PainResonanceHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<PainResonancePower>(6m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<PhantomPower>() };
    public PainResonanceHbrgoSaki() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<PainResonancePower>(choiceContext, Owner.Creature, DynamicVars["PainResonancePower"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["PainResonancePower"].UpgradeValueBy(3m);
    }
}

public sealed class LetMeBearItHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<PhantomPower>(), HoverTipFactory.FromPower<StrengthPower>() };
    public LetMeBearItHbrgoSaki() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<LetMeBearItPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
