using System.Globalization;
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
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class OverchargeChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(HotaruOverchargePower).Assembly;
    static readonly List<string> Passed=[];
    sealed record Hit(decimal Base,decimal Final,string Target,bool OwnerDealer,bool Reaction,string Props);
    static readonly List<Hit> Commands=[];
    static readonly List<object> Trajectories=[];
    sealed record TerminalLoss(decimal Hp,decimal Dp);
    static readonly Queue<TerminalLoss> TerminalLosses=[];
    static readonly List<object> AmplificationResults=[];
    static Type Host=>typeof(Checks);
    static Creature Hero=>State<Creature>("Hero");
    static Creature Enemy=>State<Creature>("Enemy");
    static Player Player=>State<Player>("Player");
    static CombatState Combat=>State<CombatState>("Combat");
    static Millsage Relic=null!;
    static Creature Cannon=null!,Second=null!;
    static T State<T>(string field)=>(T)Host.GetField(field,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method,object? owner,params object?[] args)
    {try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? H(string method,params object?[] args)=>Invoke(Host.GetMethod(method,F)!,null,args);
    static Type Helper(string name)=>Mod.GetType("HBRGOSakiMod.Core.Helpers."+name,true)!;
    static object? Call(string helper,string method,params object?[] args)=>Invoke(Helper(helper).GetMethod(method,F)!,null,args);
    static void Set(object owner,string field,object? value)=>H("Set",owner,field,value);
    static T Blank<T>()=>(T)Invoke(Host.GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static List<PowerModel> Powers(Creature target)=>(List<PowerModel>)H("Powers",target)!;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed.Add(message);}
    static void Equal(decimal actual,decimal expected,string message)=>Check(actual==expected,$"{message}: {actual} = {expected}");
    static void Wait(object? task)=>((Task)task!).GetAwaiter().GetResult();
    static bool Skip()=>false;
    static bool VisualTask(ref Task __result){__result=Task.CompletedTask;return false;}
    static object HappyKind=>Enum.Parse(typeof(HotaruCannonModel).GetProperty("Kind")!.PropertyType,"Happiness");
    static object HappyState=>Invoke(typeof(Millsage).GetMethod("StateFor",F)!,Relic,HappyKind)!;
    static decimal StateNumber(string name)=>Convert.ToDecimal(HappyState.GetType().GetProperty(name,F)!.GetValue(HappyState),CultureInfo.InvariantCulture);
    static void StateValue(string name,object value)=>HappyState.GetType().GetProperty(name,F)!.SetValue(HappyState,value);
    static HotaruOverchargePower Apply(params int[] percentages)
    {
        foreach(int percent in percentages)HotaruOverchargePower.Apply(null!,Hero,percent,null!).GetAwaiter().GetResult();
        return Hero.GetPower<HotaruOverchargePower>()??throw new Exception("Actual native Apply did not attach overcharge");
    }
    static void Reset()
    {
        H("Reset");Commands.Clear();TerminalLosses.Clear();
        Relic=new Millsage();Set(Relic,"<IsMutable>k__BackingField",true);Set(Relic,"_owner",Player);
        Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),Relic});
        Set(Combat,"_allies",new List<Creature>{Hero,Cannon});Set(Combat,"_enemies",new List<Creature>{Enemy,Second});
        StateValue("HappinessDamage",6m);StateValue("NextHappinessIncrease",1m);
    }
    // Native HP execution is the sole damage leaf replaced here. Scope,
    // full native Hook/actual wrapper, receipts and reaction dispatch are real.
    static bool DamageLeaf(MethodBase __originalMethod,object?[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {
        var parameters=__originalMethod.GetParameters();
        object? Arg(Type type)=>__args[Array.FindIndex(parameters,p=>p.ParameterType==type)];
        var targets=((IEnumerable<Creature>)Arg(typeof(IEnumerable<Creature>))!).ToArray();
        decimal amount=(decimal)Arg(typeof(decimal))!;ValueProp props=(ValueProp)Arg(typeof(ValueProp))!;
        Creature dealer=(Creature)Arg(typeof(Creature))!;CardModel? card=(CardModel?)Arg(typeof(CardModel));
        CardPlay? play=parameters.Any(p=>p.ParameterType==typeof(CardPlay))?(CardPlay?)Arg(typeof(CardPlay)):null;
        __result=ExecuteDamage(targets,amount,props,dealer,card,play);return false;
    }
    static async Task<IEnumerable<DamageResult>> ExecuteDamage(Creature[] targets,decimal amount,ValueProp props,Creature dealer,CardModel? card,CardPlay? play)
    {
        object?[] arguments=[targets,amount,props,dealer,card,play];
        object? scope=Call("HotaruCannonStatsHelper","Begin",arguments);var results=new List<DamageResult>();
        try
        {
            foreach(Creature target in targets)
            {
                Check(card==null&&play==null,"fixture command retains null natural-growth source attribution");
                decimal damage=(decimal)H("Actual",target,dealer,amount,props)!;
                Call("HotaruCannonStatsHelper","RecordFinalDamage",target,dealer,card,damage);
                TerminalLoss? loss=TerminalLosses.Count>0?TerminalLosses.Dequeue():null;
                decimal hp=loss?.Hp??decimal.Floor(Math.Max(0m,damage));
                if(loss?.Dp>0m)Call("HotaruCannonStatsHelper","RecordDpAbsorption",target,loss.Dp);
                var result=Blank<DamageResult>();Set(result,"<Receiver>k__BackingField",target);Set(result,"<UnblockedDamage>k__BackingField",(int)hp);results.Add(result);
                Call("HotaruCannonStatsHelper","BindReceipts",new object?[]{new[]{result}});
                object?[] receiptArguments=[result,null];Check((bool)Call("HotaruCannonStatsHelper","TryTakeReceipt",receiptArguments)!,"real scope binds cannon receipt");
                object receipt=receiptArguments[1]!;
                if(loss!=null)
                {
                    decimal Number(string name)=>(decimal)receipt.GetType().GetProperty(name,F)!.GetValue(receipt)!;
                    Equal(Number("ActualHpDamage"),loss.Hp,"production receipt retains isolated terminal HP loss");
                    Equal(Number("ActualDpDamage"),loss.Dp,"production receipt retains isolated terminal DP loss");
                    Equal(Number("ActualDamage"),loss.Hp+loss.Dp,"production receipt sums only actual HP and DP losses");
                }
                bool reaction=(bool)(receipt.GetType().GetProperty("IsReaction",F)?.GetValue(receipt)??receipt.GetType().GetField("IsReaction",F)!.GetValue(receipt))!;
                Commands.Add(new(amount,damage,ReferenceEquals(target,Enemy)?"first":"second",ReferenceEquals(dealer,Hero),reaction,props.ToString()));
                await (Task)Call("HotaruCannonStatsHelper","FinishAfterDamageGiven",Task.CompletedTask,null,receipt)!;
            }
            return results;
        }
        finally{Call("HotaruCannonStatsHelper","Close",scope);}
    }
    static void Install()
    {
        Console.WriteLine("Setup host");H("Setup");var harmony=new Harmony("hbr.hotaru-overcharge.checks");
        foreach(Type type in new[]{typeof(HotaruOverchargePower),typeof(ChargePower),typeof(HotaruDamageAmplificationPower)})
        {ModelDb.Inject(type);Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
#if HBR_BETA_NATIVE
        typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Modding.AssemblyInfo",true)!.GetMethod("Init",F)!.Invoke(null,null);
        typeof(ModelIdSerializationCache).GetMethod("Init",F)!.Invoke(null,null);
        typeof(ModelIdSerializationCache).GetMethod("CacheSavedPropertiesForTypeDebug",F)!.Invoke(null,[typeof(HotaruOverchargePower)]);
#else
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HotaruOverchargePower));
#endif
        foreach(Type type in Mod.GetTypes().Where(type=>type.IsDefined(typeof(HarmonyPatch),false)&&
            (type.Name.StartsWith("ExactDamagePercent",StringComparison.Ordinal)||type.Name is "HighDimensionalShellStackAmountPatch" or "DamageAmplificationStackingPatch")))
            harmony.CreateClassProcessor(type).Patch();
        var pc=Blank<PlayerCombatState>();Set(pc,"<TurnNumber>k__BackingField",1);Set(Player,"<PlayerCombatState>k__BackingField",pc);
        foreach(var row in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})
            Set(pc,"<"+row.Item1+">k__BackingField",new CardPile(row.Item2));
        Set(Player,"<Deck>k__BackingField",new CardPile(PileType.Deck));
        Cannon=(Creature)H("Actor",Blank<HotaruHappinessCannon>(),null,Player)!;Second=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;
        int patched=0;
        foreach(MethodInfo method in typeof(CreatureCmd).GetMethods(F).Where(m=>m.Name=="Damage"&&m.ReturnType==typeof(Task<IEnumerable<DamageResult>>)&&m.GetParameters().Any(p=>p.ParameterType==typeof(IEnumerable<Creature>))))
        {harmony.Patch(method,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(DamageLeaf),F)!));patched++;}
        Check(patched>0,"captures native target damage execution leaf");
        harmony.Patch(typeof(Millsage).GetMethod("RefreshIntent",F)!,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(VisualTask),F)!));
        var flash=typeof(PowerModel).GetMethod("Flash",F);if(flash!=null)harmony.Patch(flash,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(Skip),F)!));
        Reset();Console.WriteLine("Host ready");
    }
    static void ApplicationCases()
    {
        foreach(int[] layers in new[]{new[]{100},new[]{150},new[]{100,150},new[]{150,100,150}})
        {
            Reset();var power=Apply(layers);Equal(power.DamagePercent,layers.Sum(),"real native Apply preserves exact configured total");
            Check(power.Amount==layers.Length&&Powers(Hero).Count(p=>p is HotaruOverchargePower)==1,"real native first Apply and stacking keep one active instance");
            var clone=(HotaruOverchargePower)power.ClonePreservingMutability();Equal(clone.DamagePercent,power.DamagePercent,"native clone preserves overcharge payload");
            var saved=SavedProperties.From(power)!;var options=new JsonSerializerOptions{IncludeFields=true};var restored=(HotaruOverchargePower)ModelDb.Power<HotaruOverchargePower>().ToMutable();
            JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(saved,options),options)!.FillInternal(restored);Equal(restored.DamagePercent,power.DamagePercent,"native saved-property JSON roundtrip preserves overcharge percent");
            var writer=new PacketWriter();saved.Serialize(writer);var reader=new PacketReader();reader.Reset(writer.Buffer);var packet=new SavedProperties();packet.Deserialize(reader);
            var fromPacket=(HotaruOverchargePower)ModelDb.Power<HotaruOverchargePower>().ToMutable();packet.FillInternal(fromPacket);Equal(fromPacket.DamagePercent,power.DamagePercent,"native saved-property packet roundtrip preserves overcharge percent");Check(reader.BitPosition==writer.BitPosition,"packet reader consumes complete overcharge payload");
        }
        Console.WriteLine("Application and storage passed");
    }
    static void Grow()=>Wait(Invoke(typeof(Millsage).GetMethod("GrowHappiness",F)!,Relic,null,Cannon,0m,null,null));
    static void GrowthCases()
    {
        foreach(bool hbr in new[]{true,false})foreach(bool manual in new[]{false,true})foreach(int[] layers in new[]{new[]{100},new[]{150},new[]{100,150}})
        {
            Reset();H("Mode",hbr,1);Apply(layers);decimal before=StateNumber("NextHappinessIncrease");
            if(manual)Relic.FireHappiness(Enemy,null!,growBeforeDamage:true).GetAwaiter().GetResult();else Grow();
            decimal after=StateNumber("NextHappinessIncrease");int firstCount=Commands.Count;
            Grow();decimal following=StateNumber("NextHappinessIncrease");
            Trajectories.Add(new{hbr,manual,percent=layers.Sum(),before,after,following,firstCount,totalCount=Commands.Count,hits=Commands.ToArray()});
            Check(Commands.Count(h=>h.Reaction)>=4,$"HBR{hbr} manual{manual} percent{layers.Sum()} two growths hit both enemies");
            Check(Commands.All(h=>h.OwnerDealer&&h.Props==ValueProp.Move.ToString()),"all real commands retain owner dealer and Move props");
            var reactions=Commands.Where(h=>h.Reaction).ToArray();Equal(reactions[0].Base,layers.Sum()/100m,"first target reaction uses actual growth times exact overcharge percent");Equal(reactions[1].Base,reactions[0].Base,"all targets share growth base damage");
            if(hbr)foreach(Hit reaction in reactions)
            {
                Check(reaction.Final>0m,"positive HBR growth reaction causes positive resolved damage");
                Equal(reaction.Final,decimal.Floor(reaction.Base*1000m*(1m+(100m-150m)/140m)*.5m),"HBR reaction resolves exact independent attribute and Soul formula");
            }
            decimal expected=manual?1m:2m;Equal(after,expected,$"HBR{hbr} manual{manual} overcharge does not halve next growth; primary firing keeps its rule");
            Equal(following,expected+1m,"following natural growth continues its sequence");
            Check(Commands.Count==firstCount+2,"following natural growth issues exactly one reaction per enemy");
        }
    }
    static void EdgeCases()
    {
        foreach(bool hbr in new[]{true,false})
        {
            Reset();H("Mode",hbr,1);Apply(100);
            Relic.FireHappiness(Enemy,null!,growBeforeDamage:false).GetAwaiter().GetResult();
            Check(Commands.Count==1&&!Commands[0].Reaction,"direct firing issues one primary command and no overcharge growth reaction");
            Equal(StateNumber("NextHappinessIncrease"),0m,"direct positive primary firing keeps native floor-halving rule");
            Equal(StateNumber("HappinessDamage"),3m,"direct primary firing still halves stored happiness damage");
            Reset();H("Mode",hbr,1);Apply(100);
            typeof(Millsage).GetProperty("HappinessUpgradeLevel",F)!.SetValue(Relic,2);
            Grow();
            decimal hitBonus=(decimal)Helper("HotaruCannonSkillPowerHelper").GetField("HappinessHitGrowth",F)!.GetValue(null)!;
            decimal rewardedHits=Commands.Count(h=>h.Final>=1m);
            Equal(StateNumber("NextHappinessIncrease"),2m+rewardedHits*hitBonus,"tier2 reaction hits retain growth reward without halving the next increase");
            Check(Commands.Count==2&&Commands.All(h=>h.Reaction),"tier2 natural growth keeps one genuine reaction receipt per enemy");
            if(hbr)Check(rewardedHits==2m,"HBR tier2 positive reaction damage rewards both enemy hits");
        }
    }
    static HotaruDamageAmplificationPower? Buff=>Hero.GetPower<HotaruDamageAmplificationPower>();
    static bool HappyTier4=>(bool)Invoke(typeof(Millsage).GetMethod("HasCannonUpgrade")!,Relic,HappyKind,4)!;
    static void AmplificationSetup(bool hbr,int level=4)
    {
        Reset();H("Mode",hbr,1);Set(Cannon,"_currentHp",5000);
        typeof(Millsage).GetProperty("HappinessUpgradeLevel",F)!.SetValue(Relic,level);
    }
    static bool ReceiptHit(Creature target,decimal hp,decimal dp,bool reaction=false)
    {
        TerminalLosses.Enqueue(new(hp,dp));
        if(reaction){Wait(Call("HotaruCannonStatsHelper","ReactionDamage",null,Cannon,target,9999m,null,null,false,false));return true;}
        return ((Task<bool>)Call("HotaruCannonStatsHelper","Damage",Cannon,target,9999m,null,null,null,1)!).GetAwaiter().GetResult();
    }
    static void HappinessAmplificationCases()
    {
        foreach(bool hbr in new[]{true,false})
        {
            foreach(var loss in new[]{new TerminalLoss(1000m,0m),new TerminalLoss(2500m,0m),new TerminalLoss(600m,400m),new TerminalLoss(0m,1000m),new TerminalLoss(1m,0m)})
            {
                AmplificationSetup(hbr);Check(HappyTier4,"real Happiness tier4 predicate permits receipt-based amplification");
                Check(ReceiptHit(Enemy,loss.Hp,loss.Dp),"real cannon damage command reports positive terminal HP or DP loss");
                decimal expected=(loss.Hp+loss.Dp)/1000m;
                AmplificationResults.Add(new{hbr,loss.Hp,loss.Dp,expected,actual=Buff?.DamagePercent,proxy=Buff?.Amount,command=Commands.Single()});
                Equal(Buff?.DamagePercent??0m,expected,"Happiness tier4 awards one exact percentage point per1000 actual damage");
                Check(Buff!=null&&Powers(Hero).Count(power=>power is HotaruDamageAmplificationPower)==1&&Enemy.GetPower<HotaruDamageAmplificationPower>()==null,
                    "native receipt dealer receives one canonical amplification instance; target receives none");
                Check(Buff!.AmplificationPercentData==expected.ToString(CultureInfo.InvariantCulture),"actual native Apply retains exact fractional receipt award in amplification bank");
                Check(Commands.Single().Final>loss.Hp+loss.Dp,"amplification uses terminal losses rather than larger computed or issued damage");
            }
            AmplificationSetup(hbr);
            var existing=PowerCmd.Apply<HotaruDamageAmplificationPower>(null!,Hero,.375m,Hero,null,true).GetAwaiter().GetResult()!;
            Equal(existing.DamagePercent,.375m,"existing fractional amplification is created by real native Apply");
            ReceiptHit(Enemy,2500m,0m);Equal(Buff!.DamagePercent,2.875m,"native stacking adds2point5 reward to existing0point375 without rounding");
            Check(ReferenceEquals(existing,Buff)&&Powers(Hero).Count(power=>power is HotaruDamageAmplificationPower)==1,"receipt award stacks onto existing canonical amplification instance");
            AmplificationSetup(hbr);ReceiptHit(Enemy,600m,0m);ReceiptHit(Second,0m,400m);
            Equal(Buff!.DamagePercent,1m,"separate target receipts add0point6 and0point4 amplification exactly");
            Check(Commands.Count==2&&Commands[0].Target=="first"&&Commands[1].Target=="second","each target receives its own real cannon command and independently bound receipt");
            AmplificationSetup(hbr);StateValue("NextHappinessIncrease",8m);ReceiptHit(Enemy,600m,400m,reaction:true);
            Equal(Buff!.DamagePercent,1m,"genuine cannon reaction receipt receives the same1000-to-one amplification conversion");
            decimal hitBonus=(decimal)Helper("HotaruCannonSkillPowerHelper").GetField("HappinessHitGrowth",F)!.GetValue(null)!;
            Equal(StateNumber("NextHappinessIncrease"),8m+hitBonus,"tier4 reaction amplification retains tier2 growth reward without halving next increase");
            Check(Commands.Single().Reaction,"tier4 reaction attribution comes from real pending reaction intent");
            AmplificationSetup(hbr);Check(!ReceiptHit(Enemy,0m,0m),"fully blocked terminal receipt reports no actual damage despite positive issued amount");
            Check(Buff==null,"zero HP and DP actual loss grants no amplification");
            AmplificationSetup(hbr,3);Check(!HappyTier4,"real locked tier3 predicate excludes Happiness tier4");ReceiptHit(Enemy,1000m,0m);Check(Buff==null,"locked Happiness tier4 grants no receipt amplification");
            AmplificationSetup(hbr);typeof(Millsage).GetProperty("RetiredCannonsMask",F)!.SetValue(Relic,1<<Convert.ToInt32(HappyKind));
            Check(!HappyTier4&&!Millsage.IsOwnedCannon(Cannon,Player),"real permanent-retirement and ownership predicates exclude retired Happiness cannon");
            Check(!ReceiptHit(Enemy,1000m,0m)&&Commands.Count==0&&Buff==null,"retired Happiness cannon issues no damage or amplification award");
            AmplificationSetup(hbr);Set(Cannon,"_currentHp",0);
            Check(!Millsage.IsOwnedCannon(Cannon,Player)&&!ReceiptHit(Enemy,1000m,0m)&&Commands.Count==0&&Buff==null,"dead cannon fails real ownership predicate before damage and amplification");
        }
    }
    public static void Run(string reportPath)
    {
        bool ok=false;string? error=null;
        try{Install();ApplicationCases();GrowthCases();EdgeCases();HappinessAmplificationCases();ok=true;}catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,commands=Commands,trajectories=Trajectories,amplification=AmplificationResults,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Actual custom Apply and native PowerCmd Apply/ModifyAmount; native cloning and SavedProperties JSON/packet; real Millsage GrowHappiness and FireHappiness with null source; real cannon scope, full native Hook and StrongestShield actual wrapper, real receipt bind/take and FinishAfterDamageGiven. Logging/StringName, intent refresh/flash and native HP execution leaf isolated; HP receipts synthesized from the actual damage result; tier4 cases supply controlled HP/DP terminal losses via the real RecordDpAbsorption/BindReceipts path, then real FinishAfterDamageGiven and native exact amplification Apply. No game process, rendering, full HP command execution, full save loading, non-null card attribution, package or upload."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" overcharge checks");if(!ok)throw new Exception("Inspect "+reportPath);
    }
}
