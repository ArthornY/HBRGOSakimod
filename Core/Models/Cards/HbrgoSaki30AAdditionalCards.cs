using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class SoulImpactHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/soul_impact_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new DamageVar(10m, ValueProp.Move)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public SoulImpactHbrgoSaki()
        : base(0, CardType.Attack, CardRarity.Common,
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
        if (Owner.GetRelic<MomentOfReign>() is { } relic)
        {
            await relic.AdvancePhantomRevive();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}

public sealed class ChargeUpSlashHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new DamageVar(6m, ValueProp.Move),
            new PowerVar<ChargeUpSlashNextTurnPower>(3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<ChargeUpSlashNextTurnPower>()
        };

    public ChargeUpSlashHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Common,
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
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await PowerCmd.Apply<ChargeUpSlashNextTurnPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ChargeUpSlashNextTurnPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["ChargeUpSlashNextTurnPower"].UpgradeValueBy(1m);
    }
}

public sealed class SacrificialAttackHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null &&
        Owner.GetRelic<MomentOfReign>()?.GetActiveRosterEntry() != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new CalculationBaseVar(0m),
            new ExtraDamageVar(5m),
            new CalculatedDamageVar(ValueProp.Move)
                .WithMultiplier(GetActivePhantomLevel)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public SacrificialAttackHbrgoSaki()
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
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        int maxHpLoss = Owner.GetRelic<MomentOfReign>()?
            .LoseLevelFromActiveRosterEntry() ?? 0;
        if (phantom?.IsAlive == true && maxHpLoss > 0)
        {
            await CreatureCmd.LoseMaxHp(
                choiceContext,
                phantom,
                maxHpLoss,
                isFromCard: true);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.ExtraDamage.UpgradeValueBy(2m);
    }

    private static decimal GetActivePhantomLevel(
        CardModel card,
        Creature target)
    {
        if (card?.IsMutable != true)
            return 0m;

        PhantomRosterEntry? entry = card.Owner?.GetRelic<MomentOfReign>()?
            .GetActiveRosterEntry();
        Creature phantom = PhantomHelper.GetLivingPhantom(card.Owner);
        if (entry == null || phantom == null)
            return 0m;

        return phantom.GetPower<PhantomTemporaryLevelPower>()?
            .GetEffectiveLevel(entry.Value.Level) ?? entry.Value.Level;
    }
}
