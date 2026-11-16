using System.Reflection;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
static partial class EffectChecks
{
    static async Task KBasicCases()
    {
        KReset();var card=KCard();Check(card.Type==CardType.Attack&&card.Rarity==CardRarity.Rare&&card.TargetType==TargetType.AllEnemies&&FCost(card)==2&&!card.Keywords.Contains(CardKeyword.Exhaust),"85 actual base is rare2cost nonexhausting all-enemy attack");
        Equal(card.DynamicVars.Damage.BaseValue,16.86m,"85 raw units retain exact281percent");await ChasePlay(card);
        Check(KAttacks.Single()is{Raw:16.86m,Hits:1}&&KSegments.Single().Coefficient==.6m&&KSegments.Single().Amp==0,"85 native wrapper requests281percent one hit, coefficient1point8 and no self charge bonus");
        Check(card.Pile==Player.PlayerCombatState!.DiscardPile,"85 actual native result pile is Discard");
        KReset();card=KCard(0,1,2);Equal(FCost(card),1,"85 Flash2to1");await ChasePlay(card);
        Check(KAttacks.Single().Down==2&&KAttacks.Single().DpDown==2,"85 actual30normal and60DP defense-down exist before attack, both duration2");
        Check(KSegments.Single().Coefficient==4m/3m,"85 One Strike One Soul declares original coefficient4 only on own direct command");
        var normal=Enemy.GetPower<HbrDefenseDownPower>()!;var dp=Enemy.GetPower<HbrDpDefenseDownPower>()!;
        Equal((decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",normal)!,(decimal)Call("HbrPercentDebuffHelper","ResolveBasis",normal,30m,Hero,Enemy)!,"85 normal30 snapshot uses common formula");
        Equal((decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",dp)!,(decimal)Call("HbrPercentDebuffHelper","ResolveBasis",dp,60m,Hero,Enemy)!,"85 DP60 snapshot uses common formula");
        foreach(var p in Enemy.Powers.OfType<HbrPercentDebuffPower>().ToArray())await p.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,new[]{Enemy});Check(normal.Amount==1&&dp.Amount==1,"85 first enemy end leaves both debuffs one turn");
        foreach(var p in Enemy.Powers.OfType<HbrPercentDebuffPower>().ToArray())await p.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,new[]{Enemy});Check(!Enemy.HasPower<HbrDefenseDownPower>()&&!Enemy.HasPower<HbrDpDefenseDownPower>(),"85 second enemy end expires both independent types");
        KReset();KProbeBudget=true;await ChasePlay(KCard(1));Equal(decimal.Floor(KObservedBudget),8m,"85 actual one-hit destruction4 times ordinary enemy coefficient2 reaches8 units without decimal underflow");
        KReset();card=KCard(5);var draw=FCard(typeof(FOtherCard),hand:false);await ChasePlay(card);Check(draw.Pile==Player.PlayerCombatState!.Hand&&KDrawn.SequenceEqual(new[]{draw}),"85 Library Committee draws one actual card after attack without kill or Break");
        KReset();card=KCard(0,1,2,3,4,5,6);Check(card.CurrentUpgradeLevel==7&&!card.Keywords.Contains(CardKeyword.Exhaust)&&card.LeadingChargePercent==50,"85 all seven independent upgrades retain nonexhausting attack and charge50");
        KReset(false);await ChasePlay(KCard(2,4,5));Check(KAttacks.Count==0&&!Enemy.HasPower<HbrDefenseDownPower>()&&KDrawn.Count==0,"85 legacy mode body does not apply HBR damage/debuff/support");
    }
    static async Task KLeadingCases()
    {
        KReset();var ordinary=FCard(typeof(KurosawaAttackProbe));await ChasePlay(ordinary);decimal baseline=KSegments.Single().Final;
        KReset();var leader=KCard();await ChasePlay(leader);ordinary=FCard(typeof(KurosawaAttackProbe));await ChasePlay(ordinary);
        Check(KSegments.First().Amp==0&&KSegments.Last().Amp==20,"85 first actual attack activates only subsequent own attacks");Equal(KSegments.Last().Final,decimal.Floor(baseline*1.2m),"85 charge20 enters real shared damage amplification bucket");
        await ChasePlay(FCard(typeof(KurosawaSkillProbe)));Check(KSegments.Last().Amp==0,"85 real skill-attached AttackCommand does not receive attack-card-only charge");
        var peer=SupportTeammate();Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});var peerAttack=FCard(typeof(KurosawaAttackProbe),peer);Equal(KAmp(peerAttack,peer.Creature),0,"85 own bonus never amplifies teammate's card");
        Equal(KAmp(ordinary,Hero,Enemy,ValueProp.Unpowered),0,"85 unpowered noncard effects do not inherit charge");
        KReset();await ChasePlay(KCard(3));await ChasePlay(FCard(typeof(KurosawaAttackProbe)));Equal(KSegments.Last().Amp,50,"85 Full Throttle grants50instead20");Equal(KSegments.Last().Final,decimal.Floor(baseline*1.5m),"85 charge50 follows actual shared damage pipeline");
        KReset();await ChasePlay(FCard(typeof(KurosawaAttackProbe)));await ChasePlay(KCard(3));await ChasePlay(FCard(typeof(KurosawaAttackProbe)));Check(KSegments.All(s=>s.Amp==0),"85 an earlier nonleader attack prevents later leader activation");
        KReset();await ChasePlay(FCard(typeof(KurosawaAttackProbe)),true);await ChasePlay(KCard());await ChasePlay(FCard(typeof(KurosawaAttackProbe)));Check(KSegments.All(s=>s.Amp==0),"85 earlier real AutoPlay counts against first attack");
        KReset();await ChasePlay(KCard(),true);await ChasePlay(FCard(typeof(KurosawaAttackProbe)));Check(KSegments.First().Amp==0&&KSegments.Last().Amp==20,"85 an automatically played first leader activates for later own cards");
        KReset();await ChasePlay(FCard(typeof(KurosawaSkillProbe)));await ChasePlay(KCard());await ChasePlay(FCard(typeof(KurosawaAttackProbe)));Equal(KSegments.Last().Amp,20,"85 earlier skill with damage does not count as an attack card");
        KReset();Hero.GetPower<KurosawaObserverPower>()!.Extra=2;await ChasePlay(KCard());Check(KSegments.Count==3&&KSegments.All(s=>s.Amp==0),"85 native replay series of the activating leader never buffs itself");
        await ChasePlay(FCard(typeof(KurosawaAttackProbe)));Equal(KSegments.Last().Amp,20,"85 native replays do not prevent next physical card bonus");
        KReset();await ChasePlay(FCard(typeof(KurosawaGenericLeader)));await ChasePlay(FCard(typeof(KurosawaAttackProbe)));Equal(KSegments.Last().Amp,35,"85 generic second leader type shares keyword policy and independent potency");
        KReset();var nested=FCard(typeof(KurosawaAttackProbe));Hero.GetPower<KurosawaObserverPower>()!.Nested=nested;await ChasePlay(KCard());
        Check(KSegments.First().Card==nested&&KSegments.First().Amp==20&&KSegments.Last().Card is FollowBehindMeHbrgoSaki&&KSegments.Last().Amp==0,"85 nested actual other-card autoplay is later, while outer leader keeps its excluded source scope");
        KReset();await ChasePlay(KCard());await KNativeFlush();ordinary=FCard(typeof(KurosawaAttackProbe));Equal(KAmp(ordinary),0,"85 native flush ends ordinary charge");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);Set(Player.PlayerCombatState,"_energy",50);await ChasePlay(KCard());await ChasePlay(ordinary);Equal(KSegments.Last().Amp,20,"85 next ordinary turn receives independent first-attack count");
        foreach(bool od in new[]{false,true})
        {
            KReset();await ChasePlay(KCard());ordinary=FCard(typeof(KurosawaAttackProbe));
            using(MisatoExtra(od))
            {Equal(KAmp(ordinary),0,"85 inserted turn does not inherit ordinary grant od="+od);await ChasePlay(KCard(3));await ChasePlay(ordinary);Equal(KSegments.Last().Amp,50,"85 inserted turn has independent first attack od="+od);await KExtraFlush(od);Equal(KAmp(ordinary),0,"85 actual inserted flush closes only inserted grant od="+od);}
            Equal(KAmp(ordinary),20,"85 resuming suspended ordinary turn restores prior20grant od="+od);
        }
    }
    static async Task KRetainAndDogCases()
    {
        KReset();var leader=KCard(4);var kept=KCard();var normal=FCard(typeof(FOtherCard));await ChasePlay(leader);await KNativeFlush();
        Check(kept.Pile==Player.PlayerCombatState!.Hand&&normal.Pile==Player.PlayerCombatState.DiscardPile,"85 actual native FlushPlayerHand retains only garden cards, discards unrelated hand");
        Check(!kept.ShouldRetainThisTurn,"85 native EndOfTurnCleanup clears one-turn retain flag without dropping retained card");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);for(int i=0;i<5;i++)FCard(typeof(FOtherCard),hand:false);await FSetupTurn();Check(kept.Pile==Player.PlayerCombatState.Hand,"85 retained physical card is still there at next actual draw");
        await KNativeFlush();Check(kept.Pile==Player.PlayerCombatState.DiscardPile,"85 retention grant does not silently persist into next turn");
        KReset(kinds:4);leader=KCard(4);kept=KCard();await ChasePlay(leader);await KNativeFlush();Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);FCard(typeof(FMember1),hand:false);for(int i=0;i<4;i++)FCard(typeof(FOtherCard),hand:false);await FSetupTurn();Equal(FCost(kept),1,"85 native next starting draw includes retained garden card in actual31F4cost equalization");
        KReset();leader=KCard(4);await ChasePlay(leader);var late=KCard();await KNativeFlush();Check(late.Pile==Player.PlayerCombatState!.Hand,"85 a garden card entering hand after leader is included at actual end boundary");
        KReset();var dog=KCard(6);await ChasePlay(dog);await KNativeFlush();Check(dog.Pile==Player.PlayerCombatState!.Hand,"85 real native result Discard then AfterFlush returns same physical dog");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);Set(Player.PlayerCombatState,"_energy",50);await ChasePlay(dog);await KNativeFlush();Check(dog.Pile==Player.PlayerCombatState.DiscardPile,"85 same physical card returns only once this battle");
        KReset();dog=KCard(6);await KNativeFlush();Check(dog.Pile==Player.PlayerCombatState!.Hand,"85 an unplayed dog discarded at end also meets actual same-turn discard rule");
        KReset();dog=KCard(6);await CardPileCmd.Add(dog,PileType.Discard);Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await KNativeFlush();Check(dog.Pile==Player.PlayerCombatState.DiscardPile,"85 a card already discarded last turn does not return on later flush");
        KReset();dog=KCard(6);await CardPileCmd.Add(dog,PileType.Exhaust);await KNativeFlush();Check(dog.Pile==Player.PlayerCombatState!.ExhaustPile,"85 exhausted dog does not return");
        KReset();dog=KCard(6);await CardPileCmd.Add(dog,PileType.Discard);dog.RemoveFromState();await KNativeFlush();Check(dog.HasBeenRemovedFromState&&dog.Pile==null,"85 removed dog never resurrected");
        KReset();dog=KCard(6);await ChasePlay(dog);Hero.GetPower<KurosawaObserverPower>()!.Fill=true;await KNativeFlush();Check(dog.Pile==Player.PlayerCombatState!.DiscardPile&&Player.PlayerCombatState.Hand.Cards.Count==CardPile.MaxCardsInHand,"85 full native hand routes returning dog to Discard without overflow");
        KReset();var one=KCard(6);var two=KCard(6);await ChasePlay(one);await ChasePlay(two);await KNativeFlush();Check(one.Pile==Player.PlayerCombatState!.Hand&&two.Pile==Player.PlayerCombatState.Hand,"85 independent physical copies each get their one battle return");
    }
    static void KThirtyOneFCases()
    {
        KReset();string[] names={"AsTheYoungLadyCommandsHbrgoSaki","LetMeShowYouClassHbrgoSaki","ThisIsMyStageHbrgoSaki","LeaveAnyBattlefieldToMeHbrgoSaki","LegacyFlashHbrgoSaki","FollowBehindMeHbrgoSaki"};
        foreach(string name in names){Type type=Mod.GetType("HBRGOSakiMod.Core.Models.Cards."+name,true)!;KRegister(type);MisatoPermanent(FCard(type));}
        var marker=Player.GetRelic<ThirtyOneFCollection>()!;Equal(marker.DisplayAmount,6,"85 all six real authorized31F cards form all six unique members");Check((bool)Call("ThirtyOneFBondHelper","HasBulwark",Player)!&& (bool)Call("ThirtyOneFBondHelper","HasSharedCosts",Player)!,"85 real six-card deck enables both4and6 thresholds");
        Equal((int)((IThirtyOneFMemberCard)Player.Deck.Cards.OfType<FollowBehindMeHbrgoSaki>().Single()).ThirtyOneFMember,(int)ThirtyOneFMember.KurosawaMaki,"85 actual Maki uses sixth31F member");
        Check((string)Call("HbrMemoryGarden","CharacterIdOf",Player.Deck.Cards.OfType<FollowBehindMeHbrgoSaki>().Single())! == "maki","85 actual garden maps Maki character");
    }
}
