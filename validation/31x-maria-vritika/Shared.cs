using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock=HBRGOSakiMod.Core.Models.Relics.Overclock;

public sealed class X91ReplayPower:PowerModel
{
    public int Replays;
    public override PowerType Type=>PowerType.Buff;
    public override PowerStackType StackType=>PowerStackType.Counter;
    public override int ModifyCardPlayCount(CardModel card,Creature target,int amount)=>card.Owner==Owner.Player?amount+Replays:amount;
}
public sealed class X92Attack:CardModel
{public X92Attack():base(0,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy){}public override string Title=>"Calculation attack";protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>EffectChecks.X92Probe(context,play);}
public sealed class X92Skill:CardModel
{public X92Skill():base(0,CardType.Skill,CardRarity.Common,TargetType.Self){}public override string Title=>"Calculation skill";protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>EffectChecks.X92Probe(context,play);}
public sealed class X92Power:CardModel
{public X92Power():base(0,CardType.Power,CardRarity.Common,TargetType.Self){}public override string Title=>"Calculation power";protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>EffectChecks.X92Probe(context,play);}
static partial class EffectChecks
{
    sealed record X91Hit(CardModel? Source,decimal Raw,decimal Final,decimal Calculation,decimal Knock,decimal Destruction);
    static readonly List<X91Hit> X91Hits=[];
    static readonly List<(CardModel Source,decimal Bonus)> X92Bonuses=[];
    static readonly List<decimal> X91Heals=[];
    static int X91Energy,X91Draws;
    static bool X91NativeDamage,X91Throw;
    static Func<PlayerChoiceContext,CardPlay,Task>? X92Action;
    public static async Task X92Probe(PlayerChoiceContext context,CardPlay play)
    {X92Bonuses.Add((play.Card,(decimal)Call("VritikaCalculationCombatHelper","GetCalculationBonusPercent",play.Card,play.Card.Owner.Creature,ValueProp.Move)!));if(X92Action!=null)await X92Action(context,play);}
    static bool X91Listeners(ref IEnumerable<AbstractModel> __result)
    {__result=Combat.Creatures.SelectMany(unit=>unit.Powers).Cast<AbstractModel>().Concat(RunState.Players.SelectMany(player=>player.Relics).Where(relic=>relic is DeflectorProtection or Overclock)).Concat(RunState.Players.SelectMany(player=>player.PlayerCombatState?.AllCards??Array.Empty<CardModel>())).ToArray();return false;}
    static bool X91Draw(object[] __args,ref Task<IEnumerable<CardModel>> __result)
    {X91Draws+=(int)(decimal)__args[1];__result=Task.FromResult<IEnumerable<CardModel>>([]);return false;}
    static bool X91GainEnergy(decimal amount,Player player,ref Task __result)
    {X91Energy+=(int)amount;Set(player.PlayerCombatState!,"_energy",player.PlayerCombatState!.Energy+(int)amount);__result=Task.CompletedTask;return false;}
    static bool X91HealInternal(Creature __instance,decimal __0){X91Heals.Add(__0);return false;}
    static bool X91ScenePile(object[] __args,ref bool skipVisuals){skipVisuals=true;return true;}
    static bool X91DamageLeaf(object[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {if(X91NativeDamage)return true;__result=X91Damage(__args);return false;}
    static async Task<IEnumerable<DamageResult>> X91Damage(object[] args)
    {
        var targets=(IEnumerable<Creature>)Invoke(Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!.GetMethod("ReadTargets",F)!,null,new object?[]{args})!;
        decimal raw=(decimal)args[2];ValueProp props=(ValueProp)args[3];Creature? dealer=args[4]as Creature;CardModel? card=args[5]as CardModel;
        if(X91Throw)throw new InvalidOperationException("controlled91/92 damage failure");var results=new List<DamageResult>();
        foreach(Creature target in targets)
        {
            decimal calculation=(decimal)Call("VritikaCalculationCombatHelper","GetCalculationBonusPercent",card,dealer,props)!;
            decimal knock=(decimal)Call("MariaRepentanceCombatHelper","GetDamageAmplificationPercent",card,dealer,target,props)!;
            string helper=card is RepentanceTimeHbrgoSaki?"MariaRepentanceCombatHelper":"VritikaCalculationCombatHelper";
            decimal destruction=(decimal)Call(helper,"GetDestructionGainMultiplier",card,target,dealer,props)!;
            X91Hits.Add(new(card,raw,Actual(target,dealer,raw,props,card),calculation,knock,destruction));
            var hit=new DamageResult(target,props);Set(hit,"<UnblockedDamage>k__BackingField",1);Call("ItsMygoIncomingDamageCapture","AppendResults",results,new[]{hit});
            if(card!=null)await NewGiven(card,hit);
        }
        return results;
    }
    static void X91Reset(bool hbr=true)
    {
        NewReset();Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki>());H("Mode",hbr,1);Set(RunState,"_players",new List<Player>{Player});
        Native(Player.GetRelic<ElectronicSoldierManual>()!,"EndMariaRepentanceBattle");
        Set(Combat,"_allies",new List<Creature>{Hero});Set(Combat,"_enemies",new List<Creature>{Enemy});Set(Combat,"_allCards",new List<CardModel>());
        Set(Player,"<NetId>k__BackingField",1UL);Set(Player.PlayerCombatState!,"_energy",30);Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",1);
        Set(Hero,"_currentHp",5000);Set(Hero,"_maxHp",5000);Set(Hero,"_block",0);Set(Enemy,"_currentHp",5000);Set(Enemy,"_maxHp",5000);Set(Enemy,"_block",0);
        foreach(var pair in new[]{("MariaRepentanceCombatHelper","Lifesaves"),("MariaRepentanceCombatHelper","Inspiration"),("VritikaCalculationCombatHelper","Battles"),("YanagiButlerCombatHelper","Battles")})
        {object table=Helper(pair.Item1).GetField(pair.Item2,F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);}
        X91Hits.Clear();X92Bonuses.Clear();X91Heals.Clear();X91Energy=X91Draws=0;X91NativeDamage=X91Throw=false;X92Action=null;
        Helper("OverclockImmediateTurnCoordinator").GetField("_current",F)!.SetValue(null,null);Helper("PersonalImmediateTurnCoordinator").GetField("_current",F)!.SetValue(null,null);
    }
    static async Task X91Play(CardModel card,bool automatic=false)
    {
        if(automatic)await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(),card,Enemy,skipCardPileVisuals:true);
        else{var paid=await card.SpendResources();await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(),card.TargetType==TargetType.AnyEnemy?Enemy:null,false,new ResourceInfo{EnergySpent=paid.Item1,EnergyValue=paid.Item1,StarsSpent=paid.Item2,StarValue=paid.Item2},true);}
    }
    static Task<IEnumerable<DamageResult>> X91Command(Creature dealer,decimal amount,ValueProp props,CardModel? source,params Creature[] targets)
    {
        Type compatibility=Mod.GetType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility",true)!;
        MethodInfo entry=compatibility.GetMethods(F).Single(method=>method.Name=="Damage"&&method.GetParameters().Length==7&&method.GetParameters()[1].ParameterType==typeof(IEnumerable<Creature>));
        return (Task<IEnumerable<DamageResult>>)Invoke(entry,null,new ThrowingPlayerChoiceContext(),targets,amount,props,dealer,source,null)!;
    }
    static void X91Register(Type type)
    {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
    static void X91SaveType(Type type)
    {
        var assembly=typeof(SavedProperties).Assembly;
        if(assembly.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache")is{} stable){Invoke(stable.GetMethod("InjectTypeIntoCache",F)!,null,type);return;}
        var cache=assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;Invoke(cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!,null,type);cache.GetField("_initialized",F)!.SetValue(null,true);
    }
    public static void X91Run(string path,Func<Harmony,Task> cases)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();Localization();LocalContext.NetId=Player.NetId;var h=new Harmony(ProductionId+".maria-vritika");
            foreach(Type type in new[]{typeof(RepentanceTimeHbrgoSaki),typeof(NumbersAreTheSourceHbrgoSaki),typeof(MariaRepentancePower),typeof(VritikaCalculationPower),typeof(HbrFaithPower),typeof(HbrAttackUpPower),typeof(HbrAttackDownPower),typeof(HbrDefenseUpPower),typeof(HbrCalamityPower),typeof(HighDimensionalShellPower),typeof(PhantomHighDimensionalShellPower),typeof(PhantomPower),typeof(LargeHealthPower),typeof(X91ReplayPower),typeof(X92Attack),typeof(X92Skill),typeof(X92Power),typeof(StrikeIronclad),typeof(DefendIronclad),typeof(Inflame),typeof(Bloodletting),typeof(GalvanicPower),typeof(Galvanized),typeof(SmallHopper),typeof(MomentOfReign),typeof(DeflectorProtection),typeof(Overclock)})X91Register(type);
            foreach(string name in new[]{"HbrEffectApplicationPatch","HighHitDamageAttackScopePatch","HighHitDamageCommandScopePatch","CardPlayContextPatch","ModeRestrictedCardPlayPatch","VritikaCalculationPlayCleanupPatch","VritikaCalculationOrdinaryTurnEndPatch","YanagiPreparedHealCallsitePatch","CreatureDamageBlockInternalPatch","LargeCreatureLoseHpPatch","LargeCreatureSetCurrentPatch","PhantomDamageTargetPatch","PhantomBeforeDamageReceivedPatch","PhantomUnblockedDamageTargetPatch","MariaLifesaveDamagePatch"})
                Check(h.CreateClassProcessor(Mod.GetTypes().Single(type=>type.Name==name)).Patch().Count>0,"installs production "+name);
            void Patch(MethodBase method,string prefix,int priority=Priority.Normal)=>h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix,F)!){priority=priority});
            foreach(Type type in new[]{RunState.GetType(),typeof(CombatState)})Patch(type.GetMethod("IterateHookListeners",F)!,nameof(X91Listeners),Priority.First+100);
            Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));Patch(AccessTools.PropertyGetter(typeof(PowerModel),"Icon"),nameof(FormPowerIconLeaf));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState",F)!,nameof(Skip));Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent",F)!,nameof(VisualTask));
            foreach(MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method=>method.Name is"Wait"or"CustomScaledWait"))Patch(wait,nameof(VisualTask));
            foreach(MethodInfo sound in typeof(SfxCmd).GetMethods(F).Where(method=>method.Name is"Play"or"PlayDamage"))Patch(sound,nameof(Skip));
            foreach(MethodInfo anim in typeof(CreatureCmd).GetMethods(F).Where(method=>method.Name=="TriggerAnim"&&method.ReturnType==typeof(Task)))Patch(anim,nameof(VisualTask));
            foreach(MethodInfo visual in typeof(VfxCmd).GetMethods(F).Where(method=>method.Name is"PlayOnCreatureCenter"or"PlayOnSide"))Patch(visual,nameof(Skip));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec",F)!,nameof(CrackClock));Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay",F)!,nameof(ChaseManualPile));
            foreach(MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(method=>method.Name=="Add"&&method.GetParameters()[0].ParameterType==typeof(CardModel)))Patch(add,nameof(X91ScenePile));
            foreach(MethodInfo draw in typeof(CardPileCmd).GetMethods(F).Where(method=>method.Name=="Draw"&&method.GetParameters().Length==4))Patch(draw,nameof(X91Draw));
            Patch(typeof(PlayerCmd).GetMethod("GainEnergy",F)!,nameof(X91GainEnergy));Patch(typeof(Creature).GetMethod("HealInternal",F)!,nameof(X91HealInternal));
            var history=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory",true)!;
            foreach(MethodInfo entry in history.GetMethods(F).Where(method=>method.Name is"CreatureAttacked"or"DamageReceived"or"CardPlayStarted"or"CardPlayFinished"or"BlockGained"))Patch(entry,nameof(Skip));
            h.Unpatch(Canonical,typeof(EffectChecks).GetMethod(nameof(Terminal),F)!);Patch(Canonical,nameof(X91DamageLeaf),Priority.Last);
            cases(h).GetAwaiter().GetResult();ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual upgraded card bodies/native OnPlayWrapper/replays, PowerCmd and Before/AfterCardPlayed, common damage/block/recovery and native HP-write guard boundaries. Scene movement, visuals/history, energy/draw/HP-heal writes are controlled leaves; outgoing damage terminal controlled, selected incoming canonical Damage body executes natively. No Godot/rendering/network/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+"31X91/92checks");
    }
}
