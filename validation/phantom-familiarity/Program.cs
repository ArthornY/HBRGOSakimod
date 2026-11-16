using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;

static class PhantomFamiliarityStandalone {public static void Run(string path)=>Checks.RunFamiliarity(path).GetAwaiter().GetResult();}
static partial class Checks
{
    static readonly Harmony FamiliarityHarmony=new("hbr.phantom-familiarity.checks");
    static Fixture FamFixture=null!;
    static MomentOfReign FamReign=null!;
    static bool FamInProgress=true;
    static readonly List<object> FamiliarityDetails=[];
    static bool FamiliarityInProgress(ref bool __result){__result=FamInProgress;return false;}
    static bool FamiliarityNotEnding(ref bool __result){__result=false;return false;}
    static bool FamiliarityListeners(ref IEnumerable<AbstractModel> __result)
    {__result=FamFixture==null?Array.Empty<AbstractModel>():FamFixture.Combat.Creatures.SelectMany(c=>c.Powers).Cast<AbstractModel>().Concat(FamFixture.Player.Relics).ToArray();return false;}
    static bool FamiliarityWait(ref Task __result){__result=Task.CompletedTask;return false;}
    static object? FamCall(string method,params object?[] args)
    {
        try{return typeof(MomentOfReign).GetMethod(method,F)!.Invoke(FamReign,args);}
        catch(TargetInvocationException ex)when(ex.InnerException!=null){ExceptionDispatchInfo.Capture(ex.InnerException).Throw();throw;}
    }
    static void FamiliaritySetup()
    {
        var prefix=new HarmonyMethod(typeof(Checks).GetMethod(nameof(Skip),F)!);
        FamiliarityHarmony.Patch(typeof(StringName).GetConstructor([typeof(string)])!,prefix:prefix);
        Type logger=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        if(logger.GetMethod("GetIsRunningFromGodotEditor",F) is {} editor)FamiliarityHarmony.Patch(editor,prefix:prefix);
        foreach(var method in logger.GetMethods(F).Where(m=>m.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))FamiliarityHarmony.Patch(method,prefix:prefix);
        foreach(Type type in new[]{typeof(RelicModel),typeof(PowerModel)})foreach(var method in type.GetMethods(F).Where(m=>m.Name=="Flash"))FamiliarityHarmony.Patch(method,prefix:prefix);
        foreach(Type type in new[]{typeof(SfxCmd),typeof(VfxCmd)})foreach(var method in type.GetMethods(F).Where(m=>m.Name.StartsWith("Play")))FamiliarityHarmony.Patch(method,prefix:prefix);
        foreach(var method in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))FamiliarityHarmony.Patch(method,prefix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(FamiliarityWait),F)!));
        foreach(Type type in new[]{typeof(MegaCrit.Sts2.Core.Runs.RunState),typeof(CombatState)})FamiliarityHarmony.Patch(type.GetMethod("IterateHookListeners")!,prefix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(FamiliarityListeners),F)!));
        FamiliarityHarmony.Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsInProgress"),prefix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(FamiliarityInProgress),F)!));
        FamiliarityHarmony.Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),prefix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(FamiliarityNotEnding),F)!));
        FamiliarityHarmony.Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsEnding"),prefix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(FamiliarityNotEnding),F)!));
        Type history=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory",true)!;
        foreach(var method in history.GetMethods(F).Where(m=>m.Name=="PowerReceived"))FamiliarityHarmony.Patch(method,prefix:prefix);
        MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn=true;
        foreach(Type type in new[]{typeof(SmallHopper),typeof(LeafSlimeS),typeof(MomentOfReign),typeof(FamiliarityHbrgoSaki),typeof(PhantomFamiliarityPower)})Canonical(type);
        Check(true,"reuses actual resource-scale Fixture, native canonical models and roster serializer; no main initializer");
    }
    static object FamEntry(int id,int experience=0,int value=1,Type? monster=null)=>Entry.GetConstructors(F).Single(c=>c.GetParameters().Length==10)
        .Invoke([id,Canonical(monster??typeof(SmallHopper)).Id,0,experience,value,0m,0m,0m,"",0m]);
    static void FamiliarityReset(int stacks=0,bool inCombat=true)
    {
        FamInProgress=inCombat;FamFixture=new Fixture(Blank<HbrgoSaki30A>(),enabled:false);
        var state=Blank<MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState>();Set(state,"_player",FamFixture.Player);Set(state,"_pets",new List<Creature>());Set(state,"<TurnNumber>k__BackingField",1);
        foreach(var pile in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})Set(state,"<"+pile.Item1+">k__BackingField",new CardPile(pile.Item2));
        Set(FamFixture.Player,"<PlayerCombatState>k__BackingField",state);Set(FamFixture.Player,"<Deck>k__BackingField",new CardPile(PileType.Deck));Set(FamFixture.Run,"_allCards",new List<CardModel>());Set(FamFixture.Combat,"_allCards",new List<CardModel>());
        FamReign=Relic<MomentOfReign>(FamFixture.Player);((List<RelicModel>)Field(FamFixture.Player.GetType(),"_relics").GetValue(FamFixture.Player)!).Add(FamReign);
        typeof(MomentOfReign).GetProperty("ActiveRosterEntryId",F)!.SetValue(FamReign,1);
        FamCall("SetRosterEntries",Entries(FamEntry(1),FamEntry(2),FamEntry(3)));
        if(stacks!=0)FamiliarityBuff(stacks);
    }
    static PhantomFamiliarityPower FamiliarityBuff(int stacks)
    {
        var power=(PhantomFamiliarityPower)((PowerModel)Canonical(typeof(PhantomFamiliarityPower))).ToMutable();Set(power,"_owner",FamFixture.Hero);Set(power,"_amount",stacks);
        ((List<PowerModel>)Field(typeof(Creature),"_powers").GetValue(FamFixture.Hero)!).Add(power);return power;
    }
    static int FamExperience(int id)
    {
        var rows=(IEnumerable)FamCall("GetRosterEntries")!;
        foreach(object row in rows)if((int)Entry.GetProperty("InstanceId",F)!.GetValue(row)! == id)return (int)Entry.GetProperty("Experience",F)!.GetValue(row)!;
        throw new Exception("No roster instance"+id);
    }
    static Dictionary<int,int> FamPending()=>(Dictionary<int,int>)FamCall("ParsePendingRosterExperience")!;
    static async Task FamiliarityArithmeticCases()
    {
        foreach(int stacks in new[]{0,1,2,-1,-100,int.MaxValue})
        foreach(int raw in new[]{-7,0,1,3,19,int.MaxValue})
        {
            FamiliarityReset(stacks);int expected=(int)Math.Min(1000000000L,(long)Math.Max(0,raw)*(1L+Math.Max(0,stacks)));
            Equal((int)FamCall("AddCombatExperienceBonus",raw)!,expected,$"real shared helper amount{raw} stacks{stacks}");
        }
        FamiliarityReset();Equal((int)FamCall("AddCombatExperienceBonus",7)!,7,"no Familiarity leaves amount unchanged");
        var power=FamiliarityBuff(2);Equal((int)FamCall("AddCombatExperienceBonus",7)!,21,"two counter stacks give triple experience");
        await PowerCmd.Remove(power);Equal((int)FamCall("AddCombatExperienceBonus",7)!,7,"real native removal restores unbuffed experience");
    }
    static async Task FamiliarityRosterCases()
    {
        FamiliarityReset(2);FamCall("AddExperienceToActiveRosterEntry",4);Equal(FamReign.PendingCombatExperience,12,"actual active-roster route queues triple4");
        FamCall("AddExperienceToActiveRosterEntry",0);FamCall("AddExperienceToActiveRosterEntry",-2);Equal(FamReign.PendingCombatExperience,12,"zero/negative active award cannot fabricate a bonus");
        FamiliarityReset(2);FamCall("AddExperienceToAllRosterEntries",2);Equal(FamReign.PendingCombatExperience,6,"all-roster route grants active entry triple2");
        var pending=FamPending();Check(pending.Count==2&&pending[2]==6&&pending[3]==6,"all-roster route grants each inactive entry the same once-resolved6");
        var power=FamFixture.Hero.GetPower<PhantomFamiliarityPower>()!;await PowerCmd.Remove(power);FamiliarityBuff(9);
        await FamReign.AfterCombatEnd(null!);foreach(int id in new[]{1,2,3})Equal(FamExperience(id),6,"actual battle-end commit uses queued6 without current-stack multiplication");
        Check(FamReign.PendingCombatExperience==0&&string.IsNullOrEmpty(FamReign.PendingRosterExperienceData),"actual battle-end clears both pending receipts");
        await FamReign.AfterCombatEnd(null!);foreach(int id in new[]{1,2,3})Equal(FamExperience(id),6,"repeated battle end cannot commit the receipt twice");
        FamiliarityReset(int.MaxValue);FamCall("AddExperienceToAllRosterEntries",int.MaxValue);Equal(FamReign.PendingCombatExperience,1000000000,"active pending experience saturates");Check(FamPending().Values.All(v=>v==1000000000),"inactive pending experience saturates");
        FamCall("AddExperienceToAllRosterEntries",1);Equal(FamReign.PendingCombatExperience,1000000000,"additional award cannot overflow saturated pending experience");
    }
    static async Task FamiliarityMinionAndFusionCases()
    {
        FamiliarityReset(2);FamCall("AddMinionSacrificeGrowth",1,4,3m,5);Equal(FamReign.PendingCombatExperience,60,"actual active minion sacrifice count4 times5 receives triple once");Equal(FamExperience(1),0,"active minion experience stays pending");
        FamCall("AddMinionSacrificeGrowth",2,4,3m,5);Equal(FamExperience(2),60,"actual inactive minion sacrifice gets same resolved60 directly");
        FamiliarityReset(2,inCombat:false);FamCall("AddMinionSacrificeGrowth",1,4,3m,5);Equal(FamExperience(1),60,"late minion completion uses direct route after combat has ended");Equal(FamReign.PendingCombatExperience,0,"late direct minion award is not queued a second time");
        FamiliarityReset(2);FamCall("AddMinionSacrificeGrowth",1,0,3m,5);FamCall("AddMinionSacrificeGrowth",1,-1,3m,5);Equal(FamReign.PendingCombatExperience,0,"invalid minion counts grant nothing");
        FamCall("SetRosterEntries",Entries(FamEntry(1),FamEntry(2,value:4)));
        bool consumed=await (Task<bool>)FamCall("TryConsumeRosterEntryForActivePhantom",new ThrowingPlayerChoiceContext(),2,3,8m)!;
        Check(consumed,"actual in-battle roster consume succeeds without UI replacement leaf");Equal(FamReign.PendingCombatExperience,36,"combat consume material4 times3 receives triple once");
        Check((bool)typeof(MomentOfReign).GetProperty("HasConsumedRosterThisTurn",F)!.GetValue(FamReign)!,"combat consume retains its original turn receipt");
        FamiliarityReset(1);FamCall("SetRosterEntries",Entries(FamEntry(1),FamEntry(2,value:4)));
        bool repeated=await (Task<bool>)FamCall("ConsumeRosterEntryAsRepeatedFusion",new ThrowingPlayerChoiceContext(),2,2,3)!;
        Check(repeated,"actual repeated in-battle fusion completes with one existing effect and no replacement UI");Equal(FamReign.PendingCombatExperience,48,"repeated combat fusion resolves4 times2 times3 then Familiarity2x once");
        FamiliarityReset(20,inCombat:false);FamCall("SetRosterEntries",Entries(FamEntry(1),FamEntry(2,value:4)));
        bool upgraded=(bool)FamCall("TryUpgradeRosterEntry",1,new[]{2},null,false)!;Check(upgraded,"actual manual out-of-combat fusion succeeds");Equal(FamExperience(1),4,"manual fusion remains raw material4 despite Familiarity stacks");
        FamiliarityReset(20,inCombat:false);FamCall("SetRosterEntries",Entries(FamEntry(1),FamEntry(2,value:4)));
        Check((bool)FamCall("TryDevourRosterEntries",1,new[]{2})!,"actual out-of-combat devour succeeds");Equal(FamExperience(1),8,"manual devour remains native double material4 without Familiarity multiplier");
    }
    static async Task FamiliarityCardAndLocalizationCases()
    {
        FamiliarityReset();var card=(FamiliarityHbrgoSaki)((CardModel)Canonical(typeof(FamiliarityHbrgoSaki))).ToMutable();Set(card,"_owner",FamFixture.Player);FamFixture.Player.PlayerCombatState!.Hand.AddInternal(card);((List<CardModel>)Field(typeof(CombatState),"_allCards").GetValue(FamFixture.Combat)!).Add(card);
        Check(!card.Keywords.Contains(CardKeyword.Innate),"unupgraded Familiarity is not innate");card.UpgradeInternal();card.FinalizeUpgradeInternal();Check(card.Keywords.Contains(CardKeyword.Innate),"Familiarity upgrade retains native Innate keyword");
        MethodInfo onPlay=typeof(FamiliarityHbrgoSaki).GetMethod("OnPlay",F)!;
        await (Task)onPlay.Invoke(card,[new ThrowingPlayerChoiceContext(),null])!;Equal(FamFixture.Hero.GetPowerAmount<PhantomFamiliarityPower>(),1,"real Familiarity OnPlay applies exactly one counter stack");
        await (Task)onPlay.Invoke(card,[new ThrowingPlayerChoiceContext(),null])!;Equal(FamFixture.Hero.GetPowerAmount<PhantomFamiliarityPower>(),2,"second real play retains additive counter stacking");Equal((int)FamCall("AddCombatExperienceBonus",5)!,15,"two actual card applications give3x shared experience");
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.."));
        while(!File.Exists(Path.Combine(root,"HBRGOSakiMod.csproj")))root=Directory.GetParent(root)?.FullName??throw new DirectoryNotFoundException();
        var cards=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"HBRGOSakiMod/localization/zhs/cards.json")))!;
        var powers=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"HBRGOSakiMod/localization/zhs/powers.json")))!;
        string cardId=ModelDb.GetId(typeof(FamiliarityHbrgoSaki)).Entry,powerId=ModelDb.GetId(typeof(PhantomFamiliarityPower)).Entry;
        foreach(string suffix in new[]{".title",".description"}){Check(cards.TryGetValue(cardId+suffix,out var value)&&!string.IsNullOrWhiteSpace(value),"actual native ModelId has exact card localization "+cardId+suffix);Check(powers.TryGetValue(powerId+suffix,out value)&&!string.IsNullOrWhiteSpace(value),"actual native ModelId has exact power localization "+powerId+suffix);}
        Check(cards[cardId+".description"].Contains("翻倍")&&powers[powerId+".description"].Contains("100%"),"card and stacked-power descriptions state doubling and100percent per layer");
        FamiliarityDetails.Add(new{cardId,powerId,cardDescription=cards[cardId+".description"],powerDescription=powers[powerId+".description"]});
    }
    public static async Task RunFamiliarity(string report)
    {
        string? error=null;try{FamiliaritySetup();await FamiliarityArithmeticCases();await FamiliarityRosterCases();await FamiliarityMinionAndFusionCases();await FamiliarityCardAndLocalizationCases();}catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,details=FamiliarityDetails,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),profile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual shared experience helper, roster serialization/active/all-roster/minion/consume/repeated-fusion/manual-fusion/devour methods, native PowerCmd apply/remove, card upgrade and real ModelIds. No live phantom is attached during fusion; no effect-limit selection is needed. Native combat lifecycle booleans are controlled to exercise queue/direct branches; audio/visual/wait/history leaves isolated. No full combat, Godot, rendered manual, network or save-file test."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count+" Familiarity checks");if(error!=null)throw new Exception("See "+report);
    }
}
