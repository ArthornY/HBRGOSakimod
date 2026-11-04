using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ShakeTheAbyssHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new CardsVar(1)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public ShakeTheAbyssHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        decimal damage = Math.Floor(
            PhantomHelper.GetCurrentAttackIntentTotal(
                phantom,
                cardPlay.Target) / 2m);
        if (damage > 0m)
        {
            // The phantom intent has already gone through its skill curve.
            // Use its current damage as this card's base, not as native skill
            // units that would be expanded by a second curve conversion.
            using var absoluteDamage = HbrBattleStatsHelper.IsEnabled(Owner)
                ? HbrAbsoluteBaseDamageContext.Begin(Owner.Creature, this, cardPlay.Target, damage)
                : null;
            await DamageCmd.Attack(damage)
                .FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        await CardPileCmd.Draw(
            choiceContext,
            DynamicVars.Cards.BaseValue,
            Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

public sealed class ScorchingImpatienceHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/scorching_impatience_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(100m, ValueProp.Move),
            new PowerVar<ChargePower>(2m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<ChargePower>(),
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public ScorchingImpatienceHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await PowerCmd.Apply<ChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ChargePower"].BaseValue,
            Owner.Creature,
            this);

        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom?.IsAlive == true)
        {
            await PhantomHelper.PerformCurrentIntent(phantom);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ChargePower"].UpgradeValueBy(2m);
    }
}

public sealed class MercyAndCompassionHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/mercy_and_compassion_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("DpLoss", 5m),
            HbrSkillVars.Block(240m, ValueProp.Move)
        };

    public MercyAndCompassionHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        DeflectorProtection protection =
            Owner.GetRelic<DeflectorProtection>();
        protection?.LoseDp(HbrResourceValueHelper.FixedInt(Owner.Creature, DynamicVars["DpLoss"].BaseValue));
        if (protection != null)
        {
            await protection.ResolveMentalStrengthDpLossRewards(
                choiceContext,
                this);
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeBlockByPercent(120m);
    }
}

public sealed class AtTheBeginningHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override bool IsPlayable =>
        Owner.GetRelic<MomentOfReign>()?.GetRosterEntries().Count > 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("Experience", 10m)
        };

    public AtTheBeginningHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Owner.GetRelic<MomentOfReign>()?.AddExperienceToAllRosterEntries(
            DynamicVars["Experience"].IntValue);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Experience"].UpgradeValueBy(5m);
    }
}

public sealed class FallingContinuallyHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/falling_continually_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<FallingContinuallyPower>(3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<FallingContinuallyPower>(),
            HoverTipFactory.FromPower<DoomPower>()
        };

    public FallingContinuallyHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        await PowerCmd.Apply<FallingContinuallyPower>(
            choiceContext,
            cardPlay.Target,
            FallingContinuallyHelper.ResolveAmount(
                DynamicVars["FallingContinuallyPower"].BaseValue,
                Owner.Creature, cardPlay.Target),
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FallingContinuallyPower"].UpgradeValueBy(1m);
    }
}

public sealed class HolyOneHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new HpLossVar(2m),
            new EnergyVar(2)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>(),
            EnergyHoverTip
        };

    public HolyOneHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom != null)
        {
            await PhantomHelper.LoseHp(phantom, DynamicVars.HpLoss.BaseValue);
        }

        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}

public sealed class RestlessHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/restless_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable
    {
        get
        {
            Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
            return phantom != null && CombatState.Allies.Any(creature =>
                creature.IsAlive &&
                PhantomHelper.IsMinionOf(phantom, creature));
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<MinionPower>()
        };

    public RestlessHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom == null)
        {
            return;
        }

        foreach (Creature minion in CombatState.Allies
                     .Where(creature =>
                         creature.IsAlive &&
                         PhantomHelper.IsMinionOf(phantom, creature))
                     .ToArray())
        {
            await PhantomHelper.PerformCurrentIntent(minion);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

public sealed class VisitingPainHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/visiting_pain_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>(),
            HoverTipFactory.FromPower<
                PhantomHighDimensionalShellPower>()
        };

    public VisitingPainHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        HighDimensionalShellPower shell =
            phantom?.GetPower<HighDimensionalShellPower>();
        if (shell == null || shell.CurrentDp <= 0m)
        {
            return;
        }

        decimal previousDp = shell.CurrentDp;
        await PowerCmd.ModifyAmount(
            choiceContext,
            shell,
            -previousDp,
            Owner.Creature,
            this);
        decimal lostDp = Math.Max(0m, previousDp - shell.CurrentDp);
        if (lostDp > 0)
        {
            phantom.GetPower<PhantomPower>()?
                .AddCombatDamageBonus(lostDp * 0.02m);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Exhaust);
    }
}

public sealed class ImmaculateDressHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<ImmaculateDressPower>(1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<ImmaculateDressPower>(),
            HoverTipFactory.FromPower<ArtifactPower>()
        };

    public ImmaculateDressHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<ImmaculateDressPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ImmaculateDressPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}

public sealed class PainOfHellfireHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<PainOfHellfirePower>(3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PainOfHellfirePower>(),
            HoverTipFactory.FromPower<VigorPower>()
        };

    public PainOfHellfireHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<PainOfHellfirePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["PainOfHellfirePower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["PainOfHellfirePower"].UpgradeValueBy(2m);
    }
}

public sealed class CoordinatedAttackHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/coordinated_attack_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(100m, ValueProp.Move),
            new IntVar("BonusSegments", 0m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public CoordinatedAttackHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Uncommon,
            TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        Creature target = cardPlay.Target;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom?.IsAlive == true &&
            PhantomHelper.HasCurrentAttackIntent(phantom))
        {
            await PhantomHelper.PerformCurrentIntent(
                phantom,
                target,
                DynamicVars["BonusSegments"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BonusSegments"].UpgradeValueBy(1m);
    }
}

public sealed class SweepHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/sweep_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(100m, ValueProp.Move)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<
                PhantomNextAttackAllEnemiesPower>(),
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public SweepHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Uncommon,
            TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom?.IsAlive == true)
        {
            await PowerCmd.Apply<PhantomNextAttackAllEnemiesPower>(
                choiceContext,
                phantom,
                1m,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}

public sealed class RapidChargingHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new HpLossVar(3m),
            new PowerVar<ChargePower>(3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>(),
            HoverTipFactory.FromPower<ChargePower>()
        };

    public RapidChargingHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom != null)
        {
            await PhantomHelper.LoseHp(phantom, DynamicVars.HpLoss.BaseValue);
        }

        await PowerCmd.Apply<ChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ChargePower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ChargePower"].UpgradeValueBy(1m);
    }
}
