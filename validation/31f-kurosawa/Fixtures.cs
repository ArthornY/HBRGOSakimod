using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
public class KurosawaAttackProbe:CardModel
{
    public override string Title=>"Kurosawa actual attack probe";
    public KurosawaAttackProbe():base(1,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>EffectChecks.KProbeBody(context,play);
}
public sealed class KurosawaSkillProbe:CardModel
{
    public override string Title=>"Kurosawa actual skill attack probe";
    public KurosawaSkillProbe():base(1,CardType.Skill,CardRarity.Common,TargetType.AnyEnemy){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>EffectChecks.KProbeBody(context,play);
}
public sealed class KurosawaGenericLeader:KurosawaAttackProbe,ILeadingChargeCard{public decimal LeadingChargePercent=>35m;}
public sealed class KurosawaObserverPower:PowerModel
{
    public int Extra;public bool Fill;public CardModel Nested;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
    public override int ModifyCardPlayCount(CardModel card,MegaCrit.Sts2.Core.Entities.Creatures.Creature target,int count)=>count+(card is HBRGOSakiMod.Core.Models.Cards.FollowBehindMeHbrgoSaki?Extra:0);
    public override Task AfterCardDrawn(PlayerChoiceContext context,CardModel card,bool fromHandDraw){EffectChecks.KDrawn.Add(card);return Task.CompletedTask;}
    public override Task AfterFlush(PlayerChoiceContext context,Player player,IReadOnlyCollection<CardModel> flushed,IReadOnlyCollection<CardModel> retained)
    {if(Fill&&player==Owner.Player)EffectChecks.KFill();return Task.CompletedTask;}
    public override Task BeforeCardPlayed(CardPlay play)
    {if(Nested is not{} next||play.Card is not HBRGOSakiMod.Core.Models.Cards.FollowBehindMeHbrgoSaki)return Task.CompletedTask;Nested=null;return EffectChecks.KNestedPlay(next);}
}
