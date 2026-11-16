using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock=HBRGOSakiMod.Core.Models.Relics.Overclock;

static class NatsumeStandalone {public static void Run(string path)=>EffectChecks.RunNatsume(path);}
static partial class EffectChecks
{
    sealed record NatsumeHit(CardModel Card,decimal Raw,decimal Actual,decimal Amp,decimal Destruction,bool AttackUp,bool CriticalUp);
    static readonly List<NatsumeHit> NatsumeHits=[];
    static readonly List<(Creature Target,CardModel? Native,CardModel? Scoped,bool Prevented)> NatsumeDeaths=[];
    static readonly List<(int Rows,int FinalKills)> NatsumeCompletions=[];
    static bool NatsumeNested,NatsumeInsideNested,NatsumeThrow,NatsumeCannon;
    static Creature? NatsumeNestedOrigin,NatsumeNestedVictim,NatsumeCancelDeath;
    static void NatsumeNestedDeathPostfix(object[] __args,ref Task __result) =>
        __result=NatsumeNestedDeath(__result,__args.OfType<Creature>().Single());
    static async Task NatsumeNestedDeath(Task original,Creature target)
    {
        await original;
        if(target!=NatsumeNestedOrigin||NatsumeNestedVictim is not {} victim)return;
        NatsumeNestedVictim=null;
        Check(Call("NatsumeCondemnationCombatHelper","GetCondemnationSource",Hero)==null,"HP cost or owner target never borrows Condemnation credit");
        await CreatureCmd.Kill(victim);
    }
    static bool NatsumeCancelBeforeDeath(Creature creature,ref Task __result)
    {
        if(creature!=NatsumeCancelDeath)return true;
        __result=Task.FromCanceled(new CancellationToken(true));return false;
    }
    sealed class NatsumeAfterAttack : PowerModel
    {
        public override PowerType Type=>PowerType.Buff;
        public override PowerStackType StackType=>PowerStackType.Counter;
        public override Task AfterAttack(PlayerChoiceContext context,AttackCommand command)
        {
            if(command.ModelSource is LegacyFlashHbrgoSaki)
                NatsumeCompletions.Add((command.Results.Count(),command.Results.SelectMany(row=>row).Count(result=>result.WasTargetKilled)));
            return Task.CompletedTask;
        }
    }
    static bool NatsumeBodyLeaf(IEnumerable<Creature> targets,decimal raw,ValueProp props,Creature? dealer,CardModel? card,ref Task<IEnumerable<DamageResult>> __result)
    {
        if(card is not LegacyFlashHbrgoSaki)return true;
        __result=NatsumeBody(targets,raw,props,dealer!,card);return false;
    }
    static async Task<IEnumerable<DamageResult>> NatsumeBody(IEnumerable<Creature> targets,decimal raw,ValueProp props,Creature dealer,CardModel card)
    {
        await Task.Yield();var rows=new List<DamageResult>();
        foreach(var target in targets)
        {
            decimal amp=(decimal)Call("NatsumeCondemnationCombatHelper","GetDamageAmplificationPercent",card,dealer,target,props)!;
            decimal coefficient=(decimal)Call("NatsumeCondemnationCombatHelper","GetDestructionGainMultiplier",card,target,dealer,props)!;
            if(NatsumeInsideNested)
            {Equal(amp,0m,"same-source nested raw effect cannot borrow first-card amplification");Equal(coefficient,1m,"same-source nested effect cannot borrow card destruction1point5");}
            if(NatsumeThrow)throw new Exception("controlled Natsume terminal failure");
            if(NatsumeNested&&!NatsumeInsideNested)
            {NatsumeInsideNested=true;try{await Command(dealer,raw,props,card,[target]);}finally{NatsumeInsideNested=false;}}
            NatsumeHits.Add(new(card,raw,Actual(target,dealer,raw,props,card),amp,coefficient,Hero.HasPower<HbrAttackUpPower>(),Hero.HasPower<HbrCriticalRateUpPower>()));
            var result=new DamageResult(target,props);Call("ItsMygoIncomingDamageCapture","AppendResults",rows,new List<DamageResult>{result});await NewGiven(card,result);
        }
        return rows;
    }
    static void NatsumeDeathObserve(object[] __args)
    {
        Creature target=__args.OfType<Creature>().Single();
        NatsumeDeaths.Add((target,__args.OfType<CardModel>().FirstOrDefault(),(CardModel?)Call("NatsumeCondemnationCombatHelper","GetCondemnationSource",target),__args.OfType<bool>().Single()));
    }
    static void NatsumeCannonObserve(object[] __args)
    {
        if(!NatsumeCannon||__args[5] is not CardModel card)return;
        Equal((decimal)Call("NatsumeCondemnationCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,0m,"actual cannon retaining the Natsume card cannot borrow first-card amplification");
        Equal((decimal)Call("NatsumeCondemnationCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,1m,"actual cannon retains its own destruction coefficient");
    }
    static void NatsumeReset()
    {
        NewReset();NatsumeHits.Clear();NatsumeDeaths.Clear();NatsumeCompletions.Clear();NatsumeNested=NatsumeInsideNested=NatsumeThrow=NatsumeCannon=false;
        NatsumeNestedOrigin=NatsumeNestedVictim=NatsumeCancelDeath=null;
        Set(RunState,"_players",new List<Player>{Player});Set(Combat,"_enemies",new List<Creature>{Enemy});Set(Combat,"_allies",new List<Creature>{Hero});Set(Player.PlayerCombatState!,"_energy",10);
        Call("LargeCreatureHealthHelper","SetMaxHp",Enemy,1000m);Call("LargeCreatureHealthHelper","SetCurrentHp",Enemy,1000m);
        object table=Helper("NatsumeCondemnationCombatHelper").GetField("Battles",F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);Power<NatsumeAfterAttack>(Hero,1);
    }
    static async Task NatsumeBefore(CardPlay play)
    {
        MethodInfo hook=typeof(Hook).GetMethods(F).Single(method=>method.Name=="BeforeCardPlayed");
        object?[] args=hook.GetParameters().Select(parameter=>parameter.ParameterType.IsInstanceOfType(Combat)?(object)Combat:
            parameter.ParameterType==typeof(CardPlay)?play:throw new Exception(parameter.ToString())).ToArray();await(Task)Invoke(hook,null,args)!;
    }
    static async Task NatsumePlay(LegacyFlashHbrgoSaki card,Creature? target=null,bool auto=false)
    {
        var play=Play(card);Set(play,"<Target>k__BackingField",target??Enemy);Set(play,"<IsAutoPlay>k__BackingField",auto);
        await NatsumeBefore(play);await(Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),play)!;
    }
    static void NatsumeHp(Creature target,decimal current,decimal max=1000m)
    {Call("LargeCreatureHealthHelper","SetMaxHp",target,max);Call("LargeCreatureHealthHelper","SetCurrentHp",target,current);}
    static async Task NatsumeCases()
    {
        NatsumeReset();var card=NewGarden<LegacyFlashHbrgoSaki>();await NatsumePlay(card);
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),1,"base rare single-target attack costs1");Check(card.Type==CardType.Attack&&card.Rarity==CardRarity.Rare&&card.TargetType==TargetType.AnyEnemy,"base type and target remain ordinary rare attack");
        Check(card is IThirtyOneFMemberCard{ThirtyOneFMember:ThirtyOneFMember.NatsumeInori},"Natsume declares correct fifth31F member");
        Equal(NatsumeHits.Count,4,"actual native AttackCommand.Execute deals exactly four segments");Check(NatsumeHits.All(hit=>hit.Raw==2.82m&&hit.Destruction==.5m),"every native segment uses47percent raw and destruction1point5");Check(!Enemy.IsDead,"above threshold enemy survives");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>();NatsumeHp(Enemy,100m);var dp=Power<HighDimensionalShellPower>(Enemy,9999);await NatsumePlay(card);
        Check(Enemy.IsDead,"exact ten-percent target is condemned through real native Kill");Equal(dp.CurrentDp,9999m,"Condemnation ignores a full DP pool");Check(NatsumeHits.Count==4&&NatsumeCompletions.Single()==(4,1),"Condemnation occurs after fourth damage but before original AfterAttack sees one fatal and no extra hit row");
        Check(NatsumeDeaths.Single().Scoped==card&&!NatsumeDeaths.Single().Prevented,"actual native death hook sees exact scoped card credit");
#if HBR_BETA_NATIVE
        Check(NatsumeDeaths.Single().Native==card,"beta native AfterDeath receives actual cardSource");
#endif
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>();NatsumeHp(Enemy,101m);await NatsumePlay(card);Check(!Enemy.IsDead&&NatsumeDeaths.Count==0,"above ten-percent target is not executed");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>(0,1);NatsumeHp(Enemy,200m);await NatsumePlay(card);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"Flash changes1cost to0");Check(Enemy.IsDead,"MercifulBlade extends boundary to exact20percent");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>(2,3);await NatsumePlay(card);Check(NatsumeHits.All(hit=>hit.AttackUp&&hit.CriticalUp),"Cooldown and Hope are granted before all four actual segments");Check(!Hero.HasPower<HbrAttackUpPower>()&&!Hero.HasPower<HbrCriticalRateUpPower>(),"both Count1 framework buffs are consumed by this one native attack");
        await NatsumeFirstFamilyCases();await NatsumeDeathCases();
    }
    static async Task NatsumeFirstFamilyCases()
    {
        NatsumeReset();var card=NewGarden<LegacyFlashHbrgoSaki>(4);int random=RngCount(RunState.Rng.Niche);
        Equal((decimal)Call("NatsumeCondemnationCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,50m,"fresh preview reads first-card50percent without consuming eligibility");Equal(RngCount(RunState.Rng.Niche),random,"first-card preview does not spend RNG");
        await NatsumePlay(card);Check(NatsumeHits.All(hit=>hit.Amp==50m),"first actual card keeps50percent across its whole four-hit attack");NatsumeHits.Clear();await NatsumePlay(card);Check(NatsumeHits.All(hit=>hit.Amp==0m),"same own turn's second actual play gets no bonus");
        foreach(bool auto in new[]{false,true})
        {
            NatsumeReset();var earlier=FormCard<StrikeIronclad>();var play=Play(earlier);Set(play,"<IsAutoPlay>k__BackingField",auto);await NatsumeBefore(play);card=NewGarden<LegacyFlashHbrgoSaki>(4);await NatsumePlay(card);
            Check(NatsumeHits.All(hit=>hit.Amp==0m),"prior "+(auto?"automatic":"manual")+" card consumes first-card slot");
        }
        foreach(bool od in new[]{false,true})
        {
            NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>(4);await NatsumePlay(card);NatsumeHits.Clear();using(MisatoExtra(od))await NatsumePlay(card);
            Check(NatsumeHits.All(hit=>hit.Amp==50m),"inserted "+(od?"OD":"personal")+" turn receives independent first-card slot");NatsumeHits.Clear();await NatsumePlay(card);Check(NatsumeHits.All(hit=>hit.Amp==0m),"return from inserted turn restores used ordinary slot");
        }
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>(4);NatsumeNested=true;await NatsumePlay(card);Check(NatsumeHits.Count==8,"same-source nested raw probe really ran inside each native hit");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>(4);var cannon=(Creature)H("Actor",Blank<HotaruContestCannon>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,cannon});try{NatsumeCannon=true;await(Task)Call("HotaruCannonStatsHelper","ReactionDamage",new ThrowingPlayerChoiceContext(),cannon,Enemy,6m,card,null,false,false)!;}finally{NatsumeCannon=false;}
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>(6);NewGarden<AsTheYoungLadyCommandsHbrgoSaki>();NewGarden<AsTheYoungLadyCommandsHbrgoSaki>();FormCard<StrikeIronclad>();
        Equal((decimal)Call("NatsumeCondemnationCombatHelper","GetAttackUnits",card)!,4.02m,"Family pure preview counts two other31F entities including duplicates");await NatsumePlay(card);Check(NatsumeHits.All(hit=>hit.Raw==4.02m),"Family snapshots67percent for all four native segments");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>(4);NatsumeThrow=true;try{await NatsumePlay(card);throw new Exception("expected failure");}catch(Exception error)when(error.Message.Contains("controlled Natsume")){}Check(AsyncValue("NatsumeCondemnationCombatHelper","Current")==null&&AsyncValue("NatsumeCondemnationCombatHelper","Condemnation")==null,"failed damage closes active play and execution scope");
    }
    static async Task NatsumeDeathCases()
    {
        NatsumeReset();var card=NewGarden<LegacyFlashHbrgoSaki>(5);NatsumeHp(Enemy,100m);var second=MisatoEnemy();NatsumeHp(second,50m);var above=MisatoEnemy();NatsumeHp(above,101m);await NatsumePlay(card);
        Check(Enemy.IsDead&&second.IsDead&&!above.IsDead,"InstantBlade executes every threshold-qualified enemy only");Check(NatsumeCompletions.Single()==(4,2),"AoE execution credit is visible before AfterAttack and preserves four hit rows");Check(NatsumeDeaths.Select(row=>row.Target).SequenceEqual(new[]{Enemy,second}),"multi-target Condemnation retains native enemy order");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>(5);NatsumeHp(Enemy,100m);second=MisatoEnemy();NatsumeHp(second,100m);NatsumeNestedOrigin=Enemy;NatsumeNestedVictim=second;await NatsumePlay(card);
        Check(Enemy.IsDead&&second.IsDead&&NatsumeCompletions.Single().FinalKills==1,"nested unrelated native Kill of another eligible target does not become this card's Fatal credit");
        Check(NatsumeDeaths.Where(row=>row.Target==second).All(row=>row.Scoped==null),"nested and subsequently already-dead native target never borrow parent card source");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>();var growth=Misato(6);MisatoPermanent(growth);NatsumeHp(Enemy,100m);await NatsumePlay(card);Equal(MisatoGrowth(growth),1m,"real Condemnation death grants actual57permanent kill growth once");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>();growth=Misato(6);MisatoPermanent(growth);Power<MinionPower>(Enemy,1);NatsumeHp(Enemy,100m);await NatsumePlay(card);Check(Enemy.IsDead&&NatsumeCompletions.Single().FinalKills==1,"minions are valid Condemnation targets with fatal result credit");Equal(MisatoGrowth(growth),0m,"existing57minion exclusion remains unchanged");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>();decimal enormous=decimal.Parse("1000000000000000000");NatsumeHp(Enemy,enormous*.1m,enormous);await NatsumePlay(card);Check(Enemy.IsDead,"Condemnation compares authoritative huge HP rather than native proxy and drains it all");
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>();var boss=(Creature)H("Actor",(MonsterModel)ModelDb.Monster<RedCrimsonBossMonster>().ToMutable(),null,null)!;((List<Creature>)typeof(CombatState).GetField("_enemies",F)!.GetValue(Combat)!).Add(boss);NatsumeHp(boss,100m);await NatsumePlay(card,boss);Check(boss.IsDead,"real HBR Boss type is not excluded from Condemnation");
        await NatsumeRevivalCase();
        NatsumeReset();card=NewGarden<LegacyFlashHbrgoSaki>();NatsumeHp(Enemy,100m);NatsumeCancelDeath=Enemy;try{await NatsumePlay(card);throw new Exception("expected cancelled death");}catch(OperationCanceledException){}
        Check(AsyncValue("NatsumeCondemnationCombatHelper","NativeKill")==null&&AsyncValue("NatsumeCondemnationCombatHelper","Condemnation")==null&&AsyncValue("NatsumeCondemnationCombatHelper","Current")==null,"native death cancellation closes direct-credit, execution and card scopes");
    }
    static async Task NatsumeRevivalCase()
    {
        NatsumeReset();var card=NewGarden<LegacyFlashHbrgoSaki>();var growth=Misato(6);MisatoPermanent(growth);
        var subject=(MonsterModel)ModelDb.Monster<TestSubject>().ToMutable();var target=(Creature)H("Actor",subject,null,null)!;((List<Creature>)typeof(CombatState).GetField("_enemies",F)!.GetValue(Combat)!).Add(target);var adaptable=Power<AdaptablePower>(target,1);NatsumeHp(target,100m);
        await NatsumePlay(card,target);Check((bool)Native(adaptable,"get_IsReviving")!,"real native Adaptable enters its revival phase after Condemnation");Equal(MisatoGrowth(growth),0m,"reviving phase awards no final kill growth");Equal(NatsumeCompletions.Single().FinalKills,0,"reviving phase is not fabricated as a final Fatal result");
        Native(adaptable,"DoRevive");await PowerCmd.Remove(adaptable);NatsumeHp(target,100m);NatsumeCompletions.Clear();await NatsumePlay(card,target);Check(target.IsDead&&NatsumeCompletions.Single().FinalKills==1,"later true final death keeps native Fatal credit");Equal(MisatoGrowth(growth),1m,"later true final phase increments57once");
    }
    public static void RunNatsume(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".natsume84");
            foreach(Type type in new[]{typeof(LegacyFlashHbrgoSaki),typeof(AsTheYoungLadyCommandsHbrgoSaki),typeof(TengenBrilliantMoveHbrgoSaki),typeof(StrikeIronclad),typeof(HbrAttackUpPower),typeof(HbrCriticalRateUpPower),typeof(HighDimensionalShellPower),typeof(LargeHealthPower),typeof(NatsumeAfterAttack),typeof(SmallHopper),typeof(RedCrimsonBossMonster),typeof(TestSubject),typeof(AdaptablePower),typeof(MinionPower)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string patch in new[]{"NatsumeCardPlayStartedPatch","NatsumeCondemnationAttackCompletionPatch","NatsumeCondemnationNativeKillPatch","NatsumeCondemnationDeathObservedPatch","HbrEffectApplicationPatch","HighHitDamageAttackScopePatch","HighHitDamageCommandScopePatch","LargeCreatureSetCurrentPatch","LargeCreatureNativeDeathPatch","TengenBrilliantMoveDeathPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+patch,true)!).Patch().Count>0,"installs "+patch);
            void Patch(MethodBase target,string name,int priority=Priority.First+100)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(name,F)!){priority=priority});
            foreach(Type type in new[]{RunState.GetType(),typeof(CombatState)})Patch(type.GetMethod("IterateHookListeners",F)!,nameof(MisatoListeners));
            Patch(typeof(EffectChecks).GetMethod(nameof(Body),F)!,nameof(NatsumeBodyLeaf));h.Patch(Canonical,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NatsumeCannonObserve),F)!){priority=0});
            h.Patch(typeof(Hook).GetMethods(F).Single(method=>method.Name=="AfterDeath"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NatsumeDeathObserve),F)!));
            h.Patch(typeof(Hook).GetMethods(F).Single(method=>method.Name=="AfterDeath"),postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NatsumeNestedDeathPostfix),F)!));
            Patch(typeof(Hook).GetMethods(F).Single(method=>method.Name=="BeforeDeath"),nameof(NatsumeCancelBeforeDeath));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,nameof(FormPowerIconLeaf));
            foreach(MethodInfo sound in typeof(SfxCmd).GetMethods(F).Where(method=>method.Name=="Play"))Patch(sound,nameof(Skip));
            foreach(MethodInfo visual in typeof(VfxCmd).GetMethods(F).Where(method=>method.Name is "PlayOnCreatureCenter" or "PlayOnSide"))Patch(visual,nameof(Skip));
            foreach(MethodInfo animation in typeof(CreatureCmd).GetMethods(F).Where(method=>method.Name=="TriggerAnim"&&method.ReturnType==typeof(Task)))Patch(animation,nameof(VisualTask));
            foreach(MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method=>(method.Name is "Wait" or "CustomScaledWait")&&method.ReturnType==typeof(Task)))Patch(wait,nameof(VisualTask));
            Patch(typeof(TestSubject).GetMethod("TriggerDeadState",F)!,nameof(VisualTask));Patch(typeof(MegaCrit.Sts2.Core.Context.LocalContext).GetProperty("NetId",F)!.GetMethod!,nameof(MisatoNetId));
            NatsumeCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real native AttackCommand.Execute (no attack leaf), exact pre-AfterAttack callsite, normal native Kill/death/revival hooks and confirmed Fatal credit; actual source-buff consumption, shared damage, first-card native dispatch and extra-turn membership. Visual effects/terminal damage amounts controlled; no Godot/network/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Natsume checks");
    }
}

