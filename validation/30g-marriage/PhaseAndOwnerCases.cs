using System.Text.Json;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

static partial class EffectChecks
{
    static readonly List<int> GSanAtHandMove=[];
    static async Task GPhaseOrderCases()
    {
        GReset(2);var prior=GOrigin<NarcissismIsFineHbrgoSaki>();GMirror(prior);GSeedFor(2,san:25);await GPrepare();Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();
        var heart=GMirror(prior);var current=GOrigin<PaintTheWorldMyColorHbrgoSaki>();var proposed=GMirror(current);GSeedFor(2,san:25,serial:1);GSanAtHandMove.Clear();GSceneWait=new();
        Task start=GEntry();Check(!start.IsCompleted&&heart.Pile?.Type==PileType.Draw&&GGraduated(proposed),"72 next proposal presentation precedes moving last encounter's heart card from draw pile");
        var payload=(ThirtyGProposalPresentation)GPresentations.Last();Check(payload.TargetCard==proposed&&payload.Player==Player&&payload.SanRoll==25&&payload.SanAfter==50,"72 awaited presentation carries actual target/owner/rawSAN and predicted resultingSAN");
        GSceneWait.SetResult();await start;GSceneWait=null;
        Check(heart.Pile?.Type==PileType.Hand&&GSanAtHandMove.SequenceEqual(new[]{75}),"72 previous heart enters hand whileSAN still75, before deducting this proposal's25");
        Check(((ThirtyGCollectionRelic)GRelic).San==50&&GOpeningSans.Last()==50,"72 native opening-passive boundary observesSAN50 after the proposal deduction");
        ThirtyGProposalPresentationBridge.Requested-=GPresentation;
        try
        {
            GReset(2);GMirror(GOrigin<NarcissismIsFineHbrgoSaki>());GSeedFor(2,san:25);await GPrepare();Check(((ThirtyGCollectionRelic)GRelic).San==75&&GPresentations.Count==0,"72 no presentation implementation falls back without blocking mechanics");
        }
        finally{ThirtyGProposalPresentationBridge.Requested+=GPresentation;}
        bool enabled=HBRGOSakiMod.Core.Config.HBRGOSakiConfig.ThirtyGProposalPresentation;
        try
        {
            HBRGOSakiMod.Core.Config.HBRGOSakiConfig.ThirtyGProposalPresentation=false;GReset(2);var noScene=GOrigin<NarcissismIsFineHbrgoSaki>();GMirror(noScene);GSeedFor(2,san:25);await GPrepare();
            Check(GGraduated(noScene)&&((ThirtyGCollectionRelic)GRelic).San==75&&GPresentations.Count==0,"72 presentation setting suppresses only the awaited scene hook while mechanics remain active");
        }
        finally{HBRGOSakiMod.Core.Config.HBRGOSakiConfig.ThirtyGProposalPresentation=enabled;}
    }
    static async Task GSeparateOwnersCases()
    {
        GReset(2);var own=GOrigin<NarcissismIsFineHbrgoSaki>();GMirror(own);Player peer=SupportTeammate();
        var marker=(ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().ToMutable();Set(marker,"_owner",peer);Set(peer,"_relics",peer.Relics.Append(marker).ToList());
        foreach(Type type in GMembers.Take(2))
        {var card=(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();Set(card,"_owner",peer);peer.Deck.AddInternal(card);}
        var other=(NarcissismIsFineHbrgoSaki)ModelDb.Card<NarcissismIsFineHbrgoSaki>().ToMutable();Set(other,"_owner",peer);peer.Deck.AddInternal(other);CardModel otherMirror=GMirror(other);
        Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});
        await GPrepare();Check(GGraduated(own)&&!GGraduated(other)&&marker.San==100&&marker.Popularity==0,"72 first owner's proposal does not alter the second holder's card/SAN/popularity");
        int ownSan=((ThirtyGCollectionRelic)GRelic).San;await (Task)Call("ThirtyGBondHelper","PrepareBattle",new ThrowingPlayerChoiceContext(),peer)!;
        Check(GGraduated(otherMirror)&&marker.Popularity==1&&((ThirtyGCollectionRelic)GRelic).San==ownSan,"72 second holder independently proposes only to its own battle card");
        Native(marker,"SetSan",3);GResetSan();Check(((ThirtyGCollectionRelic)GRelic).San==100&&marker.San==3,"72 one owner's SAN reset leaves teammate's independent SAN untouched");
    }
    static async Task GRehydratePhysicalCases()
    {
        GReset(2);var original=GOrigin<NarcissismIsFineHbrgoSaki>();GMirror(original);GSeedFor(2,san:25);await GPrepare();Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();var current=GMirror(original);await GPrepare();
        string id=GId(original);var options=new JsonSerializerOptions{IncludeFields=true};string cardJson=JsonSerializer.Serialize(SavedProperties.From(original),options),relicJson=JsonSerializer.Serialize(SavedProperties.From(GRelic),options);
        var restoredCard=(NarcissismIsFineHbrgoSaki)ModelDb.Card<NarcissismIsFineHbrgoSaki>().ToMutable();Set(restoredCard,"_owner",Player);JsonSerializer.Deserialize<SavedProperties>(cardJson,options)!.Fill(restoredCard);
        var restoredRelic=(ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().ToMutable();Set(restoredRelic,"_owner",Player);JsonSerializer.Deserialize<SavedProperties>(relicJson,options)!.Fill(restoredRelic);
        Player.Deck.RemoveInternal(original,true);Player.Deck.AddInternal(restoredCard);var registry=(List<CardModel>)typeof(MegaCrit.Sts2.Core.Runs.RunState).GetField("_allCards",F)!.GetValue(RunState)!;registry.Remove(original);registry.Add(restoredCard);Set(Player,"_relics",Player.Relics.Select(r=>ReferenceEquals(r,GRelic)?restoredRelic:r).ToList());GRelic=restoredRelic;
        Call("ThirtyGProposalState","EnsureIdentities",Player);Check(GId(restoredCard)==id&&GHeart(restoredCard)==1,"72 true native restored card+relic JSON retains physical heart identity without old object instance");
        current.Pile!.RemoveInternal(current,true);var restoredMirror=GMirror(restoredCard);Check(GHeart(restoredMirror)==1,"72 a fresh combat mirror resolves restored permanent identity and heart state");
    }
}
