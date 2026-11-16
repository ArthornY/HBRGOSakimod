using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock=HBRGOSakiMod.Core.Models.Relics.Overclock;

static class ThirtyOneFStandalone {public static void Run(string path)=>EffectChecks.RunThirtyOneF(path);}
static partial class EffectChecks
{
    static readonly Type[] FMembers=[typeof(FMember0),typeof(FMember1),typeof(FMember2),typeof(FMember3),typeof(FMember4),typeof(FMember5)];
    static readonly List<(Creature Dealer,Creature Target,decimal Final,decimal Blocked,decimal Lost)> FDamage=[];
    static readonly List<(Creature Dealer,Creature Target,decimal Raw,int Hits,decimal Destruction,decimal Final)> FCounters=[];
    static readonly List<(CardModel Card,int Cost)> FDrawObservations=[];
    static bool FBlockDraw,FKillAttackerAfterHit,FThrowMove,FUseNullDealer;
    internal static bool FNestedArmed;
    static CardModel? FNestedProbe;
    static int FNestedObserved;
    static Task? FGate;
    static Creature[]? FTargets;
    static bool FListeners(ref IEnumerable<AbstractModel> __result)
    {
        __result=Combat.Creatures.SelectMany(c=>c.Powers).Cast<AbstractModel>()
            .Concat(RunState.Players.SelectMany(p=>p.Relics).Where(r=>r is ThirtyOneFCollection or Overclock))
            .Concat(RunState.Players.SelectMany(p=>p.PlayerCombatState?.AllCards??Array.Empty<CardModel>())).ToArray();return false;
    }
    static ThirtyOneFCollection FReset(int kinds,bool hbr=true)
    {
        NewReset();GNextBattle();Set(RunState,"<ActFloor>k__BackingField",1);H("Mode",hbr,1);Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki>());
        Set(RunState,"_allCards",new List<CardModel>());Set(Player,"<NetId>k__BackingField",1UL);
        Set(Player.PlayerCombatState!,"_energy",3);Set(Enemy,"_currentHp",5000000);Set(Enemy,"_maxHp",5000000);
        Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Player);Set(Combat,"<RoundNumber>k__BackingField",1);
        FDamage.Clear();FCounters.Clear();FDrawObservations.Clear();FNestedArmed=false;FNestedProbe=null;FNestedObserved=-1;FBlockDraw=FKillAttackerAfterHit=FThrowMove=FUseNullDealer=false;FGate=null;FTargets=null;
        var relic=(ThirtyOneFCollection)ModelDb.Relic<ThirtyOneFCollection>().ToMutable();Set(relic,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(relic).ToList());
        for(int i=0;i<kinds;i++)FOrigin(i,Player);
        relic.BeforeCombatStart().GetAwaiter().GetResult();return relic;
    }
    static CardModel FOrigin(int index,Player? owner=null)
    {
        owner??=Player;var card=(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(FMembers[index])).ToMutable();Set(card,"_owner",owner);owner.Deck.AddInternal(card);
        ((List<CardModel>)typeof(RunState).GetField("_allCards",F)!.GetValue(RunState)!).Add(card);return card;
    }
    static CardModel FCard(Type type,Player? owner=null,bool hand=true)
    {
        owner??=Player;var card=(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();Set(card,"_owner",owner);
        (hand?owner.PlayerCombatState!.Hand:owner.PlayerCombatState!.DrawPile).AddInternal(card);
        ((List<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Add(card);return card;
    }
    static bool FShouldDrawLeaf(ref bool __result,ref AbstractModel __3){if(!FBlockDraw)return true;__3=Player.GetRelic<ThirtyOneFCollection>()!;__result=false;return false;}
    static bool FAddLeaf(object[] __args,ref Task<CardPileAddResult> __result)
    {
        // Scene movement leaf only: real card/pile mutation; native Draw/History/AfterCardDrawn remain.
        var card=(CardModel)__args[0];var pile=(CardPile)__args[1];CardPile? old=card.Pile;
        old?.RemoveInternal(card);pile.AddInternal(card);
        __result=Task.FromResult(default(CardPileAddResult));return false;
    }
    static void FObserveDraw(object[] __args)
    {if(__args.OfType<CardModel>().FirstOrDefault() is {} card)FDrawObservations.Add((card,card.EnergyCost.GetWithModifiers(CostModifiers.All)));}
    static Task FSetupTurn()
    {
        MethodInfo method=typeof(CombatManager).GetMethod("SetupPlayerTurn",F)!;
        Type ctxType=typeof(PlayerChoiceContext).Assembly.GetType("MegaCrit.Sts2.Core.GameActions.Multiplayer.HookPlayerChoiceContext",true)!;
        var ctor=ctxType.GetConstructors(F).Single(c=>c.GetParameters().Length==3);
        object context=ctor.Invoke(ctor.GetParameters().Select(p=>p.ParameterType==typeof(Player)?(object)Player:p.ParameterType==typeof(ulong)?Player.NetId:
            p.ParameterType.IsEnum?Enum.Parse(p.ParameterType,"CombatPlayPhaseOnly"):throw new Exception(p.ToString())).ToArray());
        object?[] args=method.GetParameters().Select(p=>p.ParameterType==typeof(Player)?(object)Player:p.ParameterType==ctxType?context:
            typeof(CombatManager).GetField("_turnState",F)!.GetValue(CombatManager.Instance)).ToArray();
        return (Task)Invoke(method,CombatManager.Instance,args)!;
    }
    internal static async Task FNestedDraw(PlayerChoiceContext context,CardModel card,bool hand)
    {
        if(!FNestedArmed||!hand)return;FNestedArmed=false;await CardPileCmd.Draw(context,1m,Player,fromHandDraw:true);FNestedObserved=FCost(FNestedProbe!);
    }
    static bool FDamageLeaf(object[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {__result=FDamageBody(__args);return false;}
    static async Task<IEnumerable<DamageResult>> FDamageBody(object[] args)
    {
        if(FGate!=null)await FGate;
        Type api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!;
        var targets=(IEnumerable<Creature>)Invoke(api.GetMethod("ReadTargets",F)!,null,new object?[]{args})!;
        Creature? dealer=args[4] as Creature;ValueProp props=(ValueProp)args[3];decimal amount=(decimal)args[2];var result=new List<DamageResult>();
        foreach(Creature target in targets)
        {
            decimal final=dealer?.Side==CombatSide.Player?Actual(target,dealer,amount,props,args[5] as CardModel):amount;
            await (Task)NativeHookBeforeReceive(target,dealer,final,props)!;
            Creature blockReceiver=target.PetOwner?.Creature??target;
            decimal blocked=blockReceiver.DamageBlockInternal(final,props);decimal lost=Math.Max(0m,final-blocked);
            var hit=target.LoseHpInternal(lost,props);Set(hit,"<BlockedDamage>k__BackingField",(int)blocked);
            Call("ItsMygoIncomingDamageCapture","AppendResults",result,new[]{hit});FDamage.Add((dealer,target,final,blocked,lost));
            if(dealer?.Side==CombatSide.Player)
            {
                MethodInfo given=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterDamageGiven");
                object?[] par=given.GetParameters().Select(p=>p.ParameterType.IsInstanceOfType(RunState)?(object)RunState:p.ParameterType.IsInstanceOfType(Combat)?Combat:
                    typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType)?new ThrowingPlayerChoiceContext():p.ParameterType==typeof(DamageResult)?hit:p.ParameterType==typeof(ValueProp)?props:
                    p.ParameterType==typeof(Creature)?p.Name=="dealer"?dealer:target:p.ParameterType==typeof(CardModel)?null:throw new Exception(p.ToString())).ToArray();
                await (Task)Invoke(given,null,par)!;
            }
            if(FKillAttackerAfterHit&&dealer?.Side==CombatSide.Enemy)Set(dealer,"_currentHp",0);
        }
        return result;
    }
    static object? NativeHookBeforeReceive(Creature target,Creature? dealer,decimal amount,ValueProp props)
    {
        MethodInfo method=typeof(Hook).GetMethods(F).Single(m=>m.Name=="BeforeDamageReceived");
        object?[] args=method.GetParameters().Select(p=>p.ParameterType.IsInstanceOfType(RunState)?(object)RunState:p.ParameterType.IsInstanceOfType(Combat)?Combat:
            typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType)?new ThrowingPlayerChoiceContext():p.ParameterType==typeof(Creature)?p.Name=="dealer"?dealer:target:
            p.ParameterType==typeof(decimal)?amount:p.ParameterType==typeof(ValueProp)?props:p.ParameterType==typeof(CardModel)?null:throw new Exception(p.ToString())).ToArray();
        return Invoke(method,null,args);
    }
    static bool FCounterLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {
        if(__instance.ModelSource is not ThirtyOneFCollection)return true;__result=FCounterBody(__instance);return false;
    }
    static async Task<AttackCommand> FCounterBody(AttackCommand command)
    {
        Check(command.ModelSource is ThirtyOneFCollection&&command.ModelSource is not CardModel,"79 counter source is relic, never a played card");
        Creature dealer=command.Attacker;Creature target=(Creature)typeof(AttackCommand).GetField("_singleTarget",F)!.GetValue(command)!;
        await (Task)MisatoHook("BeforeAttack",command)!;
        decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!;int hits=(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!;
        object hitScope=Call("HighHitDamageVisualHelper","BeginDamage",ValueProp.Move,dealer,null)!;
        decimal coefficient;
        try{coefficient=(decimal)Call("ThirtyOneFDefenseCombatHelper","GetDestructionGainMultiplier",target,dealer,ValueProp.Move)!;
            Equal((decimal)Call("ThirtyOneFDefenseCombatHelper","GetDestructionGainMultiplier",target,dealer,ValueProp.Unpowered)!,1m,"79 unpowered nested counter does not inherit destruction");}
        finally{Call("HighHitDamageVisualHelper","CloseDamage",hitScope);}
        var results=await Command(dealer,raw,ValueProp.Move,null,new[]{target});
        ((List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!).Add(results.ToList());
        FCounters.Add((dealer,target,raw,hits,coefficient,FDamage.Last().Final));await (Task)MisatoHook("AfterAttack",command)!;return command;
    }
    static Task FMove(params decimal[] damages)=>FMoveTo(damages,FTargets??new[]{Hero},ValueProp.Move);
    static Task FMoveTo(decimal[] damages,Creature[] targets,ValueProp props,Creature? nested=null)
    {
        Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Enemy);Set(Enemy.Monster!,"<Id>k__BackingField",ModelDb.GetId(Enemy.Monster!.GetType()));
        Set(Enemy.Monster!,"<NextMove>k__BackingField",new MoveState("31F_TRUE_NATIVE_MOVE",async _=>
        {foreach(decimal damage in damages)await Command(FUseNullDealer?null:nested??Enemy,damage,props,null,targets);if(FThrowMove)throw new InvalidOperationException("79 controlled action fault");}));
        return Enemy.Monster!.PerformMove();
    }
    public static void RunThirtyOneF(string path)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".31f79");
            foreach(Type type in FMembers.Concat(new[]{typeof(FMemberX),typeof(FMemberBlocked),typeof(FOtherCard),typeof(FGlobalCostPower),typeof(FNestedDrawPower),typeof(ThirtyOneFCollection),typeof(SmallHopper),typeof(Overclock),typeof(PhantomPower),typeof(HighDimensionalShellPower),typeof(BufferPower),typeof(PhantomHighDimensionalShellPower),typeof(MomentOfReign),typeof(HbrAttackUpPower),typeof(HbrCriticalDamageUpPower)}))
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string name in new[]{"ThirtyOneFDescriptionPatch","ThirtyOneFStartingHandDrawPatch","CreatureDamageBlockInternalPatch","ThirtyOneBEnemyMoveScopePatch","ThirtyOneBEnemyActualTargetPatch","PhantomDamageTargetPatch","PhantomBeforeDamageReceivedPatch","HighHitDamageAttackScopePatch","HighHitDamageCommandScopePatch"})
                Check(h.CreateClassProcessor(Mod.GetTypes().Single(t=>t.Name==name)).Patch().Count>0,"79 installs production "+name);
            foreach(Type type in new[]{typeof(RunState),typeof(CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FListeners),F)!){priority=Priority.First+200});
            foreach(MethodInfo sfx in typeof(SfxCmd).GetMethods(F).Where(m=>m.Name=="Play"))h.Patch(sfx,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            foreach(MethodInfo think in typeof(ThinkCmd).GetMethods(F).Where(m=>m.Name=="Play"))h.Patch(think,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            h.Patch(typeof(Hook).GetMethod("ShouldDraw")!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FShouldDrawLeaf),F)!));
            h.Patch(typeof(Hook).GetMethod("AfterCardDrawn")!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FObserveDraw),F)!));
            h.Patch(typeof(CardPileCmd).GetMethods(F).Single(m=>m.Name=="Add"&&m.GetParameters() is var a&&a.Length==5&&a[0].ParameterType==typeof(CardModel)&&a[1].ParameterType==typeof(CardPile)),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FAddLeaf),F)!));
            h.Unpatch(Canonical,typeof(EffectChecks).GetMethod(nameof(Terminal),F)!);h.Patch(Canonical,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FDamageLeaf),F)!){priority=Priority.Last});
            h.Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FCounterLeaf),F)!){priority=Priority.Last});
            FMetadataCases();FOpeningCases().GetAwaiter().GetResult();FCostCases().GetAwaiter().GetResult();FDefenseCases().GetAwaiter().GetResult();FCoopCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary="Real native SetupPlayerTurn/Innate/Draw/History/AfterCardDrawn; actual per-turn cost and cleanup. Real Monster.PerformMove, canonical Damage scopes, DamageBlockInternal and resource mutation; incoming final damage, scene card Add and counter animation leaves controlled. Actual counter AttackCommand source+shared damage pipeline+afterdamage OD. No Godot scene or live network."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" 31F79 checks");
    }
}
