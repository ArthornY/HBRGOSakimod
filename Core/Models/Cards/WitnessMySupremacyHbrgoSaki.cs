using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class WitnessMySupremacyHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneXMemberCard
{
    internal const int Flash = 0, Amplify = 1, SleepingDragonDance = 2, MantisAgainstChariot = 3,
        RegenerationFormation = 4, SomethingFromNothing = 5, DualFormation = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.LiYingxia;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(47m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrCriticalRateUpPower>(),
        HoverTipFactory.FromPower<HbrCriticalDamageUpPower>(), HoverTipFactory.FromPower<HbrDefenseUpPower>(),
        HoverTipFactory.FromPower<HbrDefenseDownPower>(), HoverTipFactory.FromPower<HbrComboSmallPower>(),
        HoverTipFactory.FromPower<YingxiaRegenerationFormationPower>(), HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    };
    public WitnessMySupremacyHbrgoSaki() : base(1, CardType.Skill, TargetType.Self) { }
    protected override void AddExtraArgsToDescription(LocString text)
    { base.AddExtraArgsToDescription(text); YingxiaFormationCombatHelper.AddDescriptionParameters(this, text); }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => YingxiaFormationCombatHelper.Play(context, this, play);
}
