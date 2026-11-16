using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static class OverchargeChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Assembly Mod = typeof(NoSelfInDreamPower).Assembly;
    static readonly List<string> Passed = [];
    sealed record Hit(Creature Target, decimal Amount, ValueProp Props, Creature? Dealer, CardModel? Source);
    static readonly List<Hit> Hits = [];
    static readonly List<Hit> RecipientHits = [];
    static Player Player => State<Player>("Player");
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static CombatState Combat => State<CombatState>("Combat");
    static Creature Ally = null!;
    static int Turn;
    static TaskCompletionSource? Gate;
    static bool DelayNext;
    static bool ResolveNativeBlock;
    static bool ResolveNativeModifiers, NestedNext, InNested, ThrowNext;
    static readonly List<(Creature Target, decimal Amount)> NestedAmounts=[];
    static Creature? KillAfterFirstRecipient;
    static T State<T>(string name) => (T)typeof(Checks).GetField(name,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    { try { return method.Invoke(owner,args); } catch(TargetInvocationException ex) when(ex.InnerException != null) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; } }
    static object? H(string method, params object?[] args) => Invoke(typeof(Checks).GetMethod(method,F)!,null,args);
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers."+name,true)!;
    static void Set(object owner, string name, object? value) => H("Set",owner,name,value);
    static T Blank<T>() => (T)Invoke(typeof(Checks).GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static T Power<T>(Creature owner, int amount) where T:PowerModel => (T)Invoke(typeof(Checks).GetMethod("Power",F)!.MakeGenericMethod(typeof(T)),null,owner,amount)!;
    static List<PowerModel> Powers(Creature owner) => (List<PowerModel>)H("Powers",owner)!;
    static void Check(bool value, string message) { if(!value) throw new Exception(message); Passed.Add(message); }
    static decimal Sum(Creature target) => Hits.Where(hit=>ReferenceEquals(hit.Target,target)).Sum(hit=>hit.Amount);
    static decimal Received(Creature target) => RecipientHits.Where(hit=>ReferenceEquals(hit.Target,target)).Sum(hit=>hit.Amount);
    static DamageResult Result(int total, Creature? receiver=null)
    { var result=Blank<DamageResult>();Set(result,"<Receiver>k__BackingField",receiver??Enemy);Set(result,"<UnblockedDamage>k__BackingField",total);return result; }
    static Task Deal(NoSelfInDreamPower power, int total, Creature? target=null, Creature? dealer=null, DamageResult? result=null) =>
        power.AfterDamageGiven(new ThrowingPlayerChoiceContext(),dealer??Hero,result??Result(total),ValueProp.Move,target??Enemy,null!);
    static bool Skip() => false;
    static bool False(ref bool __result) { __result=false;return false; }
    static decimal NativeModified(Creature target, decimal amount, bool actualWrapper)
    {
        Type type=actualWrapper ? Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.StrongestShieldDamagePatch",true)!
            : Mod.GetType("HBRGOSakiMod.Core.Compatibility.HookCompatibility",true)!;
        string name=actualWrapper?"ModifyDamageWithStrongestShield":"ModifyDamage";
        MethodInfo method=type.GetMethods(F).Where(method=>method.Name==name).OrderBy(method=>method.GetParameters().Length).First();
        object?[] args=[State<MegaCrit.Sts2.Core.Runs.RunState>("RunState"),Combat,target,null,amount,ValueProp.Unpowered,null,
            ModifyDamageHookType.All,CardPreviewMode.None,null];
        if(method.GetParameters().Length==11)args=[..args,null];
        return (decimal)Invoke(method,null,args)!;
    }
    static Task NativeCommand(Creature[] targets, decimal amount)
    {
        MethodInfo method=Mod.GetType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility",true)!.GetMethods(F)
            .Single(method=>method.Name=="Damage"&&method.GetParameters().Length==7&&method.GetParameters()[1].ParameterType==typeof(IEnumerable<Creature>));
        return (Task)Invoke(method,null,new ThrowingPlayerChoiceContext(),targets,amount,ValueProp.Unpowered,null,null,null)!;
    }
    static bool DamageLeaf(object?[] __args, ref Task<IEnumerable<DamageResult>> __result)
    {
        Type api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!;
        var targets=((IEnumerable<Creature>)Invoke(api.GetMethod("ReadTargets",F)!,null,new object?[]{__args})!).ToArray();
        decimal amount=(decimal)__args[2]!;var props=(ValueProp)__args[3]!;
        if(ThrowNext){ThrowNext=false;throw new InvalidOperationException("controlled command failure");}
        if(NestedNext)
        {
            NestedNext=false;InNested=true;
            try{NativeCommand(targets,amount).GetAwaiter().GetResult();}finally{InNested=false;}
        }
        foreach(Creature target in targets)
        {
            if(!target.IsAlive)continue;
            if(!InNested)Hits.Add(new(target,amount,props,__args[4] as Creature,__args[5] as CardModel));
            // Use the original profile's real hook and current mod powers,
            // including the production Phantom postfix; only the subsequent
            // scene/HP mutation terminal is controlled by this host.
            decimal unblocked=ResolveNativeModifiers?NativeModified(target,amount,true):amount;
            if(InNested)NestedAmounts.Add((target,unblocked));
            if(ResolveNativeBlock)
            {
                Invoke(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.PhantomDamageContext",true)!.GetMethod("Push",F)!,null,target);
                Invoke(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.RedCrimsonDamageContext",true)!.GetMethod("Set",F)!,null,target);
                unblocked=Math.Max(0m,amount-(target.PetOwner?.Creature??target).DamageBlockInternal(amount,props));
                Invoke(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.RedCrimsonDamageContext",true)!.GetMethod("Clear",F)!,null,target);
            }
            Creature recipient=Hook.ModifyUnblockedDamageTarget(Combat,target,unblocked,props,__args[4] as Creature);
            if(!InNested)RecipientHits.Add(new(recipient,unblocked,props,__args[4] as Creature,__args[5] as CardModel));
            if(KillAfterFirstRecipient is { } fallen)
            {KillAfterFirstRecipient=null;Set(fallen,"_currentHp",0);}
        }
        __result=DamageResultTask(targets,amount);return false;
    }
    static async Task<IEnumerable<DamageResult>> DamageResultTask(Creature[] targets, decimal amount)
    {
        if(DelayNext) { DelayNext=false;Gate=new(TaskCreationOptions.RunContinuationsAsynchronously);await Gate.Task; }
        return targets.Select(target=>Result((int)Math.Min(int.MaxValue,amount),target)).ToArray();
    }
    static void Register(Type type)
    { if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type)); }
    static void Session(bool od, Player? owner=null)
    {
        owner??=Player;
        if(od)
        {
            Type helper=Helper("OverclockImmediateTurnCoordinator"),type=helper.GetNestedType("Session",F)!;
            object session=Activator.CreateInstance(type,F,null,new object?[]{null,Combat,new[]{owner}},null)!;
            helper.GetField("_current",F)!.SetValue(null,session);
        }
        else
        {
            Type helper=Helper("PersonalImmediateTurnCoordinator"),type=helper.GetNestedType("Session",F)!;
            object session=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            Set(session,"Owner",owner);Set(session,"Combat",Combat);
            helper.GetField("_combat",F)!.SetValue(null,Combat);helper.GetField("_current",F)!.SetValue(null,session);
        }
    }
    static void ClearSessions()
    {foreach(string name in new[]{"OverclockImmediateTurnCoordinator","PersonalImmediateTurnCoordinator"})Helper(name).GetField("_current",F)!.SetValue(null,null);}
    static NoSelfInDreamPower Reset(int heroHp=5000, int allyHp=2000)
    {
        ClearSessions();Hits.Clear();RecipientHits.Clear();NestedAmounts.Clear();Gate=null;DelayNext=false;ResolveNativeBlock=false;
        ResolveNativeModifiers=NestedNext=InNested=ThrowNext=false;
        KillAfterFirstRecipient=null;
        foreach(Creature actor in State<List<Creature>>("Actors"))Powers(actor).Clear();
        Set(Combat,"_allies",new List<Creature>{Hero,Ally});
        Set(Hero,"_maxHp",heroHp);Set(Hero,"_currentHp",heroHp);Set(Ally,"_maxHp",allyHp);Set(Ally,"_currentHp",allyHp);
        Set(Hero,"_block",0);Set(Ally,"_block",0);
        Set(Combat,"<RoundNumber>k__BackingField",1);Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Player);
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",++Turn);
        return Power<NoSelfInDreamPower>(Hero,1);
    }
    static async Task ModifierCases()
    {
        var power=Reset(503,2000);Power<BreakPower>(Hero,1);
        var fragile=Power<FragilePower>(Hero,1);
        Invoke(typeof(FragilePower).GetMethod("ConfigureHbr",F)!,fragile,50m,1);
        Power<DestructionRatePower>(Hero,51);
        decimal amplified=NativeModified(Hero,125m,false);
        Check(amplified>125m,"real native Hook.ModifyDamage amplifies unpowered unattributed damage through current Fragile and DestructionRate powers");
        ResolveNativeModifiers=true;await Deal(power,200000);
        Check(Sum(Hero)==125m&&Received(Hero)==125m,
            "actual production damage wrapper clamps amplified backlash to floor twenty-five percent MaxHP after all native damage modifiers");
        Check(NativeModified(Hero,125m,true)==amplified,
            "completed backlash scope cannot clamp a later unrelated actual damage wrapper");

        power=Reset();Power<BreakPower>(Hero,1);fragile=Power<FragilePower>(Hero,1);
        Invoke(typeof(FragilePower).GetMethod("ConfigureHbr",F)!,fragile,50m,1);
        Power<DestructionRatePower>(Hero,51);amplified=NativeModified(Hero,100m,false);
        ResolveNativeModifiers=true;NestedNext=true;await Deal(power,10000);
        Check(NestedAmounts.Single(hit=>ReferenceEquals(hit.Target,Hero)).Amount==amplified&&amplified>100m,
            "nested native damage command with identical targets amount and props cannot claim or inherit the outer backlash receipt");
        Check(Received(Hero)==100m,
            "outer backlash scope resumes its own reserved limit after the nested unrelated native command");
        Check(NativeModified(Hero,100m,true)==amplified,
            "nested command and outer completion leave no ambient limit on later damage");

        power=Reset();Power<BreakPower>(Hero,1);fragile=Power<FragilePower>(Hero,1);
        Invoke(typeof(FragilePower).GetMethod("ConfigureHbr",F)!,fragile,50m,1);
        ResolveNativeModifiers=true;ThrowNext=true;bool failed=false;
        try{await Deal(power,10000);}catch(InvalidOperationException ex)when(ex.Message=="controlled command failure"){failed=true;}
        Check(failed&&NativeModified(Hero,100m,true)==NativeModified(Hero,100m,false),
            "canonical command failure closes the backlash receipt and restores caller scope before later damage");
    }
    static Creature Pet<T>(int hp) where T:MonsterModel
    {
        var pet=(Creature)H("Actor",Blank<T>(),null,Player)!;
        Set(pet,"_maxHp",hp);Set(pet,"_currentHp",hp);
        Set(Combat,"_allies",Combat.Allies.Append(pet).ToList());return pet;
    }
    static async Task RecipientCases()
    {
        var power=Reset();var phantom=Pet<SmallHopper>(1000);Power<PhantomPower>(phantom,1);
        await Deal(power,200000);
        Check(Sum(Hero)==0m&&Sum(phantom)==250m&&Received(phantom)==250m,
            "real Phantom target projection deduplicates player and phantom before unequal MaxHP amounts split into commands");
        Check(Received(Ally)==500m,"Phantom deduplication retains an unrelated ally's independent cap");
        int count=Hits.Count;await Deal(power,200000);
        Check(count==Hits.Count,"the same Phantom recipient cannot be charged again after exhausting its real MaxHP cap");

        power=Reset();var red=Pet<RedCrimson>(1000);Power<DamageTransferPower>(Hero,1);
        await Deal(power,200000);
        Check(Sum(Hero)==250m&&Sum(red)==0m&&Received(red)==250m&&Received(Hero)==0m,
            "real DamageTransfer hook shares Red Crimson's cap across player redirect and pet's own allied hit while preserving physical player target");
        Check(Received(Ally)==500m,"Red Crimson redirect retains unrelated ally damage");
        count=Hits.Count;await Deal(power,200000);
        Check(count==Hits.Count,"exhausted Red Crimson redirect cannot receive another player's backlash hit");

        power=Reset();var feint=Pet<SmallHopper>(1000);Power<FeintDamageRedirectPower>(feint,1);
        await Deal(power,200000);
        Check(Received(feint)==250m&&Received(Hero)==0m&&Received(Ally)==0m,
            "real Feint global native redirect shares one recipient cap across every allied physical target");
        Check(Sum(Hero)==250m&&Sum(Ally)==0m&&Sum(feint)==0m,
            "Feint cap reservation preserves native physical target instead of pre-routing block and DP");
        count=Hits.Count;await Deal(power,200000);
        Check(count==Hits.Count,"later Feint redirects cannot exceed the recipient's cumulative turn cap");

        power=Reset();phantom=Pet<SmallHopper>(1000);Power<PhantomPower>(phantom,1);
        feint=Pet<SmallHopper>(1000);Power<FeintDamageRedirectPower>(feint,1);
        await Deal(power,200000);
        Check(Received(phantom)==250m&&Received(feint)==250m,
            "production Phantom native-hook postfix remains authoritative when global Feint redirect is also present");

        power=Reset();red=Pet<RedCrimson>(1000);Power<DamageTransferPower>(Hero,1);
        feint=Pet<SmallHopper>(600);Power<FeintDamageRedirectPower>(feint,1);
        await Deal(power,200000);
        Check(Received(feint)==150m&&Received(red)==0m&&Sum(Hero)==150m,
            "real native sequential DamageTransfer then Feint hook chain reserves the final recipient's lower MaxHP cap");

        power=Reset();red=Pet<RedCrimson>(200);Power<DamageTransferPower>(Hero,1);
        feint=Pet<SmallHopper>(1000);Power<FeintDamageRedirectPower>(feint,1);
        Set(red,"_block",1000);ResolveNativeBlock=true;
        await Deal(power,10000);await Deal(power,10000);await Deal(power,10000);
        Check(red.Block==950&&Sum(Hero)==50m,
            "real native DamageBlockInternal and production Red redirect preserve block routing and cap intermediate Red recipient at fifty before Feint HP redirection");
        Check(Received(feint)==200m,
            "blocked Red damage does not refund reserved Feint budget and unrelated allied damage keeps native Feint routing");
        ResolveNativeBlock=false;

        power=Reset();var firstFeint=Pet<SmallHopper>(200);Power<FeintDamageRedirectPower>(firstFeint,1);
        var lastFeint=Pet<SmallHopper>(400);Power<FeintDamageRedirectPower>(lastFeint,1);
        KillAfterFirstRecipient=lastFeint;await Deal(power,3000);
        Check(Received(lastFeint)==30m&&Received(firstFeint)==20m&&Received(firstFeint)<=50m,
            $"real native Feint hook reselects a living earlier owner after controlled final-owner death without exceeding fallback cap (last={Received(lastFeint)}, fallback={Received(firstFeint)}, cap=50)");

        power=Reset();var firstRed=Pet<RedCrimson>(400);var fallbackRed=Pet<RedCrimson>(200);
        var petOne=Pet<SmallHopper>(2000);var petTwo=Pet<SmallHopper>(2000);
        Power<DamageTransferPower>(Hero,1);
        Set(Combat,"_allies",new List<Creature>{Hero,Ally,petOne,petTwo,firstRed,fallbackRed});
        Set(fallbackRed,"_block",1000);ResolveNativeBlock=true;KillAfterFirstRecipient=firstRed;
        await Deal(power,3000);
        Check(Received(firstRed)==30m&&fallbackRed.Block==980&&Sum(petOne)==20m&&Sum(petTwo)==0m,
            $"real native Red block redirect reselects the next living owned Red after controlled first-Red death and caps shared fallback block budget (first={Received(firstRed)}, fallbackBlockSpent={1000-fallbackRed.Block}, cap=50)");
        ResolveNativeBlock=false;
    }
    static async Task Cases()
    {
        var power=Reset();
        Check(Combat.GetTeammatesOf(Hero).Contains(Hero),"native teammate lookup includes the holder");
        await Deal(power,10000);
        Check(Sum(Hero)==100m&&Sum(Ally)==100m,"one percent of actual enemy damage goes to each live ally");
        await Deal(power,200000);
        Check(Sum(Hero)==1250m&&Sum(Ally)==500m,"per-victim accumulated backlash caps independently at twenty-five percent MaxHP");
        int count=Hits.Count;await Deal(power,int.MaxValue);
        Check(Hits.Count==count,"later hits and very large AoE damage cannot pass an exhausted turn budget");
        Check(Hits.All(hit=>hit.Props==ValueProp.Unpowered&&hit.Dealer==null&&hit.Source==null),"backlash keeps unpowered unattributed damage with no skill, dice, crit or actor attribute declaration");

        power=Reset(503,3);await Deal(power,99);
        Check(Hits.Count==0,"sub-one-point backlash floors to zero without spending budget");
        await Deal(power,100000);
        Check(Sum(Hero)==125m&&Sum(Ally)==0m,"fractional MaxHP cap floors down and a sub-four HP ally receives zero damage");

        power=Reset();var original=Result(10000);await Deal(power,10000,result:original);await Deal(power,10000,result:original);
        Check(Sum(Hero)==100m&&Sum(Ally)==100m,"the same DamageResult hook receipt is idempotent");
        Powers(Hero).Remove(power);power=Power<NoSelfInDreamPower>(Hero,1);await Deal(power,200000);
        Check(Sum(Hero)==1250m&&Sum(Ally)==500m,"removing and reapplying the power retains the same turn's spent budget");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",++Turn);await Deal(power,10000);
        Check(Sum(Hero)==1350m&&Sum(Ally)==600m,"the next native player turn receives fresh independent budgets");

        foreach(bool od in new[]{false,true})
        {
            power=Reset();await Deal(power,10000);Session(od);await Deal(power,200000);
            Check(Sum(Hero)==1350m&&Sum(Ally)==600m,(od?"OD":"personal")+" inserted turn owns a distinct damage budget");
            ClearSessions();await Deal(power,200000);
            Check(Sum(Hero)==2500m&&Sum(Ally)==1000m,(od?"OD":"personal")+" restoration resumes only the original ordinary turn's remaining budget");
            count=Hits.Count;await Deal(power,10000);Check(count==Hits.Count,"ordinary budget stays exhausted after the inserted turn");
        }

        power=Reset();await Deal(power,200000);Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Enemy);await Deal(power,200000);
        Check(Sum(Hero)==2500m&&Sum(Ally)==1000m,"enemy-side triggered outgoing damage uses that side turn's fresh budget");
        Set(Combat,"<RoundNumber>k__BackingField",2);Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Player);await Deal(power,10000);
        Check(Sum(Hero)==2600m&&Sum(Ally)==1100m,"a new round cannot reuse a prior turn's budget even before native counter update");

        power=Reset();DelayNext=true;Task first=Deal(power,200000);
        Check(Gate!=null&&!first.IsCompleted,"controlled damage leaf yields before completing the first target group");
        await Deal(power,200000);Gate!.SetResult();await first;
        Check(Sum(Hero)==1250m&&Sum(Ally)==500m,"all target budgets are reserved before awaiting so reentrant hits cannot exceed a cap");

        power=Reset();Power<CrimsonBladeFlowPower>(Hero,1);await Deal(power,10000);
        Check(Hits.Count==0,"Crimson Blade Flow still suppresses all allied backlash");
        Powers(Hero).RemoveAll(item=>item is CrimsonBladeFlowPower);await Deal(power,10000);
        Check(Sum(Hero)==100m&&Sum(Ally)==100m,"a Crimson-exempt hit does not spend the backlash budget");
        power=Reset();await Deal(power,10000,target:Ally);await Deal(power,10000,dealer:Ally);
        Check(Hits.Count==0,"allied targets and another dealer cannot cause or spend this holder's backlash");
        Set(power,"_amount",0);await Deal(power,10000);Check(Hits.Count==0,"expired ability cannot emit backlash");

        power=Reset();Set(Ally,"_currentHp",0);await Deal(power,10000);
        Check(Sum(Hero)==100m&&Sum(Ally)==0m,"dead teammates are excluded from backlash");
        power=Reset();Set(Hero,"_block",99999);await Deal(power,200000);count=Hits.Count;await Deal(power,10000);
        Check(Hits.Count==count&&Sum(Hero)==1250m,"submitted backlash spends cap before later block, DP or mitigation rather than refunding a reentry loophole");

        power=Reset();var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;
        Power<PhantomPower>(phantom,1);
        var large=Power<LargeHealthPower>(phantom,1);large.CurrentHpData="10000000000";large.MaxHpData="10000000000";
        var hits=(Array)Invoke(Helper("NoSelfInDreamBacklashHelper").GetMethod("Reserve",F)!,null,Hero,Result(1),3000000000m,new[]{phantom})!;
        decimal amount=(decimal)hits.GetValue(0)!.GetType().GetProperty("Amount",F)!.GetValue(hits.GetValue(0))!;
        Check(amount==2500000000m,"cap uses exact LargeHealth maximum rather than the native Int32 projection");
        await RecipientCases();
        await ModifierCases();
    }
    public static void Run(string reportPath)
    {
        bool ok=false;string? error=null;
        try
        {
            H("Setup");Set(Player,"<PlayerCombatState>k__BackingField",Blank<PlayerCombatState>());
            var allyPlayer=Blank<Player>();Set(allyPlayer,"_runState",State<MegaCrit.Sts2.Core.Runs.RunState>("RunState"));
            Set(allyPlayer,"_relics",new List<RelicModel>());
            Ally=(Creature)H("Actor",null,allyPlayer,null)!;Set(allyPlayer,"<Creature>k__BackingField",Ally);
            Set(Combat,"_allies",new List<Creature>{Hero,Ally});
            foreach(Type type in new[]{typeof(NoSelfInDreamPower),typeof(CrimsonBladeFlowPower),typeof(LargeHealthPower),typeof(PhantomPower),typeof(DamageTransferPower),typeof(FeintDamageRedirectPower),typeof(FragilePower),typeof(BreakPower),typeof(DestructionRatePower)})Register(type);
            var harmony=new Harmony("hbr.no-self.turn-cap");
            foreach(MethodInfo flash in typeof(PowerModel).GetMethods(F).Where(method=>method.Name=="Flash"))harmony.Patch(flash,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(Skip),F)!));
            harmony.Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(False),F)!));
            foreach(string name in new[]{"PhantomDamageTargetPatch","PhantomUnblockedDamageTargetPatch","CreatureDamageBlockInternalPatch","NoSelfInDreamBacklashContextPatch","StrongestShieldDamagePatch"})
                Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,
                    "installs production "+name);
            Type api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!;
            MethodBase moveNext=(MethodBase)api.GetField("MoveNext",F)!.GetValue(null)!;
            Check(PatchProcessor.GetOriginalInstructions(moveNext).Count(instruction=>instruction.operand is MethodInfo called&&called.DeclaringType==typeof(Hook)&&called.Name=="ModifyDamage")==1,
                "actual native canonical Damage MoveNext has one ModifyDamage callsite per target so the post-modifier receipt is consumed once");
            harmony.Patch((MethodInfo)api.GetField("Command",F)!.GetValue(null)!,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(DamageLeaf),F)!){priority=Priority.Last});
            Cases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        finally{ClearSessions();}
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary="Real compiled NoSelfInDreamPower.AfterDamageGiven, production budget/GetTurnToken/Phantom target patch and native Hook.ModifyUnblockedDamageTarget with current mod Phantom, DamageTransfer and Feint powers; native models/GetTeammatesOf use original profile. Combined Red/Feint case executes real native DamageBlockInternal with production block redirect and contexts. Modifier cases execute full native Hook.ModifyDamage with real Fragile/DestructionRate and production post-modifier wrapper limiter, including identical nested native command and exception cleanup. Godot Flash/logger and final damage terminal are controlled; recipient submissions are recorded after real native hooks, not HP mutations. DP absorption and final HP loss are outside this fixture. Native extra-turn entry/exit sessions are represented by real session objects, not a live network queue. No Godot renderer/full combat."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" No Self turn cap checks");if(!ok)throw new Exception("Inspect "+reportPath);
    }
}
