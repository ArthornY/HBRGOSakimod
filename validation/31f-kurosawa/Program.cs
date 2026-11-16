using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock=HBRGOSakiMod.Core.Models.Relics.Overclock;
static class KurosawaStandalone{public static void Run(string path)=>EffectChecks.RunKurosawa(path);}
static partial class EffectChecks
{
    sealed record KAttack(CardModel Card,decimal Raw,int Hits,int Down,int DpDown);
    sealed record KSegment(CardModel Card,decimal Amp,decimal Coefficient,decimal Final);
    static decimal KObservedBudget;static bool KProbeBudget;
    static readonly List<KAttack> KAttacks=[];static readonly List<KSegment> KSegments=[];
    internal static readonly List<CardModel> KDrawn=[];static bool KFailAttack;
    static void KReset(bool hbr=true,int kinds=0)
    {FReset(kinds,hbr);Player.MaxEnergy=3;Set(Player.PlayerCombatState!,"_energy",50);KAttacks.Clear();KSegments.Clear();KDrawn.Clear();KFailAttack=false;KProbeBudget=false;KObservedBudget=0;Power<KurosawaObserverPower>(Hero,1);}
    static FollowBehindMeHbrgoSaki KCard(params int[] up)=>NewGarden<FollowBehindMeHbrgoSaki>(up);
    static decimal KAmp(CardModel card,Creature? dealer=null,Creature? target=null,ValueProp props=ValueProp.Move)=>(decimal)Call("LeadingChargeCombatHelper","GetDamageAmplificationPercent",card,dealer??Hero,target??Enemy,props)!;
    internal static void KFill(){while(Player.PlayerCombatState!.Hand.Cards.Count<CardPile.MaxCardsInHand)FCard(typeof(FOtherCard));}
    internal static Task KNestedPlay(CardModel card)=>ChasePlay(card,true);
    internal static Task KProbeBody(PlayerChoiceContext context,CardPlay play)
    {
        var cmd=new AttackCommand(6m);MethodInfo from=typeof(AttackCommand).GetMethod("FromCard",F)!;
        Invoke(from,cmd,from.GetParameters().Length==1?new object?[]{play.Card}:new object?[]{play.Card,play});
        cmd.TargetingAllOpponents(Combat);return cmd.Execute(context);
    }
    static void KSkipPileVisuals(ref bool skipVisuals)=>skipVisuals=true;
    static bool KAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result){__result=KResolveAttack(__instance);return false;}
    static async Task<AttackCommand> KResolveAttack(AttackCommand cmd)
    {
        await Hook.BeforeAttack(Combat,cmd);var card=(CardModel)cmd.ModelSource!;
        decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(cmd)!;
        int hits=(int)Hook.ModifyAttackHitCount(Combat,cmd,(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(cmd)!);
        KAttacks.Add(new(card,raw,hits,Enemy.GetPowerAmount<HbrDefenseDownPower>(),Enemy.GetPowerAmount<HbrDpDefenseDownPower>()));
        if(KFailAttack)throw new InvalidOperationException("85 controlled attack fault");
        for(int n=0;n<hits;n++)
        {
            var results=(await Command(cmd.Attacker,raw,ValueProp.Move,card,Combat.HittableEnemies)).ToArray();cmd.AddResultsInternal(results);
            foreach(var result in results)await NewGiven(card,result);
        }
        await Hook.AfterAttack(Combat,new ThrowingPlayerChoiceContext(),cmd);return cmd;
    }
    static bool KDamageLeaf(object[] __args,ref Task<IEnumerable<DamageResult>> __result){__result=KResolveDamage(__args);return false;}
    static async Task<IEnumerable<DamageResult>> KResolveDamage(object[] args)
    {
        var card=args[5]as CardModel;var dealer=args[4]as Creature;var props=(ValueProp)args[3];
        decimal amp=card!=null?KAmp(card,dealer,Enemy,props):0m;
        if(KProbeBudget)KObservedBudget+=(decimal)Call("HbrDestructionGainHelper","GetGain",Enemy,dealer,card,props)!;
        decimal coefficient=card is FollowBehindMeHbrgoSaki?(decimal)Call("KurosawaLeaderCombatHelper","GetDestructionGainMultiplier",card,Enemy,dealer,props)!:1m;
        Set(RunState,"<Rng>k__BackingField",new RunRngSet(NonCriticalSeed));var results=await FDamageBody(args);
        if(card!=null)KSegments.Add(new(card,amp,coefficient,FDamage.Last().Final));return results;
    }
    static object KChoiceContext(Player owner)
    {
        Type type=typeof(PlayerChoiceContext).Assembly.GetType("MegaCrit.Sts2.Core.GameActions.Multiplayer.HookPlayerChoiceContext",true)!;
        var ctor=type.GetConstructors(F).Single(c=>c.GetParameters().Length==3);
        return ctor.Invoke(ctor.GetParameters().Select(p=>p.ParameterType==typeof(Player)?(object)owner:p.ParameterType==typeof(ulong)?owner.NetId:p.ParameterType.IsEnum?Enum.Parse(p.ParameterType,"CombatPlayPhaseOnly"):throw new Exception(p.ToString())).ToArray());
    }
    static async Task KNativeFlush(Player? owner=null)
    {
        owner??=Player;await Hook.BeforeFlush(Combat,owner);object ctx=KChoiceContext(owner);
        MethodInfo method=typeof(CombatManager).GetMethod("FlushPlayerHand",F)!;
        object?[] args=method.GetParameters().Select(p=>p.ParameterType==typeof(Player)?(object)owner:p.ParameterType.IsInstanceOfType(ctx)?ctx:typeof(CombatManager).GetField("_turnState",F)!.GetValue(CombatManager.Instance)).ToArray();
        await(Task)Invoke(method,CombatManager.Instance,args)!;
    }
    static async Task KExtraFlush(bool od)
    {
        if(!od){await(Task)Call("PersonalTurnBoundaryHelper","Flush",new ThrowingPlayerChoiceContext(),Player,Call("OverclockImmediateTurnCoordinator","GetTurnToken",Player))!;return;}
        Type coordinator=Helper("OverclockImmediateTurnCoordinator");object session=coordinator.GetField("_current",F)!.GetValue(null)!;
        await(Task)Invoke(coordinator.GetMethod("FlushOverclockHandCore",F)!,null,session,Player,new ThrowingPlayerChoiceContext())!;
    }
    static void KRegister(Type type){if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
    public static void RunKurosawa(string path)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();Localization();LocalContext.NetId=Player.NetId;var h=new Harmony(ProductionId+".kurosawa85");
            foreach(Type type in FMembers.Concat(new[]{typeof(FOtherCard),typeof(FGlobalCostPower),typeof(ThirtyOneFCollection),typeof(FollowBehindMeHbrgoSaki),typeof(KurosawaAttackProbe),typeof(KurosawaSkillProbe),typeof(KurosawaGenericLeader),typeof(KurosawaObserverPower),typeof(HbrDefenseDownPower),typeof(HbrDpDefenseDownPower),typeof(HbrComboMediumPower),typeof(HighDimensionalShellPower),typeof(Overclock),typeof(ArtifactPower)}))KRegister(type);
            foreach(string name in new[]{"LeadingChargeBeforeCardPatch","LeadingChargeAttackPatch","KurosawaBeforeFlushPatch","KurosawaAfterFlushPatch","HighHitDamageAttackScopePatch","HighHitDamageCommandScopePatch","CardPlayContextPatch","MoreThanOneTruthPlayScopePatch","ModeRestrictedCardPlayPatch","ThirtyOneFStartingHandDrawPatch","CreatureDamageBlockInternalPatch"})
                Check(h.CreateClassProcessor(Mod.GetTypes().Single(t=>t.Name==name)).Patch().Count>0,"85 installs actual production "+name);
            void Patch(MethodBase target,string prefix)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix,F)!));
            foreach(Type type in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FListeners),F)!){priority=Priority.First+200});
            Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState",F)!,nameof(Skip));Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent",F)!,nameof(VisualTask));Patch(AccessTools.PropertyGetter(typeof(PowerModel),"Icon"),nameof(FormPowerIconLeaf));
            foreach(MethodInfo wait in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait"or"CustomScaledWait")&&m.ReturnType==typeof(Task)))Patch(wait,nameof(VisualTask));
            foreach(MethodInfo method in typeof(SfxCmd).GetMethods(F).Where(m=>m.Name=="Play"))Patch(method,nameof(Skip));foreach(MethodInfo method in typeof(ThinkCmd).GetMethods(F).Where(m=>m.Name=="Play"))Patch(method,nameof(Skip));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec",F)!,nameof(CrackClock));Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay",F)!,nameof(ChaseManualPile));
            foreach(MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(m=>m.Name=="Add"))Patch(add,nameof(KSkipPileVisuals));
            Patch(typeof(AttackCommand).GetMethod("Execute",F)!,nameof(KAttackLeaf));h.Unpatch(Canonical,typeof(EffectChecks).GetMethod(nameof(Terminal),F)!);Patch(Canonical,nameof(KDamageLeaf));
            KBasicCases().GetAwaiter().GetResult();KLeadingCases().GetAwaiter().GetResult();KRetainAndDogCases().GetAwaiter().GetResult();KSnapshotCases().GetAwaiter().GetResult();KThirtyOneFCases();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary="Real CardModel.OnPlayWrapper/manual+AutoPlay/GeneratePlayCount and native hooks; actual shared arithmetic and authored attack request; real native BeforeFlush/FlushPlayerHand/AfterFlush/full CardPileCmd.Add and native single-turn Retain/cleanup. Real shared suspension/PopulateCombatState/restore and both inserted flush bodies; input, texture/time/waits/attack animation/canonical damage scene terminal controlled. No Godot renderer/live multiplayer."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Kurosawa85 checks");if(!ok)throw new Exception("Inspect "+path);
    }
}
