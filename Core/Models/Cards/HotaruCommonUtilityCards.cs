using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_EXHALE")]
public sealed class HotaruExhale : HotaruCannonCard
{
    public HotaruExhale() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<ChargePower>(3m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<ChargePower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HotaruCommonUtilityCardHelper.CanContinue(this)) return;
        var combat = Owner.Creature.CombatState;
        CardModel selected = (await CardSelectCmd.FromHand(context, Owner,
                new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
                card => !ReferenceEquals(card, this), this))
            .FirstOrDefault(card => HotaruCommonUtilityCardHelper.IsOtherHandCard(this, card));
        if (selected == null || !HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;

        // The selected card is a fixed one-card payment. Observe the native
        // exhaust history so prevention grants nothing, while an exhaust hook
        // that returns the card to hand still counts as a successful payment.
        int exhaustedBefore = CombatManager.Instance.History.Entries.OfType<CardExhaustedEntry>()
            .Count(entry => ReferenceEquals(entry.Card, selected));
        await CardExhaustRuntimeCompatibility.Exhaust(context, selected);
        bool paid = CombatManager.Instance.History.Entries.OfType<CardExhaustedEntry>()
            .Count(entry => ReferenceEquals(entry.Card, selected)) > exhaustedBefore;
        if (!paid || !HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
        await PowerCmd.Apply<ChargePower>(context, Owner.Creature,
            DynamicVars["ChargePower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["ChargePower"].UpgradeValueBy(1m);
}

[CustomID("HOTARU_BUOY")]
public sealed class HotaruBuoy : HotaruCannonCard
{
    public HotaruBuoy() : base(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    // It cut the target's highest six-sense resistance; since 2026-11-09 each point
    // became one stack of a debuff the target does not have yet.
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new IntVar("Times", 1m), new PowerVar<WeakPower>(1m), new PowerVar<VulnerablePower>(1m), new PowerVar<FragilePower>(1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => HbrMissingDebuffHelper.HoverTips();

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HbrMissingDebuffHelper.ApplyRandomMissing(context, play.Target, Owner.Creature, this,
            DynamicVars["Times"].IntValue);

    protected override void OnUpgrade() => DynamicVars["Times"].UpgradeValueBy(1m);
}

[CustomID("HOTARU_ECHO_LOCATION")]
public sealed class HotaruEchoLocation : HotaruCannonCard
{
    public HotaruEchoLocation() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new CardsVar(1), new IntVar("RollBonus", 15m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<HotaruEchoLocationPower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HotaruCommonUtilityCardHelper.CanContinue(this)) return;
        var combat = Owner.Creature.CombatState;
        // Drawing may autoplay a card. The newly granted bonus belongs to a
        // subsequent card, after all of this draw's hooks have completed.
        await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
        if (!HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
        await HotaruEchoLocationPower.Apply(context, Owner.Creature,
            DynamicVars["RollBonus"].BaseValue, this);
    }

    protected override void OnUpgrade() => DynamicVars["RollBonus"].UpgradeValueBy(10m);
}

[CustomID("HOTARU_POCKET_SORT")]
public sealed class HotaruPocketSort : HotaruCannonCard
{
    public HotaruPocketSort() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new CardsVar(2) };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HotaruCommonUtilityCardHelper.CanContinue(this)) return;
        var combat = Owner.Creature.CombatState;
        await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
        if (!IsUpgraded || !HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
        CardModel selected = (await CardSelectCmd.FromHandForDiscard(context, Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
                card => !ReferenceEquals(card, this), this))
            .FirstOrDefault(card => HotaruCommonUtilityCardHelper.IsOtherHandCard(this, card));
        if (selected == null || !HotaruCommonUtilityCardHelper.CanContinue(this, combat)) return;
        await CardCmd.Discard(context, selected);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

internal static class HotaruCommonUtilityCardHelper
{
    internal static bool CanContinue(CardModel card, ICombatState expectedCombat = null) =>
        card?.Owner?.Creature?.IsAlive == true && card.Owner.PlayerCombatState != null &&
        card.Owner.Creature.CombatState is { } combat &&
        (expectedCombat == null || ReferenceEquals(combat, expectedCombat)) &&
        !CombatManager.Instance.IsOverOrEnding;

    internal static bool IsOtherHandCard(CardModel source, CardModel card) =>
        card != null && !ReferenceEquals(card, source) && card.Owner == source.Owner &&
        card.Pile?.Type == PileType.Hand && PileType.Hand.GetPile(source.Owner).Cards.Contains(card);
}
