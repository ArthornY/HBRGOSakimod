using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class OverchargeChecks {public static void Run(string path)=>FallingChecks.Run(path);}
static class FallingChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(FallingContinuallyPower).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    sealed record Command(string Kind,PowerModel Power,Creature Target,decimal Amount,Creature? Applier,CardModel? Card);
    static readonly List<Command> Commands=[];
    static int Attacks;
    static int BlockHookCalls;
    static readonly List<decimal> NativeChanges=[];
    static Creature Hero=>State<Creature>("Hero");
    static Creature Enemy=>State<Creature>("Enemy");
    static Player Player=>State<Player>("Player");
    static RunState RunState=>State<RunState>("RunState");
    static CombatState Combat=>State<CombatState>("Combat");
    static T State<T>(string name)=>(T)typeof(Checks).GetField(name,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method,object? owner,params object?[] args){try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? H(string method,params object?[] args)=>Invoke(typeof(Checks).GetMethod(method,F)!,null,args);
    static object? Call(string helper,string method,params object?[] args)=>Invoke(Mod.GetType("HBRGOSakiMod.Core.Helpers."+helper,true)!.GetMethod(method,F)!,null,args);
    static T Blank<T>()=>(T)Invoke(typeof(Checks).GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static T Power<T>(Creature creature,int amount=1)where T:PowerModel=>(T)Invoke(typeof(Checks).GetMethod("Power",F)!.MakeGenericMethod(typeof(T)),null,creature,amount)!;
    static void Set(object obj,string field,object? value)=>H("Set",obj,field,value);
    static void Sense(string name,int amount)=>H("Sense",name,amount);
    static void Mode(bool enabled)=>H("Mode",enabled,1);
    static void Reset(){H("Reset");Set(Hero,"_currentHp",5000);Set(Enemy,"_currentHp",5000);Set(RunState,"<ActFloor>k__BackingField",1);Commands.Clear();NativeChanges.Clear();Attacks=0;}
    static T Card<T>(bool upgraded=false)where T:CardModel,new(){var card=(T)Invoke(typeof(Checks).GetMethod("OwnedCard",F)!.MakeGenericMethod(typeof(T)),null)!;if(upgraded)card.UpgradeInternal();return card;}
    static void Play(CardModel card,Creature? target=null)=>H("Play",card,target??Enemy);
    static FallingContinuallyPower? Active(Creature? creature=null)=>(creature??Enemy).GetPower<FallingContinuallyPower>();
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);Passed.Add(label);}
    static void Equal(decimal actual,decimal expected,string label)=>Check(Math.Abs(actual-expected)<.00000000000000000001m,$"{label}: {actual} = {expected}");
    static decimal Potency(decimal basis,Creature? applier=null,Creature? target=null)=>(decimal)Call("FallingContinuallyHelper","ResolveAmount",basis,applier??Hero,target??Enemy)!;
    static decimal Expected(decimal raw,decimal intelligence,decimal luck,decimal enemy)=>raw*100m*(1m+Math.Max(0m,(2m*intelligence+luck)/3m-enemy)*.001m);
    static void Boost(){Sense("Talent",75);Power<DexterityPower>(Hero,300);}
    static bool SkipAttack(AttackCommand __instance,ref Task<AttackCommand> __result){Attacks++;__result=Task.FromResult(__instance);return false;}
    static bool Wait(ref Task __result){__result=Task.CompletedTask;return false;}
    static void ObserveApply(object[] __args){if(__args[1] is FallingContinuallyPower or DoomPower)Commands.Add(new("apply",(PowerModel)__args[1],(Creature)__args[2],(decimal)__args[3],(Creature?)__args[4],(CardModel?)__args[5]));}
    static void ObserveModify(object[] __args){if(__args[1] is FallingContinuallyPower or DoomPower)Commands.Add(new("modify",(PowerModel)__args[1],((PowerModel)__args[1]).Owner,(decimal)__args[2],(Creature?)__args[3],(CardModel?)__args[4]));}
    sealed class GivenPower:PowerModel
    {
        public override PowerType Type=>PowerType.Buff;
        public override PowerStackType StackType=>PowerStackType.Counter;
        public int Callbacks;
        public decimal Bonus=.75m,Factor=2m;
        public override decimal ModifyPowerAmountGivenAdditive(PowerModel power,Creature giver,decimal amount,Creature? target,CardModel? card)=>power is FallingContinuallyPower&&giver==Owner ? Bonus : 0m;
        public override decimal ModifyPowerAmountGivenMultiplicative(PowerModel power,Creature giver,decimal amount,Creature? target,CardModel? card)=>power is FallingContinuallyPower&&giver==Owner?Factor:1m;
        public override Task AfterModifyingPowerAmountGiven(PowerModel power){if(power is FallingContinuallyPower)Callbacks++;return Task.CompletedTask;}
    }
    static void ObserveAfterChange(object[] __args){if(__args[2] is FallingContinuallyPower)NativeChanges.Add((decimal)__args[3]);}
    sealed class ScaleReceivedPower:PowerModel
    {
        public override PowerType Type=>PowerType.Buff;
        public override PowerStackType StackType=>PowerStackType.Counter;
        public decimal Factor;
        public int Callbacks;
        public override bool TryModifyPowerAmountReceived(PowerModel power,Creature target,decimal amount,Creature? applier,out decimal modifiedAmount)
        {bool applies=power is FallingContinuallyPower&&target==Owner;modifiedAmount=applies?amount*Factor:amount;return applies;}
        public override Task AfterModifyingPowerAmountReceived(PowerModel power){if(power is FallingContinuallyPower)Callbacks++;return Task.CompletedTask;}
    }
    sealed class SuppressPower:PowerModel
    {
        public override PowerType Type=>PowerType.Buff;
        public override PowerStackType StackType=>PowerStackType.Counter;
        public int Calls,Callbacks;
        public override bool TryModifyPowerAmountReceived(PowerModel power,Creature target,decimal amount,Creature? applier,out decimal modifiedAmount)
        {bool applies=power is FallingContinuallyPower&&target==Owner;modifiedAmount=applies?0m:amount;if(applies)Calls++;return applies;}
        public override Task AfterModifyingPowerAmountReceived(PowerModel power){if(power is FallingContinuallyPower)Callbacks++;return Task.CompletedTask;}
    }
    static void ObserveBlock()=>BlockHookCalls++;
    static void Install()
    {
        H("Setup");var harmony=new Harmony("hbr.falling-continually.checks");
        Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.IntegerStatAmountPatch",true)!).Patch().Count==2,"installs actual native initial and stack integer normalization boundaries");
        Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch",true)!).Patch().Count>0,"installs actual card-description boundary");
        foreach(Type type in new[]{typeof(FallingContinuallyPower),typeof(DoomPower),typeof(ArtifactPower)}){ModelDb.Inject(type);Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
        harmony.Patch(typeof(AttackCommand).GetMethod("Execute",F)!,prefix:new HarmonyMethod(typeof(FallingChecks).GetMethod(nameof(SkipAttack),F)!));
        harmony.Patch(typeof(Cmd).GetMethod("CustomScaledWait",F)!,prefix:new HarmonyMethod(typeof(FallingChecks).GetMethod(nameof(Wait),F)!));
        MethodInfo apply=typeof(PowerCmd).GetMethods(F).Single(m=>m.Name=="Apply"&&!m.IsGenericMethod&&m.GetParameters().Length==7);
        harmony.Patch(apply,prefix:new HarmonyMethod(typeof(FallingChecks).GetMethod(nameof(ObserveApply),F)!));harmony.Patch(typeof(PowerCmd).GetMethod("ModifyAmount",F)!,prefix:new HarmonyMethod(typeof(FallingChecks).GetMethod(nameof(ObserveModify),F)!));
        harmony.Patch(typeof(Hook).GetMethod("AfterPowerAmountChanged",F)!,prefix:new HarmonyMethod(typeof(FallingChecks).GetMethod(nameof(ObserveAfterChange),F)!));
        harmony.Patch(typeof(Hook).GetMethod("ModifyBlock",F)!,prefix:new HarmonyMethod(typeof(FallingChecks).GetMethod(nameof(ObserveBlock),F)!));
        InitializeLocalization();
    }
    static string Root {get{for(DirectoryInfo? folder=new(AppContext.BaseDirectory);folder!=null;folder=folder.Parent)if(File.Exists(Path.Combine(folder.FullName,"HBRGOSakiMod.csproj")))return folder.FullName;throw new DirectoryNotFoundException();}}
    static void InitializeLocalization()
    {
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        var tables=new Dictionary<string,LocTable>();foreach(string name in new[]{"cards","powers","card_keywords"}){var text=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("D:/stsmod/sts/localization/zhs/"+name+".json"))!;foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"HBRGOSakiMod/localization/zhs",name+".json")))!)text[pair.Key]=pair.Value;tables[name]=new LocTable(name,text,null!);}Set(manager,"_tables",tables);typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
    }
    static (decimal? Display,string Text) Render(CardModel card,Creature? target,bool unpowered=false)
    {
        using var scope=(IDisposable)Invoke(Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrCardDescriptionContext",true)!.GetMethods(F).Single(m=>m.Name=="Begin"&&m.GetParameters().Length==5),null,card,false,false,target,unpowered)!;
        LocString text=card.Description;card.DynamicVars.AddTo(text);AccessTools.Method(card.GetType(),"AddExtraArgsToDescription").Invoke(card,[text]);
        text.Add(new IfUpgradedVar(card.IsUpgraded?UpgradeDisplay.Upgraded:UpgradeDisplay.Normal));text.Add("OnTable",false);text.Add("InCombat",false);text.Add("IsTargeting",false);text.Add("TargetType",card.TargetType.ToString());text.Add("GainsBlock",card.GainsBlock);text.Add("IsOstyAlive",false);text.Add("energyPrefix","energy");text.Add("singleStarIcon","[img]star.png[/img]");
        decimal? display=text.Variables.TryGetValue("FallingContinuallyPowerDisplay",out object? value)?decimal.Parse(Regex.Replace((string)value!,"\\[/?(?:green|red)\\]",""),CultureInfo.InvariantCulture):null;
        return(display,text.GetFormattedText());
    }
    static void NativeCards()
    {
        Reset();var falling=Card<FallingContinuallyHbrgoSaki>();Play(falling);
        Cases.Add(new{label="actual Falling OnPlay INT100 LUCK100 E150",raw=falling.DynamicVars["FallingContinuallyPower"].BaseValue,nativeCommand=Commands.Single().Amount,stored=Active()?.Amount});
        Equal(Active()!.Amount,300,"real Falling OnPlay first apply uses raw3 times100 at baseline stats");
        Equal(Commands.Single().Amount,300,"real Falling OnPlay sends resolved base300 to native application");
        Check(Commands[0].Kind=="apply"&&Commands[0].Target==Enemy&&Commands[0].Applier==Hero&&ReferenceEquals(Commands[0].Card,falling)&&Active()!.Applier==Hero,"native first application preserves target applier source and actual owner");
        var first=Active()!;Play(falling);Equal(first.Amount,600,"repeated real card stacks two native300 grants as600");Check(ReferenceEquals(first,Active())&&Enemy.Powers.OfType<FallingContinuallyPower>().Count()==1&&Commands.Last().Kind=="modify","native generic Apply keeps ordinary single-instance counter stacking");
        foreach(bool boosted in new[]{false,true})foreach(bool upgraded in new[]{false,true})foreach(bool death in new[]{false,true})
        {
            Reset();if(boosted)Boost();CardModel card=death?Card<DeathToYouHbrgoSaki>(upgraded):Card<FallingContinuallyHbrgoSaki>(upgraded);decimal raw=death?(upgraded?6:4):(upgraded?4:3);decimal expected=Expected(raw,boosted?1000:100,100,150);
            Equal(card.DynamicVars["FallingContinuallyPower"].BaseValue,raw,"authored base and native upgrade remain "+card.GetType().Name+" upgraded="+upgraded+" boosted="+boosted);
            var preview=Render(card,Enemy);Play(card);Equal(Active()!.Amount,decimal.Floor(expected),"real card stores base100 plus debuff-curve layers "+card.GetType().Name+" upgraded="+upgraded+" boosted="+boosted);Equal(preview.Display!.Value,Active()!.Amount,"target preview matches stored count "+card.GetType().Name+" upgraded="+upgraded+" boosted="+boosted);Equal(card.DynamicVars["FallingContinuallyPower"].BaseValue,raw,"preview and play preserve raw storage "+card.GetType().Name+" upgraded="+upgraded+" boosted="+boosted);Check(!preview.Text.Contains('{')&&preview.Text.Contains("智力")&&preview.Text.Contains("幸运"),"real SmartFormat resolves final intelligence and luck rule "+card.GetType().Name+" upgraded="+upgraded+" boosted="+boosted);Equal(Attacks,death?1:0,"Death preceding attack remains separate from Falling application");
            Cases.Add(new{label=card.GetType().Name,upgraded,boosted,raw,resolved=Commands.Single(c=>c.Power is FallingContinuallyPower).Amount,stored=Active()!.Amount,preview=preview.Display});
        }
        Reset();Sense("Talent",75);Power<DexterityPower>(Hero,169);falling=Card<FallingContinuallyHbrgoSaki>();Play(falling);Equal(Commands.Single().Amount,399.5m,"debuff curve retains fractional potency399.5 until native power modifiers");Play(falling);Equal(Active()!.Amount,798,"two fractional applications store399 each rather than floor of aggregate799");
        Reset();Set(Enemy,"_currentHp",0);Play(Card<DeathToYouHbrgoSaki>());Check(Active()==null&&Attacks==1,"real Death skips Falling application when attack target is dead");
    }
    static void FormulaAndLiveCases()
    {
        Reset();Equal(Potency(3),300,"default INT100 LUCK100 E150 keeps base raw3 times100");Equal(Potency(4),400,"default raw4 keeps base400");Power<DexterityPower>(Hero,50);Equal(Potency(3),300,"INT150 below weighted-E threshold does not use superseded raw times INT block rule");
        Reset();Power<HbrCalamityPower>(Enemy,150);Equal(Potency(3),330,"live target E reduction exposes default weighted-stat potency");Sense("Talent",75);Equal(Potency(3),360,"INT increases potency with two-thirds weight");Sense("Talent",0);Sense("Simplicity",75);Equal(Potency(3),345,"LUCK increases potency with one-third weight");var luckCard=Card<FallingContinuallyHbrgoSaki>();Equal(Render(luckCard,Enemy).Display!.Value,345,"target preview reflects LUCK-only potency growth");Play(luckCard);Equal(Active()!.Amount,345,"real OnPlay applies LUCK-only growth to actual native stored counter");Equal(Potency(0),0,"zero raw basis produces zero potency");
        Reset();Sense("Talent",75);Power<DexterityPower>(Hero,169);var card=Card<FallingContinuallyHbrgoSaki>();Equal(Potency(3),399.5m,"potency immediately below integer boundary retains fractional precision");Equal(Render(card,Enemy).Display!.Value,399,"preview below boundary floors399.5 to399");Play(card);Equal(Active()!.Amount,399,"native initial amount below boundary floors399.5 to399");
        Reset();Sense("Talent",75);Power<DexterityPower>(Hero,170);card=Card<FallingContinuallyHbrgoSaki>();Equal(Potency(3),400,"INT675 LUCK100 E150 exact boundary potency becomes400");Equal(Render(card,Enemy).Display!.Value,400,"preview exact boundary becomes400");Play(card);Equal(Active()!.Amount,400,"native exact boundary stores400");
        Set(RunState,"<ActFloor>k__BackingField",2);Equal(Potency(3),398.2m,"same card reads live changed target E instead of prior application snapshot");Equal(Render(card,Enemy).Display!.Value,398,"same card target preview responds to live E");Play(card);Equal(Active()!.Amount,798,"later application uses live E while preserving prior400 stored layers");
        Reset();Boost();card=Card<FallingContinuallyHbrgoSaki>();Play(card);var stored=Active()!;Sense("Talent",0);Hero.GetPower<DexterityPower>()!.SetAmount(0,true);Equal(stored.Amount,465,"changing current applier attributes never grows or shrinks stored Falling amount");Equal(Render(card,Enemy).Display!.Value,300,"same owned card preview reads current applier stats");Play(card);Equal(stored.Amount,765,"later application reads current stats while native old counter remains465");
        Reset();Boost();card=Card<FallingContinuallyHbrgoSaki>();Equal(Render(card,null).Display!.Value,300,"untargeted owned card preview uses raw times100 reference without inventing target E");Equal(Render(card,Enemy,true).Display!.Value,300,"explicit unpowered preview uses raw times100 reference");var canonical=new FallingContinuallyHbrgoSaki();Equal(Render(canonical,Enemy).Display!.Value,300,"canonical card cannot borrow current owner's live stats");Check(!(bool)Call("HbrPercentDebuffHelper","Supports",ModelDb.Power<FallingContinuallyPower>())!,"Falling counter never joins independent percent-duration debuff models");
        Equal((decimal)Call("HbrPercentDebuffHelper","EvaluateBasePotency",3000m,decimal.MaxValue,decimal.MaxValue,0m)!,decimal.MaxValue,"extreme raw basis and stats use overflow fallback and saturate safely");
        Equal((decimal)Call("HbrPercentDebuffHelper","EvaluateBasePotency",decimal.MaxValue,decimal.MaxValue,decimal.MaxValue,0m)!,decimal.MaxValue,"maximal basis plus maximal stats saturates without decimal exception");
        Equal((decimal)Call("HbrPercentDebuffHelper","EvaluateBasePotency",300m,decimal.MaxValue,decimal.MaxValue,decimal.MaxValue)!,300m,"extreme equal weighted stats and target E preserve base via safe fallback");
        Equal((decimal)Call("HbrPercentDebuffHelper","EvaluateBasePotency",0m,decimal.MaxValue,decimal.MaxValue,0m)!,0m,"zero basis remains zero under maximal stats");
        Equal(BlockHookCalls,0,"Falling layer calculation never invokes actual native block modifier Hook");Equal(Hero.Block,0,"applying Falling grants no real block to caster");
    }
    static void NativeHooksAndLegacy()
    {
        Reset();Boost();var given=Power<GivenPower>(Hero);given.Bonus=.75m;given.Factor=2m;var card=Card<FallingContinuallyHbrgoSaki>();Equal(Render(card,Enemy).Display!.Value,931,"preview applies native Given additive and multiplicative hooks after resolved465 potency");Play(card);Equal(Active()!.Amount,931,"native application matches floor((465+0.75)*2) rather than applying attributes after hooks");Check(given.Callbacks>0,"native Given modifier notification still runs after actual application");Equal(card.DynamicVars["FallingContinuallyPower"].BaseValue,3,"native Given hooks do not alter authored card storage");
        Reset();Boost();var artifact=Power<ArtifactPower>(Enemy);Play(Card<FallingContinuallyHbrgoSaki>());Check(Active()==null&&artifact.Amount==0,"actual native Artifact blocks resolved Falling and consumes one charge");
        Reset();Boost();var suppress=Power<SuppressPower>(Enemy);Play(Card<DeathToYouHbrgoSaki>());Check(Active()==null&&suppress.Calls==1&&suppress.Callbacks==1,"ordinary native received suppression hook and callback remain intact");
        foreach(bool death in new[]{false,true})foreach(bool upgraded in new[]{false,true})
        {
            Reset();Boost();Mode(false);CardModel legacy=death?Card<DeathToYouHbrgoSaki>(upgraded):Card<FallingContinuallyHbrgoSaki>(upgraded);decimal raw=legacy.DynamicVars["FallingContinuallyPower"].BaseValue;Play(legacy);Equal(Active()!.Amount,raw,"non-HBR real OnPlay keeps authored count "+legacy.GetType().Name+" upgraded="+upgraded);Check(Render(legacy,Enemy).Display==null,"legacy description uses original native power variable");Equal(Potency(raw),raw,"non-HBR helper preserves supplied raw basis without hidden scale or growth");
        }
    }
    static void NativeCapCases()
    {
        const decimal cap=999999999m;
        Reset();H("Mode",true,int.MaxValue);Power<DexterityPower>(Hero,999999999);var card=Card<FallingContinuallyHbrgoSaki>();decimal resolved=Potency(3);Check(resolved>int.MaxValue,"legitimate high level and native Dexterity produce potency above Int32 range");Equal(Render(card,Enemy).Display!.Value,cap,"large target preview saturates at actual native counter cap");Play(card);Equal(Active()!.Amount,cap,"real native first apply caps after huge stat calculation without Int32 overflow");Equal(NativeChanges.Single(),cap,"native first-apply amount-changed notification receives normalized integer cap");Check(Commands.Single().Amount==resolved,"high-stat card retains full decimal potency until actual command boundary");
        Reset();card=Card<FallingContinuallyHbrgoSaki>();Play(card);var existing=Active()!;H("Mode",true,int.MaxValue);Power<DexterityPower>(Hero,999999999);Play(card);Equal(existing.Amount,cap,"native existing stack caps oversized offset and total without overflow");Check(ReferenceEquals(existing,Active())&&Commands.Last().Kind=="modify","huge second application preserves real native stacking route and instance");Equal(NativeChanges.Last(),cap,"native stack amount-changed notification sees bounded integer offset");
        Reset();H("Mode",true,int.MaxValue);Power<DexterityPower>(Hero,999999999);var given=Power<GivenPower>(Hero);given.Bonus=0;given.Factor=.00000001m;card=Card<FallingContinuallyHbrgoSaki>();resolved=decimal.Floor(Potency(3)*given.Factor);Check(resolved>0&&resolved<cap,"Given downscale leaves genuine huge potency within native range");Equal(Render(card,Enemy).Display!.Value,resolved,"preview defers cap until after Given multiplier");Play(card);Equal(Active()!.Amount,resolved,"real initial application applies Given multiplier before normalization cap");Check(given.Callbacks>0,"high-stat normalization retains actual Given notification");
        Reset();H("Mode",true,int.MaxValue);given=Power<GivenPower>(Hero);given.Bonus=0;given.Factor=1000m;var received=Power<ScaleReceivedPower>(Enemy);received.Factor=.0002m;card=Card<FallingContinuallyHbrgoSaki>();resolved=decimal.Floor(Potency(3)*given.Factor*received.Factor);Check(Potency(3)*given.Factor>int.MaxValue&&resolved<cap,"Given can exceed Int32 before Received downscales final amount below counter cap");Play(card);Equal(Active()!.Amount,resolved,"real first application normalizes only after both Given and Received multipliers");Equal(NativeChanges.Single(),resolved,"native amount-changed notification receives same final integer as stored counter");Check(received.Callbacks==1&&given.Callbacks>0,"large amount keeps both native modifier notification paths");
        Play(card);Equal(Active()!.Amount,cap,"second post-hook integer offset uses native total cap without overflow");Equal(NativeChanges.Last(),resolved,"existing stack normalizes post-Received offset rather than raw issued potency");
        Cases.Add(new{label="post-hook native cap",raw=card.DynamicVars["FallingContinuallyPower"].BaseValue,issued=Commands.Last().Amount,normalizedOffset=NativeChanges.Last(),stored=Active()!.Amount});
    }
    static void TriggerCases()
    {
        Reset();Play(Card<FallingContinuallyHbrgoSaki>());var falling=Active()!;Sense("Talent",99);Sense("Simplicity",99);Power<DexterityPower>(Hero,900);
        var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom);var result=new DamageResult(Enemy,ValueProp.Move){UnblockedDamage=1};Commands.Clear();
        falling.AfterDamageReceived(null!,Enemy,result,ValueProp.Move,phantom,null!).GetAwaiter().GetResult();Equal(Enemy.GetPowerAmount<DoomPower>(),300,"real Falling trigger applies stored300 Doom once despite changed current INT and LUCK");Check(Commands.Count==1&&Commands[0].Power is DoomPower&&Commands[0].Amount==300&&Commands[0].Applier==phantom&&Commands[0].Card==null,"real trigger issues exactly one native Doom command with phantom source and null card");Equal(falling.Amount,300,"trigger preserves applied Falling counter");
        falling.AfterDamageReceived(null!,Enemy,result,ValueProp.Move,phantom,null!).GetAwaiter().GetResult();Equal(Enemy.GetPowerAmount<DoomPower>(),600,"second legitimate native callback stacks stored300 once again without coefficient or debuff-curve recalculation");
        int before=Commands.Count;falling.AfterDamageReceived(null!,Enemy,new DamageResult(Enemy,ValueProp.Move),ValueProp.Move,phantom,null!).GetAwaiter().GetResult();falling.AfterDamageReceived(null!,Enemy,result,ValueProp.Unpowered,phantom,null!).GetAwaiter().GetResult();falling.AfterDamageReceived(null!,Enemy,result,ValueProp.Move,Hero,null!).GetAwaiter().GetResult();falling.AfterDamageReceived(null!,Hero,result,ValueProp.Move,phantom,null!).GetAwaiter().GetResult();Set(phantom,"_petOwner",null);falling.AfterDamageReceived(null!,Enemy,result,ValueProp.Move,phantom,null!).GetAwaiter().GetResult();Equal(Commands.Count,before,"zero damage non-Move non-phantom wrong target and ownerless phantom retain trigger guards");Equal(Enemy.GetPowerAmount<DoomPower>(),600,"invalid callbacks do not add Doom or rescale prior storage");
    }
    public static void Run(string path)
    {
        bool ok=false;string? error=null;try{Install();NativeCards();NativeHooksAndLegacy();NativeCapCases();TriggerCases();FormulaAndLiveCases();ok=true;}catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),boundary="Real two-card OnPlay, native generic/non-generic Apply/ModifyAmount/clone/storage/stacking with the actual production integer-normalization patch, native Given/Received hooks and Artifact, real INT/LUCK plus target E and base100 coefficient, card-description patch/LocManager/SmartFormat, and actual Falling AfterDamageReceived->native Doom apply execute. Death preceding AttackCommand.Execute is a controlled no-damage leaf preserving target-alive gate; no claim of attack damage pipeline or physical HP mutation. Shared Fragile host isolates StringName/logging and enumerates actual fixture actor powers for native hooks; wait leaf is immediate. Custom deterministic Power listeners exercise native Given and Received boundaries without replacing Hook. Beta strict telemetry isolation only. No Godot, game process, full saves, package or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Falling checks");if(!ok)throw new Exception("Inspect "+path);
    }
}