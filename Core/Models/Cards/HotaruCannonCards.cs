using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public abstract class HotaruCannonCard : CardModel
{
    protected HotaruCannonCard(int energyCost, CardType type, CardRarity rarity, TargetType targetType)
        : base(energyCost, type, rarity, targetType)
    {
    }

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable => Owner?.GetRelic<Millsage>() != null;
}

[CustomID("HOTARU_ECHO")]
public sealed class HotaruEcho : HotaruCannonCard
{
    public HotaruEcho() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new IntVar("BaseBlockBonus", 0m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.Static(StaticHoverTip.Block) };

    public override bool GainsBlock => true;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Owner.GetRelic<Millsage>().QueueDefensiveEcho(DynamicVars["BaseBlockBonus"].BaseValue);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["BaseBlockBonus"].UpgradeValueBy(2m);
}

[CustomID("HOTARU_SALVO")]
public sealed class HotaruSalvo : HotaruCannonCard
{
    public HotaruSalvo() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    public override TargetType TargetType => IsUpgraded ? TargetType.Self : TargetType.AnyEnemy;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Millsage millsage = Owner.GetRelic<Millsage>();
        if (IsUpgraded)
        {
            await millsage.TriggerAllCannons(choiceContext, this, cardPlay);
            return;
        }

        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await millsage.FireAllAttackIntents(cardPlay.Target, choiceContext, this, cardPlay);
    }

    protected override void OnUpgrade()
    {
    }
}

[CustomID("HOTARU_OVERCHARGE")]
public sealed class HotaruOvercharge : HotaruCannonCard
{
    public HotaruOvercharge() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new IntVar("DamagePercent", 100m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<ChargePower>() };

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        HotaruOverchargePower.Apply(choiceContext, Owner.Creature,
            DynamicVars["DamagePercent"].IntValue, this);

    protected override void OnUpgrade() => DynamicVars["DamagePercent"].UpgradeValueBy(50m);
}
