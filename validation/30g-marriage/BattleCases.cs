using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

static partial class EffectChecks
{
    static readonly List<int[]> GUpgradeOffers=[];
    static int GChoiceReservations,GChoiceSyncs;
    static TaskCompletionSource? GSceneWait;
    static int GSceneWaitedSan;
    static Task GPresentation(object presentation)
    {GPresentations.Add(presentation);GSceneWaitedSan=((ThirtyGCollectionRelic)GRelic).San;return GSceneWait?.Task??Task.CompletedTask;}
    static bool GMoveHand(CardModel card,PileType newPileType,ref Task<CardPileAddResult> __result)
    {
        var previous=card.Pile;previous?.RemoveInternal(card,true);CardPile next=card.Owner.PlayerCombatState!.AllPiles.Single(pile=>pile.Type==newPileType);next.AddInternal(card);GDrawMoves.Add(card);GSanAtHandMove.Add(card.Owner.GetRelic<ThirtyGCollectionRelic>()?.San??-1);
        __result=Task.FromResult(new CardPileAddResult{success=true,cardAdded=card,oldPile=previous,modifyingModels=[]});return false;
    }
    static object GReadData()=>Call("ThirtyGProposalState","Read",GRelic)!;
    static void GUpdateData(Action<JsonObject> update)
    {
        var json=string.IsNullOrEmpty(((ThirtyGCollectionRelic)GRelic).ProposalData)?new JsonObject():JsonNode.Parse(((ThirtyGCollectionRelic)GRelic).ProposalData)!.AsObject();update(json);Native(GRelic,"SetProposalData",json.ToJsonString());
    }
    static string GId(CardModel card)=>(string)Call("ThirtyGProposalState","IdentityOf",card)!;
    static Rng GNamed(RunRngSet rngs,int serial)
    {
        Type api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.RngCompatibility",true)!;
        return (Rng)Invoke(api.GetMethods(F).Single(m=>m.Name=="CreateNamed"&&m.GetParameters()[0].ParameterType==typeof(RunRngSet)),null,rngs,"hbrgo_30g_proposal_"+Player.NetId+"_"+serial)!;
    }
    static void GSeedFor(int kinds,int reaction=0,int? san=null,int serial=0)
    {
        for(int index=0;index<10000;index++)
        {
            var rngs=new RunRngSet("marriage72-"+index);Rng rng=GNamed(rngs,serial);rng.NextInt(1);int roll=kinds>=6?rng.NextInt(1,4):0,loss=rng.NextInt(1,101);
            if(roll==reaction&&(!san.HasValue||san==loss)){Set(RunState,"<Rng>k__BackingField",rngs);GSanRaw=loss;GReactionRaw=roll;return;}
        }
        throw new Exception("No deterministic named proposal RNG seed");
    }
    static async Task GNormalAndKiryuCases()
    {
        GReset(2);CardModel target=GOrigin<NarcissismIsFineHbrgoSaki>();Native(target,"PrepareUpgradeEffect",5);target.UpgradeInternal();target.FinalizeUpgradeInternal();NewDp();var mirror=GMirror(target);var self=GMirror(GDeck[0]);GSeedFor(2,san:25);
        var bonus=Power<RollBonusPower>(Hero,999);Native(bonus,"ConfigureUses",5);var advantage=Power<AdvantageDicePower>(Hero,3);Native(advantage,"ConfigureUses",4);var heartPower=Power<SwordheartPower>(Hero,7);
        int niche=RngCount(RunState.Rng.Niche),roll=RngCount(RunState.Rng.CombatCardSelection);GSceneWait=new();Task preparation=GEntry();
        Check(!preparation.IsCompleted&&GPresentations.Count==1,"72 real opening adapter waits for proposal presentation before ordinary opening passives");
        Check(GGraduated(target)&&GGraduated(mirror)&&!GGraduated(self),"72 proposed physical card and its battle mirror are graduated; Yuina is excluded");
        Check(GSceneWaitedSan==100&&((ThirtyGCollectionRelic)GRelic).San==100,"72 SAN deduction waits until proposal presentation and hearts hand placement complete");
        GSceneWait.SetResult();await preparation;GSceneWait=null;
        Check(GCardNotifications.Contains(target),"72 public changed callback names the changed physical card for the presentation layer");
        Check(!Hero.HasPower<NiinaMakeupPower>(),"72 graduated Niina permanent-deck card cannot grant its actual opening Makeup passive");
        Equal(((ThirtyGCollectionRelic)GRelic).San,75,"72 independent native named d100 deducts its exact raw25");
        Check(bonus.Amount==999&&bonus.RemainingUses==5&&advantage.Amount==3&&advantage.RemainingUses==4&&heartPower.Amount==7,"72 proposal SAN die neither applies nor spends bonus/advantage and awards no swordheart");
        Check(RngCount(RunState.Rng.Niche)==niche&&RngCount(RunState.Rng.CombatCardSelection)==roll,"72 proposal target/SAN stream does not consume ordinary modifier/card-selection RNG");
        Check(!mirror.CanPlay(out _,out _),"72 graduated card is rejected by actual native manual-play path");
        await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(),mirror,null);Check(!MarriageGardenFixture.Events.Any(e=>e.EndsWith(":play")),"72 graduated card also cannot enter native AutoPlay");
        Call("ThirtyGBondHelper","EndBattle",Player);Check(!GGraduated(target)&&GHeart(target)==0,"72 combat end clears active graduation and heart markers");
        GNextBattle();var returned=GMirror(target);await GPrepare();Check(GHeart(returned)==1&&returned.Pile?.Type==PileType.Hand,"72 next encounter moves the actual heart card to hand outside opening draw");
        Equal(GDrawMoves.Count,1,"72 hearts in hand require one physical pile move and no draw command");Check(!GGraduated(returned),"72 an active heart card cannot be proposed again");
        Equal(GRelic.ModifyCardPlayCount(returned,null!,1),2,"72 actual relic play-count hook adds one replay for a heartfelt card");
        Call("ThirtyGBondHelper","EndBattle",Player);Check(GHeart(target)==0,"72 heart ends with that encounter");

        GReset(2);target=GDeck[1];mirror=GMirror(target);GSeedFor(2,san:25);await GPrepare();Check(GGraduated(mirror),"72 Kiryu's first proposal is graduated");
        Equal((int)GSnapshot(mirror).GetType().GetProperty("GraduatedBattlesRemaining")!.GetValue(GSnapshot(mirror))!,2,"72 Kiryu begins with two encounter counters");
        Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();mirror=GMirror(target);await GPrepare();Check(GGraduated(mirror)&&GHeart(mirror)==0,"72 Kiryu remains graduated for the second encounter and cannot be reselected");
        Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();mirror=GMirror(target);await GPrepare();Check(!GGraduated(mirror)&&GHeart(mirror)==2&&mirror.Pile?.Type==PileType.Hand,"72 Kiryu enters the third encounter with doubled heart and bonus hand placement");
        Equal(GRelic.ModifyCardPlayCount(mirror,null!,1),3,"72 Kiryu's real play-count hook adds two replays");
    }
    static async Task GSanAndOwnerCases()
    {
        GReset(2);var target=GOrigin<NarcissismIsFineHbrgoSaki>();GMirror(target);GSeedFor(2,san:100);await GPrepare();Equal(((ThirtyGCollectionRelic)GRelic).San,0,"72 raw100 breaks full SAN exactly");Check(GRunNotifications.Any(e=>e.Player==Player&&e.State.Broken&&e.State.San==0),"72 public run-state notification reportsSAN break with its owner");
        Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();var mirror=GMirror(target);GMirror(GDeck[1]);await GPrepare();Check(GHeart(mirror)==1&&!GGraduated(GDeck[1]),"72 broken SAN stops new proposals while pending hearts still resolve");
        GResetSan();Check(GRunNotifications.Last().State.San==100&&!GRunNotifications.Last().State.Broken,"72 public run-state notification reports recovery and clears broken state");Equal(((ThirtyGCollectionRelic)GRelic).San,100,"72 explicit SAN recovery restores100 without clearing pending state");
        Native(GRelic,"SetSan",3);await GRelic.AfterRoomEntered(Blank<MegaCrit.Sts2.Core.Rooms.RestSiteRoom>());Equal(((ThirtyGCollectionRelic)GRelic).San,100,"72 actual rest-room relic hook restores SAN regardless of rest action");
        Native(GRelic,"SetSan",3);Set(RunState,"_currentActIndex",1);await GRelic.AfterRoomEntered(Blank<MegaCrit.Sts2.Core.Rooms.CombatRoom>());Equal(((ThirtyGCollectionRelic)GRelic).San,100,"72 new-act room entry restores SAN");
        GReset(2);GMirror(GDeck[0]);var excluded=GOrigin<AllOrdersConfirmedHbrgoSaki>();GMirror(excluded);var peer=SupportTeammate();var peerCard=(NarcissismIsFineHbrgoSaki)ModelDb.Card<NarcissismIsFineHbrgoSaki>().ToMutable();Set(peerCard,"_owner",peer);peer.PlayerCombatState!.Hand.AddInternal(peerCard);
        Set(RunState,"_players",new List<MegaCrit.Sts2.Core.Entities.Players.Player>{Player,peer});Set(Combat,"_allies",new List<MegaCrit.Sts2.Core.Entities.Creatures.Creature>{Hero,peer.Creature});await GPrepare();
        Check(((ThirtyGCollectionRelic)GRelic).Popularity==0&&((ThirtyGCollectionRelic)GRelic).San==100,"72 no eligible own candidate means no popularity, SAN die or proposal");
        Check(!GGraduated(peerCard)&&!GGraduated(excluded),"72 other player's garden and permanent-only Mari card cannot be proposed");
    }
}
