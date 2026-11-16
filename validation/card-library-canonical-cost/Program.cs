using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Environment=System.Environment;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.TestSupport;

static class OverchargeChecks { public static void Run(string path)=>CanonicalCostChecks.Run(path); }
static class CanonicalCostChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static readonly Assembly Native=typeof(ModelDb).Assembly,Mod=typeof(HotaruAddLight).Assembly;
    static readonly Harmony H=new("hbr.library.canonical-cost.regression");
    static readonly List<string> Passed=[];
    static decimal GlobalInput;
    static bool Skip()=>false;
    static bool NameCtor(StringName __instance){GC.SuppressFinalize(__instance);return false;}
    static object Blank(Type t){object o=RuntimeHelpers.GetUninitializedObject(t);GC.SuppressFinalize(o);return o;}
    static T Blank<T>()=>(T)Blank(typeof(T));
    static FieldInfo Field(Type t,string name){for(Type? x=t;x!=null;x=x.BaseType)if(x.GetField(name,F|BindingFlags.DeclaredOnly)is{} f)return f;throw new MissingFieldException(t.FullName,name);}
    static void Set(object o,string name,object? value)=>Field(o.GetType(),name).SetValue(o,value);
    static void Check(bool value,string name){if(!value)throw new Exception(name);Passed.Add(name);}
    static void Equal(decimal value,decimal expected,string name)=>Check(value==expected,$"{name}: {value} = {expected}");
    static object? Invoke(MethodInfo m,object? o,params object?[] args){try{return m.Invoke(o,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static void Prefix(MethodBase m,string name)=>H.Patch(m,prefix:new HarmonyMethod(typeof(CanonicalCostChecks).GetMethod(name,F)!));
    static bool GlobalLeaf([HarmonyArgument(2)] decimal cost,ref decimal __result){GlobalInput=cost;__result=cost-1;return false;}
    static void Install()
    {
        // Stable's pinned Ritsu runtime uses the ordinary managed Hashing9
        // dependency; load the existing local NuGet copy without changing the profile.
        string hashing=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".nuget/packages/system.io.hashing/9.0.0/lib/net9.0/System.IO.Hashing.dll");
        AssemblyLoadContext.Default.LoadFromAssemblyPath(hashing);
        TestMode.IsOn=true;
        Prefix(typeof(StringName).GetConstructor([typeof(string)])!,nameof(NameCtor));
        Type logger=Native.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        foreach(var m in logger.GetMethods(F).Where(m=>m.Name is "GetIsRunningFromGodotEditor" or "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))Prefix(m,nameof(Skip));
        foreach(string name in new[]{"Combat.IchikoFrugalBaseCostPatch","HotaruFirstManualEnergyPreviewPatch","Combat.MonakaFaithEnergyCostPatch","Combat.YamawakiBreakCostPatch"})
            Check(H.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches."+name,true)!).Patch().Count>0,"installs actual production "+name);
    }
    static CardModel Canonical(Type t)
    {
        if(!ModelDb.Contains(t))ModelDb.Inject(t);
        var card=ModelDb.GetById<CardModel>(ModelDb.GetId(t));
        Set(card,"<Id>k__BackingField",ModelDb.GetId(t));return card;
    }
    static Type[] Types()=>[typeof(Cruelty),typeof(BlackHole),typeof(LetMeShowYouClassHbrgoSaki),typeof(HotaruAddLight)];
    static void OwnerStillProtected(CardModel card)
    {
        bool rejected=false;try{_ = card.Owner;}catch(Exception e)when(e.GetType().Name=="CanonicalModelException"){rejected=true;}
        Check(rejected,"canonical.Owner still raises native CanonicalModelException; no global assertion bypass");
    }
    static void CanonicalCases()
    {
        CardModel[] cards=Types().Select(Canonical).ToArray();OwnerStillProtected(cards[0]);
        var before=cards.Select(c=>(c.EnergyCost.Canonical,c.IsCanonical,c.CurrentUpgradeLevel)).ToArray();
        foreach(var card in cards)
        {
            Equal(card.EnergyCost.GetResolved(),Math.Max(0,card.EnergyCost.Canonical),card.GetType().Name+" canonical GetResolved");
            Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),card.EnergyCost.Canonical,card.GetType().Name+" canonical GetWithModifiers");
        }
        Type gridType=Native.GetType("MegaCrit.Sts2.Core.Nodes.Cards.NCardGrid")??Native.GetTypes().Single(t=>t.Name=="NCardGrid");
        object grid=Blank(gridType);
        IDictionary comparers=(IDictionary)gridType.GetProperty("SortingAlgorithms",F)!.GetValue(grid)!;
        foreach(string direction in new[]{"CostAscending","CostDescending"})
        {
            object key=Enum.Parse(comparers.GetType().GetGenericArguments()[0],direction);
            var comparer=(Func<CardModel,CardModel,int>)comparers[key]!;
            var sorted=cards.Reverse().ToList();sorted.Sort((a,b)=>comparer(a,b));
            int[] values=sorted.Select(c=>c.EnergyCost.GetResolved()).ToArray();
            Check(values.SequenceEqual(direction=="CostAscending"?values.Order():values.OrderDescending()),"real native NCardGrid "+direction+" comparer sorts all four canonical prototypes");
        }
        Check(cards.Select(c=>(c.EnergyCost.Canonical,c.IsCanonical,c.CurrentUpgradeLevel)).SequenceEqual(before),"canonical costs, canonical flags and upgrade levels remain unchanged");
        OwnerStillProtected(cards[3]);
    }
    static void MutableCases()
    {
        var player=Blank<Player>();var hero=Blank<Creature>();Set(hero,"_powers",new List<PowerModel>());Set(hero,"<Player>k__BackingField",player);Set(player,"<Creature>k__BackingField",hero);
        Set(player,"<Deck>k__BackingField",new CardPile(PileType.Deck));
        var manual=new ElectronicSoldierManual();Set(manual,"<IsMutable>k__BackingField",true);Set(manual,"_owner",player);Set(manual,"<Id>k__BackingField",ModelDb.GetId(typeof(ElectronicSoldierManual)));Set(player,"_relics",new List<RelicModel>{manual});
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData",F)!.SetValue(manual,JsonSerializer.Serialize(new{Schema=1,Enabled=true,HealthScaleApplied=true,Level=1}));
        var target=new DiveAsDeepAsIWantHbrgoSaki();Set(target,"<IsMutable>k__BackingField",true);Set(target,"_owner",player);
        Equal(target.EnergyCost.GetResolved(),10,"mutable owned cost stays10without permanent Frugal");
        var supplier=new EldestOshimaIsHereHbrgoSaki();Set(supplier,"<IsMutable>k__BackingField",true);Set(supplier,"_owner",player);Set(supplier,"<Id>k__BackingField",ModelDb.GetId(typeof(EldestOshimaIsHereHbrgoSaki)));
        typeof(ThirtyOneBGardenCard).GetProperty("UpgradeEffectOrder",F)!.SetValue(supplier,new[]{2});Set(supplier,"_currentUpgradeLevel",1);player.Deck.AddInternal(supplier);
        Equal(target.EnergyCost.GetResolved(),5,"actual permanent Frugal supplier halves native starting base10to5");
        target.EnergyCost.AddThisTurn(2);Equal(target.EnergyCost.GetWithModifiers(CostModifiers.Local),7,"native relative local modifier runs after Frugal5then+2");
        hero.CombatState=Blank<CombatState>();
        var combat=Blank<PlayerCombatState>();Set(combat,"_player",player);Set(player,"<PlayerCombatState>k__BackingField",combat);
        foreach(var row in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})Set(combat,"<"+row.Item1+">k__BackingField",new CardPile(row.Item2));
        combat.Hand.AddInternal(target);Check(target.Pile?.Type==PileType.Hand&&target.CombatState!=null,"mutable cost fixture is a real native combat hand card");
        Prefix(typeof(Hook).GetMethods(F).Single(m=>m.Name=="ModifyEnergyCostInCombat"),nameof(GlobalLeaf));
        Equal(target.EnergyCost.GetResolved(),6,"native global cost boundary executes after Frugal and local modifiers");Equal(GlobalInput,7,"actual native global Hook receives7rather than pre-Frugal or pre-local amount");
        Equal(target.EnergyCost.Canonical,10,"Frugal does not mutate canonical base");
        player.Deck.RemoveInternal(supplier,true);Equal(target.EnergyCost.GetWithModifiers(CostModifiers.Local),12,"actual Deck ContentsChanged invalidates Frugal cache and preserves local+2");
    }
    static void NegativeCase()
    {
        CardModel card=Canonical(typeof(Cruelty));OwnerStillProtected(card);
        bool rejected=false;try{_ = card.EnergyCost.GetResolved();}catch(Exception e)when(e.GetType().Name=="CanonicalModelException"){rejected=true;}
        Check(rejected,"old delivered DLL genuinely throws CanonicalModelException from canonical GetResolved with real production patches");
    }
    public static void Run(string path)
    {
        bool ok=false;string?error=null;bool negative=Environment.GetEnvironmentVariable("HBR_EXPECT_CANONICAL_COST_FAILURE")=="1";
        try{Install();if(negative)NegativeCase();else{CanonicalCases();MutableCases();}ok=true;}
        catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,expectedNegativeControl=negative,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real Harmony four GetWithModifiers patches, ModelDb canonical prototypes, native NCardGrid cost comparers, native local modifier order and permanent Frugal deck policy. Godot StringName/logging and one global-cost Hook return isolated; no complete library scene or in-game rendering."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" canonical cost checks");
    }
}
