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
        power.BeforeSideTurnStart(null!,side??power.Owner.Side,participants??new[]{power.Owner},combat??power.Owner.CombatState!).GetAwaiter().GetResult();
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
        foreach(string patch in new[]{"IntegerStatAmountPatch","HbrNumericBlockPatch","HotaruCannonBlockScopePatch","HotaruCannonBlockRoundingPatch"})
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+patch,true)!).Patch().Count>0,"installs real "+patch);
        h.Patch(Helper("HbrStatPowerHelper").GetMethod("GetOutgoingAttack",F)!,postfix:new HarmonyMethod(typeof(CalamityChecks).GetMethod(nameof(ObserveOutgoing),F)!));
        foreach(var method in typeof(SfxCmd).GetMethods(F).Where(method=>method.Name=="Play"&&method.ReturnType==typeof(void)))
            h.Patch(method,prefix:new HarmonyMethod(typeof(CalamityChecks).GetMethod(nameof(SkipFx),F)!));
        foreach(var method in typeof(VfxCmd).GetMethods(F).Where(method=>method.Name=="PlayOnCreatureCenter"&&method.ReturnType==typeof(void)))
            h.Patch(method,prefix:new HarmonyMethod(typeof(CalamityChecks).GetMethod(nameof(SkipFx),F)!));
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
    static void End(CalamityPower power, CombatSide side = CombatSide.Enemy, IEnumerable<Creature>? participants = null) =>
        power.AfterSideTurnEnd(null!, side, participants ?? new[] { power.Owner }).GetAwaiter().GetResult();
    static CalamityPower[] Active(Creature target) => target.Powers.OfType<CalamityPower>().ToArray();
    static void EndAll(Creature target, CombatSide side = CombatSide.Enemy)
    { foreach (CalamityPower power in Active(target)) End(power, side); }
    static void ApplicationCases()
    {
        Reset(); Creature hero=State<Creature>("Hero"), enemy=State<Creature>("Enemy");
        var first=Apply(8)!;
        Check(first.Amount==8 && first.RemainingTurns==2 && Active(enemy).Single()==first,
            "native generic Apply creates an eight-point independent two-enemy-end instance");
        End(first);
        var second=Apply(3, applier:enemy)!;
        Check(!ReferenceEquals(first,second) && first.Amount==8 && first.RemainingTurns==1 &&
            second.Amount==3 && second.RemainingTurns==2 && Active(enemy).Length==2,
            "a weaker reapplication is a new None instance and never refreshes the older clock");
        Check(first.Applier==hero && second.Applier==enemy, "independent applications retain their own applier provenance");
        var third=Apply(8)!; var fourth=Apply(12)!;
        Check(Active(enemy).Length==4 && new[]{first,second,third,fourth}.Distinct().Count()==4,
            "None allows more than two independent equal or unequal applications");
        Equal(CalamityPower.GetTotalReduction(enemy),31m,"all active Calamity strengths add rather than taking a maximum");
        string secondBefore=Snapshot(second);
        PowerCmd.ModifyAmount(null!,first,0,hero,null,true).GetAwaiter().GetResult();
        Check(Snapshot(first)=="8:1", "zero direct amount mutation leaves the independent clock untouched");
        PowerCmd.ModifyAmount(null!,first,-2,hero,null,true).GetAwaiter().GetResult();
        PowerCmd.ModifyAmount(null!,first,4,hero,null,true).GetAwaiter().GetResult();
        Check(Snapshot(first)=="10:1" && Snapshot(second)==secondBefore && Active(enemy).Length==4,
            "direct native amount changes add/subtract only on the chosen instance without refreshing or merging");
        var supplied=(CalamityPower)ModelDb.Power<CalamityPower>().ToMutable();
        PowerCmd.Apply(null!,supplied,enemy,2,hero,null,true).GetAwaiter().GetResult();
        Check(Active(enemy).Length==5 && Active(enemy).Contains(supplied) && supplied.Amount==2 && supplied.RemainingTurns==2,
            "non-generic native Apply also creates a separate None instance");
        Reset(); first=Apply(15)!; End(first); Power<HalveIncoming>(enemy,1);
        var halved=Apply(20)!;
        Check(first.Amount==15 && first.RemainingTurns==1 && halved.Amount==10 && halved.RemainingTurns==2,
            "receiver modifiers affect only the new instance's strength, not an older instance or its clock");
        Equal(CalamityPower.GetTotalReduction(enemy),25m,"the aggregate uses effective received strengths after native hooks");
        Reset(); first=Apply(decimal.MaxValue)!;
        Check(first.Amount==999999999 && first.RemainingTurns==2,"native amount normalization safely bounds a huge reduction grant");
        Reset(); var artifact=Power<ArtifactPower>(enemy,1); Apply(8);
        Check(!enemy.HasPower<CalamityPower>() && artifact.Amount==0 && !Powers(enemy).Contains(artifact),
            "real Artifact blocks a first Calamity instance and consumes its charge");
        first=Apply(8)!; End(first); artifact=Power<ArtifactPower>(enemy,1); string before=Snapshot(first);
        Apply(3);
        Check(Active(enemy).Length==1 && Snapshot(first)==before && artifact.Amount==0,
            "Artifact rejects a fresh weaker instance without modifying the older strength or remaining time");
        Reset(); var doom=PowerCmd.Apply<DoomPower>(null!,enemy,2,hero,null,true).GetAwaiter().GetResult()!;
        PowerCmd.Apply<DoomPower>(null!,enemy,3,hero,null,true).GetAwaiter().GetResult();
        Check(doom.Amount==5 && !enemy.HasPower<CalamityPower>(),"native Doom retains its original additive behavior");
        var vanilla=PowerCmd.Apply<NativeCalamityPower>(null!,hero,2,hero,null,true).GetAwaiter().GetResult()!;
        PowerCmd.Apply<NativeCalamityPower>(null!,hero,3,hero,null,true).GetAwaiter().GetResult();
        Check(vanilla.Amount==5 && !hero.HasPower<CalamityPower>(),"original native Calamity buff remains distinct and additive");
        Reset();
    }
    static void ClockAndSaveCases()
    {
        Reset(); Creature hero=State<Creature>("Hero"), enemy=State<Creature>("Enemy"); var first=Apply(23)!;
        Start(first); Check(Snapshot(first)=="23:2", "turn starts no longer consume Calamity duration");
        End(first,CombatSide.Player); Check(Snapshot(first)=="23:2", "player-side ends do not count an EnemyTurnEnd effect");
        first.SkipNextDurationTick=true; End(first);
        Check(Snapshot(first)=="23:1", "the first enemy-side end consumes time without decaying strength or skipping natively");
        var second=Apply(7)!;
        Check(second.RemainingTurns==2 && first.RemainingTurns==1,"later applications keep their own two-end clock");
        var clone=(CalamityPower)first.ClonePreservingMutability();
        Check(clone.Owner==null && clone.Amount==23 && clone.RemainingTurns==1,"native clone preserves strength and one remaining end independently of ownership");
        var property=typeof(CalamityPower).GetProperty("RemainingTurns")!;
        Check(property.GetCustomAttributes().Any(attribute=>attribute.GetType().Name=="SavedPropertyAttribute"),"remaining ends have native saved-property metadata");
        var restored=(CalamityPower)ModelDb.Power<CalamityPower>().ToMutable(); Set(restored,"_amount",first.Amount);
        property.SetValue(restored,JsonSerializer.Deserialize<int>(JsonSerializer.Serialize(property.GetValue(first))));
        Check(restored.Amount==23 && restored.RemainingTurns==1,"save-value roundtrip retains remaining-one without resetting or converting it into strength");
        property.SetValue(clone,2); Check(first.RemainingTurns==1 && first.Amount==23,"clone timer changes leave the source instance untouched");
        EndAll(enemy);
        Check(!Powers(enemy).Contains(first) && Powers(enemy).Contains(second) && second.RemainingTurns==1,
            "one enemy-side end can expire the old instance while the newer instance stays at full strength");
        Equal(CalamityPower.GetTotalReduction(enemy),7m,"expiry removes only the expired instance's reduction");
        EndAll(enemy); Check(!enemy.HasPower<CalamityPower>(),"the second instance expires on its own second enemy-side end");
        var playerDebuff=Apply(4,hero)!; Start(playerDebuff); End(playerDebuff,CombatSide.Player);
        Check(playerDebuff.RemainingTurns==2,"a player holder's starts and player ends do not replace original EnemyTurnEnd timing");
        End(playerDebuff,CombatSide.Enemy,[enemy]);
        Check(playerDebuff.RemainingTurns==1,"an enemy-side end counts even though the debuffed player is not an enemy participant");
        End(playerDebuff,CombatSide.Enemy,[enemy]); Check(!hero.HasPower<CalamityPower>(),"a player-held instance expires after two enemy-side ends");
        Reset();
    }
    static void DoubleCases()
    {
        Reset(); Creature hero=State<Creature>("Hero"), enemy=State<Creature>("Enemy");
        var first=Apply(8)!; End(first); var second=Apply(3)!;
        var card=(CardModel)Fixture.GetMethod("OwnedCard",F)!.MakeGenericMethod(typeof(DoublePowersHbrgoSaki)).Invoke(null,null)!;
        Invoke("Play",card,null);
        Check(Active(enemy).Length==2 && Powers(enemy).Contains(first) && Powers(enemy).Contains(second),
            "actual Double preserves every independent Calamity instance and its identity");
        Check(Snapshot(first)=="16:2" && Snapshot(second)=="6:4",
            "actual Double doubles each instance's strength and its own remaining duration");
        Check(first.Applier==hero && second.Applier==hero,"Double does not replace original Calamity applier provenance");
        EndAll(enemy);
        Check(Snapshot(first)=="16:1" && Snapshot(second)=="6:3","doubled durations continue counting independently rather than clamping back to two");
        EndAll(enemy); Equal(CalamityPower.GetTotalReduction(enemy),6m,"the shorter doubled instance expires without removing the longer one");
        Reset(); var guarded=Apply(8)!; End(guarded); var artifact=Power<ArtifactPower>(enemy,1);
        Invoke("Play",card,null);
        Check(Snapshot(guarded)=="8:1", "Artifact rejects actual Double's strength and duration atomically for a Calamity instance");
        Check(!Powers(enemy).Contains(artifact), "the native Artifact charge is consumed while rejecting the Calamity transformation");
        Reset();
    }
    static void StatCases()
    {
        Reset();Creature hero=State<Creature>("Hero"),enemy=State<Creature>("Enemy");Player player=State<Player>("Player");
        AllStats(hero,100,"opening hero baseline");var heroDebuff=Apply(30,hero)!;AllStats(hero,70,"hero loses thirty in every attribute");
        Power<StrengthPower>(hero,10);Power<DexterityPower>(hero,20);Check(Stats(hero).SequenceEqual(new decimal[]{90,90,110,70,70,70}),"native Strength/Dexterity retain their own stat mapping alongside Calamity");
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
        Reset();var expiring=Apply(30,hero)!;End(expiring);AllStats(hero,70,"reduction stays full through remaining-one enemy end");End(expiring);AllStats(hero,100,"clock expiry immediately restores original attributes");
        var enemyExpiring=Apply(30,enemy)!;End(enemyExpiring);End(enemyExpiring);Equal(EnemyE(enemy),baseline,"enemy clock expiry restores original E without permanent loss");Reset();
        var happiness=Actor(Blank<HotaruHappinessCannon>(),player);typeof(Millsage).GetProperty("HappinessUpgradeLevel",F)!.SetValue(player.GetRelic<Millsage>(),3);
        Power<StrengthPower>(hero,10);Apply(200,hero);LastOutgoing=null;
        Call("HotaruCannonStatsHelper","PreviewDamage",happiness,6m,false,null,HBRGOSakiMod.Core.Helpers.HotaruCannonPhase.End);
        Check(LastOutgoing is {Length:6}&&LastOutgoing[0]==0&&LastOutgoing[1]==0,"real Happiness Lv3 attack combines extra Strength before Calamity floors to zero");
        Reset();Power<StrengthPower>(hero,10);Apply(125,hero);Apply(10,happiness);AllStats(happiness,0,"ordinary Happiness view floors normal source before upgrade-only bonus");LastOutgoing=null;
        Call("HotaruCannonStatsHelper","PreviewDamage",happiness,6m,false,null,HBRGOSakiMod.Core.Helpers.HotaruCannonPhase.End);
        Check(LastOutgoing is {Length:6}&&LastOutgoing[0]==5&&LastOutgoing[1]==5,"real Happiness Lv3 combines100 times1point4 minus125 minus10 to five without intermediate clamp loss");
        typeof(Millsage).GetProperty("HappinessUpgradeLevel",F)!.SetValue(player.GetRelic<Millsage>(),0);Reset();
    }
    static void ImmunityCases()
    {
        Reset();Creature hero=State<Creature>("Hero");CombatState combat=State<CombatState>("Combat");var boss=Actor(Blank<NararaShibaBossMonster>());
        FieldInfo enemies=(FieldInfo)Fixture.GetMethod("Field",F)!.Invoke(null,[typeof(CombatState),"_enemies"])!;object? old=enemies.GetValue(combat);Set(combat,"_enemies",new List<Creature>{boss});
        try
        {
            // Nullification is switched off (2026-11-09): inside the card's own play the
            // fresh independent application meets Artifact as it would anywhere else.
            Power<NararaNullificationPower>(boss,1);var resistance=Power<SixSenseResistancePower>(boss,1);typeof(SixSenseResistancePower).GetProperty("Resistances",F)!.SetValue(resistance,new[]{10,10,10,10,10,10});
            var card=(FragileHbrgoSaki)Invoke("TestCard")!;
            var current=Apply(8,boss)!;End(current);var artifact=Power<ArtifactPower>(boss,1);
            var scope=Call("NararaNullificationHelper","Begin",card)!;
            try{Check(!(bool)Call("NararaNullificationHelper","IsCardNullified",boss,card)!,"the switched-off Narara nullification captures no card");Apply(3,boss,hero,card);Check(artifact.Amount==0,"inside the card's play Artifact still blocks the application and is consumed, as without Narara");}
            finally{Call("NararaNullificationHelper","Close",scope);}
        }
        finally{Set(combat,"_enemies",old);Reset();}
    }
    static string RepositoryRoot()
    {
        for(DirectoryInfo? directory=new(AppContext.BaseDirectory);directory!=null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"HBRGOSakiMod.csproj")))return directory.FullName;
        throw new DirectoryNotFoundException("Fixture must remain below its checkout");
    }
    static void DescriptionCases()
    {
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        string root=RepositoryRoot();var json=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"HBRGOSakiMod/localization/zhs/powers.json")))!;
        Set(manager,"_tables",new Dictionary<string,LocTable>{["powers"]=new LocTable("powers",json,null!)});typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
        var canonical=ModelDb.Power<CalamityPower>();Check(!canonical.Description.GetFormattedText().Contains('{'),"canonical reference renders independent duration without requiring an owner");
        var power=Apply(7)!;End(power);var description=power.Description;string text=description.GetFormattedText();Check(!text.Contains('{')&&text.Contains("剩余1个敌方回合")&&Convert.ToInt32(description.Variables["Amount"],CultureInfo.InvariantCulture)==7,"owned description binds strength seven separately from rendered remaining-one timer");
        Check(power.CustomPackedIconPath=="res://images/powers/hbr_calamity_power.png"&&power.CustomBigBetaIconPath==power.CustomPackedIconPath,"custom icon path is distinct from native Calamity buff icon");Reset();
    }
    public static Task Run()
    {
        bool ok=false;string? error=null;
        try{Install();ApplicationCases();ClockAndSaveCases();DoubleCases();StatCases();ImmunityCases();DescriptionCases();ok=true;}
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"report.json"),JsonSerializer.Serialize(new{
            ok,checks=Passed.Count,passed=Passed,error,
            runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE")??"stable",
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(PowerModel).Assembly.Location))),
            boundary="Reuses real native Fragile setup without running its 650 checks. Actual PowerCmd generic/non-generic Apply/ModifyAmount, Artifact hooks, Narara scope, None/unlimited independent instances, real EnemyTurnEnd callbacks and actual Double card, native remove/clone and saved-property roundtrip, HBR six-stat/E helpers, owner-sourced cannon damage preview, native cannon block Gain command/scopes/writer, and native localization. Only Godot StringName/logging/hook enumeration plus sound/VFX leaves are isolated. No Doom replacement, full game UI, full save loading, multiplayer, package or upload."
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Calamity checks");if(!ok)throw new Exception("Calamity checks failed; inspect report.json");return Task.CompletedTask;
    }
}
