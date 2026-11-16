using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class NumbersAreTheSourceHbrgoSaki:ThirtyOneBGardenCard,IThirtyOneXMemberCard
{
    internal const int Flash=0, CompleteSubtraction=1, DancingKali=2, RhythmOfPrinciples=3, UltimateCurry=4, Multiplication=5, Kabaddi=6;
    protected override int FlashEffectId=>Flash;
    public ThirtyOneXMember ThirtyOneXMember=>ThirtyOneXMember.VritikaBalakrishnan;
    protected override IEnumerable<DynamicVar> CanonicalVars=>new[]{HbrSkillVars.Damage(51m)};
    protected override IEnumerable<IHoverTip> ExtraHoverTips=>new[]
    {
        HoverTipFactory.FromPower<VritikaCalculationPower>(),HoverTipFactory.FromPower<HbrAttackDownPower>(),
        HoverTipFactory.FromPower<HbrCalamityPower>(),HoverTipFactory.FromPower<HbrDefenseUpPower>(),HoverTipFactory.FromPower<DestructionRatePower>()
    };
    public NumbersAreTheSourceHbrgoSaki():base(1,CardType.Skill,TargetType.None){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>VritikaCalculationCombatHelper.Play(context,this);
}
