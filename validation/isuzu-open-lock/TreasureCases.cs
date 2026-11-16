using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

public sealed class LockCommonA:RelicModel{public override RelicRarity Rarity=>RelicRarity.Common;}
public sealed class LockCommonB:RelicModel{public override RelicRarity Rarity=>RelicRarity.Common;}
public sealed class LockUncommonA:RelicModel{public override RelicRarity Rarity=>RelicRarity.Uncommon;}
public sealed class LockUncommonB:RelicModel{public override RelicRarity Rarity=>RelicRarity.Uncommon;}
public sealed class LockRareA:RelicModel{public override RelicRarity Rarity=>RelicRarity.Rare;}
public sealed class LockRareB:RelicModel{public override RelicRarity Rarity=>RelicRarity.Rare;}
static partial class EffectChecks
{
    static readonly Type[] LockRelicTypes=[typeof(LockCommonA),typeof(LockCommonB),typeof(LockUncommonA),typeof(LockUncommonB),typeof(LockRareA),typeof(LockRareB)];
    static int LockRelicMode,LockRelicSelections,LockObtainCalls;
    static readonly List<Player> LockObtainOrder=[];
    static Player? LockSelectedPlayer;
    static RelicModel[] LockRelicOptions=[];
    static TaskCompletionSource<RelicModel?>? LockWaitChoice;
    static bool LockRelicSelectLeaf(Player player,IReadOnlyList<RelicModel> relics,ref Task<RelicModel?> __result)
    {
        LockRelicSelections++;LockSelectedPlayer=player;LockRelicOptions=relics.ToArray();
        Check(relics.Count==2 && relics[0].Id!=relics[1].Id,"70 native private selector receives two distinct exact relic options");
        if(LockRelicMode==3){__result=Task.FromException<RelicModel?>(new OperationCanceledException("choice cancelled"));return false;}
        if(LockRelicMode==4){LockWaitChoice=new();__result=LockWaitChoice.Task;return false;}
        __result=Task.FromResult(LockRelicMode==2?null:relics[LockRelicMode]);return false;
    }
    static bool LockObtainLeaf(RelicModel relic,Player player,ref Task<RelicModel> __result)
    {
        LockObtainCalls++;LockObtainOrder.Add(player);Check(relic.IsMutable,"70 selected actual obtain argument is a mutable native relic instance");
        Set(relic,"_owner",player);Set(player,"_relics",player.Relics.Append(relic).ToList());
        __result=Task.FromResult(relic);return false;
    }
    static (TreasureRoomRelicSynchronizer Sync,Rng Rng,RelicModel Original) LockTreasureReset(bool passive=true)
    {
        LockReset();
        object scoutCaches=Mod.GetType("HBRGOSakiMod.Core.Map.HbrThirtyOneDScoutingHelper",true)!.GetField("Caches",F)!.GetValue(null)!;scoutCaches.GetType().GetMethod("Remove")!.Invoke(scoutCaches,[RunState]);
        LockRelicMode=LockRelicSelections=LockObtainCalls=0;LockObtainOrder.Clear();LockRelicOptions=[];LockSelectedPlayer=null;LockWaitChoice=null;
        var card=NewGarden<OpenTheLockHbrgoSaki>(6);if(passive)MisatoPermanent(card);
        Set(RunState,"_players",new List<Player>{Player});
        foreach(RelicModel relic in Player.Relics)Set(relic,"<Id>k__BackingField",ModelDb.GetId(relic.GetType()));
        var bag=new RelicGrabBag(true);bag.Populate(LockRelicTypes.Select(t=>ModelDb.GetById<RelicModel>(ModelDb.GetId(t))),new Rng(1316));
        Set(RunState,"<SharedRelicGrabBag>k__BackingField",bag);
        RelicModel original=bag.PullFromFront(RelicRarity.Common,RunState)!.ToMutable();
        var rng=new Rng(4631);var sync=new TreasureRoomRelicSynchronizer(RunState,Player.NetId,null!,bag,rng);
        Set(RunManager.Instance,"<TreasureRoomRelicSynchronizer>k__BackingField",sync);
        Call("IsuzuTreasureUnlockHelper","Begin",sync);
        return(sync,rng,original);
    }
    static Task<RelicModel> LockObtain(RelicModel relic)=> (Task<RelicModel>)Call("IsuzuTreasureUnlockHelper","Obtain",relic,Player,-1)!;
    static RelicGrabBag LockPeerBag()
    {
        var bag=new RelicGrabBag(false);bag.Populate(LockRelicTypes.Select(t=>ModelDb.GetById<RelicModel>(ModelDb.GetId(t))),new Rng(77));return bag;
    }
    static bool LockPeerFront(RelicGrabBag bag,RelicModel relic)=>((Dictionary<RelicRarity,List<RelicModel>>)typeof(RelicGrabBag).GetField("_deques",F)!.GetValue(bag)!).Values.SelectMany(x=>x).Any(x=>x.Id==relic.Id);
    static bool LockPeerFallback(RelicGrabBag bag,RelicModel relic)=>((List<RelicModel>)typeof(RelicGrabBag).GetField("_mpFallbackDequeue",F)!.GetValue(bag)!).Any(x=>x.Id==relic.Id);
    static void LockFallback(RelicGrabBag bag,RelicModel original)=>Call("IsuzuTreasureUnlockHelper","MoveToFallback",bag,ModelDb.GetById<RelicModel>(original.Id));
    static async Task LockFallbackCases()
    {
        foreach(bool pending in new[]{false,true})
        {
            var data=LockTreasureReset();var peer=LockPeerBag();LockRelicMode=pending?4:1;Task<RelicModel> task=LockObtain(data.Original);RelicModel extra=LockRelicOptions[1];
            LockFallback(peer,data.Original);
            if(pending)
            {
                Check(LockPeerFront(peer,data.Original)&&LockPeerFront(peer,extra),"70 native peer fallback waits for synchronized private selection instead of demoting the original early");
                LockWaitChoice!.SetResult(extra);
            }
            await task;Check(LockPeerFront(peer,data.Original)&&!LockPeerFallback(peer,data.Original),"70 discarded original preserves peer bag's original dequeue tier; pending="+pending);
            Check(!LockPeerFront(peer,extra)&&LockPeerFallback(peer,extra),"70 actual selected extra alone moves into peer fallback; pending="+pending);
            LockFallback(peer,data.Original);Check(LockPeerFront(peer,data.Original),"70 repeated native fallback callback cannot demote unchosen original");
        }
        {
            var data=LockTreasureReset();var peer=LockPeerBag();LockRelicMode=4;Task<RelicModel> task=LockObtain(data.Original);RelicModel extra=LockRelicOptions[1];LockFallback(peer,data.Original);
            LockWaitChoice!.SetException(new OperationCanceledException("controlled selection failure before sync"));try{await task;}catch(OperationCanceledException){}
            LockFallback(peer,data.Original);Check(LockPeerFront(peer,data.Original)&&LockPeerFront(peer,extra)&&!LockPeerFallback(peer,data.Original)&&!LockPeerFallback(peer,extra),"70 failed selector neither changes peer dequeue tiers nor replays a late fallback");
        }
        {
            var data=LockTreasureReset(false);var peer=LockPeerBag();await LockObtain(data.Original);LockFallback(peer,data.Original);
            Check(!LockPeerFront(peer,data.Original)&&LockPeerFallback(peer,data.Original),"70 no permanent treasure upgrade retains exact native peer-fallback fast path");
        }
    }
    static async Task LockTreasureCases(Harmony h)
    {
        foreach(Type t in LockRelicTypes){if(!ModelDb.Contains(t))ModelDb.Inject(t);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(t)),"<Id>k__BackingField",ModelDb.GetId(t));}
        foreach(string name in new[]{"IsuzuTreasureUnlockBeginPatch","IsuzuTreasureUnlockAwardPatch","IsuzuTreasurePendingFilterPatch","ThirtyOneDScoutingReservedRelicFilterPatch"})
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches."+name,true)!).Patch().Count>0,"70 installs exact native treasure integration "+name);
        h.Patch(typeof(RelicSelectCmd).GetMethod(nameof(RelicSelectCmd.FromChooseARelicScreen))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(LockRelicSelectLeaf),F)!));
        h.Patch(typeof(RelicCmd).GetMethod(nameof(RelicCmd.Obtain),new[]{typeof(RelicModel),typeof(Player),typeof(int)})!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(LockObtainLeaf),F)!));
        {
            var data=LockTreasureReset();var named=new Rng(163);
            Type scout=Mod.GetType("HBRGOSakiMod.Core.Map.HbrThirtyOneDScoutingHelper",true)!;
            object[] rows=((System.Collections.IEnumerable)Invoke(scout.GetMethod("ReserveTreasure",F)!,null,RunState,named)!).Cast<object>().ToArray();
            Check(rows.Length==1&&RngCount(named)==1&&LockRelicSelections==0&&RngCount(data.Rng)==0,"70 actual31D scouting freezes only its original slot and never draws Isuzu private extra early");
            string reserved=(string)rows[0].GetType().GetProperty("RelicId")!.GetValue(rows[0])!;
            var independent=new RelicGrabBag(true);independent.Populate(LockRelicTypes.Select(t=>ModelDb.GetById<RelicModel>(ModelDb.GetId(t))),new Rng(19));
            RelicModel reservedModel=ModelDb.GetById<RelicModel>(new ModelId("RELIC",reserved));
            Check(independent.PullFromFront(reservedModel.Rarity,r=>r.Id.Entry==reserved,RunState)==null,"70 private extra path composes with31D future-reservation exclusion in other bags");
            await LockObtain(data.Original);Equal(RngCount(data.Rng),1,"70 Isuzu extra rolls only when actually acquiring an assigned chest item");
            Check(LockRelicOptions.All(r=>r.Id.Entry!=reserved),"70 private offered extra does not consume an unrelated future31D reservation");
        }
        foreach(int mode in new[]{0,1,2})
        {
            var data=LockTreasureReset();LockRelicMode=mode;int before=RngCount(data.Rng);
            RelicModel result=await LockObtain(data.Original);
            Equal(LockRelicSelections,1,"70 private relic selection runs once at mode"+mode);Equal(LockObtainCalls,1,"70 one native obtain only at mode"+mode);
            Check(LockSelectedPlayer==Player,"70 offer belongs only to actual awarded card holder");
            Equal(RngCount(data.Rng)-before,1,"70 extra relic rarity drawn once at actual opening, never during31D scouting");
            Check(result.Id==(mode==1?LockRelicOptions[1]:data.Original).Id,"70 choice/cancel/fault retains expected actual item at mode"+mode);
            RelicModel unused=mode==1?data.Original:LockRelicOptions[1];
            Check(RunState.SharedRelicGrabBag.Contains(ModelDb.GetById<RelicModel>(unused.Id)),"70 unchosen physical item returns to shared bag at mode"+mode);
            Task<RelicModel> duplicate=LockObtain(data.Original);Check(ReferenceEquals(await duplicate,result)&&LockRelicSelections==1&&LockObtainCalls==1,"70 duplicate exact award callback reuses completed receipt without another draw or obtain");
            Check(!(bool)Call("IsuzuTreasureUnlockHelper","HasPending",RunState)!,"70 pending filter closes after choice completion");
        }
        {
            var data=LockTreasureReset();LockRelicMode=3;bool fault=false;
            try{await LockObtain(data.Original);}catch(OperationCanceledException){fault=true;}
            Check(fault&&LockObtainCalls==0&&LockRelicSelections==1,"70 exceptional UI teardown does not grant an unsynchronized local-only item");
            Check(RunState.SharedRelicGrabBag.Contains(ModelDb.GetById<RelicModel>(data.Original.Id))&&RunState.SharedRelicGrabBag.Contains(ModelDb.GetById<RelicModel>(LockRelicOptions[1].Id))&&!(bool)Call("IsuzuTreasureUnlockHelper","HasPending",RunState)!,"70 exceptional selection returns both offers and releases all pending IDs");
        }
        {
            var data=LockTreasureReset(false);int before=RngCount(data.Rng);await LockObtain(data.Original);
            Check(LockRelicSelections==0&&LockObtainCalls==1&&RngCount(data.Rng)==before,"70 combat-only copy never activates permanent treasure passive");
        }
        {
            var data=LockTreasureReset();LockRelicMode=4;Task<RelicModel> pending=LockObtain(data.Original);
            Check(!pending.IsCompleted&&LockWaitChoice!=null,"70 private native choice may suspend without granting either item early");
            var otherBag=new RelicGrabBag(true);otherBag.Populate(LockRelicOptions,new Rng(542));
            foreach(RelicModel item in LockRelicOptions)
                Check(otherBag.PullFromFront(item.Rarity,relic=>relic.Id==item.Id,RunState)==null,"70 another player's bag cannot draw the pending original/extra through refresh");
            LockWaitChoice!.SetResult(LockRelicOptions[1]);RelicModel result=await pending;
            Check(result.Id==LockRelicOptions[1].Id&&!(bool)Call("IsuzuTreasureUnlockHelper","HasPending",RunState)!,"70 delayed completion releases pending filter after one selected obtain");
            var another=RunState.SharedRelicGrabBag.PullFromFront(RelicRarity.Common,RunState)!.ToMutable();int before=RngCount(data.Rng);await LockObtain(another);
            Check(RngCount(data.Rng)==before&&LockRelicSelections==1,"70 second native award in same chest does not offer a second private extra");
        }
        {
            var data=LockTreasureReset();
            var peer=Blank<Player>();Set(peer,"_runState",RunState);Set(peer,"_relics",new List<RelicModel>());Set(peer,"<Deck>k__BackingField",new MegaCrit.Sts2.Core.Entities.Cards.CardPile(MegaCrit.Sts2.Core.Entities.Cards.PileType.Deck));Set(peer,"<NetId>k__BackingField",2UL);
            Set(RunState,"_players",new List<Player>{Player,peer});
            RelicModel assigned=RunState.SharedRelicGrabBag.PullFromFront(RelicRarity.Common,RunState)!.ToMutable();
            LockRelicMode=4;Task<RelicModel> first=LockObtain(data.Original);
            Task<RelicModel> second=(Task<RelicModel>)Call("IsuzuTreasureUnlockHelper","Obtain",assigned,peer,-1)!;
            Check(!first.IsCompleted&&!second.IsCompleted&&LockObtainCalls==0,"70 teammate's ordinary award queues behind pending private choice rather than racing native obtain");
            LockWaitChoice!.SetResult(LockRelicOptions[0]);await first;await second;
            Check(LockObtainOrder.SequenceEqual(new[]{Player,peer})&&LockRelicSelections==1&&RngCount(data.Rng)==1,"70 multiplayer assigned items retain original deterministic order and non-holder never receives an extra choice");
        }
        {
            var data=LockTreasureReset();var bag=new RelicGrabBag(false);bag.Populate(Array.Empty<RelicModel>(),new Rng(1));Set(RunState,"<SharedRelicGrabBag>k__BackingField",bag);LockRelicMode=1;
            RelicModel result=await LockObtain(data.Original);
            Check(result.Id==data.Original.Id&&LockRelicSelections==0&&LockObtainCalls==1,"70 empty extra bag falls back to awarded original without inventing an option");
        }
        await LockFallbackCases();
    }
}
