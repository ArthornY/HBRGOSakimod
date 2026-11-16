using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;

static class EnemyCancerChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static readonly Assembly Mod=typeof(HbrgoStaticMonster).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Roster=[];
    // Independent task-approved inventory, deliberately not read from the helper's private set.
    static readonly Type[] Expected=
    [
        typeof(AbyssKnocker),
        typeof(AbyssKnockerKiller),
        typeof(ArachneLine),
        typeof(BalbFlea),
        typeof(BarrelWalker),
        typeof(BigotryGate),
        typeof(CatHorn),
        typeof(CatHornKiller),
        typeof(ClayKnocker),
        typeof(CorollaKnife),
        typeof(CrestHopper),
        typeof(CrestHopperKiller),
        typeof(CrowHorse),
        typeof(DarkKnockerKiller),
        typeof(DeathSlug),
        typeof(DiamondEyeball),
        typeof(FlatHand),
        typeof(GiraffeHalo),
        typeof(GoldHopper),
        typeof(HealHopper),
        typeof(IvoryScooper),
        typeof(Knocker),
        typeof(Marionette),
        typeof(MarionetteKiller),
        typeof(MolArms),
        typeof(NeedleBird),
        typeof(OctopusTail),
        typeof(OctopusTailKiller),
        typeof(PileGolemEdge),
        typeof(PillarGuardian),
        typeof(RedCrimsonBossMonster),
        typeof(ShrimpCrab),
        typeof(SkullFeather),
        typeof(SlimySerpent),
        typeof(SmallHopper),
        typeof(TargetWireFrame),
        typeof(TargetWireFrameKiller),
        typeof(TriClaw),
        typeof(WireSlasher)
    ];
    static string RepositoryRoot
    {
        get {for(DirectoryInfo? d=new(AppContext.BaseDirectory);d!=null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"HBRGOSakiMod.csproj")))return d.FullName;throw new Exception("Missing checkout");}
    }
    static void Check(bool value,string description){if(!value)throw new Exception(description);Passed.Add(description);}
    static FieldInfo Field(Type type,string name)
    {for(Type? t=type;t!=null;t=t.BaseType)if(t.GetField(name,F) is {} field)return field;throw new MissingFieldException(type.FullName,name);}
    static void Set(object value,string name,object? item)=>Field(value.GetType(),name).SetValue(value,item);
    static object Blank(Type type){object value=RuntimeHelpers.GetUninitializedObject(type);GC.SuppressFinalize(value);return value;}
    static T Blank<T>()=>(T)Blank(typeof(T));
    static bool Skip()=>false;
    static Creature Actor(Type? type,CombatSide side=CombatSide.Enemy,Player? player=null,Player? petOwner=null)
    {
        Creature actor=Blank<Creature>();
        Set(actor,"_powers",new List<PowerModel>());Set(actor,"_maxHp",5000);Set(actor,"_currentHp",5000);
        Set(actor,"<Side>k__BackingField",side);Set(actor,"<Player>k__BackingField",player);Set(actor,"_petOwner",petOwner);
        if(type!=null){var monster=(MonsterModel)Blank(type);Set(monster,"<IsMutable>k__BackingField",true);Set(monster,"_creature",actor);Set(actor,"<Monster>k__BackingField",monster);}
        return actor;
    }
    static bool IsCancer(Creature creature)=>(bool)Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrEnemyCancerHelper",true)!.GetMethod("IsCancer",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[creature])!;
    static void Install()
    {
        TestMode.IsOn=true;
        var harmony=new Harmony("hbr.enemy-cancer.native");
        harmony.Patch(typeof(StringName).GetConstructor([typeof(string)])!,prefix:new HarmonyMethod(typeof(EnemyCancerChecks).GetMethod(nameof(Skip),F)!));
        Type logger=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        foreach(MethodInfo method in logger.GetMethods(F).Where(m=>m.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose" or "GetIsRunningFromGodotEditor"))
            harmony.Patch(method,prefix:new HarmonyMethod(typeof(EnemyCancerChecks).GetMethod(nameof(Skip),F)!));
        Type[] modelTypes=Mod.GetTypes().Where(t=>!t.IsAbstract&&
            (typeof(IHbrgoMonster).IsAssignableFrom(t)||typeof(EncounterModel).IsAssignableFrom(t))).ToArray();
        foreach(Type type in modelTypes)
        {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
        Check(modelTypes.All(ModelDb.Contains),"78 registers actual canonical HBR monsters and encounters with native ModelDb");
    }
    static void IdentityCases()
    {
        Check(Expected.Length==39&&Expected.Distinct().Count()==39,"78 task inventory contains exactly39 distinct enemy model classes");
        Type[] concrete=Mod.GetTypes().Where(t=>!t.IsAbstract&&typeof(MonsterModel).IsAssignableFrom(t)&&typeof(IHbrgoMonster).IsAssignableFrom(t)).ToArray();
        Check(concrete.ToHashSet().SetEquals(Expected.Append(typeof(TezukaSaki)).Append(typeof(NararaShibaBossMonster))),"78 every current concrete IHbrgoMonster class explicitly classified; no silent future expansion");
        Player player=Blank<Player>();
        foreach(Type type in Expected)
        {
            Creature enemy=Actor(type);Check(IsCancer(enemy),"78 enemy classification "+type.Name);
            Set(enemy,"_currentHp",0);Check(IsCancer(enemy),"78 death does not erase classification "+type.Name);
            Check(!IsCancer(Actor(type,CombatSide.Player)),"78 same model on friendly side excluded "+type.Name);
            Check(!IsCancer(Actor(type,petOwner:player)),"78 hostile-side pet metadata still excluded "+type.Name);
            Check(!IsCancer(Actor(type,player:player)),"78 hostile-side player metadata still excluded "+type.Name);
            Creature phantom=Actor(type);var marker=Blank<PhantomPower>();((List<PowerModel>)Field(typeof(Creature),"_powers").GetValue(phantom)!).Add(marker);
            Check(IsCancer(phantom),"78 incidental PhantomPower does not change actual unowned enemy identity "+type.Name);
            Creature allyPhantom=Actor(type,CombatSide.Player,petOwner:player);((List<PowerModel>)Field(typeof(Creature),"_powers").GetValue(allyPhantom)!).Add(Blank<PhantomPower>());
            Check(!IsCancer(allyPhantom),"78 actual allied phantom remains excluded "+type.Name);
            Creature minion=Actor(type);((List<PowerModel>)Field(typeof(Creature),"_powers").GetValue(minion)!).Add(Blank<MinionPower>());
            Check(IsCancer(minion),"78 actual enemy minion remains Cancer "+type.Name);
        }
        Type[] excluded=[typeof(TezukaSaki),typeof(NararaShibaBossMonster),typeof(RedCrimson),typeof(HotaruHappinessCannon),typeof(HotaruCoffinCannon),typeof(HotaruContestCannon),typeof(HotaruLongingCannon),typeof(HotaruAbsolutionCannon),typeof(HotaruFinalCannon)];
        foreach(Type type in excluded)
        {
            Check(!IsCancer(Actor(type)),"78 excluded model even with hostile metadata "+type.Name);
            Check(!IsCancer(Actor(type,CombatSide.Player,petOwner:player)),"78 friendly summon/NPC excluded "+type.Name);
        }
        Type[] vanilla=typeof(MonsterModel).Assembly.GetTypes().Where(t=>!t.IsAbstract&&typeof(MonsterModel).IsAssignableFrom(t)).ToArray();
        Check(vanilla.Length>30,"78 scans complete loaded vanilla monster catalog rather than a single sample");
        foreach(Type type in vanilla)Check(!IsCancer(Actor(type)),"78 native vanilla enemy excluded "+type.FullName);
        Check(!IsCancer(Actor(null)),"78 no MonsterModel excluded");Check(!IsCancer(null!),"78 null creature excluded");
        Creature repeat=Actor(typeof(SmallHopper));
        PowerModel[] before=repeat.Powers.ToArray();for(int i=0;i<1000;i++)if(!IsCancer(repeat))throw new Exception("Nonstable classification");
        Check(repeat.Powers.SequenceEqual(before)&&repeat.CurrentHp==5000&&repeat.MaxHp==5000,"78 repeated query does not create icon/power, mutate resources, or require active combat");
    }
    static void RegistryCases()
    {
        var union=new HashSet<Type>();var mapping=new Dictionary<Type,HashSet<int>>();
        foreach(var act in new[]{(1,"One",22),(2,"Two",20),(3,"Three",18),(4,"Four",2)})
        {
            Type registry=Mod.GetType("HBRGOSakiMod.Core.Models.Encounters.HbrgoAct"+act.Item2+"EncounterRegistry",true)!;
            var encounters=(IReadOnlyList<EncounterModel>)registry.GetMethod("GetAll",F)!.Invoke(null,null)!;
            Check(encounters.Count==act.Item3,$"78 act{act.Item1} complete real encounter registry count{act.Item3}");
            foreach(EncounterModel encounter in encounters)
            foreach(MonsterModel monster in encounter.AllPossibleMonsters)
            {
                Type type=monster.GetType();union.Add(type);if(!mapping.TryGetValue(type,out var acts))mapping[type]=acts=[];acts.Add(act.Item1);
                bool expected=type!=typeof(TezukaSaki);
                Check(Expected.Contains(type)==expected,$"78 encounter inventory accounted for act{act.Item1}/{encounter.GetType().Name}/{type.Name}");
                Check(IsCancer(Actor(type))==expected,$"78 actual encounter model classification act{act.Item1}/{encounter.GetType().Name}/{type.Name}");
            }
        }
        Check(union.SetEquals(Expected.Append(typeof(TezukaSaki))),"78 all four native HBR registry catalogs equal39 Cancer plus Tezuka; no missing boss or table class");
        Check(IsCancer(Actor(typeof(RedCrimsonBossMonster)))&&!IsCancer(Actor(typeof(RedCrimson))),"78 third-act RedCrimsonBossMonster distinct from friendly RedCrimson");
        foreach(Type type in union.OrderBy(t=>t.Name))Roster.Add(new{model=type.FullName,acts=mapping[type].Order().ToArray(),cancer=IsCancer(Actor(type)),source="actual native GetAll / AllPossibleMonsters"});
        Roster.Add(new{model=typeof(NararaShibaBossMonster).FullName,acts=Array.Empty<int>(),cancer=false,source="explicit non-Cancer boss outside four-act registry"});
        string doc=File.ReadAllText(Path.Combine(RepositoryRoot,"docs/hbr-enemy-cancer-roster.md"));
        foreach(Type type in Expected)Check(doc.Contains("`"+type.Name+"`",StringComparison.Ordinal),"78 user-facing complete class list contains "+type.Name);
    }
    public static void Run(string report)
    {
        bool ok=false;Exception? failure=null;
        try{Install();IdentityCases();RegistryCases();ok=true;Console.WriteLine("PASS "+Passed.Count);}
        catch(Exception error){failure=error;Console.Error.WriteLine(error);}
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            File.WriteAllText(report,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error=failure?.ToString(),
                assembly=Mod.Location,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),profile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
                nativeSourceSha256=System.Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),nativeRuntimeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
                roster=Roster,fixtureBoundaries=new[]{"Actual Creature/MonsterModel and native four-act GetAll+AllPossibleMonsters; no helper replacement.","Only Godot StringName constructor and logger leaves suppressed; no combat, card, stat or enemy factory patches.","No Godot scene, real network or packaging performed."}},new JsonSerializerOptions{WriteIndented=true}));
        }
        if(!ok)throw new Exception("Enemy Cancer native suite failed",failure);
    }
}
