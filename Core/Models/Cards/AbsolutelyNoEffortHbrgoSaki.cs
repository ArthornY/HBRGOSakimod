using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class AbsolutelyNoEffortHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneEMemberCard
{
    internal const int DecisiveEve = 0, Homebody = 1, CandyBurst = 2, BubbleBurst = 3,
        OneMoreShot = 4, SleepIn = 5, DreamAwakening = 6;
    protected override int FlashEffectId => -1;
    public ThirtyOneEMember ThirtyOneEMember => ThirtyOneEMember.OshimaYotsuha;
    protected override bool IsPlayable => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Unplayable, CardKeyword.Retain };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    { HbrSkillVars.Damage(95m), HbrSkillVars.DamageBonus("FollowupDamage", 42m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrCriticalRateUpPower>(),
      HoverTipFactory.FromPower<HbrCriticalDamageUpPower>(), HoverTipFactory.FromPower<HbrComboSmallPower>(),
      HoverTipFactory.FromKeyword(CardKeyword.Retain), HoverTipFactory.FromKeyword(CardKeyword.Unplayable),
      HoverTipCompatibility.CreateKeyword(new LocString("cards", "HBRGO_ONE_MORE_SHOT.title"),
          new LocString("cards", "HBRGO_ONE_MORE_SHOT.description"), "HBRGO_ONE_MORE_SHOT") };
    public AbsolutelyNoEffortHbrgoSaki() : base(-1, CardType.Skill, TargetType.Self) { }
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) => !ReferenceEquals(card, this);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
    protected override void OnUpgrade()
    {
        base.OnUpgrade();
        if (HasUpgradeEffect(Homebody)) AddKeyword(CardKeyword.Innate);
    }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("YotsuhaAttackUp", HbrCardFaceDescription.Percent(this, "AttackUp", YotsuhaNoEffortCombatHelper.PreviewAttackUpBasis(this), true));
        text.Add("YotsuhaResting", HasUpgradeEffect(DecisiveEve) && YotsuhaNoEffortCombatHelper.WillSkip(this)
            ? "下次回合结束时不会获得攻击力上升。" : string.Empty);
    }
    public override Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants) =>
        YotsuhaNoEffortCombatHelper.TurnEnd(context, this, side, participants);
    public override Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play) =>
        YotsuhaNoEffortCombatHelper.AfterCardPlayed(context, this, play);
    public override Task BeforeCardPlayed(CardPlay play)
    {
        YotsuhaNoEffortCombatHelper.CapturePayment(this, play);
        return Task.CompletedTask;
    }
}
