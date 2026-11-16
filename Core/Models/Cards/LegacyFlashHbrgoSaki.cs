using System.Collections.Generic;
using System.Linq;
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

public sealed class LegacyFlashHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneFMemberCard
{
    internal const int Flash = 0, MercifulBlade = 1, CoolDown = 2, Hope = 3,
        UnwaveringHeart = 4, InstantBlade = 5, Family = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneFMember ThirtyOneFMember => ThirtyOneFMember.NatsumeInori;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(47m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrCriticalRateUpPower>(),
        HoverTipFactory.FromPower<DestructionRatePower>()
    };
    public LegacyFlashHbrgoSaki() : base(1, CardType.Attack, TargetType.AnyEnemy) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("CondemnationThreshold", NatsumeCondemnationCombatHelper.GetThresholdPercent(this));
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => NatsumeCondemnationCombatHelper.Play(context, this, play);
}
