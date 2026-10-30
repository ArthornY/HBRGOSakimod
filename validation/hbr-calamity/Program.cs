using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;
using CalamityPower=HBRGOSakiMod.Core.Models.Powers.HbrCalamityPower;
using NativeCalamityPower=MegaCrit.Sts2.Core.Models.Powers.CalamityPower;

static class CalamityChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Type Fixture=typeof(Checks);
    static readonly Assembly Mod=typeof(CalamityPower).Assembly;
    static readonly List<string> Passed=[];
    static object? Invoke(string name,params object?[] args)=>Fixture.GetMethod(name,F)!.Invoke(null,args);
    static T State<T>(string name)=>(T)Fixture.GetField(name,F)!.GetValue(null)!;
    static void Set(object owner,string name,object? value)=>Invoke("Set",owner,name,value);
    static T Blank<T>()=>(T)Fixture.GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)).Invoke(null,null)!;
    static T Power<T>(Creature owner,int amount)where T:PowerModel=>(T)Fixture.GetMethod("Power",F)!.MakeGenericMethod(typeof(T)).Invoke(null,[owner,amount])!;
    static List<PowerModel> Powers(Creature owner)=>(List<PowerModel>)Invoke("Powers",owner)!;
    static Creature Actor(MonsterModel model,Player? owner=null)=>(Creature)Invoke("Actor",model,null,owner)!;
    static Type Helper(string name)=>Mod.GetType("HBRGOSakiMod.Core.Helpers."+name,true)!;
    static object? Call(string helper,string method,params object?[] args)=>Helper(helper).GetMethod(method,F)!.Invoke(null,args);
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);Passed.Add(name);}
    static void Equal(decimal actual,decimal expected,string name)=>Check(actual==expected,$"{name}: {actual} = {expected}");
    static void Reset()=>Invoke("Reset");
    static bool SkipFx()=>false;
    static decimal[]? LastOutgoing;
    static void ObserveOutgoing(object __result)=>LastOutgoing=new[]{"Strength","Dexterity","Intelligence","Vitality","Spirit","Luck"}.Select(p=>(decimal)__result.GetType().GetProperty(p)!.GetValue(__result)!).ToArray();
    static void Register<T>()where T:PowerModel
    {
        var content=(Dictionary<ModelId,AbstractModel>)typeof(ModelDb).GetField("_contentById",F)!.GetValue(null)!;
        if(!content.Values.Any(p=>p.GetType()==typeof(T)))ModelDb.Inject(typeof(T));
        Set(ModelDb.Power<T>(),"<Id>k__BackingField",ModelDb.GetId(typeof(T)));
    }
    static CalamityPower? Apply(decimal amount,Creature? target=null,Creature? applier=null,CardModel? card=null)=>
        PowerCmd.Apply<CalamityPower>(null!,target??State<Creature>("Enemy"),amount,applier??State<Creature>("Hero"),card,true).GetAwaiter().GetResult();
    static void Start(CalamityPower power,CombatSide? side=null,IReadOnlyList<Creature>? participants=null,ICombatState? combat=null)=>
        power.BeforeSideTurnStart(null!,side??power.Owner.Side,participants??new[]{power.Owner},combat??power.Owner.CombatState).GetAwaiter().GetResult();
    static string Snapshot(CalamityPower power)=>$"{power.Amount}:{power.RemainingTurns}";
    static decimal[] Stats(Creature creature)
    {
        object?[] args=[creature,null];Check((bool)Helper("HbrBattleStatsHelper").GetMethod("TryGetStats",F)!.Invoke(null,args)!,"real HBR stats resolve "+(creature.Monster?.GetType().Name??"hero"));
        return new[]{"Strength","Dexterity","Intelligence","Vitality","Spirit","Luck"}.Select(p=>(decimal)args[1]!.GetType().GetProperty(p)!.GetValue(args[1])!).ToArray();
    }
    static decimal EnemyE(Creature target)=>(decimal)Call("HbrBattleStatsHelper","GetEnemyParameter",target)!;
    static void AllStats(Creature creature,decimal expected,string name)=>Check(Stats(creature).All(v=>v==expected),name+" all six = "+expected);
    static void Install()
    {
        Invoke("Setup");
        var relic=new Millsage();Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",State<Player>("Player"));
        var relicField=(FieldInfo)Fixture.GetMethod("Field",F)!.Invoke(null,[typeof(Player),"_relics"])!;
        var relics=(List<RelicModel>)relicField.GetValue(State<Player>("Player"))!;
        relics.Add(relic);Register<CalamityPower>();Register<DoomPower>();Register<NativeCalamityPower>();
        var h=new Harmony("hbr.calamity.managed.checks");
        foreach(string patch in new[]{"CalamityApplyPatch","CalamityModifyPatch","HbrNumericBlockPatch","HotaruCannonBlockScopePatch","HotaruCannonBlockRoundingPatch"})
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+patch,true)!).Patch().Count>0,"installs real "+patch);
        h.Patch(Helper("HbrStatPowerHelper").GetMethod("GetOutgoingAttack",F)!,postfix:new HarmonyMethod(typeof(CalamityChecks).GetMethod(nameof(ObserveOutgoing),F)!));
        h.Patch(AccessTools.Method(typeof(SfxCmd),"Play",[typeof(string),typeof(float)]),prefix:new HarmonyMethod(typeof(CalamityChecks).GetMethod(nameof(SkipFx),F)!));
        h.Patch(AccessTools.Method(typeof(VfxCmd),"PlayOnCreatureCenter",[typeof(Creature),typeof(string)]),prefix:new HarmonyMethod(typeof(CalamityChecks).GetMethod(nameof(SkipFx),F)!));
        Check(ModelDb.Power<CalamityPower>().Id!=ModelDb.Power<NativeCalamityPower>().Id,"mod Calamity ID differs from original native Calamity buff");
        Check(ModelDb.Power<CalamityPower>().Type==MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff&&ModelDb.Power<NativeCalamityPower>().Type==MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff,"mod debuff and native buff remain distinct types");
    }
    sealed class HalveIncoming : PowerModel
    {
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
        public override bool TryModifyPowerAmountReceived(PowerModel power,Creature target,decimal amount,Creature? applier,out decimal modified)
        {modified=amount;if(power is not CalamityPower||target!=Owner||amount<=0)return false;modified=amount/2m;return true;}
    }
    static void ApplicationCases()
    {
        Reset();Creature hero=State<Creature>("Hero"),enemy=State<Creature>("Enemy");
        var first=Apply(8)!;Check(first.Amount==8&&first.RemainingTurns==2&&enemy.GetPower<CalamityPower>()==first,"native generic Apply creates eight reduction stacks with independent two-turn clock");
        Start(first);Check(first.Amount==8&&first.RemainingTurns==1,"first actual owner start consumes time without decaying strength");
        var weak=Apply(3,applier:enemy)!;Check(ReferenceEquals(weak,first)&&first.Amount==8&&first.RemainingTurns==2,"weaker native reapplication retains maximum and refreshes to two");
        Check(first.Applier==hero,"refresh preserves original applier provenance");
        Start(first);var equal=Apply(8)!;Check(ReferenceEquals(equal,first)&&first.Amount==8&&first.RemainingTurns==2,"equal application refreshes independent duration");
        Start(first);var stronger=Apply(12)!;Check(stronger==first&&first.Amount==12&&first.RemainingTurns==2,"stronger application replaces strength by max rather than adding");
        Check(enemy.Powers.OfType<CalamityPower>().Count()==1,"all generic reapplications retain one instance");
        Start(first);PowerCmd.ModifyAmount(null!,first,0,hero,null,true).GetAwaiter().GetResult();Check(first.Amount==12&&first.RemainingTurns==1,"zero explicit offset never refreshes");
        PowerCmd.ModifyAmount(null!,first,-2,hero,null,true).GetAwaiter().GetResult();Check(first.Amount==10&&first.RemainingTurns==1,"negative native offset reduces stacks but never refreshes");
        PowerCmd.ModifyAmount(null!,first,4,hero,null,true).GetAwaiter().GetResult();Check(first.Amount==10&&first.RemainingTurns==2,"positive direct ModifyAmount applies max-and-refresh instead of addition");
        Start(first);PowerCmd.ModifyAmount(null!,first,11,hero,null,true).GetAwaiter().GetResult();Check(first.Amount==11&&first.RemainingTurns==2,"positive stronger direct ModifyAmount replaces by maximum");
        var supplied=(CalamityPower)ModelDb.Power<CalamityPower>().ToMutable();Start(first);
        PowerCmd.Apply(null!,supplied,enemy,2,hero,null,true).GetAwaiter().GetResult();Check(enemy.GetPower<CalamityPower>()==first&&first.Amount==11&&first.RemainingTurns==2,"non-generic Apply shares the same existing-instance refresh semantics");

        Reset();Apply(0);Apply(-1);Apply(.9m);Check(!enemy.HasPower<CalamityPower>(),"nonpositive and fractional-zero first applications never leave a zero-strength clock");
        first=Apply(15)!;Start(first);Power<HalveIncoming>(enemy,1);Apply(20);
        Check(first.Amount==15&&first.RemainingTurns==2,"max compares effective ten-stack received result rather than authored twenty");
        Start(first);Apply(40);Check(first.Amount==20&&first.RemainingTurns==2,"receiver-modified stronger twenty replaces fifteen");
        Reset();first=Apply(decimal.MaxValue)!;Check(first.Amount==999999999&&first.RemainingTurns==2,"huge application saturates at the native stack bound without overflow");

        Reset();var artifact=Power<ArtifactPower>(enemy,1);Apply(8);Check(!enemy.HasPower<CalamityPower>(),"real Artifact blocks the first application entirely");
        Check(artifact.Amount==0&&!enemy.Powers.Contains(artifact),"real receiver after-hook consumes Artifact once");
        first=Apply(8)!;Start(first);artifact=Power<ArtifactPower>(enemy,1);string before=Snapshot(first);Apply(3);
        Check(Snapshot(first)==before,"Artifact blocks a weaker zero-difference refresh before its timer changes");
        Check(artifact.Amount==0&&!enemy.Powers.Contains(artifact),"blocked weaker refresh still consumes actual Artifact");
        Reset();var doom=PowerCmd.Apply<DoomPower>(null!,enemy,2,hero,null,true).GetAwaiter().GetResult()!;
        PowerCmd.Apply<DoomPower>(null!,enemy,3,hero,null,true).GetAwaiter().GetResult();Check(doom.Amount==5&&!enemy.HasPower<CalamityPower>(),"native Doom remains additive and is never replaced by the new debuff");
        var vanilla=PowerCmd.Apply<NativeCalamityPower>(null!,hero,2,hero,null,true).GetAwaiter().GetResult()!;
        PowerCmd.Apply<NativeCalamityPower>(null!,hero,3,hero,null,true).GetAwaiter().GetResult();Check(vanilla.Amount==5&&!hero.HasPower<CalamityPower>(),"native Calamity buff remains additive and does not enter custom patches");
        Reset();
    }
    static void ClockAndSaveCases()
    {
        Reset();Creature hero=State<Creature>("Hero"),enemy=State<Creature>("Enemy");var power=Apply(23)!;
        Start(power,CombatSide.Player,new[]{enemy});Check(power.RemainingTurns==2,"wrong side does not advance owner clock");
        Start(power,CombatSide.Enemy,new[]{hero});Check(power.RemainingTurns==2,"owner absent from actual turn participants does not tick");
        Start(power,combat:Blank<CombatState>());Check(power.RemainingTurns==2,"unrelated combat state does not tick");
        power.SkipNextDurationTick=true;Start(power);Check(power.RemainingTurns==1&&power.Amount==23,"actual turn start ignores native duration skip marker and keeps full strength");
        var clone=(CalamityPower)power.ClonePreservingMutability();Check(clone.Owner==null&&clone.Amount==23&&clone.RemainingTurns==1,"real native clone retains saved one-turn clock independently of owner");
        var property=typeof(CalamityPower).GetProperty("RemainingTurns")!;Check(property.GetCustomAttributes().Any(a=>a.GetType().Name=="SavedPropertyAttribute"),"remaining clock has explicit native saved-property metadata");
        var restored=(CalamityPower)ModelDb.Power<CalamityPower>().ToMutable();Set(restored,"_amount",power.Amount);property.SetValue(restored,JsonSerializer.Deserialize<int>(JsonSerializer.Serialize(property.GetValue(power))));
        Check(restored.Amount==23&&restored.RemainingTurns==1,"remaining-one roundtrip does not become stack count or reset to two");
        property.SetValue(clone,2);Check(power.RemainingTurns==1&&power.Amount==23,"clone mutation leaves original clock and stacks untouched");
        Start(power);Check(power.RemainingTurns==0&&!enemy.HasPower<CalamityPower>(),"second actual owner start removes the entire reduction rather than a layer");
        power=Apply(4,hero)!;Start(power);Check(power.RemainingTurns==1,"player-owned debuff also ticks at its first actual start");
        Start(power);Check(!hero.HasPower<CalamityPower>(),"player-owned debuff expires at its second actual start");Reset();
    }
    static void StatCases()
    {
        Reset();Creature hero=State<Creature>("Hero"),enemy=State<Creature>("Enemy");Player player=State<Player>("Player");
        AllStats(hero,100,"opening hero baseline");var heroDebuff=Apply(30,hero)!;AllStats(hero,70,"hero loses thirty in every attribute");
        Power<StrengthPower>(hero,10);Power<DexterityPower>(hero,20);Check(Stats(hero).SequenceEqual(new decimal[]{80,80,90,70,70,70}),"native Strength/Dexterity retain their own stat mapping alongside Calamity");
        Powers(hero).RemoveAll(p=>p is StrengthPower or DexterityPower);
        var phantom=Actor(Blank<SmallHopper>(),player);var phantomIdentity=Power<PhantomPower>(phantom,1);
        Set(phantomIdentity,"<IsSummonedMinion>k__BackingField",true);Set(phantomIdentity,"<SummonedMinionLevel>k__BackingField",7);
        AllStats(phantom,112,"phantom retains own level bonus without inheriting summoner debuff");Apply(5,phantom);AllStats(phantom,107,"phantom only loses its local five");
        var cannon=Actor(Blank<HotaruCoffinCannon>(),player);var other=Actor(Blank<HotaruContestCannon>(),player);Apply(5,cannon);
        AllStats(cannon,65,"cannon inherits owner thirty plus local five exactly once");AllStats(other,70,"other cannon only inherits owner thirty");
        decimal block=(decimal)Call("HotaruCannonBlockHelper","Preview",hero,4m,null,cannon)!;
        Equal(block,130,"real cannon block preview uses local65INT then final half");
        Equal((decimal)Call("HotaruCannonBlockHelper","Preview",hero,4m,null,other)!,140,"unaffected cannon preview retains owner70INT");
        Check(ReferenceEquals(Call("HotaruCannonBlockHelper","GetStatSource",hero),hero),"block preview restores previous stat source after completion");
        Set(hero,"_block",0);decimal gained=((Task<decimal>)Call("HotaruCannonBlockHelper","Gain",hero,4m,null,cannon,false)!).GetAwaiter().GetResult();
        Equal(gained,130,"actual native cannon block command uses local reduction exactly once");Equal(hero.Block,130,"actual native block writer receives the computed combat amount");
        Check(ReferenceEquals(Call("HotaruCannonBlockHelper","GetStatSource",hero),hero),"actual gain scope restores owner stat source afterward");
        // Same owner-sourced attack route and Soul half used by real cannons.
        decimal attack=(decimal)Call("HotaruCannonStatsHelper","PreviewDamage",cannon,6m,false,null,HBRGOSakiMod.Core.Helpers.HotaruCannonPhase.End)!;
        decimal expected=decimal.Floor(decimal.Floor(6000m*(1m+(65m-150m)/140m)*100m)/100m*.5m);
        Equal(attack,expected,"owner-sourced cannon attack deducts thirty plus local five, not owner twice");
        decimal baseline=EnemyE(enemy);var enemyDebuff=Apply(30,enemy)!;Equal(EnemyE(enemy),baseline-30,"enemy ability E loses reduction layers independently of resource pools");
        Equal((decimal)Call("HbrEnemyProfileHelper","GetBaseEnemyParameter",enemy)!,baseline,"temporary Calamity never changes map progression baseline");
        string manualBefore=State<ElectronicSoldierManual>("Manual").HbrBattleProgressData;
        string heroBefore=Snapshot(heroDebuff),enemyBefore=Snapshot(enemyDebuff),cannonBefore=Snapshot(cannon.GetPower<CalamityPower>()!);
        for(int i=0;i<12;i++){Stats(hero);Stats(phantom);Stats(cannon);EnemyE(enemy);Call("HotaruCannonBlockHelper","Preview",hero,4m,null,cannon);}
        Check(manualBefore==State<ElectronicSoldierManual>("Manual").HbrBattleProgressData&&heroBefore==Snapshot(heroDebuff)&&enemyBefore==Snapshot(enemyDebuff)&&cannonBefore==Snapshot(cannon.GetPower<CalamityPower>()!),"repeated stats and previews never rewrite progression, stack or clock state");
        Apply(200,hero);AllStats(hero,0,"reduction above base floors every hero attribute at zero");Apply(1000,enemy);Equal(EnemyE(enemy),0,"reduction above enemy E floors at zero");
        Reset();var expiring=Apply(30,hero)!;Start(expiring);AllStats(hero,70,"reduction stays full through remaining-one turn");Start(expiring);AllStats(hero,100,"clock expiry immediately restores original attributes");
        var enemyExpiring=Apply(30,enemy)!;Start(enemyExpiring);Start(enemyExpiring);Equal(EnemyE(enemy),baseline,"enemy clock expiry restores original E without permanent loss");Reset();
        var happiness=Actor(Blank<HotaruHappinessCannon>(),player);typeof(Millsage).GetProperty("HappinessUpgradeLevel",F)!.SetValue(player.GetRelic<Millsage>(),3);
        Power<StrengthPower>(hero,10);Apply(200,hero);LastOutgoing=null;
        Call("HotaruCannonStatsHelper","PreviewDamage",happiness,6m,false,null,HBRGOSakiMod.Core.Helpers.HotaruCannonPhase.End);
        Check(LastOutgoing is {Length:6}&&LastOutgoing[0]==0&&LastOutgoing[1]==0,"real Happiness Lv3 attack combines extra Strength before Calamity floors to zero");
        Reset();Power<StrengthPower>(hero,10);Apply(105,hero);Apply(10,happiness);AllStats(happiness,0,"ordinary Happiness view floors normal source before upgrade-only bonus");LastOutgoing=null;
        Call("HotaruCannonStatsHelper","PreviewDamage",happiness,6m,false,null,HBRGOSakiMod.Core.Helpers.HotaruCannonPhase.End);
        Check(LastOutgoing is {Length:6}&&LastOutgoing[0]==5&&LastOutgoing[1]==5,"real Happiness Lv3 combines100 times1point2 minus105 minus10 to five without intermediate clamp loss");
        typeof(Millsage).GetProperty("HappinessUpgradeLevel",F)!.SetValue(player.GetRelic<Millsage>(),0);Reset();
    }
    static void ImmunityCases()
    {
        Reset();Creature hero=State<Creature>("Hero");CombatState combat=State<CombatState>("Combat");var boss=Actor(Blank<NararaShibaBossMonster>());
        FieldInfo enemies=(FieldInfo)Fixture.GetMethod("Field",F)!.Invoke(null,[typeof(CombatState),"_enemies"])!;object? old=enemies.GetValue(combat);Set(combat,"_enemies",new List<Creature>{boss});
        try
        {
            Power<NararaNullificationPower>(boss,1);var resistance=Power<SixSenseResistancePower>(boss,1);typeof(SixSenseResistancePower).GetProperty("Resistances",F)!.SetValue(resistance,new[]{10,10,10,10,10,10});
            var card=(FragileHbrgoSaki)Invoke("TestCard")!;ModelCapabilities.EnsureInitialized();ModelCapabilityRegistry.Register<SixSenseCardCapability>("fixture.calamity.six-sense");
            var capability=ModelCapabilityRegistry.Create<SixSenseCardCapability>();Set(capability,"_attribute",SixSenseAttribute.Simplicity);card.Capabilities().Add(capability,false,false);
            var current=Apply(8,boss)!;Start(current);var artifact=Power<ArtifactPower>(boss,1);string before=Snapshot(current);
            var scope=Call("NararaNullificationHelper","Begin",card)!;
            try{Apply(3,boss,hero,card);Check(Snapshot(current)==before&&artifact.Amount==1,"actual captured Narara immunity rejects weak refresh before consuming Artifact or changing time");}
            finally{Call("NararaNullificationHelper","Close",scope);}
        }
        finally{Set(combat,"_enemies",old);Reset();}
    }
    static void DescriptionCases()
    {
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));var json=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"HBRGOSakiMod/localization/zhs/powers.json")))!;
        Set(manager,"_tables",new Dictionary<string,LocTable>{["powers"]=new LocTable("powers",json,null!)});typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
        var canonical=ModelDb.Power<CalamityPower>();Check(!canonical.Description.GetFormattedText().Contains('{'),"canonical reference renders independent duration without requiring an owner");
        var power=Apply(7)!;Start(power);var description=power.Description;string text=description.GetFormattedText();Check(!text.Contains('{')&&text.Contains("剩余1回合")&&Convert.ToInt32(description.Variables["Amount"],CultureInfo.InvariantCulture)==7,"owned description binds strength seven separately from rendered remaining-one timer");
        Check(power.CustomPackedIconPath=="res://images/powers/hbr_calamity_power.png"&&power.CustomBigBetaIconPath==power.CustomPackedIconPath,"custom icon path is distinct from native Calamity buff icon");Reset();
    }
    public static Task Run()
    {
        bool ok=false;string? error=null;
        try{Install();ApplicationCases();ClockAndSaveCases();StatCases();ImmunityCases();DescriptionCases();ok=true;}
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"report.json"),JsonSerializer.Serialize(new{
            ok,checks=Passed.Count,passed=Passed,error,
            runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE")??"stable",
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(PowerModel).Assembly.Location))),
            boundary="Reuses real native Fragile setup without running its 650 checks. Actual PowerCmd generic/non-generic Apply/ModifyAmount, Artifact hooks, Narara scope, real holder turn-start callbacks, native remove/clone and saved-property roundtrip, HBR six-stat/E helpers, owner-sourced cannon damage preview, native cannon block Gain command/scopes/writer, and native localization. Only Godot StringName/logging/hook enumeration plus sound/VFX leaves are isolated. No Doom replacement, full game UI, full save loading, multiplayer, package or upload."
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Calamity checks");if(!ok)throw new Exception("Calamity checks failed; inspect report.json");return Task.CompletedTask;
    }
}
