using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ProofThroughPainHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<ProofThroughPainPower>(2m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<ProofThroughPainPower>(),
        HoverTipFactory.FromPower<PhantomPower>(),
        HoverTipFactory.FromPower<ChargePower>()
    };

    public ProofThroughPainHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ProofThroughPainPower>(choiceContext, Owner.Creature,
            DynamicVars["ProofThroughPainPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() =>
        DynamicVars["ProofThroughPainPower"].UpgradeValueBy(1m);
}

public sealed class TogetherAtTheEndHbrgoSaki : CardModel
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/together_at_the_end_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(200m, ValueProp.Move) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<PhantomPower>() };

    public TogetherAtTheEndHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int hits = 1 + (Owner.GetRelic<MomentOfReign>()?.PhantomDamageThisTurn ?? 0);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(hits)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(200m, divisor: 3m);
}

public sealed class MemoryRefluxHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_reflux_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<MemoryRefluxPower>(1m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<MemoryRefluxPower>(),
        HoverTipFactory.FromPower<PhantomPower>(),
        HoverTipFactory.FromPower<ChargePower>()
    };

    public MemoryRefluxHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<MemoryRefluxPower>(choiceContext, Owner.Creature,
            DynamicVars["MemoryRefluxPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["MemoryRefluxPower"].UpgradeValueBy(1m);
}

public sealed class AwaitingEchoHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Block(160m, ValueProp.Move), new CardsVar(2) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<PhantomPower>() };

    public AwaitingEchoHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom?.Monster?.NextMove != null && !PhantomHelper.HasCurrentAttackIntent(phantom))
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeBlockByPercent(60m);
}

public sealed class EmberRecoveryHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        IsMutable && PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DynamicVar("Recovery", 6m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<PhantomPower>()
    };

    public EmberRecoveryHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom == null)
        {
            return;
        }

        await CreatureCmd.Heal(phantom, HbrResourceValueHelper.ConvertFixedRecovery(
            Owner.Creature, DynamicVars["Recovery"].BaseValue));
    }

    protected override void OnUpgrade() => DynamicVars["Recovery"].UpgradeValueBy(3m);
}

public sealed class EchoSlashHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/echo_slash_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(100m, ValueProp.Move),
        HbrSkillVars.Damage(100m, ValueProp.Move, name: "ExtraDamage")
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<PhantomPower>() };

    public EchoSlashHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        using var comboAttack = HbrComboHelper.BeginCardAttack(this);
        List<Func<Task<AttackCommand>>> attacks = comboAttack == null ? null : new(2);
        try
        {
            bool followUp = Owner.GetRelic<MomentOfReign>()?
                .WasAttackedByPhantomThisTurn(cardPlay.Target) == true;
            decimal primaryDamage = DynamicVars.Damage.BaseValue;
            Task<AttackCommand> PrimaryAttack() => DamageCmd.Attack(primaryDamage)
                .FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            attacks?.Add(PrimaryAttack);
            AttackCommand primary = await PrimaryAttack();
            comboAttack?.Record(primary);

            if (followUp && cardPlay.Target?.IsAlive == true)
            {
                decimal extraDamage = DynamicVars["ExtraDamage"].BaseValue;
                Task<AttackCommand> FollowUpAttack() => DamageCmd.Attack(extraDamage)
                    .FromCardCompat(this, cardPlay)
                    .Targeting(cardPlay.Target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
                attacks?.Add(FollowUpAttack);
                AttackCommand followUpAttack = await FollowUpAttack();
                comboAttack?.Record(followUpAttack);
            }

            if (comboAttack != null)
                await comboAttack.ReplayExtra(attacks, () =>
                    !CombatManager.Instance.IsOverOrEnding && cardPlay.Target?.IsAlive == true);
        }
        finally
        {
            if (comboAttack != null) await comboAttack.Complete(choiceContext);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(50m);
}

public sealed class StandAloneHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/stand_alone_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Block(200m, ValueProp.Move), new CardsVar(1) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<PhantomPower>() };

    public StandAloneHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        if (Owner.GetRelic<MomentOfReign>() is { } relic)
        {
            await relic.AdvancePhantomRevive();
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
