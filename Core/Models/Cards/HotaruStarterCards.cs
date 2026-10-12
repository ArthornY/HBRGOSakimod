using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public abstract class HotaruStarterCard : CardModel
{
    protected HotaruStarterCard(CardType type, TargetType targetType)
        : base(1, type, CardRarity.Basic, targetType)
    {
    }

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg");

    public override string BetaPortraitPath => PortraitPath;
}

public sealed class HotaruStrike : HotaruStarterCard
{
    public HotaruStrike() : base(CardType.Attack, TargetType.AnyEnemy) { }

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

public sealed class HotaruDefend : HotaruStarterCard
{
    public HotaruDefend() : base(CardType.Skill, TargetType.Self) { }

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(5m, ValueProp.Move) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.Static(StaticHoverTip.Block) };

    public override bool GainsBlock => true;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

public sealed class HotaruStimulate : HotaruStarterCard
{
    public HotaruStimulate() : base(CardType.Skill, HotaruTargetTypes.OwnedCannonOrEnemy) { }

    protected override bool IsPlayable => Owner?.GetRelic<Millsage>() != null;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        Millsage millsage = Owner.GetRelic<Millsage>();
        if (cardPlay.Target.Side == Owner.Creature.Side)
            await millsage.TriggerAllEffects(cardPlay.Target, choiceContext, this, cardPlay,
                baseValueBonus: IsUpgraded ? 1m : 0m);
        else
            await millsage.FireHappiness(cardPlay.Target, choiceContext, this, cardPlay,
                growBeforeDamage: IsUpgraded);
    }

    protected override void OnUpgrade()
    {
    }
}

public sealed class HotaruCharge : HotaruStarterCard
{
    public HotaruCharge() : base(CardType.Skill, HotaruTargetTypes.OwnedCannon) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new IntVar("ExtraTriggers", 1m) };

    protected override bool IsPlayable => Owner?.GetRelic<Millsage>() != null;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        Owner.GetRelic<Millsage>().Charge(cardPlay.Target, DynamicVars["ExtraTriggers"].IntValue);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["ExtraTriggers"].UpgradeValueBy(1m);
}
