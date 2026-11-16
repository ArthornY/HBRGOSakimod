using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

static class NativeAdditiveStandalone
{
    public static void Run(string report)=>EffectChecks.RunNativeAdditive(report);
}

static partial class EffectChecks
{
    static readonly string[] AdditiveNativeNames=["Powers.AccuracyPower","Powers.CalcifyPower","Powers.LeadershipPower",
        "Powers.PhantomBladesPower","Powers.TaintedPower","Relics.FakeStrikeDummy","Relics.MiniatureCannon","Relics.MysticLighter"];
    sealed record AdditiveObservation(string Model,decimal Raw);
    static readonly List<AdditiveObservation> AdditiveObserved=[];
    static readonly List<object> AdditiveRows=[];
    static Type? AdditiveNativeType(string name)=>typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models."+name,false);
    static void ObserveAdditive(AbstractModel __instance,decimal __result)=>AdditiveObserved.Add(new(__instance.GetType().Name,__result));
    static void ResetAdditive(bool hbr=true,string? seed=null)
    {
        Reset(hbr,seed);CombatManager.Instance.History.Clear();AdditiveObserved.Clear();
    }
    static void InstallAdditiveObservers()
    {
        var harmony=new Harmony("hbr.native.additive.checks.observation");
        foreach(Type type in AdditiveNativeNames.Select(AdditiveNativeType).Where(type=>type!=null).Cast<Type>().Append(typeof(PhantomPollutionPower)))
        {
            MethodInfo method=type.GetMethods(F).Where(method=>method.DeclaringType==type&&method.Name=="ModifyDamageAdditive")
                .OrderByDescending(method=>method.GetParameters().Length).First();
            harmony.Patch(method,postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ObserveAdditive),F)!));
        }
        foreach(MethodInfo method in typeof(Cmd).GetMethods(F).Where(method=>method.Name=="CustomScaledWait"&&method.ReturnType==typeof(Task)))
            harmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
    }
    static PowerModel AdditivePower(Type type,Creature owner,int amount)=>(PowerModel)Invoke(
        typeof(Checks).GetMethod("Power",F)!.MakeGenericMethod(type),null,owner,amount)!;
    static RelicModel AdditiveRelic(Type type)
    {
        var relic=(RelicModel)Activator.CreateInstance(type)!;Set(relic,"<IsMutable>k__BackingField",true);
        Set(relic,"_owner",Player);Set(relic,"<Id>k__BackingField",ModelDb.GetId(type));
        Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),relic});return relic;
    }
    static Sharp ZeroSharp(CardModel card)
    {
        var sharp=new Sharp();Set(sharp,"<IsMutable>k__BackingField",true);Set(sharp,"<Id>k__BackingField",ModelDb.GetId(typeof(Sharp)));
        sharp.Card=card;sharp.Amount=0;Set(card,"<Enchantment>k__BackingField",sharp);return sharp;
    }
    static async Task<Hit> AdditiveHit(Creature dealer,Creature target,decimal raw=6m,CardModel? card=null,ValueProp props=ValueProp.Move)
    {
        Hits.Clear();AdditiveObserved.Clear();await Command(dealer,raw,props,card,new[]{target});
        Check(Hits.Count==1,"additive case reaches one actual native damage boundary");return Hits.Single();
    }
    static decimal IncomingAdditive(decimal units,decimal source=150m,decimal defense=100m)=>
        units<=0m?0m:Math.Max(1m,units*100m*(1m+(source-defense)/140m));
    static decimal NativeRaw(AbstractModel model,Creature target,Creature dealer,CardModel? card,ValueProp props=ValueProp.Move)=>
        (decimal)Native(model,"ModifyDamageAdditive",target,6m,props,dealer,card)!;
    static void SingleRaw(AbstractModel model,decimal expected)
    {
        var calls=AdditiveObserved.Where(row=>row.Model==model.GetType().Name).ToArray();
        Check(calls.Length==1&&calls[0].Raw==expected,"unchanged native additive hook executes once and returns raw "+expected+": "+model.GetType().Name);
    }
    static async Task TaintedCoreCase()
    {
        ResetAdditive();var card=Owned<StrikeIronclad>();
        decimal baseline=(await AdditiveHit(Hero,Enemy,card:card)).Final;
        var tainted=Power<TaintedPower>(Enemy,3);Equal(NativeRaw(tainted,Enemy,Hero,card),3m,"real native Tainted hook still returns three stored raw units");
        Hit hit=await AdditiveHit(Hero,Enemy,card:card);decimal expected=decimal.Floor(Base(6m)+Base(3m));
        AdditiveRows.Add(new{source="TaintedPower",direction="hero-to-enemy",baseline,hit.Raw,hit.Final,expected,
            oldLiteralPlus3=hit.Final==decimal.Floor(Base(6m)+3m),stored=tainted.Amount,nativeCalls=AdditiveObserved.ToArray()});
        // Keep this expectation unchanged for the old-DLL negative control.
        Equal(hit.Final,expected,"native Tainted3 contributes a fifty-percent skill component rather than literal plus3");
        SingleRaw(tainted,3m);Equal(tainted.Amount,3m,"Tainted application counter is not converted or rewritten");
    }
    static void AdditiveRegistryCases()
    {
        Type registry=Helper("HbrAdditiveSkillDamageSources");
        Type[] registered=((IEnumerable<Type>)registry.GetProperty("Types",F)!.GetValue(null)!).ToArray();
        Type[] expected=AdditiveNativeNames.Select(AdditiveNativeType).Where(type=>type!=null).Cast<Type>().Append(typeof(PhantomPollutionPower)).ToArray();
        Check(registered.ToHashSet().SetEquals(expected),"explicit source registry exactly matches the audited native and phantom types");
        Check(!registered.Contains(typeof(StrengthPower))&&!registered.Contains(typeof(VigorPower))&&!registered.Contains(typeof(StrikeDummy))&&
            !registered.Contains(typeof(Sharp)),"attribute/base/enchantment boundaries are not converted again");
    }
    static async Task AllAdditiveSourcesCases()
    {
        foreach(Type type in AdditiveNativeNames.Select(AdditiveNativeType).Where(type=>type!=null).Cast<Type>().Append(typeof(PhantomPollutionPower)))
        {
            ResetAdditive();Creature dealer=Hero,target=Enemy;CardModel card=Owned<StrikeIronclad>();AbstractModel source;decimal units;
            if(typeof(RelicModel).IsAssignableFrom(type))
            {
                source=AdditiveRelic(type);
                if(type==typeof(MiniatureCannon)){card.UpgradePreviewType=CardUpgradePreviewType.Combat;card.UpgradeInternal();card.FinalizeUpgradeInternal();}
                if(type.Name=="MysticLighter")ZeroSharp(card);
                units=type.Name=="MysticLighter"?9m:type.Name=="FakeStrikeDummy"?1m:3m;
            }
            else
            {
                Creature owner=type.Name is "TaintedPower" or "PhantomPollutionPower"?Enemy:Hero;
                if(type.Name=="LeadershipPower")
                {owner=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,owner});}
                if(type.Name=="CalcifyPower")
                {dealer=(Creature)H("Actor",Blank<Osty>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,dealer});}
                if(type.Name is "AccuracyPower" or "PhantomBladesPower")card=Owned<Shiv>();
                source=AdditivePower(type,owner,3);units=3m;
            }
            Equal(NativeRaw(source,target,dealer,card),units,"actual source retains original eligibility and raw additive amount: "+type.Name);
            Hit hit=await AdditiveHit(dealer,target,card:card);
            Equal(hit.Final,decimal.Floor(Base(6m)+Base(units)),"actual full hook scales the additive skill component: "+type.Name);
            SingleRaw(source,units);
            if(source is PowerModel storedPower)Equal(storedPower.Amount,3m,"actual hit preserves native raw power counter: "+type.Name);
            if(source is RelicModel storedRelic)
                Equal(storedRelic.DynamicVars[type.Name=="MysticLighter"?"Damage":"ExtraDamage"].BaseValue,units,
                    "actual hit preserves native raw relic variable: "+type.Name);
            AdditiveRows.Add(new{source=type.Name,units,hit.Final,stored=source is PowerModel p?(decimal?)p.Amount:null});
            Equal(NativeRaw(source,target,dealer,card,ValueProp.Unpowered),0m,"native Unpowered exclusion remains active: "+type.Name);
            hit=await AdditiveHit(dealer,target,card:card,props:ValueProp.Unpowered);
            Equal(hit.Final,6m,"Unpowered command keeps its literal input and no bonus: "+type.Name);
        }
    }
    static async Task AdditiveEligibilityCases()
    {
        foreach(string name in new[]{"AccuracyPower","PhantomBladesPower"})
        {
            Type? type=AdditiveNativeType("Powers."+name);if(type==null)continue;
            ResetAdditive();PowerModel source=AdditivePower(type,Hero,3);var strike=Owned<StrikeIronclad>();
            Equal(NativeRaw(source,Enemy,Hero,strike),0m,"non-Shiv attack keeps native exclusion: "+name);
            Equal((await AdditiveHit(Hero,Enemy,card:strike)).Final,decimal.Floor(Base(6m)),"non-Shiv attack has no converted bonus: "+name);
            var shiv=Owned<Shiv>();
            if(name=="PhantomBladesPower")
            {
                var play=Blank<CardPlay>();Set(play,"<Card>k__BackingField",shiv);typeof(CardPlay).GetField("<Player>k__BackingField",F)?.SetValue(play,Player);
                CombatManager.Instance.History.CardPlayFinished(Combat,play);
                AdditiveRows.Add(new{source="PhantomBladesHistory",round=Combat.RoundNumber,side=Combat.CurrentSide.ToString(),
                    entries=CombatManager.Instance.History.CardPlaysFinished.Select(entry=>new{happened=(bool)Native(entry,"HappenedThisTurn",Combat)!,
                        sameCard=ReferenceEquals(entry.CardPlay.Card,shiv),sameOwner=ReferenceEquals(entry.CardPlay.Card.Owner.Creature,Hero),
                        tags=entry.CardPlay.Card.Tags.Select(tag=>tag.ToString()).ToArray()}).ToArray(),
                    nativeBody=PatchProcessor.GetOriginalInstructions(source.GetType().GetMethods(F).First(method=>method.Name.Contains("b__")&&method.DeclaringType==source.GetType())).Select(instruction=>instruction.ToString()).ToArray()});
                Equal(NativeRaw(source,Enemy,Hero,shiv),0m,"real native history excludes a second Shiv this turn");
                Equal((await AdditiveHit(Hero,Enemy,card:shiv)).Final,decimal.Floor(Base(6m)),"history-qualified second Shiv gains no additive skill");
            }
        }
        ResetAdditive();var calcify=Power<CalcifyPower>(Hero,3);var card=Owned<StrikeIronclad>();
        Equal(NativeRaw(calcify,Enemy,Hero,card),0m,"Calcify still excludes its hero's own attacks");
        ResetAdditive();var lighter=AdditiveRelic(AdditiveNativeType("Relics.MysticLighter")!);card=Owned<StrikeIronclad>();
        Equal(NativeRaw(lighter,Enemy,Hero,card),0m,"Mystic Lighter still excludes an unenchanted attack");
        ResetAdditive();var fake=AdditiveRelic(AdditiveNativeType("Relics.FakeStrikeDummy")!);
        Equal(NativeRaw(fake,Enemy,Hero,Owned<Shiv>()),0m,"Fake Strike Dummy still requires the native Strike tag");
    }
    static async Task AdditiveDirectionAndBoundaryCases()
    {
        ResetAdditive();var tainted=Power<TaintedPower>(Hero,3);
        Hit hit=await AdditiveHit(Enemy,Hero);
        Equal(hit.Final,decimal.Floor(IncomingAdditive(6m)+IncomingAdditive(3m)),"enemy-to-hero Tainted uses existing incoming E and defense curve");
        SingleRaw(tainted,3m);AdditiveRows.Add(new{direction="enemy-to-hero",hit.Final});
        ResetAdditive();Creature second=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;
        Set(Combat,"_enemies",new List<Creature>{Enemy,second});tainted=Power<TaintedPower>(second,3);
        hit=await AdditiveHit(Enemy,second);
        Equal(hit.Final,900m,"confused enemy-to-enemy uses source and target E instead of fabricated player stats");
        SingleRaw(tainted,3m);AdditiveRows.Add(new{direction="enemy-to-enemy",hit.Final});
        ResetAdditive();Creature phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;
        Set(Combat,"_allies",new List<Creature>{Hero,phantom});Power<PhantomPower>(phantom,1);
        tainted=Power<TaintedPower>(Enemy,3);Power<TaintedPower>(Hero,99);
        hit=await AdditiveHit(phantom,Enemy);
        Equal(hit.Final,decimal.Floor(Base(6m)+Base(3m)),"phantom uses its actual inherited attributes and only the target's Tainted3");
        SingleRaw(tainted,3m);AdditiveRows.Add(new{direction="phantom-to-enemy",hit.Final});
        ResetAdditive();tainted=Power<TaintedPower>(Enemy,3);var card=Owned<StrikeIronclad>();
        hit=await AdditiveHit(Hero,Enemy,0m,card);
        Equal(hit.Final,decimal.Floor(Base(3m)),"zero attack base retains positive Tainted skill damage");
        ResetAdditive(false);tainted=Power<TaintedPower>(Enemy,3);card=Owned<StrikeIronclad>();
        Equal((await AdditiveHit(Hero,Enemy,card:card)).Final,9m,"legacy mode keeps native raw6 plus3");
        ResetAdditive();tainted=Power<TaintedPower>(Enemy,0);card=Owned<StrikeIronclad>();
        Equal((await AdditiveHit(Hero,Enemy,card:card)).Final,decimal.Floor(Base(6m)),"zero amount does not create a positive minimum component");
        Equal(tainted.Amount,0m,"zero counter is not rewritten during damage calculation");
        Set(tainted,"_amount",-3);
        Equal((await AdditiveHit(Hero,Enemy,card:card)).Final,decimal.Floor(Base(6m)-3m),"nonpositive raw additive result keeps the previous signed behavior");
        Equal(tainted.Amount,-3m,"signed nonpositive counter retains its original storage");
        ResetAdditive();tainted=Power<TaintedPower>(Enemy,3);card=Owned<StrikeIronclad>();
        using((IDisposable)Call("PrecalculatedDamageContext","Begin",card,Enemy,6m)!)hit=await AdditiveHit(Hero,Enemy,card:card);
        Equal(hit.Final,6m,"precalculated world damage still skips the whole additive phase");
        Check(AdditiveObserved.All(call=>call.Model!="TaintedPower"),"precalculated damage never queries Tainted's native hook");
        Hits.Clear();AdditiveObserved.Clear();await (Task)Call("HpOnlyDamageContext","Damage",new ThrowingPlayerChoiceContext(),Enemy,6m,Hero,card)!;
        Equal(Hits.Single().Final,6m,"resolved HP-only command still bypasses additive conversion");
        Check(AdditiveObserved.All(call=>call.Model!="TaintedPower"),"HP-only resolved command never queries Tainted's native hook");
    }
    sealed class AdditiveTestCap : PowerModel
    {
        public override PowerType Type=>PowerType.Buff;
        public override PowerStackType StackType=>PowerStackType.Counter;
#if HBR_BETA_NATIVE
        public override decimal ModifyDamageCap(Creature? target,ValueProp props,Creature? dealer,CardModel? card,CardPlay? play)=>
#else
        public override decimal ModifyDamageCap(Creature? target,ValueProp props,Creature? dealer,CardModel? card)=>
#endif
            target==Owner?2500.95m:decimal.MaxValue;
    }
    static async Task AdditiveCriticalAndCapCases()
    {
        ResetAdditive(seed:FindSeed(true));var tainted=Power<TaintedPower>(Enemy,3);var card=Owned<StrikeIronclad>();
        Hit hit=await AdditiveHit(Hero,Enemy,card:card);
        Equal(hit.Final,13500m,"base and Tainted component share the same successful critical curve");
        Check(RngCount(RunState.Rng.Niche)==1,"Tainted component does not consume another critical sample");
        Equal(tainted.Amount,3m,"critical hit does not consume or rewrite the native debuff counter");
        var cap=Blank<AdditiveTestCap>();Set(cap,"<IsMutable>k__BackingField",true);Set(cap,"_owner",Enemy);Powers(Enemy).Add(cap);
        hit=await AdditiveHit(Hero,Enemy,card:card);
        Equal(hit.Final,2500m,"native target cap applies to the final combined damage before flooring");
        ResetAdditive();tainted=Power<TaintedPower>(Enemy,3);card=Owned<StrikeIronclad>();
        H("Act",3);hit=await AdditiveHit(Hero,Enemy,card:card);
        Equal(hit.Final,2m,"very high defense retains existing independent one-point component minima");
    }
    static async Task AdditiveLifecycleCases()
    {
        ResetAdditive();var tainted=Power<TaintedPower>(Hero,3);var context=new ThrowingPlayerChoiceContext();
        await (Task)Native(tainted,"AfterSideTurnEnd",context,CombatSide.Player,new[]{Hero})!;
        Check(Hero.GetPower<TaintedPower>()==tainted&&tainted.Amount==3,"native Tainted survives player-side end");
        await (Task)Native(tainted,"AfterSideTurnEnd",context,CombatSide.Enemy,new[]{Enemy})!;
        Check(Hero.GetPower<TaintedPower>()==null,"native Tainted still removes itself at enemy-side end");
        ResetAdditive();var pollution=Power<PhantomPollutionPower>(Enemy,3);
        await (Task)Native(pollution,"BeforeSideTurnStart",context,CombatSide.Player,new[]{Hero},Combat)!;
        Check(Enemy.GetPower<PhantomPollutionPower>()==pollution&&pollution.Amount==3,"fusion pollution ignores the other side's start");
        await (Task)Native(pollution,"BeforeSideTurnStart",context,CombatSide.Enemy,new[]{Enemy},Combat)!;
        Check(Enemy.GetPower<PhantomPollutionPower>()==null,"fusion pollution keeps its separate owner's-next-start removal");
        ResetAdditive();
        foreach(Type type in new[]{typeof(Tainted),typeof(TaintedPower)})
        {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
        var spark=Power<VitalSparkPower>(Enemy,3);var skill=Owned<DefendIronclad>();
        await (Task)Native(spark,"AfterCardEnteredCombat",skill)!;
        Check(skill.Affliction is Tainted&&skill.Affliction.Amount==3,"real Vital Spark card-entered hook attaches three native Tainted stacks");
        var play=Blank<CardPlay>();Set(play,"<Card>k__BackingField",skill);
        await (Task)Native(spark,"AfterCardPlayed",context,play)!;
        Check(Hero.GetPower<TaintedPower>() is { Amount:3 },"real Vital Spark play hook applies exactly three native counter stacks");
        Check(skill.Affliction!.Amount==3&&spark.Amount==3,"application chain never rewrites affliction or source stack storage");
    }
    public static void RunNativeAdditive(string report)
    {
        bool ok=false;string? error=null;ReportDirectory=Path.GetDirectoryName(Path.GetFullPath(report))!;
        try
        {
            Install();InstallAdditiveObservers();TaintedCoreCase().GetAwaiter().GetResult();
            if(Environment.GetEnvironmentVariable("HBR_ADDITIVE_TAINTED_ONLY")!="1")
            {
                AdditiveRegistryCases();AllAdditiveSourcesCases().GetAwaiter().GetResult();AdditiveEligibilityCases().GetAwaiter().GetResult();
                AdditiveDirectionAndBoundaryCases().GetAwaiter().GetResult();AdditiveCriticalAndCapCases().GetAwaiter().GetResult();AdditiveLifecycleCases().GetAwaiter().GetResult();
            }
            ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(report,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=AdditiveRows,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),taintedOnly=Environment.GetEnvironmentVariable("HBR_ADDITIVE_TAINTED_ONLY")=="1",
            boundary="Real native additive hooks and actual production Hook/StrongestShield calculation. Native qualifications, history and VitalSpark/Tainted command chain run; only terminal HP writes, logging/StringName and wait leaves are isolated by the shared host. Passive observations do not replace calculations. No game, Godot, package, installed files/config or saves."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" native additive skill checks");if(!ok)throw new Exception("Inspect "+report);
    }
}
