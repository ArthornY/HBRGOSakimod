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

public sealed class LeaveAnyBattlefieldToMeHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneFMemberCard
{
    internal const int Flash = 0, ConcentratedStrike = 1, RotorKick = 2, RefreshingCheers = 3,
        RoadToStardom = 4, ScarsAreMyPride = 5, NearAndFar = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneFMember ThirtyOneFMember => ThirtyOneFMember.MatsuokaChiroru;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(36m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<MatsuokaBlowAwayPower>(), HoverTipFactory.FromPower<HbrDefenseUpPower>(),
        HoverTipFactory.FromPower<HbrComboSmallPower>(), HoverTipFactory.FromPower<HbrComboMediumPower>(),
        HoverTipFactory.FromPower<HbrCriticalRateUpPower>()
    };
    public LeaveAnyBattlefieldToMeHbrgoSaki() : base(2, CardType.Power, TargetType.Self) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        MatsuokaBlowAwayCombatHelper.AddDescriptionParameters(this, text);
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => MatsuokaBlowAwayCombatHelper.Play(context, this, play);
}
