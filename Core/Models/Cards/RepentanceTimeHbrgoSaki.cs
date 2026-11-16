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

public sealed class RepentanceTimeHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneXMemberCard
{
    internal const int Flash=0, Punishment=1, Knock=2, Requiem=3, WarriorsBlessing=4, DinnerIntroduction=5, DoNotDareDie=6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.MariaDeAngelis;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]{HbrSkillVars.Damage(105m)};
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrFaithPower>(),HoverTipFactory.FromPower<MariaRepentancePower>(),
        new HoverTip(new LocString("card_keywords","MARIA_SACRIFICE.title"),new LocString("card_keywords","MARIA_SACRIFICE.description")),
        new HoverTip(new LocString("card_keywords","MARIA_INSPIRATION.title"),new LocString("card_keywords","MARIA_INSPIRATION.description")),
        HoverTipFactory.FromPower<HbrAttackUpPower>(),HoverTipFactory.FromPower<HbrAttackDownPower>(),
        HoverTipFactory.FromPower<DestructionRatePower>()
    };
    public RepentanceTimeHbrgoSaki():base(1,CardType.Power,TargetType.None){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>MariaRepentanceCombatHelper.Play(context,this);
}
