using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

// Future-member cards are test-only; production 72 deliberately adds no cards.
public abstract class MarriageGardenFixture : ThirtyOneBGardenCard,IThirtyGMemberCard
{
    public static readonly List<string> Events=[];
    protected override int FlashEffectId=>0;
    public abstract ThirtyGMember ThirtyGMember {get;}
    protected MarriageGardenFixture():base(1,CardType.Skill,TargetType.Self){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {Events.Add(GetType().Name+":play");return Task.CompletedTask;}
    public override Task BeforeSideTurnEnd(PlayerChoiceContext context,CombatSide side,IEnumerable<Creature> participants)
    {if(side==CombatSide.Player&&participants.Contains(Owner.Creature))Events.Add(GetType().Name+":end");return Task.CompletedTask;}
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context,Player player)
    {if(player==Owner)Events.Add(GetType().Name+":start");return Task.CompletedTask;}
}
public sealed class MarriageYuinaFixture:MarriageGardenFixture{public override ThirtyGMember ThirtyGMember=>ThirtyGMember.ShirakawaYuina;}
public sealed class MarriageKiryuFixture:MarriageGardenFixture{public override ThirtyGMember ThirtyGMember=>ThirtyGMember.KiryuMiya;}
public sealed class MarriageMonakaFixture:MarriageGardenFixture{public override ThirtyGMember ThirtyGMember=>ThirtyGMember.TsukishiroMonaka;}
public sealed class MarriageChieFixture:MarriageGardenFixture{public override ThirtyGMember ThirtyGMember=>ThirtyGMember.SugawaraChie;}
public sealed class MarriageHisameFixture:MarriageGardenFixture{public override ThirtyGMember ThirtyGMember=>ThirtyGMember.OgasawaraHisame;}
public sealed class MarriageSatomiFixture:MarriageGardenFixture{public override ThirtyGMember ThirtyGMember=>ThirtyGMember.KuraSatomi;}
