using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class OverchargeChecks { public static void Run(string reportPath)=>EffectChecks.Run(reportPath); }

static partial class EffectChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    const string ProductionId="hbr.effect-skill.checks.production";
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    sealed record Hit(Creature Target,Creature? Dealer,CardModel? Card,decimal Raw,ValueProp Props,decimal Final,bool Enabled,int? Sample,int Depth,bool HpOnly);
    static readonly List<Hit> Hits=[];
    static Player Player=>State<Player>("Player");
    static Creature Hero=>State<Creature>("Hero");
    static Creature Enemy=>State<Creature>("Enemy");
    static CombatState Combat=>State<CombatState>("Combat");
    static RunState RunState=>State<RunState>("RunState");
    static MethodInfo Canonical=null!;
    static object? LastCommand;
    sealed record NativeAdditiveCall(decimal Result,string Props,bool OwnerDealer,bool OwnerCard);
    static readonly List<NativeAdditiveCall> MiniatureCalls=[];
    static bool AsyncLeaf=true,ThrowSync,ThrowAsync,DoNested,InNested,DispatchThorns;
    static int Depth;
    static string NonCriticalSeed="";
    static T State<T>(string field)=>(T)typeof(Checks).GetField(field,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method,object? owner,params object?[] args)
    {try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? H(string method,params object?[] args)=>Invoke(typeof(Checks).GetMethod(method,F)!,null,args);
    static Type Helper(string name)=>Mod.GetType("HBRGOSakiMod.Core.Helpers."+name,true)!;
    static object? Call(string helper,string method,params object?[] args)=>Invoke(Helper(helper).GetMethod(method,F)!,null,args);
    static void Set(object owner,string field,object? value)=>H("Set",owner,field,value);
    static T Blank<T>()=>(T)Invoke(typeof(Checks).GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static T Power<T>(Creature owner,int amount) where T:PowerModel=>(T)Invoke(typeof(Checks).GetMethod("Power",F)!.MakeGenericMethod(typeof(T)),null,owner,amount)!;
    static List<PowerModel> Powers(Creature owner)=>(List<PowerModel>)H("Powers",owner)!;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed.Add(message);}
    static void Equal(decimal actual,decimal expected,string message)=>Check(actual==expected,$"{message}: {actual} = {expected}");
    static object? AsyncValue(string helper,string field)
    {
        Type? type=Mod.GetType("HBRGOSakiMod.Core.Helpers."+helper);
        object? local=type?.GetField(field,F)?.GetValue(null);
        return local?.GetType().GetProperty("Value")!.GetValue(local);
    }
    static bool Enabled(object? command)=>command!=null&&(bool)command.GetType().GetField("Enabled",F)!.GetValue(command)!;
    static int? Sample(object? command)=>command==null?null:(int?)command.GetType().GetField("CriticalSample",F)!.GetValue(command);
    static IDisposable Begin(Creature? target,Creature? dealer,CardModel? card,decimal raw,ValueProp props=ValueProp.Unpowered,Creature? statSource=null)=>
        (IDisposable)Call("HbrEffectSkillDamageContext","Begin",target,dealer,card,raw,props,statSource)!;
    static Task<IEnumerable<DamageResult>> Command(Creature? dealer,decimal raw,ValueProp props=ValueProp.Unpowered,CardModel? card=null,IEnumerable<Creature>? targets=null)
    {
        Type wrapper=Mod.GetType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility",true)!;
        MethodInfo entry=wrapper.GetMethods(F).Single(method=>method.Name=="Damage"&&method.GetParameters() is var p&&p.Length==7&&p[1].ParameterType==typeof(IEnumerable<Creature>));
        return (Task<IEnumerable<DamageResult>>)Invoke(entry,null,null,targets??new[]{Enemy},raw,props,dealer,card,null)!;
    }
    static decimal Actual(Creature target,Creature? dealer,decimal raw,ValueProp props,CardModel? card)
    {
        Type wrapper=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.StrongestShieldDamagePatch",true)!;
        MethodInfo method=wrapper.GetMethods(F).Where(m=>m.Name=="ModifyDamageWithStrongestShield").OrderBy(m=>m.GetParameters().Length).First();
        return (decimal)Invoke(method,null,RunState,Combat,target,dealer,raw,props,card,ModifyDamageHookType.All,CardPreviewMode.None,null)!;
    }
    static decimal Preview(Creature target,Creature? dealer,decimal raw,ValueProp props,CardModel? card)=>(decimal)H("Preview",target,dealer,raw,props,card)!;
    static void ObserveMiniatureAdditive(MiniatureCannon __instance,object[] __args,decimal __result)
    {MiniatureCalls.Add(new(__result,__args[2].ToString()!,ReferenceEquals(__args[3],__instance.Owner.Creature),ReferenceEquals((__args[4] as CardModel)?.Owner,__instance.Owner)));}
    static bool Skip()=>false;
    static bool Energy(ref string __result){__result="energy";return false;}
    static bool EnergyPool(ref IPoolModel __result)
    {
        Type type=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool",true)!;
        __result=(CardPoolModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        Set(__result,"<Id>k__BackingField",ModelDb.GetId(type));return false;
    }
    static bool VisualTask(ref Task __result){__result=Task.CompletedTask;return false;}
    static bool Listeners(ref IEnumerable<AbstractModel> __result)
    {__result=Combat.Creatures.SelectMany(creature=>creature.Powers).Cast<AbstractModel>().Concat(Player.Relics).ToArray();return false;}
    static bool Terminal(object[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {
        Type api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!;
        var targets=(IEnumerable<Creature>)Invoke(api.GetMethod("ReadTargets",F)!,null,new object?[]{__args})!;
        decimal amount=(decimal)__args[2];ValueProp props=(ValueProp)__args[3];
        Creature? dealer=__args[4] as Creature;CardModel? cardSource=__args[5] as CardModel;
        LastCommand=AsyncValue("HbrEffectSkillDamageContext","CurrentCommand");
        if(ThrowSync)throw new InvalidOperationException("controlled-sync-effect-leaf");
        __result=Body(targets,amount,props,dealer,cardSource);return false;
    }
    static async Task<IEnumerable<DamageResult>> Body(IEnumerable<Creature> targets,decimal raw,ValueProp props,Creature? dealer,CardModel? card)
    {
        object? command=AsyncValue("HbrEffectSkillDamageContext","CurrentCommand");
        int depth=Depth++;if(depth>5)throw new Exception("retaliation recursion exceeded native Unpowered guard");
        try
        {
            Creature[] snapshot=targets.ToArray();if(AsyncLeaf)await Task.Yield();
            if(ThrowAsync)throw new InvalidOperationException("controlled-async-effect-leaf");
            Check(ReferenceEquals(command,AsyncValue("HbrEffectSkillDamageContext","CurrentCommand")),"production effect command scope survives async leaf continuation");
            if(DoNested&&!InNested)
            {
                InNested=true;try{await Command(dealer,raw,props,card,snapshot);}finally{InNested=false;}
                Check(ReferenceEquals(command,AsyncValue("HbrEffectSkillDamageContext","CurrentCommand")),"unknown nested command restores parent effect command scope");
            }
            var results=new List<DamageResult>();
            foreach(Creature target in snapshot)
            {
                decimal final=Actual(target,dealer,raw,props,card);
                Hits.Add(new(target,dealer,card,raw,props,final,Enabled(command),Sample(command),depth,(bool)Call("HpOnlyDamageContext","IsCurrentHpDamage",target,dealer,card,props)!));
                var result=Blank<DamageResult>();Set(result,"<Receiver>k__BackingField",target);Set(result,"<UnblockedDamage>k__BackingField",(int)Math.Min(int.MaxValue,Math.Max(0m,final)));results.Add(result);
                if(DispatchThorns)foreach(ThornsPower thorns in target.Powers.OfType<ThornsPower>().ToArray())
                    await (Task)Native(thorns,"BeforeDamageReceived",null,target,final,props,dealer,card)!;
            }
            return results;
        }
        finally{Depth--;}
    }
    static object? Native(object model,string method,params object?[] args)
    {
        MethodInfo native=model.GetType().GetMethods(F).Where(m=>m.Name==method).OrderByDescending(m=>m.DeclaringType==model.GetType()).ThenByDescending(m=>m.GetParameters().Length).First();
        if(native.GetParameters().Length==args.Length+1)args=[..args,null];return Invoke(native,model,args);
    }
    static int RngCount(Rng rng) => (int)(typeof(Rng).GetProperty("Counter",F)?.GetValue(rng) ?? typeof(Rng).GetField("_counter",F)?.GetValue(rng) ?? throw new MissingMemberException("Rng counter; fields: "+string.Join(",",typeof(Rng).GetFields(F).Select(field=>field.Name))));
    static string FindSeed(bool critical)
    {for(int index=0;index<10000;index++){string seed="EFFECT-SKILL-"+index;if((new RunRngSet(seed).Niche.NextInt(10000)<150)==critical)return seed;}throw new Exception("No deterministic critical seed");}
    static void Reset(bool hbr=true,string? seed=null)
    {
        H("Reset");H("Mode",hbr,1);Hits.Clear();Depth=0;ThrowSync=ThrowAsync=DoNested=InNested=DispatchThorns=false;AsyncLeaf=true;
        Set(RunState,"<Rng>k__BackingField",new RunRngSet(seed??NonCriticalSeed));
        Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual")});
        Set(Combat,"_enemies",new List<Creature>{Enemy});Set(Combat,"_allies",new List<Creature>{Hero});
        Hero.CombatState=Enemy.CombatState=Combat;
    }
    static async Task Declared(decimal raw,Creature? dealer=null,CardModel? card=null,ValueProp props=ValueProp.Unpowered,Creature? target=null)
    {dealer??=Hero;target??=Enemy;using(Begin(target,dealer,card,raw,props))await Command(dealer,raw,props,card,new[]{target});}
    static void Clean(string label)
    {
        Check(AsyncValue("HbrEffectSkillDamageContext","CurrentCommand")==null&&AsyncValue("HbrEffectSkillDamageContext","CurrentCalculation")==null&&AsyncValue("HbrEffectSkillDamageContext","Pending")==null,label+" closes all effect context frames");
        Check(LastCommand==null||!(bool)LastCommand.GetType().GetField("Active",F)!.GetValue(LastCommand)!,label+" finalizer closes the actual command object");
    }
    static decimal Base(decimal raw,decimal stat=100m,decimal enemy=150m)=>decimal.Floor(raw*1000m*(1m+(stat-enemy)/140m)*100m)/100m;
    static bool InstallAbsoluteHookOnce(Harmony harmony,Type type)
    {
        MethodBase target=(MethodBase)Invoke(type.GetMethod("TargetMethod",F)!,null)!;
        MethodInfo prefix=type.GetMethod("Prefix",F)!,finalizer=type.GetMethod("Finalizer",F)!;
        var patches=Harmony.GetPatchInfo(target);
        int prefixes=patches?.Prefixes.Count(patch=>patch.PatchMethod==prefix)??0;
        int finalizers=patches?.Finalizers.Count(patch=>patch.PatchMethod==finalizer)??0;
        // The linked stat host may already install this stateful boundary.
        // A second registration is a second BeginHook, not another test case.
        if(prefixes==0&&finalizers==0)
            harmony.CreateClassProcessor(type).Patch();
        patches=Harmony.GetPatchInfo(target);
        return patches?.Prefixes.Count(patch=>patch.PatchMethod==prefix)==1&&
            patches.Finalizers.Count(patch=>patch.PatchMethod==finalizer)==1;
    }
    static void Install()
    {
        H("Setup");NonCriticalSeed=FindSeed(false);var harmony=new Harmony(ProductionId);
        Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki>());
        var combat=Blank<PlayerCombatState>();Set(combat,"<TurnNumber>k__BackingField",1);Set(Player,"<PlayerCombatState>k__BackingField",combat);
        foreach(var row in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})Set(combat,"<"+row.Item1+">k__BackingField",new CardPile(row.Item2));
        Set(Player,"<Deck>k__BackingField",new CardPile(PileType.Deck));
        harmony.Patch(typeof(RunState).GetMethod("IterateHookListeners")!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Listeners),F)!){priority=Priority.First});
        harmony.Patch(typeof(CombatState).GetMethod("IterateHookListeners")!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Listeners),F)!){priority=Priority.First});
        foreach(Type type in Mod.GetTypes().Where(type=>type.IsDefined(typeof(HarmonyPatch),false)&&
            (type.Name.StartsWith("ExactDamagePercent",StringComparison.Ordinal)||type.Name is "HighDimensionalShellStackAmountPatch" or "DamageAmplificationStackingPatch")))harmony.CreateClassProcessor(type).Patch();
        foreach(string name in new[]{"HbrNativeFixedResourcePatch","HbrEffectSkillDamageContextPatch","HbrNumericCriticalScopePatch","HbrAbsoluteBaseDamageCommandPatch","HbrAbsoluteBaseDamageHookPatch","HpOnlyDamageContextPatch","HbrNativeEffectSkillDamagePatch","HbrNativePotionEffectSkillDamagePatch","HbrNativeThornsDamagePatch","HbrNativeGalvanicDamagePatch","HbrNativeRelicEffectSkillDamagePatch","HbrNativeDamageOverTimePatch","HbrNativePoisonPreviewPatch",
            "HbrNativeEffectSkillDescriptionPatch","HbrNativeThornsSkillDescriptionPatch","HbrNativeGalvanizedSkillDescriptionPatch","HbrNativeGalvanicHoverTipPatch","HbrNativeEffectSkillTextPatch","HbrMerchantRelicSkillDescriptionPatch"})
        {
            Type? type=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name)??Mod.GetType("HBRGOSakiMod.Core.Patches."+name);
            if(type==null)continue;
            if(name=="HbrAbsoluteBaseDamageHookPatch")
            {
                Check(InstallAbsoluteHookOnce(harmony,type),"installs exactly one production absolute-base Prefix/Finalizer pair");
                Check(InstallAbsoluteHookOnce(harmony,type),"repeated fixture setup reuses the single absolute-base hook pair");
            }
            else Check(harmony.CreateClassProcessor(type).Patch().Count>0,"installs production "+name);
        }
        foreach(Type type in new[]{typeof(HotaruDamageAmplificationPower)}){ModelDb.Inject(type);Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
        foreach(MethodInfo flash in new[]{typeof(PowerModel),typeof(RelicModel)}.SelectMany(type=>type.GetMethods(F)).Where(method=>method.Name=="Flash").Distinct())harmony.Patch(flash,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
        foreach(MethodInfo vfx in typeof(VfxCmd).GetMethods(F).Where(method=>method.Name=="PlayOnCreature"))harmony.Patch(vfx,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
        foreach(MethodInfo visual in typeof(Kusarigama).GetMethods(F).Where(method=>method.Name=="Visuals"&&method.ReturnType==typeof(Task)))harmony.Patch(visual,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
        Type energy=typeof(PowerModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper",true)!;
        foreach(MethodInfo method in energy.GetMethods(F).Where(method=>method.Name=="GetPrefix"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Energy),F)!));
        foreach(MethodInfo method in energy.GetMethods(F).Where(method=>method.Name=="GetPool"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(EnergyPool),F)!));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(MiniatureCannon),"ModifyDamageAdditive"),postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ObserveMiniatureAdditive),F)!));
        Canonical=(MethodInfo)Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!.GetField("Command",F)!.GetValue(null)!;
        new Harmony("hbr.effect-skill.checks.terminal").Patch(Canonical,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Terminal),F)!){priority=Priority.Last,after=[ProductionId]});Reset();
    }
    static async Task NativeThornsCase()
    {
        Reset();var thorns=Power<ThornsPower>(Hero,2);Power<ThornsPower>(Enemy,2);DispatchThorns=true;
        await (Task)Native(thorns,"BeforeDamageReceived",null,Hero,6m,ValueProp.Move,Enemy,null)!;
        Check(Hits.Count==1,"real native Thorns handler with Thorns on both actors emits only one retaliation command");
        Hit hit=Hits.Single();Cases.Add(new{source="Thorns",hit.Raw,hit.Final,props=hit.Props.ToString(),hit.Enabled,hit.Sample});
        Equal(hit.Raw,2m,"native Thorns raw stack2 reaches declared effect command without fixed-resource scaling");
        Equal(hit.Final,decimal.Floor(Base(2m)),"native Thorns2 resolves its33point333percent skill coefficient through actor stats");
        Check(hit.Enabled&&hit.Props==(ValueProp.Unpowered|ValueProp.SkipHurtAnim)&&ReferenceEquals(hit.Dealer,Hero)&&ReferenceEquals(hit.Target,Enemy)&&hit.Card==null,
            "real Thorns command keeps original recursion flags, owner dealer, target and null card");
        Check(thorns.Amount==2&&Enemy.GetPower<ThornsPower>()!.Amount==2,"native Thorns counters unchanged after retaliation");Clean("native Thorns");
    }
    static async Task NativeGalvanicCase()
    {
        // Globe Head's Galvanic hits whoever plays a Galvanized card, with no dealer. Like an
        // enemy's Thorns, its raw stacks are that enemy's skill on the incoming curve.
        Localization();ModelDb.Inject(typeof(Galvanized));
        Set(ModelDb.GetById<AfflictionModel>(ModelDb.GetId(typeof(Galvanized))),"<Id>k__BackingField",ModelDb.GetId(typeof(Galvanized)));
        ValueProp props=ValueProp.Unpowered|ValueProp.Move;
        async Task<(Hit hit,GalvanicPower power,AfflictionModel affliction)> Play(bool hbr)
        {
            Reset(hbr);var power=Power<GalvanicPower>(Enemy,8);var card=Owned<Inflame>();
            var affliction=(AfflictionModel)ModelDb.Affliction<Galvanized>().ToMutable();affliction.Amount=8;affliction.Card=card;Set(card,"<Affliction>k__BackingField",affliction);
            var play=Blank<CardPlay>();Set(play,"<Card>k__BackingField",card);
            await (Task)Native(power,"AfterCardPlayed",new ThrowingPlayerChoiceContext(),play)!;
            Check(Hits.Count==1,$"real native Galvanic handler issues one command for a Galvanized card (HBR {hbr})");
            return (Hits.Single(),power,affliction);
        }
        string HoverTipText(GalvanicPower power)
        {
            IHoverTip tip=((IEnumerable<IHoverTip>)Invoke(AccessTools.PropertyGetter(typeof(GalvanicPower),"ExtraHoverTips"),power)!).First();
            return (string)tip.GetType().GetProperty("Description")!.GetValue(tip)!;
        }
        var (hit,galvanic,galvanized)=await Play(true);
        Cases.Add(new{source="Galvanic",hit.Raw,hit.Final,props=hit.Props.ToString(),hit.Enabled,hit.Sample});
        Check(hit.Enabled&&hit.Raw==8m&&hit.Props==props&&ReferenceEquals(hit.Target,Hero)&&hit.Dealer==null&&hit.Card==null,
            "real Galvanic command keeps raw8, native flags, the playing hero as target and null dealer and card");
        Hits.Clear();using(Begin(Hero,null,null,8m,props,Enemy))await Command(null,8m,props,null,new[]{Hero});
        Equal(hit.Final,Hits.Single().Final,"native Galvanic8 resolves exactly as the enemy's declared skill");
        Check(hit.Final>8m&&hit.Final!=800m,"Galvanic uses the enemy incoming curve rather than literal8 or environmental800");
        Check(galvanic.Amount==8&&galvanized.Amount==8,"native Galvanic and Galvanized counters unchanged");Clean("native Galvanic");
        string cardText=galvanized.DynamicExtraCardText!.GetFormattedText(),tipText=HoverTipText(galvanic);
        Check(cardText.Contains("8000")&&!cardText.Contains('%'),"Galvanized card text shows numeric eight-unit hero damage basis without coefficient wording");
        Check(tipText.Contains("800")&&!tipText.Contains('%'),"cardless Galvanized hover tip built by the enemy uses its numeric incoming skill basis");
        var (native,nativePower,nativeAffliction)=await Play(false);
        Equal(native.Final,8m,"disabled HBR Galvanic keeps native8 damage");Clean("disabled HBR Galvanic");
        Check(nativeAffliction.DynamicExtraCardText!.GetFormattedText().Contains('8')&&!HoverTipText(nativePower).Contains('%'),
            "disabled HBR Galvanized card text and hover tip keep native8");
    }
    static async Task PrimitiveCases()
    {
        foreach(decimal raw in new[]{6m,2m,3m})
        {
            Reset();await Declared(raw);Hit hit=Hits.Single();Equal(hit.Final,decimal.Floor(Base(raw)),"explicit effect raw skill uses actor100 against enemy150: "+raw);
            Check(hit.Enabled&&hit.Sample.HasValue&&RngCount(RunState.Rng.Niche)==1,"actual declared command owns one natural-critical sample");Clean("declared primitive");
        }
        Reset();await Command(Hero,2m);Equal(Hits.Single().Final,2m,"unregistered Unpowered damage remains literal2");
        Reset();using(Begin(Enemy,Hero,null,6m))
        {int before=RngCount(RunState.Rng.Niche);Equal(Preview(Enemy,Hero,6m,ValueProp.Unpowered,null),decimal.Floor(Base(6m)),"explicit direct preview receives skill attributes");Check(RngCount(RunState.Rng.Niche)==before,"direct preview never advances own critical RNG");}
        Equal(Preview(Enemy,Hero,2m,ValueProp.Unpowered,null),2m,"disposed preview cannot leak declaration into unknown damage");
        Reset();using(Begin(Enemy,null,null,3m))await Command(null,3m);
        Equal(Hits.Single().Final,300m,"declaration with no dealer uses environmental raw units times the100 player HP scale");Check(RngCount(RunState.Rng.Niche)==0,"environmental skill has no actor critical sample");
        Reset(false);await Declared(2m);Equal(Hits.Single().Final,2m,"legacy mode retains original native effect damage");
    }
    static async Task ModifiersAndIsolationCases()
    {
        Reset();Power<StrengthPower>(Hero,20);Power<WeakPower>(Hero,1);Power<VulnerablePower>(Enemy,1);
        var amp=PowerCmd.Apply<HotaruDamageAmplificationPower>(null!,Hero,25m,Hero,null,true).GetAwaiter().GetResult()!;
        var vigor=Power<VigorPower>(Hero,999);
        await Declared(6m);Equal(Hits.Single().Final,decimal.Floor(Base(6m,140m)*1.25m*.75m*1.5m),"effect damage combines current actor attributes, native Weak/Vulnerable and persistent HBR amplification once");
        Check(vigor.Amount==999&&amp.DamagePercent==25m,"effect command and calculation neither borrow nor change native Vigor or persistent amplification");
        Reset();var card=Owned<StrikeIronclad>();var nib=Owned<PenNib>();Set(nib,"_attackToDouble",card);Set(nib,"_attacksPlayed",9);
        Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),nib});
        Equal((decimal)Native(nib,"ModifyDamageMultiplicative",Enemy,6m,ValueProp.Move,Hero,card)!,2m,"native PenNib ready marker really doubles matching powered card control");
        await Declared(6m,Hero,card);Equal(Hits.Single().Final,decimal.Floor(Base(6m)),"declared Unpowered skill does not borrow ready native PenNib attack multiplier");
        Check((int)typeof(PenNib).GetField("_attacksPlayed",F)!.GetValue(nib)! ==9&&ReferenceEquals(typeof(PenNib).GetField("_attackToDouble",F)!.GetValue(nib),card),"effect damage leaves native PenNib attack counter and ready card marker unchanged");
        Reset();DoNested=true;await Declared(2m);Equal(Hits.Single(hit=>hit.Depth==1).Final,2m,"same actor/card/raw unknown nested command cannot borrow parent declaration");
        Equal(Hits.Single(hit=>hit.Depth==0).Final,decimal.Floor(Base(2m)),"parent effect scope remains valid after unknown nested command");Clean("nested declaration");
        foreach(bool sync in new[]{false,true})
        {
            Reset();ThrowSync=sync;ThrowAsync=!sync;bool failed=false;
            try{await Declared(2m);}catch(InvalidOperationException){failed=true;}
            Check(failed,"controlled effect leaf fault propagates: "+sync);Clean("faulted declaration");ThrowSync=ThrowAsync=false;Hits.Clear();await Command(Hero,2m);Equal(Hits.Single().Final,2m,"fault cleanup leaves following unknown command literal");
        }
        foreach(bool critical in new[]{false,true})
        {
            Reset(seed:FindSeed(critical));object parent=Call("HbrCriticalDamageContext","Begin",Hero,null,ValueProp.Move)!;Set(parent,"Sample",critical?9999:0);
            try{await Declared(6m);Equal(Hits.Single().Final,critical?9000m:decimal.Floor(Base(6m)),"effect actual uses own RNG sample instead of parent critical result");Check(RngCount(RunState.Rng.Niche)==1,"own effect critical sample advances RNG exactly once");}
            finally{Call("HbrCriticalDamageContext","Close",parent);}Clean("independent effect critical scope");
        }
    }
    static async Task ResolvedContextCases()
    {
        foreach(string outer in new[]{"PrecalculatedDamageContext","HbrAbsoluteBaseDamageContext"})
        {
            Reset();var card=Owned<StrikeIronclad>();ValueProp props=ValueProp.Move;
            decimal retained=outer=="PrecalculatedDamageContext"?6m:decimal.Floor(Base(.006m));
            using((IDisposable)(outer=="PrecalculatedDamageContext"?Call(outer,"Begin",card,Enemy,6m):Call(outer,"Begin",Hero,card,Enemy,6m))!)
            {
                Equal(Preview(Enemy,Hero,6m,props,card),retained,outer+" retains its declared resolved or world-basis preview behavior");
                using(Begin(Enemy,Hero,card,6m,props))
                    Equal(Preview(Enemy,Hero,6m,props,card),decimal.Floor(Base(6m)),outer+" cannot suppress newly declared skill preview conversion");
                Equal(Preview(Enemy,Hero,6m,props,card),retained,outer+" restores its basis after explicit skill scope disposal");
                Check(RngCount(RunState.Rng.Niche)==0,outer+" explicit preview does not consume actual RNG");
                await Command(Hero,6m,props,card);Equal(Hits.Single().Final,retained,outer+" retains its declared resolved or world-basis actual command");Hits.Clear();
                await Declared(6m,Hero,card,props);Equal(Hits.Single().Final,decimal.Floor(Base(6m)),outer+" cannot suppress newly declared actual skill conversion");
                Check(ReferenceEquals(Hits.Single().Card,card)&&Hits.Single().Props==props,outer+" actual skill retains original card attribution and powered flags");
                Hits.Clear();await Command(Hero,6m,props,card);Equal(Hits.Single().Final,retained,outer+" restores its actual basis after explicit skill scope disposal");
            }
            Clean(outer+" isolation");
        }
        Reset();var second=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;
        Set(Combat,"_enemies",new List<Creature>{Enemy,second});var cardSource=Owned<StrikeIronclad>();
        using(Begin(null,Hero,cardSource,3m))await Command(Hero,3m,card:cardSource,targets:new[]{Enemy,second});
        Check(Hits.Count==2&&Hits.All(hit=>hit.Final==decimal.Floor(Base(3m))&&hit.Enabled&&ReferenceEquals(hit.Card,cardSource)),"null target declaration resolves both AoE targets and retains card source");
        Check(Hits[0].Sample==Hits[1].Sample&&RngCount(RunState.Rng.Niche)==1,"one AoE command shares only its own single critical sample across targets");Clean("AoE effect");
    }
    static T Owned<T>() where T:AbstractModel,new()
    {var model=new T();Set(model,"<IsMutable>k__BackingField",true);Set(model,"_owner",Player);Set(model,"<Id>k__BackingField",ModelDb.GetId(typeof(T)));return model;}
    static async Task NativeKusarigamaCase()
    {
        Reset();var relic=Owned<Kusarigama>();Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),relic});
        var card=Owned<StrikeIronclad>();var play=Blank<CardPlay>();Set(play,"<Card>k__BackingField",card);
        decimal damage=relic.DynamicVars.Damage.BaseValue;int cards=relic.DynamicVars["Cards"].IntValue;
        Equal(damage,6m,"native Kusarigama DamageVar stores raw6");Check(cards==3,"native Kusarigama attack trigger interval stores3");
        await (Task)Native(relic,"AfterCardPlayed",new ThrowingPlayerChoiceContext(),play)!;
        await (Task)Native(relic,"AfterCardPlayed",new ThrowingPlayerChoiceContext(),play)!;
        Check(Hits.Count==0,"real native Kusarigama first two attack-card handlers do not issue damage");
        int targetsBefore=RngCount(RunState.Rng.CombatTargets);
        await (Task)Native(relic,"AfterCardPlayed",new ThrowingPlayerChoiceContext(),play)!;
        Check(Hits.Count==1,"real native third attack invokes original typed-DamageVar adapter and one canonical command");Hit hit=Hits.Single();
        Cases.Add(new{source="Kusarigama",hit.Raw,hit.Final,props=hit.Props.ToString(),hit.Enabled,hit.Sample,targetsBefore,targetsAfter=RngCount(RunState.Rng.CombatTargets)});
        Equal(hit.Final,decimal.Floor(Base(6m)),"real native Kusarigama6 resolves100percent through actor stats rather than literal fixed damage");
        Check(hit.Enabled&&hit.Raw==6m&&hit.Props==ValueProp.Unpowered&&ReferenceEquals(hit.Target,Enemy)&&ReferenceEquals(hit.Dealer,Hero)&&hit.Card==null,
            "native Kusarigama keeps random chosen target, dealer, Unpowered flags and null source");
        Equal(relic.DynamicVars.Damage.BaseValue,6m,"Kusarigama original raw DynamicVar storage is not scaled or rewritten");
        Check((int)typeof(Kusarigama).GetField("_attacksPlayedThisTurn",F)!.GetValue(relic)! ==3,"native Kusarigama attack counter advances exactly three times");
        Check(relic.DynamicVars["Cards"].IntValue==3,"native Kusarigama trigger count is unchanged");Clean("native Kusarigama");
    }
    static string RepositoryRoot
    {
        get{for(DirectoryInfo? directory=new(AppContext.BaseDirectory);directory!=null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"HBRGOSakiMod.csproj")))return directory.FullName;
            throw new DirectoryNotFoundException("Fixture must be inside checkout");}
    }
    static void Localization()
    {
        string nativeRuntime=System.Environment.GetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY")??"D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving+=(context,name)=>{string path=Path.Combine(nativeRuntime,name.Name+".dll");return File.Exists(path)?context.LoadFromAssemblyPath(path):null;};
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);var tables=new Dictionary<string,LocTable>();
        foreach(string table in new[]{"cards","relics","powers","events","potions","static_hover_tips","card_keywords","afflictions"})
        {
            var text=new Dictionary<string,string>();string native=Path.Combine("D:/stsmod/sts/localization/zhs",table+".json");
            if(File.Exists(native))foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(native))!)text[pair.Key]=pair.Value;
            string mod=Path.Combine(RepositoryRoot,"HBRGOSakiMod/localization/zhs",table+".json");
            if(File.Exists(mod))foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(mod))!)text[pair.Key]=pair.Value;
            tables[table]=new LocTable(table,text,null!);
        }
        Set(manager,"_tables",tables);typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
    }
    static async Task NativeStatusPotionAndHpOnlyCases()
    {
        Reset();var burn=Owned<Burn>();
        await (Task)Native(burn,"OnTurnEndInHand",new ThrowingPlayerChoiceContext())!;
        Hit hit=Hits.Single();Cases.Add(new{source="Burn.OnTurnEndInHand",hit.Raw,hit.Final,props=hit.Props.ToString(),hit.Enabled,hit.Sample,dealer=hit.Dealer==null?"null":ReferenceEquals(hit.Dealer,Hero)?"hero":"other",card=hit.Card?.GetType().Name});
        Check(hit.Enabled&&hit.Final>2m,"real native Burn hand-turn-end handler reaches declared skill calculation instead of literal2");
        Equal(hit.Final,200m,"native Burn raw2 is environmental hand-tick damage200 rather than the holder's skill");
        Check(ReferenceEquals(hit.Target,Hero)&&hit.Raw==2m&&hit.Props==burn.DynamicVars.Damage.Props&&burn.DynamicVars.Damage.BaseValue==2m&&ReferenceEquals(hit.Dealer,Hero)&&ReferenceEquals(hit.Card,burn),"Burn handler retains native target, raw DamageVar flags and raw storage");Clean("native Burn handler");
        Reset();var fire=Owned<FirePotion>();
        await (Task)Native(fire,"OnUse",new ThrowingPlayerChoiceContext(),Enemy)!;
        hit=Hits.Single();Cases.Add(new{source="FirePotion.OnUse",hit.Raw,hit.Final,props=hit.Props.ToString(),hit.Enabled,hit.Sample,dealer=hit.Dealer==null?"null":ReferenceEquals(hit.Dealer,Hero)?"hero":"other",card=hit.Card?.GetType().Name});
        Check(hit.Enabled&&hit.Raw==fire.DynamicVars.Damage.BaseValue&&hit.Final>hit.Raw,"real native FirePotion OnUse direct-decimal consumer reaches declared skill calculation");
        Equal(hit.Final,decimal.Floor(Base(20m)),"native FirePotion raw20 resolves exact actor skill damage12857");
        Check(ReferenceEquals(hit.Target,Enemy)&&hit.Props==fire.DynamicVars.Damage.Props&&fire.DynamicVars.Damage.BaseValue==20m&&ReferenceEquals(hit.Dealer,Hero)&&hit.Card==null,"FirePotion handler retains target, original flags and raw20 storage");Clean("native FirePotion handler");
        Reset();H("Sense","Madness",30);var shell=Power<HighDimensionalShellPower>(Enemy,2000);var source=Owned<StrikeIronclad>();
        object stats=H("Stats",Hero)!;Equal((decimal)H("Stat",stats,"Strength")!,160m,"HP-only weight smoke has independent Strength160");Equal((decimal)H("Stat",stats,"Dexterity")!,100m,"HP-only weight smoke has independent Dexterity100");
        ValueProp props=ValueProp.Unblockable|ValueProp.Unpowered;
        await (Task)Call("HpOnlyDamageContext","Damage",new ThrowingPlayerChoiceContext(),Enemy,1m,Hero,source)!;
        hit=Hits.Single();Equal(hit.Final,1m,"unknown HP-only amount1 keeps native already-resolved behavior despite DP");Check(hit.HpOnly&&!hit.Enabled,"unknown HP-only command owns HP route without declaring a skill");Hits.Clear();
        using(Begin(Enemy,Hero,source,1m,props))await (Task)Call("HpOnlyDamageContext","Damage",new ThrowingPlayerChoiceContext(),Enemy,1m,Hero,source)!;
        hit=Hits.Single();Cases.Add(new{source="explicitHpOnly",hit.Raw,hit.Final,props=hit.Props.ToString(),hit.Enabled,hit.HpOnly,hit.Sample,dp=shell.CurrentDp});
        Equal(hit.Final,decimal.Floor(Base(1m,130m)),"explicit HP-only raw1 uses physical weighted-stat130 regardless of positive target DP");
        Check(hit.Final!=decimal.Floor(Base(1m,120m))&&hit.HpOnly&&hit.Enabled&&hit.Raw==1m&&hit.Props==props&&ReferenceEquals(hit.Dealer,Hero)&&ReferenceEquals(hit.Card,source),"HP-only skill bypasses old resolved-base shortcut, preserves attribution and does not reuse the retired DP-biased weighted-stat120");
        Equal(shell.CurrentDp,2000m,"HP-only calculation smoke never spends its terminal-isolated target DP");Clean("HP-only effect");
        Check(AsyncValue("HpOnlyDamageContext","Pending")==null&&AsyncValue("HpOnlyDamageContext","Current")==null,"native HP-only helper and canonical postfix close their pending/current frames");
    }
    static async Task NativeEventAdapterCases()
    {
        Localization();ModelDb.Inject(typeof(RoyalPoison));
        foreach((string name,string handler,decimal raw,string option) in new[]{("AbyssalBaths","OnImmerse",3m,"ABYSSAL_BATHS.pages.INITIAL.options.IMMERSE"),
            ("RoomFullOfCheese","Search",14m,"ROOM_FULL_OF_CHEESE.pages.INITIAL.options.SEARCH"),("RoundTeaParty","ContinueFight",11m,"ROUND_TEA_PARTY.pages.INITIAL.options.PICK_FIGHT"),
            ("TheLegendsWereTrue","SlowlyFindAnExit",8m,"THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.SLOWLY_FIND_AN_EXIT")})
        {
            Reset();Type type=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Models.Events."+name,true)!;
            var model=(EventModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            Set(model,"<IsMutable>k__BackingField",true);Set(model,"<Owner>k__BackingField",Player);Set(model,"<Id>k__BackingField",ModelDb.GetId(type));
            MethodInfo native=AccessTools.Method(type,handler);Type state=native.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType;
            var calls=PatchProcessor.GetOriginalInstructions(AccessTools.Method(state,"MoveNext")).Select(instruction=>instruction.operand).OfType<MethodInfo>()
                .Where(method=>method.DeclaringType==typeof(CreatureCmd)&&method.Name=="Damage"&&method.GetParameters().Any(parameter=>parameter.ParameterType==typeof(DamageVar))).ToArray();
            Check(calls.Length==1,"native event handler reaches exactly one declared typed-DamageVar adapter: "+name);
            DamageVar variable=model.DynamicVars.Damage;Equal(variable.BaseValue,raw,"native event raw DamageVar remains original: "+name);
            MethodInfo adapter=calls.Single();object?[] args=adapter.GetParameters().Select(parameter=>parameter.ParameterType==typeof(DamageVar)?(object)variable:
                parameter.ParameterType==typeof(Creature)?(parameter.Name=="target"?Hero:null):parameter.ParameterType==typeof(PlayerChoiceContext)?new ThrowingPlayerChoiceContext():null).ToArray();
            await (Task)Invoke(adapter,null,args)!;Hit hit=Hits.Single();Equal(hit.Final,raw*100m,"actual native event typed adapter declares environmental skill units: "+name);
            Check(hit.Enabled&&hit.Raw==raw&&ReferenceEquals(hit.Target,Hero)&&hit.Dealer==null&&hit.Card==null&&hit.Props==variable.Props,
                "event command retains target, null dealer/card and original DamageVar flags: "+name);
            Check(RngCount(RunState.Rng.Niche)==0&&variable.BaseValue==raw,"event environment has no actor critical RNG and does not rewrite storage: "+name);Clean("native event adapter "+name);
            // Environmental damage displays its amount and keeps the native point unit, not a coefficient.
            string text=OptionText(model,option);
            Check(text.Contains($"[red]{raw*100m}[/red]点")&&!text.Contains('%'),"native event option text shows environmental amount "+raw*100m+": "+name);
            H("Mode",false,1);
            Check(OptionText(model,option).Contains($"[red]{raw}[/red]点"),"disabled HBR event option text keeps native "+raw+": "+name);
        }
    }
    static string OptionText(EventModel model,string option)
    {
        LocString text=model.GetOptionDescription(option)!;
        foreach(DynamicVar variable in model.DynamicVars.Values)text.Add(variable);
        return text.GetFormattedText();
    }
    static void DescriptionAndSourceCases()
    {
        Reset();Localization();var kusa=Owned<Kusarigama>();decimal raw=kusa.DynamicVars.Damage.BaseValue;
        string text=kusa.DynamicDescription.GetFormattedText();
        Check(text.Contains("6000")&&!text.Contains('%')&&!text.Contains('{'),"real native Kusarigama DynamicDescription shows numeric six-unit damage basis");Equal(kusa.DynamicVars.Damage.BaseValue,raw,"description never rewrites Kusarigama raw DamageVar");
        var burn=Owned<Burn>();text=burn.Description.GetFormattedText();
        Check(text.Contains("200")&&!text.Contains("2000")&&!text.Contains('%')&&!text.Contains('{'),"native Burn diff-format description safely shows environmental hand-tick damage200");Equal(burn.DynamicVars.Damage.BaseValue,2m,"native Burn stores raw2 after formatting");
        var thorns=Power<ThornsPower>(Hero,2);LocString smart=thorns.SmartDescription;smart.Add("OwnerName","测试角色");
        Invoke(AccessTools.Method(typeof(PowerModel),"AddDumbVariablesToDescription"),thorns,smart,null);
        Check(smart.GetFormattedText().Contains("2000")&&!smart.GetFormattedText().Contains('%')&&thorns.Amount==2,"native Thorns description shows numeric damage without changing its two-stack counter");
        var merchant=new Kusarigama();
        string native=merchant.DynamicDescription.GetFormattedText();Check(!native.Contains("6000"),"unowned canonical Kusarigama stays native without merchant context");
        using((IDisposable)Call("HbrNativeEffectSkillHelper","BeginMerchantDisplay",merchant,Player)!)
            Check(merchant.DynamicDescription.GetFormattedText().Contains("6000"),"explicit merchant context renders unowned Kusarigama numeric skill basis without Owner access");
        Check(!merchant.DynamicDescription.GetFormattedText().Contains("6000"),"merchant scope cleanup leaves following canonical description native");
        foreach((Type model,decimal expected) in new[]{(typeof(Burn),2m),(typeof(Decay),2m),(typeof(Infection),3m),(typeof(Toxic),5m),(typeof(Wither),3m)})
        {
            var card=(CardModel)Activator.CreateInstance(model)!;Set(card,"<IsMutable>k__BackingField",true);Set(card,"_owner",Player);
            Check((bool)Call("HbrNativeEffectSkillHelper","IsSkillVariable",card.DynamicVars.Damage)!&&card.DynamicVars.Damage.BaseValue==expected,
                "audited native status declares skill DamageVar while retaining original units: "+model.Name);
            Equal((decimal)Call("HbrNativeFixedResourceHelper","ReadBase",card.DynamicVars.Damage)!,expected,"old fixed-resource reader does not pre-scale effect skill units: "+model.Name);
        }
        foreach(string name in new[]{"AbyssalBaths","RoomFullOfCheese","RoundTeaParty","TheLegendsWereTrue"})
        {
            Type type=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Models.Events."+name,true)!;
            var model=(AbstractModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            Check((bool)Call("HbrNativeEffectSkillHelper","IsSkillModel",model)!,"audited native event is declared in source registry: "+name);
        }
        foreach((string patch,int expected) in new[]{("HbrNativeEffectSkillDamagePatch",5),("HbrNativePotionEffectSkillDamagePatch",3)})
        {
            Type type=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+patch,true)!;
            var targets=((IEnumerable<System.Reflection.MethodBase>)Invoke(type.GetMethod("TargetMethods",F)!,null)!).ToArray();
            Check(targets.Length==expected&&targets.All(method=>method.DeclaringType!.Assembly==typeof(ModelDb).Assembly),"native consumer adapter metadata has exact declared method count: "+patch);
        }
    }
    static (MiniatureCannon Relic,StrikeIronclad Card) MiniatureSetup(bool upgraded=true)
    {
        var relic=Owned<MiniatureCannon>();var card=Owned<StrikeIronclad>();
        card.UpgradePreviewType=CardUpgradePreviewType.Combat;
        if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
        Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),relic});return(relic,card);
    }
    static async Task MiniatureCannonCases()
    {
        Reset();var (mini,card)=MiniatureSetup();
        Check(card.IsUpgraded,"Miniature Cannon smoke uses a real natively upgraded attack card");
        Check(RunState.IterateHookListeners(Combat).Count(model=>ReferenceEquals(model,mini))==1,"actual RunState hook-listener enumeration contains Miniature Cannon exactly once");
        Equal(mini.DynamicVars["ExtraDamage"].BaseValue,3m,"native Miniature Cannon ExtraDamage variable retains raw3");
        Equal((decimal)Native(mini,"ModifyDamageAdditive",Enemy,6m,ValueProp.Move,Hero,card)!,3m,"real native Miniature Cannon additive hook returns original raw3");
        MiniatureCalls.Clear();await Command(Hero,6m,ValueProp.Move,card);Hit hit=Hits.Single();
        Cases.Add(new{source="MiniatureCannon",hit.Raw,hit.Final,props=hit.Props.ToString(),criticalRng=RngCount(RunState.Rng.Niche),nativeListeners=RunState.IterateHookListeners(Combat).Select(model=>model.GetType().FullName).ToArray(),nativeAdditiveCalls=MiniatureCalls.ToArray()});
        Equal(hit.Final,decimal.Floor(Base(6m)+Base(3m)),"Miniature Cannon raw3 adds50percent skill damage through the same attribute curve");
        Check(MiniatureCalls.Count==1&&MiniatureCalls.Single().Result==3m,"real actual damage pipeline calls the unchanged native Miniature Cannon additive hook exactly once");
        Check(Hits.Count==1&&hit.Raw==6m&&hit.Props==ValueProp.Move&&ReferenceEquals(hit.Card,card)&&ReferenceEquals(hit.Dealer,Hero),"Miniature Cannon adds within the original command while preserving raw base, flags and card/dealer");
        Equal(mini.DynamicVars["ExtraDamage"].BaseValue,3m,"Miniature Cannon combat calculation does not rewrite raw ExtraDamage storage");
        Reset();(mini,card)=MiniatureSetup();Power<StrengthPower>(Hero,20);Power<WeakPower>(Hero,1);Power<VulnerablePower>(Enemy,1);
        PowerCmd.Apply<HotaruDamageAmplificationPower>(null!,Hero,25m,Hero,null,true).GetAwaiter().GetResult();
        await Command(Hero,6m,ValueProp.Move,card);
        Equal(Hits.Single().Final,decimal.Floor((Base(6m,140m)+Base(3m,140m))*.75m*1.5m*1.25m),"Miniature Cannon skill bonus shares current attributes, Weak, Vulnerable and persistent amplification exactly once");
        Reset(seed:FindSeed(true));(mini,card)=MiniatureSetup();await Command(Hero,6m,ValueProp.Move,card);
        Equal(Hits.Single().Final,13500m,"Miniature Cannon base9000 and bonus4500 use the same successful native critical roll");
        Check(RngCount(RunState.Rng.Niche)==1,"Miniature Cannon additional skill component never draws a second critical sample");
        Reset();(mini,card)=MiniatureSetup();await Command(Hero,0m,ValueProp.Move,card);
        Equal(Hits.Single().Final,decimal.Floor(Base(3m)),"zero base damage still retains the real native Miniature Cannon additive skill component");
        Reset();(mini,card)=MiniatureSetup();H("Act",3);await Command(Hero,6m,ValueProp.Move,card);
        Equal(Hits.Single().Final,2m,"very high enemy parameter keeps the independently resolved base and additive skill minimums at one each");
        Reset();(mini,card)=MiniatureSetup(false);await Command(Hero,6m,ValueProp.Move,card);
        Equal(Hits.Single().Final,decimal.Floor(Base(6m)),"unupgraded attack receives no native Miniature Cannon bonus");
        Reset();(mini,card)=MiniatureSetup();await Command(Hero,6m,ValueProp.Unpowered,card);
        Equal(Hits.Single().Final,6m,"non-powered damage retains native Miniature Cannon exclusion and literal Unpowered amount");
        Reset();(mini,card)=MiniatureSetup();var foreign=Blank<Player>();var foreignHero=(Creature)H("Actor",null,foreign,null)!;Set(foreign,"<Creature>k__BackingField",foreignHero);Set(mini,"_owner",foreign);
        Equal((decimal)Native(mini,"ModifyDamageAdditive",Enemy,6m,ValueProp.Move,Hero,card)!,0m,"native ownership guard excludes both foreign dealer and foreign card owner");
        await Command(Hero,6m,ValueProp.Move,card);Equal(Hits.Single().Final,decimal.Floor(Base(6m)),"another player's Miniature Cannon does not add to this player's attack");
        Set(mini,"_owner",Player);Equal((decimal)Native(mini,"ModifyDamageAdditive",Enemy,6m,ValueProp.Move,foreignHero,card)!,3m,"native ownership OR preserves matching card owner even when dealer differs");
        Reset(false);(mini,card)=MiniatureSetup();await Command(Hero,6m,ValueProp.Move,card);Equal(Hits.Single().Final,9m,"legacy mode retains native additive3 instead of HBR skill conversion");
        Reset(seed:FindSeed(true));(mini,card)=MiniatureSetup();
        using((IDisposable)Call("HbrAbsoluteBaseDamageContext","Begin",Hero,card,Enemy,6m)!)await Command(Hero,6m,ValueProp.Move,card);
        Equal(Hits.Single().Final,decimal.Floor((.006m+3m)*1500m),"absolute world-space base6 normalizes once and shares the critical skill curve with Miniature Cannon");
        Check(RngCount(RunState.Rng.Niche)==1,"absolute world-space attack and bonus share one critical sample");
        Reset();(mini,card)=MiniatureSetup();
        using((IDisposable)Call("PrecalculatedDamageContext","Begin",card,Enemy,6m)!)await Command(Hero,6m,ValueProp.Move,card);
        Equal(Hits.Single().Final,6m,"already-precalculated attack continues to skip the entire additive phase");
        Reset();(mini,card)=MiniatureSetup();await (Task)Call("HpOnlyDamageContext","Damage",new ThrowingPlayerChoiceContext(),Enemy,6m,Hero,card)!;
        Equal(Hits.Single().Final,6m,"unknown HP-only resolved damage continues to skip Miniature Cannon additive phase");
        Reset();(mini,card)=MiniatureSetup();
        var ghost=Power<GhostStatePower>(Hero,1);var down=Power<HbrDefenseDownPower>(Enemy,1);Call("HbrPercentDebuffHelper","Configure",down,10m,1);
        var monster=Blank<TezukaSaki>();Set(monster,"<IsMutable>k__BackingField",true);Set(monster,"_creature",Hero);
        Set(monster,"<NextMove>k__BackingField",new MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState("FOCUSED_BREAKTHROUGH_MOVE",_=>Task.CompletedTask));Set(Hero,"<Monster>k__BackingField",monster);
        try
        {
            Equal((decimal)Invoke(typeof(GhostStatePower).GetMethod("GetBonusUnits",F)!,ghost,Enemy,ValueProp.Move)!,.36m,"real Ghost focused-breakthrough coefficient bonus is active for duplicate-bonus guard");
            await Command(Hero,6m,ValueProp.Move,card);
            Equal(Hits.Single().Final,decimal.Floor((Base(6.36m)+Base(3m))*1.1m),"Miniature Cannon bonus does not reapply Ghost base coefficient bonus to the extra component");
        }
        finally{Set(Hero,"<Monster>k__BackingField",null);}
        Reset();Localization();(mini,card)=MiniatureSetup();
        string text=mini.DynamicDescription.GetFormattedText();Check(text.Contains("3000")&&!text.Contains('%')&&!text.Contains('{'),"native owned Miniature Cannon description shows numeric additional skill basis");
        Equal(mini.DynamicVars["ExtraDamage"].BaseValue,3m,"Miniature Cannon description formatting leaves ordinary ExtraDamage variable at3");
        var merchant=new MiniatureCannon();Check(!merchant.DynamicDescription.GetFormattedText().Contains("3000"),"unowned canonical Miniature Cannon stays native without explicit merchant context");
        using((IDisposable)Call("HbrNativeEffectSkillHelper","BeginMerchantDisplay",merchant,Player)!)
            Check(merchant.DynamicDescription.GetFormattedText().Contains("3000"),"explicit merchant scope displays unowned Miniature Cannon numeric additional skill basis");
        Check(!merchant.DynamicDescription.GetFormattedText().Contains("3000"),"Miniature Cannon merchant scope restores canonical native formatting after disposal");
    }
    static readonly List<decimal> VigorCalls=[];
    static void ObserveVigorAdditive(decimal __result)=>VigorCalls.Add(__result);
    static Type VigorPatch=>Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HotaruCannonVigorPatch",true)!;
    static bool Reserved(VigorPower vigor)=>(bool)Invoke(VigorPatch.GetMethod("IsVigorReservedForNativeAttack",F)!,null,vigor)!;
    static AttackCommand VigorAttack(Creature actor,CardModel? card=null,ValueProp props=ValueProp.Move)
    {
        var command=new AttackCommand(6m);
        if(card!=null)
        {
            MethodInfo from=typeof(AttackCommand).GetMethods(F).Single(method=>method.Name=="FromCard");
            Invoke(from,command,from.GetParameters().Length==1?[card]:[card,null]);
        }
        else{Set(command,"<Attacker>k__BackingField",actor);Set(command,"<DamageProps>k__BackingField",props);}
        return command.Targeting(Enemy);
    }
    static void InstallVigorLifetime()
    {
        var harmony=new Harmony("hbr.effect-skill.vigor-lifecycle");
        foreach(string name in new[]{"HotaruCannonVigorPatch","HotaruCannonVigorBeforeAttackReservationPatch","HotaruCannonVigorAfterAttackReservationPatch","HotaruCannonVigorNativeAttackCompletionPatch","HotaruCannonDamageContextPatch"})
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs actual native Vigor/cannon lifetime boundary "+name);
        foreach(string name in new[]{"HbrVigorSkillDescriptionPatch","HbrVigorCardHoverDescriptionPatch"})
            if(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name) is {} description)harmony.CreateClassProcessor(description).Patch();
        harmony.Patch(AccessTools.PropertyGetter(typeof(PowerModel),"Icon"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
        ModelDb.Inject(typeof(VigorPower));Set(ModelDb.Power<VigorPower>(),"<Id>k__BackingField",ModelDb.GetId(typeof(VigorPower)));
        harmony.Patch(typeof(VigorPower).GetMethods(F).Single(method=>method.Name=="ModifyDamageAdditive"&&method.DeclaringType==typeof(VigorPower)),postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ObserveVigorAdditive),F)!){priority=Priority.Last,after=[harmony.Id,ProductionId]});
    }
    static async Task<decimal> VigorNativeHit(bool hbr=true,bool critical=false,bool modifiers=false)
    {
        Reset(hbr,FindSeed(critical));var vigor=Power<VigorPower>(Hero,3);var card=Owned<StrikeIronclad>();AttackCommand attack=VigorAttack(Hero,card);
        if(modifiers){Power<StrengthPower>(Hero,20);Power<WeakPower>(Hero,1);Power<VulnerablePower>(Enemy,1);PowerCmd.Apply<HotaruDamageAmplificationPower>(null!,Hero,25m,Hero,null,true).GetAwaiter().GetResult();}
        await Hook.BeforeAttack(Combat,attack);Check(Reserved(vigor)&&vigor.Amount==3,"real native BeforeAttack reserves existing three Vigor without consuming it");
        VigorCalls.Clear();IEnumerable<DamageResult> results=await Command(Hero,6m,ValueProp.Move,card);attack.AddResultsInternal(results);Hit hit=Hits.Single();
        Check(vigor.Amount==3,"real actual damage calculation retains Vigor until native AfterAttack");
        Check(VigorCalls.Count==1&&VigorCalls.Single()==3m,"actual attack evaluates the effective native raw Vigor additive hook exactly once");
        await Hook.AfterAttack(Combat,new ThrowingPlayerChoiceContext(),attack);
        Check(Hero.GetPower<VigorPower>()==null||Hero.GetPower<VigorPower>()!.Amount==0,"real native AfterAttack consumes its reserved Vigor");Check(!Reserved(vigor),"native AfterAttack releases production cannon exclusion reservation");
        Check(hit.Raw==6m&&hit.Props==ValueProp.Move&&ReferenceEquals(hit.Card,card)&&ReferenceEquals(hit.Dealer,Hero),"Vigor conversion leaves native command raw6 source flags and attribution unchanged");
        Cases.Add(new{source="nativeVigor",hbr,critical,modifiers,hit.Raw,hit.Final,vigorAfter=vigor.Amount,remaining=Hero.GetPower<VigorPower>()?.Amount,nativeAdditiveCalls=VigorCalls.ToArray()});return hit.Final;
    }
    static string[] NativeTipTexts(CardModel card)
    {
        var tips=(System.Collections.IEnumerable)typeof(CardModel).GetProperty("HoverTips",F)!.GetValue(card)!;
        return tips.Cast<object>().Select(tip=>tip.GetType().GetProperty("Description",F)?.GetValue(tip) as string??"").ToArray();
    }
    static async Task VigorSkillCases()
    {
        InstallVigorLifetime();decimal actual=await VigorNativeHit();Equal(actual,decimal.Floor(Base(9m)),"native Vigor3 joins base6 as one150percent coefficient before the attribute curve");
        actual=await VigorNativeHit(modifiers:true);Equal(actual,decimal.Floor(Base(9m,140m)*.75m*1.5m*1.25m),"Vigor coefficient shares actor stats and native Weak Vulnerable persistent amplification");
        actual=await VigorNativeHit(critical:true);Equal(actual,13500m,"combined Vigor150percent skill shares the attack critical curve once");Check(RngCount(RunState.Rng.Niche)==1,"combined Vigor critical calculation draws only one native sample");
        actual=await VigorNativeHit(hbr:false);Equal(actual,9m,"legacy native attack retains original raw6 plus3 additive behavior and consumption");
        Reset();var vigor=Power<VigorPower>(Hero,3);VigorCalls.Clear();await Command(Hero,6m,ValueProp.Unpowered);
        Equal(Hits.Single().Final,6m,"unknown Unpowered damage does not borrow or convert Vigor");Check(vigor.Amount==3,"Unpowered command does not consume native Vigor");
        Reset();vigor=Power<VigorPower>(Hero,3);await Command(Hero,0m,ValueProp.Move);Equal(Hits.Single().Final,decimal.Floor(Base(3m)),"zero attack base plus Vigor3 still resolves one50percent skill");
        Reset();vigor=Power<VigorPower>(Hero,3);H("Act",3);await Command(Hero,6m,ValueProp.Move);Equal(Hits.Single().Final,1m,"high enemy parameter applies only one minimum to combined base plus Vigor");
        Reset(seed:FindSeed(true));vigor=Power<VigorPower>(Hero,3);var absoluteCard=Owned<StrikeIronclad>();
        using((IDisposable)Call("HbrAbsoluteBaseDamageContext","Begin",Hero,absoluteCard,Enemy,250m)!)await Command(Hero,250m,ValueProp.Move,absoluteCard);
        Equal(Hits.Single().Final,decimal.Floor((.25m+3m)*1500m),"absolute world base250 normalizes once before combining Vigor and the same critical curve");Check(RngCount(RunState.Rng.Niche)==1,"absolute Vigor uses one original critical sample");
        Reset();vigor=Power<VigorPower>(Hero,3);var resolvedCard=Owned<StrikeIronclad>();VigorCalls.Clear();
        using((IDisposable)Call("PrecalculatedDamageContext","Begin",resolvedCard,Enemy,6m)!)await Command(Hero,6m,ValueProp.Move,resolvedCard);
        Equal(Hits.Single().Final,6m,"precalculated world damage skips Vigor collection and additive phase");Check(VigorCalls.Count==0,"precalculated command never evaluates native Vigor a second time");
        Reset();vigor=Power<VigorPower>(Hero,3);VigorCalls.Clear();await (Task)Call("HpOnlyDamageContext","Damage",new ThrowingPlayerChoiceContext(),Enemy,6m,Hero,resolvedCard)!;
        Equal(Hits.Single().Final,6m,"unknown HP-only resolved damage remains6 with Vigor present");Check(VigorCalls.Count==0,"unknown HP-only damage retains complete additive skip");
        Reset();vigor=Power<VigorPower>(Hero,3);var explicitCard=Owned<StrikeIronclad>();
        using(Begin(Enemy,Hero,explicitCard,6m,ValueProp.Move))await Command(Hero,6m,ValueProp.Move,explicitCard);
        Equal(Hits.Single().Final,decimal.Floor(Base(9m)),"explicit powered skill with Vigor converts base6 plus3 while claiming the original raw6 intent");Check(Hits.Single().Enabled&&Hits.Single().Raw==6m,"Vigor skill bonus does not rewrite canonical amount or break explicit declaration matching");
        Reset();var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);var own=Power<VigorPower>(phantom,3);var owner=Power<VigorPower>(Hero,9);Set(Combat,"_allies",new List<Creature>{Hero,phantom});
        AttackCommand attack=VigorAttack(phantom);await Hook.BeforeAttack(Combat,attack);VigorCalls.Clear();var results=await Command(phantom,6m,ValueProp.Move);attack.AddResultsInternal(results);await Hook.AfterAttack(Combat,new ThrowingPlayerChoiceContext(),attack);
        Equal(Hits.Single().Final,decimal.Floor(Base(9m)),"phantom uses only its own Vigor3 in base6 coefficient and excludes summoner Vigor9");Check(owner.Amount==9&&(phantom.GetPower<VigorPower>()==null||phantom.GetPower<VigorPower>()!.Amount==0),"phantom native attack consumes its own Vigor while preserving summoner Vigor");
        foreach(bool nativeReservation in new[]{false,true})
        {
            Reset();vigor=Power<VigorPower>(Hero,3);var relic=new Millsage();Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",Player);Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),relic});
            var cannon=(Creature)H("Actor",Blank<HotaruContestCannon>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,cannon});var card=Owned<StrikeIronclad>();attack=VigorAttack(Hero,card);
            if(nativeReservation)await Hook.BeforeAttack(Combat,attack);
            object phase=Enum.Parse(Helper("HotaruCannonPhase"),"End");
            using((IDisposable)Call("HotaruCannonVigorHelper","BeginCompleteActionBatch",relic,new[]{cannon},false)!)
            {
                object action=Call("HotaruCannonVigorHelper","BeginCompleteAction",cannon,phase)!;
                try{await (Task<bool>)Call("HotaruCannonStatsHelper","Damage",cannon,Enemy,6m,null,null,null,1)!;}
                finally{await (Task)Invoke(action.GetType().GetMethod("Complete",F)!,action,new ThrowingPlayerChoiceContext())!;}
            }
            Equal(Hits.Single().Final,decimal.Floor(Base(nativeReservation?6m:9m)*.5m),"real cannon reservation applies Vigor before one native Soul factor: "+nativeReservation);
            if(nativeReservation)
            {
                Check(vigor.Amount==3&&Reserved(vigor),"cannon cannot steal Vigor reserved for the native owner attack");Hits.Clear();var native=await Command(Hero,6m,ValueProp.Move,card);attack.AddResultsInternal(native);await Hook.AfterAttack(Combat,new ThrowingPlayerChoiceContext(),attack);
                Equal(Hits.Single().Final,decimal.Floor(Base(9m)),"owner native attack retains its reserved Vigor coefficient after cannon action");
            }
            Check(Hero.GetPower<VigorPower>()==null||Hero.GetPower<VigorPower>()!.Amount==0,"existing native or cannon action completion consumes Vigor exactly once");
        }
        Reset();vigor=Power<VigorPower>(Hero,3);Localization();LocString text=vigor.SmartDescription;text.Add("OwnerName","测试角色");Invoke(AccessTools.Method(typeof(PowerModel),"AddDumbVariablesToDescription"),vigor,text,null);
        Check(text.GetFormattedText().Contains("50%")&&vigor.Amount==3,"owned Vigor3 native SmartFormat displays50percent skill without rewriting counter");
        Check(!ModelDb.Power<VigorPower>().Description.GetFormattedText().Contains("100%"),"canonical Vigor generic description stays native outside explicit card context");
        var patter=Owned<Patter>();Check(NativeTipTexts(patter).Any(value=>value.Contains("100%")),"real native HBR Patter HoverTips constructs generic Vigor six-to100percent explanation");
        H("Mode",false,1);Check(NativeTipTexts(patter).All(value=>!value.Contains("100%")),"following real legacy Patter HoverTips retains native Vigor explanation without cached HBR leak");
        Check(!ModelDb.Power<VigorPower>().Description.GetFormattedText().Contains("100%"),"card hover scope cleanup restores canonical native Vigor description");
        H("Mode",true,1);Check(NativeTipTexts(patter).Any(value=>value.Contains("100%")),"returning to HBR Patter HoverTips restores scoped skill explanation");
    }
    public static void Run(string reportPath)
    {
        if(System.Environment.GetEnvironmentVariable("HBR_NATIVE_HAND_TICK_ONLY")=="1")
        {RunNativeHandTick(reportPath);return;}
        bool ok=false;string? error=null;ReportDirectory=Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        try{Install();NativeKusarigamaCase().GetAwaiter().GetResult();NativeThornsCase().GetAwaiter().GetResult();NativeGalvanicCase().GetAwaiter().GetResult();PrimitiveCases().GetAwaiter().GetResult();ModifiersAndIsolationCases().GetAwaiter().GetResult();ResolvedContextCases().GetAwaiter().GetResult();NativeEventAdapterCases().GetAwaiter().GetResult();NativeStatusPotionAndHpOnlyCases().GetAwaiter().GetResult();DescriptionAndSourceCases();MiniatureCannonCases().GetAwaiter().GetResult();VigorSkillCases().GetAwaiter().GetResult();NativeRelicDamageCases().GetAwaiter().GetResult();NativeDamageOverTimeCases().GetAwaiter().GetResult();NativeEnemyDamageTextCases();SixSenseRemovalStructureCases();SixSenseRemovalCases().GetAwaiter().GetResult();SixSenseRemovalTextCases();SixSenseOverflowCases().GetAwaiter().GetResult();EatPassersbyCases().GetAwaiter().GetResult();EatPassersbyTextCases();TriggerTextCases().GetAwaiter().GetResult();DodgeCases().GetAwaiter().GetResult();DodgeTextCases();ManualGlossaryCases();SixSenseCardFaceCases();RetiredCharacterCases();FateDiceCases().GetAwaiter().GetResult();MagicSwordBalanceCases().GetAwaiter().GetResult();MagicSwordTurnEndCases();MagicSwordOverclockCases().GetAwaiter().GetResult();UnifiedRollModifierCases().GetAwaiter().GetResult();MoonlightReplayCases().GetAwaiter().GetResult();BeyondProtectionMergeCases().GetAwaiter().GetResult();SynergyCounterCases().GetAwaiter().GetResult();SakuraRainCases().GetAwaiter().GetResult();MagicSwordCrackCases().GetAwaiter().GetResult();SwordheartClarityCases().GetAwaiter().GetResult();MagicSwordCostDisplayCases().GetAwaiter().GetResult();FormTransitionFullEffectCases().GetAwaiter().GetResult();CrimsonFocusedShotCases().GetAwaiter().GetResult();ThreeCardBalanceCases().GetAwaiter().GetResult();CardTemplateBalanceCases().GetAwaiter().GetResult();DualSwordsChaseCases().GetAwaiter().GetResult();CrimsonStatInteractionCases().GetAwaiter().GetResult();UnifiedEffectDamageCases().GetAwaiter().GetResult();DesuwaCases().GetAwaiter().GetResult();ok=true;}
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            sourceNativeSha256=System.Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),telemetryIsolation=System.Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Production explicit effect declaration and real canonical numeric Damage Prefix/Postfix/Finalizer execute; lowest-priority terminal prefix captures only native HP command body and executes actual StrongestShield wrapper/full native Hook. Native Kusarigama three-attack handler, original typed DamageVar adapter and target RNG execute; both native Thorns handlers see original Unpowered flags. Synthetic HP results do not change HP/DP. Native formatting and source/consumer metadata execute; real Miniature Cannon hook runs unchanged at its native additive stage with passive call observation and actual listener enumeration. Context async/fault/nested/RNG boundaries tested; real Vigor native BeforeAttack/AfterAttack consumption and production cannon reservations execute, with PowerModel.Icon resource loading isolated for native Patter tooltip construction. No full AttackCommand.Execute body, full game, Godot rendering, save/clone suite, package or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" effect-skill checks");if(!ok)throw new Exception("Inspect "+reportPath);
    }
}
