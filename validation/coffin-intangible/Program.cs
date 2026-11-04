using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class OverchargeChecks { public static void Run(string report)=>CoffinChecks.Run(report); }
static class CoffinChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(HotaruMyCoffinPower).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    sealed record CapCall(Creature? Target,Creature? Dealer,CardModel? Card,CardPlay? Play,ValueProp Props,decimal Result);
    static readonly List<CapCall> CapCalls=[];
    static readonly List<object> Receipts=[];
    sealed record CapNotification(CardModel? Source,decimal DpBefore,AbstractModel[] Modifiers);
    static readonly List<CapNotification> CapNotifications=[];
    static Creature Hero=>State<Creature>("Hero");
    static Creature Enemy=>State<Creature>("Enemy");
    static Player Player=>State<Player>("Player");
    static CombatState Combat=>State<CombatState>("Combat");
    static Creature Cannon=null!,Second=null!;
    static HotaruMyCoffinPower Coffin=null!;
    static HighDimensionalShellPower? Shell;
    static CardModel Card=null!;
    static CardPlay Play=null!;
    static int DamageCommands,CapZeroCalls;
    static T State<T>(string name)=>(T)typeof(Checks).GetField(name,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method,object? owner,params object?[] args)
    {try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? H(string method,params object?[] args)=>Invoke(typeof(Checks).GetMethod(method,F)!,null,args);
    static Type Helper(string name)=>Mod.GetType("HBRGOSakiMod.Core.Helpers."+name,true)!;
    static void Set(object owner,string field,object? value)=>H("Set",owner,field,value);
    static T Blank<T>()=>(T)Invoke(typeof(Checks).GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static List<PowerModel> Powers(Creature owner)=>(List<PowerModel>)H("Powers",owner)!;
    static T NativePower<T>(Creature owner,int amount)where T:PowerModel=>(T)Invoke(typeof(Checks).GetMethod("Power",F)!.MakeGenericMethod(typeof(T)),null,owner,amount)!;
    static T Attach<T>(T power,Creature owner,int amount)where T:PowerModel
    {Set(power,"<IsMutable>k__BackingField",true);Set(power,"_owner",owner);Set(power,"_amount",amount);Set(power,"<Id>k__BackingField",ModelDb.GetId(typeof(T)));Powers(owner).Add(power);return power;}
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed.Add(message);}
    static void Equal(decimal actual,decimal expected,string message)=>Check(actual==expected,$"{message}: {actual} = {expected}");
    static bool Skip()=>false;
    static bool VisualTask(ref Task __result){__result=Task.CompletedTask;return false;}
    static void CountDamage()=>DamageCommands++;
    static bool Listeners(ref IEnumerable<AbstractModel> __result)
    {__result=Combat.Creatures.SelectMany(creature=>creature.Powers).Cast<AbstractModel>().Concat(Player.Relics).ToArray();return false;}
    static void ObserveIntangible(object[] __args,decimal __result)=>CapCalls.Add(new((Creature?)__args[0],(Creature?)__args[2],(CardModel?)__args[3],__args.Length>4?(CardPlay?)__args[4]:null,(ValueProp)__args[1],__result));
    static void ObserveReceipt(object receipt)=>Receipts.Add(receipt);
    static void ObserveCapNotification(object[] __args)=>CapNotifications.Add(new((CardModel?)__args[2],Shell?.CurrentDp??0m,((IEnumerable<AbstractModel>)__args[3]).ToArray()));
    static decimal ReceiptNumber(object receipt,string name)=>(decimal)receipt.GetType().GetProperty(name,F)!.GetValue(receipt)!;
    static object? ReceiptValue(object receipt,string name)=>receipt.GetType().GetProperty(name,F)!.GetValue(receipt);
    public sealed class ZeroCapPower:PowerModel
    {
        public decimal Limit=0m;
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
#if HBR_BETA_NATIVE
        public override decimal ModifyDamageCap(Creature? target,ValueProp props,Creature? dealer,CardModel? source,CardPlay? play)
#else
        public override decimal ModifyDamageCap(Creature? target,ValueProp props,Creature? dealer,CardModel? source)
#endif
        {CapZeroCalls++;return target==Owner?Limit:decimal.MaxValue;}
    }
    static void Install()
    {
        H("Setup");var harmony=new Harmony("hbr.coffin-intangible.checks");
        var pc=Blank<PlayerCombatState>();Set(pc,"<TurnNumber>k__BackingField",1);Set(Player,"<PlayerCombatState>k__BackingField",pc);
        foreach(var row in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})Set(pc,"<"+row.Item1+">k__BackingField",new CardPile(row.Item2));
        Set(Player,"<Deck>k__BackingField",new CardPile(PileType.Deck));
        harmony.Patch(typeof(MegaCrit.Sts2.Core.Runs.RunState).GetMethod("IterateHookListeners")!,prefix:new HarmonyMethod(typeof(CoffinChecks).GetMethod(nameof(Listeners),F)!){priority=Priority.First});
        harmony.Patch(typeof(CombatState).GetMethod("IterateHookListeners")!,prefix:new HarmonyMethod(typeof(CoffinChecks).GetMethod(nameof(Listeners),F)!){priority=Priority.First});
        foreach(string name in new[]{"HighDimensionalShellNativeAmountPatch","HighDimensionalShellStackAmountPatch"})
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs production DP amount adapter "+name);
        ModelDb.Inject(typeof(DoomPower));Set(ModelDb.Power<DoomPower>(),"<Id>k__BackingField",ModelDb.GetId(typeof(DoomPower)));
        foreach(MethodInfo flash in typeof(PowerModel).GetMethods(F).Where(method=>method.Name=="Flash"))harmony.Patch(flash,prefix:new HarmonyMethod(typeof(CoffinChecks).GetMethod(nameof(Skip),F)!));
        harmony.Patch(typeof(HighDimensionalShellPower).GetMethod("ResolveBreak",F)!,prefix:new HarmonyMethod(typeof(CoffinChecks).GetMethod(nameof(VisualTask),F)!));
        harmony.Patch(typeof(IntangiblePower).GetMethods(F).Single(method=>method.Name=="ModifyDamageCap"&&method.DeclaringType==typeof(IntangiblePower)),postfix:new HarmonyMethod(typeof(CoffinChecks).GetMethod(nameof(ObserveIntangible),F)!));
        harmony.Patch(typeof(Hook).GetMethod("AfterModifyingDamageAmount",F)!,prefix:new HarmonyMethod(typeof(CoffinChecks).GetMethod(nameof(ObserveCapNotification),F)!));
        harmony.Patch(Helper("HotaruCannonStatsHelper").GetMethod("FinishAfterDamageGiven",F)!,prefix:new HarmonyMethod(typeof(CoffinChecks).GetMethod(nameof(ObserveReceipt),F)!));
        foreach(MethodInfo damage in typeof(CreatureCmd).GetMethods(F).Where(method=>method.Name=="Damage"))harmony.Patch(damage,prefix:new HarmonyMethod(typeof(CoffinChecks).GetMethod(nameof(CountDamage),F)!));
        Cannon=(Creature)H("Actor",Blank<HotaruCoffinCannon>(),null,Player)!;Second=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;
    }
    static void Reset(bool hbr=true,decimal dp=1000m)
    {
        H("Reset");H("Mode",hbr,1);CapCalls.Clear();Receipts.Clear();CapNotifications.Clear();CapZeroCalls=DamageCommands=0;
        Set(State<MegaCrit.Sts2.Core.Runs.RunState>("RunState"),"<Rng>k__BackingField",new RunRngSet("COFFIN-CAP"));
        var relic=new Millsage();Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",Player);Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),relic});
        Set(Combat,"_allies",new List<Creature>{Hero,Cannon});Set(Combat,"_enemies",new List<Creature>{Enemy,Second});Set(Enemy,"_currentHp",5000);Set(Second,"_currentHp",4000);
        Card=new DefendIronclad();Set(Card,"<IsMutable>k__BackingField",true);Set(Card,"_owner",Player);Set(Card,"<Id>k__BackingField",ModelDb.GetId(typeof(DefendIronclad)));Play=Blank<CardPlay>();Set(Play,"<Card>k__BackingField",Card);
        Coffin=Attach(new HotaruMyCoffinPower(),Hero,1);Set(Coffin,"_upgradedCopies",1);
        Shell=Attach(new HighDimensionalShellPower(),Enemy,(int)dp);typeof(HighDimensionalShellPower).GetProperty("CurrentDpData",F)!.SetValue(Shell,dp.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    static void Block(decimal block)=>((Task)Invoke(typeof(HotaruMyCoffinPower).GetMethod("OnCoffinBlock",F)!,Coffin,new ThrowingPlayerChoiceContext(),Cannon,block,Card,Play)!).GetAwaiter().GetResult();
    static void Case(string name,decimal block,decimal spend,bool receipt=true)
    {
        decimal before=Shell?.CurrentDp??0m;int hp=Enemy.CurrentHp,secondHp=Second.CurrentHp;
        Block(block);decimal after=Shell?.CurrentDp??0m;decimal doom=Enemy.GetPower<DoomPower>()?.Amount??0;
        Cases.Add(new{name,block,before,after,spent=before-after,hpBefore=hp,hpAfter=Enemy.CurrentHp,doom,receipts=Receipts.Select(value=>new{hp=ReceiptNumber(value,"ActualHpDamage"),dp=ReceiptNumber(value,"ActualDpDamage"),actual=ReceiptNumber(value,"ActualDamage")}).ToArray(),caps=CapCalls.Select(value=>new{value.Props,value.Result,target=ReferenceEquals(value.Target,Enemy),dealer=ReferenceEquals(value.Dealer,Hero),source=ReferenceEquals(value.Card,Card)}).ToArray(),notifications=CapNotifications.Select(value=>new{source=ReferenceEquals(value.Source,Card),value.DpBefore,modifiers=value.Modifiers.Select(model=>model.GetType().Name).ToArray()}).ToArray()});
        Equal(before-after,spend,name+" spends the expected real target DP amount");Check(Enemy.CurrentHp==hp&&Second.CurrentHp==secondHp,name+" causes no HP loss or alternate-target HP mutation");
        Equal(doom,decimal.Floor(block),name+" upgraded Doom uses original resolved block independently of DP cap");Check(DamageCommands==0,name+" does not issue a new damage command or re-evaluate full damage modifiers");
        Check(Receipts.Count==(receipt?1:0),name+" dispatches one actual receipt only when DP was spent");
        if(receipt)
        {
            object value=Receipts.Single();Equal(ReceiptNumber(value,"ActualDpDamage"),spend,name+" actual cannon receipt reports exact spent DP");Equal(ReceiptNumber(value,"ActualHpDamage"),0m,name+" actual cannon receipt contains no HP damage");
            Check(ReferenceEquals(ReceiptValue(value,"Dealer"),Hero)&&ReferenceEquals(ReceiptValue(value,"Target"),Enemy)&&ReferenceEquals(ReceiptValue(value,"CardSource"),Card)&&ReferenceEquals(ReceiptValue(value,"CardPlay"),Play)&&ReferenceEquals(ReceiptValue(value,"Cannon"),Cannon),name+" real receipt preserves dealer, highest-HP target, source card/play and cannon");
            Check((bool)ReceiptValue(value,"IsReaction")!&&(ValueProp)ReceiptValue(value,"DamageProps")! ==ValueProp.Unpowered,name+" receipt remains original Unpowered reaction");
        }
    }
    static void MainCases()
    {
        Reset();NativePower<IntangiblePower>(Enemy,1);Case("native Intangible1",100m,1m);
        Check(CapNotifications.Count==1&&CapNotifications.Single().DpBefore==1000m&&ReferenceEquals(CapNotifications.Single().Source,Card)&&CapNotifications.Single().Modifiers is [IntangiblePower],"native cap notification runs before DP spend with original card and only effective Intangible modifier");
        Check(CapCalls.Count==1&&CapCalls.All(call=>ReferenceEquals(call.Target,Enemy)&&ReferenceEquals(call.Dealer,Hero)&&ReferenceEquals(call.Card,Card)&&call.Props==ValueProp.Unpowered&&call.Result==1m),"real native Intangible cap sees exactly one original target/dealer/card/Unpowered invocation");
#if HBR_BETA_NATIVE
        Check(CapCalls.All(call=>ReferenceEquals(call.Play,Play)),"beta native cap receives original source CardPlay");
#endif
        Reset();Case("no defensive cap",100m,100m);Check(CapNotifications.Count==0,"no-cap damage sends no spurious cap-modifier notification");
        Reset();Attach(new ZeroCapPower(),Enemy,1);Case("normal listener cap0",100m,0m,receipt:false);Check(CapZeroCalls==1,"zero-protection cap hook participates once in normal native listener stage");Check(CapNotifications.Count==1&&CapNotifications.Single().DpBefore==1000m&&CapNotifications.Single().Modifiers is [ZeroCapPower],"cap0 protection receives its genuine effective-modifier callback before any spend");
        Reset();NativePower<IntangiblePower>(Second,1);Case("other target Intangible",100m,100m);Check(CapNotifications.Count==0,"irrelevant target Intangible is excluded from cap notification modifiers");Check(CapCalls.Count==1&&CapCalls.Single().Result==decimal.MaxValue,"another creature's native Intangible cap preserves its native target-owner gate");
        Reset();var first=Attach(new ZeroCapPower{Limit=-1m},Enemy,1);Attach(new ZeroCapPower{Limit=-2m},Enemy,1);Case("negative cap normalization",100m,0m,receipt:false);
        Check(CapZeroCalls==2&&CapNotifications.Count==1&&CapNotifications.Single().Modifiers.Length==1&&ReferenceEquals(CapNotifications.Single().Modifiers[0],first),"negative caps clamp to zero before comparison so only first actual reduction notifies");
        Reset(dp:0m);Case("zero target DP",100m,0m,receipt:false);
        Reset(dp:500m);Case("DP overflow",1500m,500m);
        Reset();NativePower<StrengthPower>(Hero,999);NativePower<WeakPower>(Hero,1);NativePower<VulnerablePower>(Enemy,1);Attach(new HotaruDamageAmplificationPower(),Hero,100);Case("already-resolved block",100m,100m);
        Reset(false);NativePower<IntangiblePower>(Enemy,1);Case("legacy native Intangible1",100m,1m);
    }
    public static void Run(string path)
    {
        bool ok=false;string? error=null;try{Install();MainCases();ok=true;}catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Real HotaruMyCoffinPower.OnCoffinBlock/target selection/DamageAndDoom, real shell DamageDpOnly/TryAbsorb/exact DP state mutation, actual native Doom Apply, full real NotifyDirectDpDamage/FinishAfterDamageGiven with passive receipt/native-cap observation. Logging/StringName/Flash and shell-break attachment/removal lifecycle isolated; no damage amount, DP spend, cap result, Doom amount or receipt is replaced. No full block gain command, HP damage command, game session, rendering, save/clone suite, package or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" coffin direct-DP checks");if(!ok)throw new Exception("Inspect "+path);
    }
}
