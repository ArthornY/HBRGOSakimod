using System.Reflection;
using System.Globalization;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Localization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using Environment=System.Environment;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

static class OverchargeChecks {public static void Run(string path)=>StoryChecks.Run(path);}
static class StoryChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly,Native=typeof(ModelDb).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    sealed record Offer(Player Player,Reward[] Rewards);
    static readonly List<Offer> Offers=[];
    static bool FailNextOffer;
    static RunState RunState=null!;
    static Player A=null!,B=null!;
    static ElectronicSoldierManual ManualA=null!,ManualB=null!;
    static HbrStoryEvent Canonical=null!,StoryA=null!,StoryB=null!;
    static object Synchronizer=null!;
    static Type SyncType=>Native.GetType("MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer",true)!;
    static string Root {get{for(DirectoryInfo? dir=new(AppContext.BaseDirectory);dir!=null;dir=dir.Parent)if(File.Exists(Path.Combine(dir.FullName,"HBRGOSakiMod.csproj")))return dir.FullName;throw new DirectoryNotFoundException();}}
    static FieldInfo Field(Type type,string name){for(Type? t=type;t!=null;t=t.BaseType)if(t.GetField(name,F) is {} f)return f;throw new MissingFieldException(type.FullName,name);}
    static void Set(object obj,string name,object? value)=>Field(obj.GetType(),name).SetValue(obj,value);
    static object? Invoke(MethodInfo method,object? owner,params object?[] args){try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? Call(object obj,string method,params object?[] args)=>Invoke(obj.GetType().GetMethod(method,F)!,obj,args);
    static object? Helper(string name,params object?[] args)=>Invoke(Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrMemoryGarden",true)!.GetMethod(name,F)!,null,args);
    static T Blank<T>(){var obj=(T)RuntimeHelpers.GetUninitializedObject(typeof(T));GC.SuppressFinalize(obj!);return obj;}
    static object Blank(Type type){var obj=RuntimeHelpers.GetUninitializedObject(type);GC.SuppressFinalize(obj);return obj;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);Passed.Add(label);}
    static void Equal(int actual,int expected,string label)=>Check(actual==expected,$"{label}: {actual} = {expected}");
    static bool Skip()=>false;
    static bool OfferUi(Player __0,List<Reward> __1,ref Task __result)
    {
        Offers.Add(new(__0,__1.ToArray()));
        __result=FailNextOffer?Task.FromException(new InvalidOperationException("fixture reward UI failure")):Task.CompletedTask;
        FailNextOffer=false;return false;
    }
    static void Install()
    {
        TestMode.IsOn=true;var h=new Harmony("hbr.story.independent-choice.checks");
        h.Patch(typeof(StringName).GetConstructor([typeof(string)])!,prefix:new HarmonyMethod(typeof(StoryChecks).GetMethod(nameof(Skip),F)!));
        Type logger=Native.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        foreach(var method in logger.GetMethods(F).Where(m=>m.Name is "GetIsRunningFromGodotEditor" or "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))h.Patch(method,prefix:new HarmonyMethod(typeof(StoryChecks).GetMethod(nameof(Skip),F)!));
        foreach(var method in typeof(RelicModel).GetMethods(F).Where(m=>m.Name=="Flash"))h.Patch(method,prefix:new HarmonyMethod(typeof(StoryChecks).GetMethod(nameof(Skip),F)!));
        h.Patch(typeof(RewardsCmd).GetMethod(nameof(RewardsCmd.OfferCustom),F)!,prefix:new HarmonyMethod(typeof(StoryChecks).GetMethod(nameof(OfferUi),F)!));
        object definitions=Mod.GetType("HBRGOSakiMod.Core.Story.HbrStoryDefinitionStore",true)!.GetField("Definitions",F)!.GetValue(null)!;
        foreach(string file in Directory.GetFiles(Path.Combine(Root,"story/events"),"*.json"))
        {
            string path="res://story/events/"+Path.GetFileName(file);
            var definition=HbrStoryDefinition.Parse(File.ReadAllText(file),path);
            definitions.GetType().GetProperty("Item")!.SetValue(definitions,definition,[path]);
        }
        string game="D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";
        AssemblyLoadContext.Default.Resolving+=(context,name)=>{string file=Path.Combine(game,name.Name+".dll");return File.Exists(file)?context.LoadFromAssemblyPath(file):null;};
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        var tables=new Dictionary<string,LocTable>();foreach(string table in new[]{"events","rest_site_ui","characters"}){var text=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("D:/stsmod/sts/localization/zhs/"+table+".json"))!;foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"HBRGOSakiMod/localization/zhs/"+table+".json")))!)text[pair.Key]=pair.Value;tables[table]=new LocTable(table,text,null!);}Set(manager,"_tables",tables);typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
        ModelDb.Inject(typeof(HBRGOSaki31OCardPool));
    }
    static (Player,ElectronicSoldierManual) Player(ulong id)
    {
        var player=Blank<Player>();Set(player,"<NetId>k__BackingField",id);Set(player,"_runState",RunState);
        var creature=Blank<Creature>();Set(creature,"_powers",new List<PowerModel>());Set(creature,"_maxHp",3000);Set(creature,"_currentHp",1500);Set(creature,"<Player>k__BackingField",player);Set(player,"<Creature>k__BackingField",creature);
        var character=new HbrgoSaki31O();Set(character,"<Id>k__BackingField",new ModelId("CHARACTER","HBRGO_SAKI_31O"));Set(player,"<Character>k__BackingField",character);
        var manual=new ElectronicSoldierManual();Set(manual,"<IsMutable>k__BackingField",true);Set(manual,"<Id>k__BackingField",ModelDb.GetId(typeof(ElectronicSoldierManual)));Set(manual,"_owner",player);Set(player,"_relics",new List<RelicModel>{manual});
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData",F)!.SetValue(manual,JsonSerializer.Serialize(new{Schema=1,Enabled=true,HealthScaleApplied=true,Level=1}));
        return(player,manual);
    }
    static HbrStoryEvent Story(string id)
    {
        Type type=Mod.GetType("HBRGOSakiMod.Core.Models.Events."+(id=="LIBRARY"?"LibraryHbrgoSaki":"Story_"+id+"_HBRGO_SAKI"),true)!;
        var story=(HbrStoryEvent)Activator.CreateInstance(type)!;Set(story,"<Id>k__BackingField",new ModelId("EVENT","HBRGO_SAKI_MOD_EVENT_"+id+"_HBRGO_SAKI"));return story;
    }
    static void Begin(HbrStoryEvent story,Player player)
    {
        MethodInfo begin=typeof(EventModel).GetMethod("BeginEvent",F)!;
        object?[] args;
        if(begin.GetParameters().Length==2)args=[player,false];
        else{Type type=begin.GetParameters()[1].ParameterType;object combat=Activator.CreateInstance(type,F,null,[RunState,RunState],null)!;args=[player,combat,false];}
        ((Task)Invoke(begin,story,args)!).GetAwaiter().GetResult();
    }
    static void Reset()
    {
        Offers.Clear();FailNextOffer=false;RunState=Blank<RunState>();
        Set(RunState,"<Rng>k__BackingField",new RunRngSet("STORY-INDEPENDENT"));Set(RunState,"_visitedMapCoords",new List<MegaCrit.Sts2.Core.Map.MapCoord>());Set(RunState,"_mapPointHistory",new List<List<MapPointHistoryEntry>>{new()});Set(RunState,"_currentRooms",new List<AbstractRoom>());Set(RunState,"<ActFloor>k__BackingField",1);
        (A,ManualA)=Player(101);(B,ManualB)=Player(202);Set(RunState,"_players",new List<Player>{A,B});
        Canonical=Story("GYM");StoryA=(HbrStoryEvent)Canonical.ToMutable();StoryB=(HbrStoryEvent)Canonical.ToMutable();Begin(StoryA,A);Begin(StoryB,B);
        Synchronizer=Blank(SyncType);Set(Synchronizer,"_playerCollection",RunState);Set(Synchronizer,"_events",new List<EventModel>{StoryA,StoryB});Set(Synchronizer,"_canonicalEvent",Canonical);Set(Synchronizer,"_pendingOptionTasks",new List<Task>());Set(Synchronizer,"_logger",Blank(Native.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!));
    }
    static void Receive(Player sender,int index,bool awaitPending=true)
    {
        MethodInfo receiver=SyncType.GetMethod("HandleEventOptionChosenMessage",F)!;Type messageType=receiver.GetParameters()[0].ParameterType;object message=Activator.CreateInstance(messageType)!;
        Set(message,"type",Enum.ToObject(Field(messageType,"type").FieldType,0));Set(message,"optionIndex",(uint)index);
        Invoke(receiver,Synchronizer,message,sender.NetId);
        if(awaitPending)Await();
    }
    static void Await()=>((Task)Call(Synchronizer,"AwaitPendingOptionTasks")!).GetAwaiter().GetResult();
    static void Choose(EventOption option)=>option.Chosen().GetAwaiter().GetResult();
    static object Snapshot(string label)=>new{label,a=new{page=StoryA.CurrentStoryPageId,StoryA.IsFinished,ManualA.LeadershipCharm,ManualA.Madness,ManualA.MemoryGardenMetCharacters},b=new{page=StoryB.CurrentStoryPageId,StoryB.IsFinished,ManualB.LeadershipCharm,ManualB.Madness,ManualB.MemoryGardenMetCharacters},offers=Offers.Select(offer=>new{recipient=offer.Player.NetId,rewards=offer.Rewards.Select(reward=>new{type=reward.GetType().Name,owner=reward.Player.NetId,options=reward is CardReward cr? (int?)Field(typeof(CardReward),"<OptionCount>k__BackingField").GetValue(cr):null}).ToArray()}).ToArray()};
    static void MainCases()
    {
        Reset();Check(!ReferenceEquals(StoryA,StoryB)&&StoryA.Owner==A&&StoryB.Owner==B,"actual native ToMutable and BeginEvent produce distinct player-owned story states");
        EventOption oldA=StoryA.CurrentOptions[0];Receive(A,0);Check(StoryA.CurrentStoryPageId=="TAKI"&&StoryB.CurrentStoryPageId=="INITIAL","native received-message route changes only sender A's branch");
        Receive(B,1);Check(StoryA.CurrentStoryPageId=="TAKI"&&StoryB.CurrentStoryPageId=="KAREN","same room sender B independently selects different KAREN branch");
        Check(ManualA.MemoryGardenMetCharacters=="taki"&&ManualB.MemoryGardenMetCharacters=="karen","real RecordEncounters stores each owner's own branch character");
        Choose(oldA);Check(StoryA.CurrentStoryPageId=="TAKI"&&Offers.Count==0,"stale native INITIAL option cannot change page or grant reward");
        int version=(int)Field(typeof(HbrStoryEvent),"_stateVersion").GetValue(StoryA)!;EventOption currentOption=StoryA.CurrentOptions[0];
        ((Func<Task>)typeof(EventOption).GetProperty("OnChosen",F)!.GetValue(oldA)!)().GetAwaiter().GetResult();
        Equal((int)Field(typeof(HbrStoryEvent),"_stateVersion").GetValue(StoryA)!,version,"stored old callback cannot increment production page version after native gate is bypassed");
        Check(ReferenceEquals(StoryA.CurrentOptions[0],currentOption),"production stale-callback guard preserves current option instance without page reentry");
        EventOption rewardA=StoryA.CurrentOptions[0];Receive(A,0);Cases.Add(Snapshot("A completes before B"));
        Equal(ManualA.LeadershipCharm,8,"real A reward applies LeadershipCharm eight times");Equal(ManualA.Madness,0,"A does not receive B's Madness");Equal(ManualB.Madness,0,"A completion does not award B early");
        Check(StoryA.IsFinished&&!StoryB.IsFinished&&StoryB.CurrentStoryPageId=="KAREN","native A finished state leaves B reading and choosing independently");
        Check(Offers.Count==1&&Offers[0].Player==A&&Offers[0].Rewards is [CardReward card]&&card.Player==A&&(int)Field(typeof(CardReward),"<OptionCount>k__BackingField").GetValue(card)! ==3,"real A CardReward offer retains owner A and three options");
        Choose(rewardA);Equal(ManualA.LeadershipCharm,8,"old completed reward delegate cannot regrant Sense");Equal(Offers.Count,1,"old completed reward delegate cannot re-offer cards");
        Receive(B,0);Cases.Add(Snapshot("both complete"));Equal(ManualB.Madness,8,"real B reward applies Madness eight times");Equal(ManualB.LeadershipCharm,0,"B does not receive A's LeadershipCharm");
        Check(Offers.Count==2&&Offers[1].Player==B&&Offers[1].Rewards is [CardReward cardB]&&cardB.Player==B&&(int)Field(typeof(CardReward),"<OptionCount>k__BackingField").GetValue(cardB)! ==3,"real B CardReward offer retains owner B and three options");
        Check(((IReadOnlyList<EventModel>)SyncType.GetProperty("Events",F)!.GetValue(Synchronizer)!).All(story=>story.IsFinished),"native pending option tasks drain and both own states finish");
        Check(ManualA.MemoryGardenMetCharacters=="taki"&&ManualB.MemoryGardenMetCharacters=="karen","finishing both does not merge personal encounter records");
    }
    static void FailureCases()
    {
        Reset();Receive(A,0);EventOption reward=StoryA.CurrentOptions[0];FailNextOffer=true;bool failed=false;
        try{Choose(reward);}catch(InvalidOperationException e)when(e.Message=="fixture reward UI failure"){failed=true;}
        Check(failed,"controlled reward-selection UI fault propagates through real Choose");Equal(ManualA.LeadershipCharm,8,"Sense is actually granted before controlled UI fault");
        Check(StoryA.CurrentStoryPageId=="TAKI"&&!StoryA.IsFinished&&StoryB.CurrentStoryPageId=="INITIAL","UI fault retains own page and leaves teammate untouched");
        Check(!(bool)Field(typeof(HbrStoryEvent),"_choiceInProgress").GetValue(StoryA)!,"real Choose finally clears in-progress flag after reward UI fault");Choose(reward);Check(!StoryA.IsFinished&&StoryA.CurrentStoryPageId=="TAKI","native already-chosen option remains inert after controlled UI fault");Equal(ManualA.LeadershipCharm,8,"native already-chosen option prevents duplicate Sense after fault");Equal(Offers.Count,1,"native already-chosen option prevents repeated card offer after fault");Cases.Add(Snapshot("reward UI fault remains inert"));
    }
    static void RepeatCases()
    {
        Reset();var previous=new MapPointHistoryEntry(default,RunState){Rooms=new List<MapPointRoomHistoryEntry>{new(){RoomType=RoomType.Event,ModelId=Canonical.Id}}};var current=new MapPointHistoryEntry(default,RunState){Rooms=new List<MapPointRoomHistoryEntry>{new(){RoomType=RoomType.Event,ModelId=Canonical.Id}}};Set(RunState,"_mapPointHistory",new List<List<MapPointHistoryEntry>>{new(){previous,current}});
        string visit=(string)Helper("RepeatVisitKey",A,StoryA)!;Call(ManualA,"SetMemoryGardenRepeatRewardClaims",visit+"taki");
        Check((bool)Helper("IsRepeatMemoryEvent",A,StoryA)!&&RunState.CurrentMapPointHistoryEntry==current,"real native history distinguishes prior GYM visit from current node");
        Check(!(bool)Helper("NeedsRepeatMemoryRewardStep",A,StoryA,new[]{"taki"})!&&(bool)Helper("NeedsRepeatMemoryRewardStep",B,StoryB,new[]{"taki"})!,"independent repeat step reads personal visit claim instead of teammate eligibility");
        Receive(A,0);Receive(B,0);Receive(A,0);Receive(B,0);
        Check(StoryA.IsFinished&&!StoryB.IsFinished&&StoryB.CurrentStoryPageId=="SCENE"&&StoryB.CurrentOptions.Count==1,"same repeated map story finishes already-claimed owner while only pending owner gets repeat step");
        Check(ManualA.MemoryGardenRepeatRewardClaims==visit+"taki"&&string.IsNullOrEmpty(ManualB.MemoryGardenRepeatRewardClaims),"repeat step display does not write either owner's claim storage");Equal(current.GetEntry(A.NetId).EventChoices.Count,2,"native event-choice history records only A own two choices");Equal(current.GetEntry(B.NetId).EventChoices.Count,2,"native event-choice history records only B own two choices");Cases.Add(Snapshot("personal repeat step"));
        Reset();var onlyCurrent=new MapPointHistoryEntry(default,RunState){Rooms=new List<MapPointRoomHistoryEntry>{new(){RoomType=RoomType.Event,ModelId=Canonical.Id}}};Set(RunState,"_mapPointHistory",new List<List<MapPointHistoryEntry>>{new(){onlyCurrent}});Check(!(bool)Helper("IsRepeatMemoryEvent",A,StoryA)!,"current event's own history entry alone does not create repeat reward");
    }
    static void SharingCases()
    {
        foreach(string id in new[]{"CAFE","CINEMA","GYM","LIBRARY","NARVY","STUDIO"}){var story=Story(id);Check(!story.IsShared,"map memory "+id+" uses native personal event route");Check(story.Definition.Pages.All(page=>page.Choices.All(choice=>choice.Combat.Length==0)),"actual "+id+" JSON has no combat choices");}
        foreach(string id in new[]{"THEBEGINNING","INSIDETHEDOOR"}){var story=Story(id);Check(story.IsShared,"special "+id+" keeps shared native route");Set(Synchronizer,"_canonicalEvent",story);bool rejected=false;try{Receive(A,0);}catch(InvalidOperationException e)when(e.Message.Contains("during a shared event")){rejected=true;}Check(rejected,"native personal message receiver rejects shared special "+id);}
    }
    public static void Run(string path)
    {
        bool ok=false;string? error=null;try{Install();MainCases();FailureCases();RepeatCases();SharingCases();ok=true;}catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Native.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),boundary="Actual native EventModel.ToMutable/BeginEvent, received OptionIndexChosenMessage handler shared gate, sender lookup, GetEventForPlayer, native EventOption.Chosen and pending-task drain execute with production GYM Choose/ApplyRewards/Sense/encounter/claim/page logic. Real parsed repository JSON preloads definition cache instead of Godot resource I/O. OfferCustom UI terminal captures genuine owner-bound CardReward but does not select or add cards. StringName, logging and relic Flash isolated. Repeat tests use native history objects/readers and existing saved claim strings; no full save-load or repeated-memory card upgrade selection. No transport/message-buffer location validation, real network peers, renderer or game-room end/transition test; no packaging or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" independent story checks");if(!ok)throw new Exception("Inspect "+path);
    }
}