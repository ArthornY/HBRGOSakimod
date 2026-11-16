using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
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
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
static class YingxiaCharlottaStandalone { public static void Run(string path)=>EffectChecks.RunYingxiaCharlotta(path); }
static partial class EffectChecks
{
    sealed record YCStrike(CardModel Card, decimal Raw, int Hits, Creature[] Targets, int DefenseBefore);
    sealed record YCSegment(CardModel Card, Creature Target, decimal Final, decimal Coefficient);
    static readonly List<YCStrike> YCStrikes=[];
    static readonly List<YCSegment> YCSegments=[];
    static readonly List<decimal> YCAttackOdGains=[];
    static int[] YCChoices=[0]; static int YCOptionCount; static bool YCRejectSelection; static int YCPeerPlays;
    static bool YCListeners(ref IEnumerable<AbstractModel> __result)
    { __result=Combat.Creatures.SelectMany(c=>c.Powers).Cast<AbstractModel>().Concat(RunState.Players.SelectMany(p=>p.Relics)).Concat(RunState.Players.SelectMany(p=>p.PlayerCombatState?.AllCards??Array.Empty<CardModel>())).ToArray();return false; }
    static void YCReset(bool hbr=true)
    {
        FReset(0,hbr);Player.MaxEnergy=3;Set(Player.PlayerCombatState!,"_energy",30);YCStrikes.Clear();YCSegments.Clear();YCAttackOdGains.Clear();YCChoices=[0];YCRejectSelection=false;YCOptionCount=0;YCPeerPlays=0;
        Set(RunState,"<Rng>k__BackingField",new RunRngSet(NonCriticalSeed));
    }
    static WitnessMySupremacyHbrgoSaki YCard(params int[] upgrades)
    {var card=NewGarden<WitnessMySupremacyHbrgoSaki>(upgrades);Call("HbrMemoryGardenProgression","Publish",card);return card;}
    static LoveCharlottaHbrgoSaki CCard(params int[] upgrades)
    {var card=NewGarden<LoveCharlottaHbrgoSaki>(upgrades);Call("HbrMemoryGardenProgression","Publish",card);return card;}
    static decimal YCPotency(PowerModel p)=>(decimal)Native(p,"get_EffectPotency")!;
    static bool YCSelect(object[] __args, ref Task<IEnumerable<CardModel>> __result)
    {
        var options=__args.OfType<IEnumerable<CardModel>>().First().ToArray();YCOptionCount=options.Length;
        Check(options.All(card=>card is YingxiaFormationChoiceHbrgoSaki&&card.Pile==null),"93 native selection boundary has only temporary formation choices");
        Check(options.Select(card=>card.Title).Distinct().Count()==options.Length,"93 actual choice titles distinguish every formation");
        __result=Task.FromResult<IEnumerable<CardModel>>(YCRejectSelection?Array.Empty<CardModel>():YCChoices.Select(i=>options[i]));return false;
    }
    static bool YCAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=YCResolveAttack(__instance);return false;}
    static async Task<AttackCommand> YCResolveAttack(AttackCommand cmd)
    {
        await Hook.BeforeAttack(Combat,cmd);
        var card=(CardModel)cmd.ModelSource!;decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(cmd)!;
        int hits=(int)Hook.ModifyAttackHitCount(Combat,cmd,(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(cmd)!);
        var single=(Creature?)typeof(AttackCommand).GetField("_singleTarget",F)!.GetValue(cmd);
        var targets=single!=null?new[]{single}:Combat.GetOpponentsOf(cmd.Attacker).Where(e=>e.IsHittable).ToArray();
        var gauge=cmd.Attacker.Player?.GetRelic<Overclock>();decimal odBefore=gauge?.PreciseCharge??0m;
        YCStrikes.Add(new(card,raw,hits,targets,Enemy.GetPowerAmount<HbrDefenseDownPower>()));
        for(int hit=0;hit<hits;hit++)
        {
            var results=(await Command(cmd.Attacker,raw,ValueProp.Move,card,targets)).ToArray();cmd.AddResultsInternal(results);
            foreach(var result in results)await NewGiven(card,result);
        }
        await Hook.AfterAttack(Combat,new ThrowingPlayerChoiceContext(),cmd);if(gauge!=null)YCAttackOdGains.Add(gauge.PreciseCharge-odBefore);return cmd;
    }
    static bool YCDamageLeaf(object[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {__result=YCResolveDamage(__args);return false;}
    static async Task<IEnumerable<DamageResult>> YCResolveDamage(object[] args)
    {
        var source=args[5]as CardModel;var dealer=args[4]as Creature;var props=(ValueProp)args[3];
        decimal coefficient=source is WitnessMySupremacyHbrgoSaki?(decimal)Call("YingxiaFormationCombatHelper","GetDestructionGainMultiplier",source,Enemy,dealer,props)!:
            source is LoveCharlottaHbrgoSaki?(decimal)Call("CharlottaLoveCombatHelper","GetDestructionGainMultiplier",source,Enemy,dealer,props)!:1m;
        Set(RunState,"<Rng>k__BackingField",new RunRngSet(NonCriticalSeed));int before=FDamage.Count;var result=await FDamageBody(args);
        if(source!=null)foreach(var segment in FDamage.Skip(before))YCSegments.Add(new(source,segment.Target,segment.Final,coefficient));return result;
    }
    static async Task YCStart(bool inserted=false,Player? owner=null)
    {
        owner??=Player;await PlayerCmd.SetEnergy(3,owner);
        if(inserted)await(Task)Call("HbrInsertedTurnStartHelper","Start",new ThrowingPlayerChoiceContext(),owner)!;
        else await Hook.AfterEnergyReset(Combat,owner);
    }
    static void YCRegister(Type type)=>KRegister(type);
    public static void RunYingxiaCharlotta(string path)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();Localization();LocalContext.NetId=Player.NetId;var h=new Harmony(ProductionId+".yingxia-charlotta93-94");
            foreach(var type in FMembers.Concat(new[]{typeof(FOtherCard),typeof(YCNongardenSkill),typeof(ThirtyOneFCollection),typeof(ThirtyOneXCollection),typeof(Overclock),typeof(WitnessMySupremacyHbrgoSaki),typeof(YingxiaFormationChoiceHbrgoSaki),typeof(LoveCharlottaHbrgoSaki),typeof(PallasCatDollHbrgoSaki),typeof(YingxiaRegenerationFormationPower),typeof(CharlottaEngagementRingPower),typeof(HbrEternalVowPower),typeof(HbrAttackUpPower),typeof(HbrCriticalRateUpPower),typeof(HbrCriticalDamageUpPower),typeof(HbrDefenseUpPower),typeof(HbrDefenseDownPower),typeof(HbrComboSmallPower),typeof(FragilePower),typeof(DeflectorProtection),typeof(HighDimensionalShellPower),typeof(PhantomHighDimensionalShellPower),typeof(PhantomPower),typeof(ArtifactPower),typeof(YCReplayPower)}))YCRegister(type);
            foreach(string name in new[]{"CharlottaJealousyPatch","HighHitDamageAttackScopePatch","HighHitDamageCommandScopePatch","CardPlayContextPatch","MoreThanOneTruthPlayScopePatch","ModeRestrictedCardPlayPatch","CreatureDamageBlockInternalPatch","PhantomBeforeDamageReceivedPatch"})
                Check(h.CreateClassProcessor(Mod.GetTypes().Single(type=>type.Name==name)).Patch().Count>0,"93-94 installs actual production "+name);
            void Patch(MethodBase target,string prefix)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix,F)!));
            foreach(Type type in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(YCListeners),F)!){priority=Priority.First+200});
            Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState",F)!,nameof(Skip));Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent",F)!,nameof(VisualTask));Patch(AccessTools.PropertyGetter(typeof(PowerModel),"Icon"),nameof(FormPowerIconLeaf));
            foreach(MethodInfo wait in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait"or"CustomScaledWait")&&m.ReturnType==typeof(Task)))Patch(wait,nameof(VisualTask));
            foreach(var type in new[]{typeof(SfxCmd),typeof(ThinkCmd)})foreach(var method in type.GetMethods(F).Where(m=>m.Name=="Play"))Patch(method,nameof(Skip));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec",F)!,nameof(CrackClock));Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay",F)!,nameof(ChaseManualPile));foreach(var add in typeof(CardPileCmd).GetMethods(F).Where(m=>m.Name=="Add"))Patch(add,nameof(KSkipPileVisuals));
            Patch(typeof(MegaCrit.Sts2.Core.Commands.CardSelectCmd).GetMethod("FromSimpleGrid",F)!,nameof(YCSelect));Patch(typeof(AttackCommand).GetMethod("Execute",F)!,nameof(YCAttackLeaf));h.Unpatch(Canonical,typeof(EffectChecks).GetMethod(nameof(Terminal),F)!);Patch(Canonical,nameof(YCDamageLeaf));
            YCFormationCases().GetAwaiter().GetResult();YCCharlottaCases().GetAwaiter().GetResult();YCDelayedCases().GetAwaiter().GetResult();YCMetadataCases();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real native card wrapper, payment, AutoPlay, replay and hooks; actual shared buff/debuff/DP/power counters and arithmetic. Controlled native selection result and attack-animation/canonical scene terminal; real target/dealer context and common inserted-turn start. No Godot renderer or live network."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Yingxia/Charlotta checks");if(!ok)throw new Exception("Inspect "+path);
    }
}
public sealed class YCReplayPower:PowerModel
{
    public int Repeats;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
    public override int ModifyCardPlayCount(CardModel card,Creature target,int count)=>count+(card is LoveCharlottaHbrgoSaki?Repeats:0);
}
public sealed class YCNongardenSkill:CardModel
{
    public override string Title=>"Native non-garden skill fixture";
    public YCNongardenSkill():base(0,CardType.Skill,CardRarity.Common,TargetType.Self){}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>Task.CompletedTask;
}
