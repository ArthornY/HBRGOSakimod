using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
namespace HBRGOSakiMod.Core.Models.Cards;
public sealed class FollowBehindMeHbrgoSaki : ThirtyOneBGardenCard,IThirtyOneFMemberCard,ILeadingChargeCard
{
    internal const int Flash=0,OneStrikeOneSoul=1,BikersNiceToMeetYou=2,FullThrottle=3,NeverAbandonCompanions=4,LibraryCommittee=5,RescuedDog=6;
    protected override int FlashEffectId=>Flash;
    public ThirtyOneFMember ThirtyOneFMember=>ThirtyOneFMember.KurosawaMaki;
    public decimal LeadingChargePercent=>HasUpgradeEffect(FullThrottle)?50m:20m;
    internal decimal DestructionCoefficient=>HasUpgradeEffect(OneStrikeOneSoul)?4m:1.8m;
    protected override IEnumerable<DynamicVar> CanonicalVars=>new[]{HbrSkillVars.Damage(281m)};
    protected override IEnumerable<IHoverTip> ExtraHoverTips=>new[]{HoverTipFactory.FromPower<HbrDefenseDownPower>(),HoverTipFactory.FromPower<HbrDpDefenseDownPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Retain),HoverTipFactory.FromPower<DestructionRatePower>()};
    public FollowBehindMeHbrgoSaki():base(2,CardType.Attack,TargetType.AllEnemies){}
    protected override void AddExtraArgsToDescription(LocString text){base.AddExtraArgsToDescription(text);text.Add("KurosawaLeading",LeadingChargePercent);}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>KurosawaLeaderCombatHelper.Play(context,this,play);
    public override Task AfterCardChangedPiles(CardModel card,PileType oldPileType,AbstractModel clonedBy)
    {if(ReferenceEquals(this,card))KurosawaLeaderCombatHelper.RecordPile(this);return Task.CompletedTask;}
}
