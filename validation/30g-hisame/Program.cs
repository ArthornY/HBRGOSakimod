using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static class HisameStandalone {public static void Run(string path)=>EffectChecks.RunHisame(path);}
static partial class EffectChecks
{
    sealed record HisameHit(Creature Target,decimal Raw,int Hits,bool Eye,bool AttackUp,bool Forced,decimal Hp,decimal Dp,decimal Destruction);
    static readonly List<HisameHit> HisameHits=[];
    static readonly List<decimal> HisameEnergy=[];
    static bool HisameZero,HisameBreak,HisameThrow,HisameProbeNested,HisameNestedRaw,HisameInsideNested;
    static int HisameCommands;
    static bool HisameCannonProbeEnabled;
    static void HisameCannonProbe(object[] __args)
    {
        if(!HisameCannonProbeEnabled||__args[5] is not CardModel source)return;
        Check(!(bool)Call("HisameCloseSlashCombatHelper","ForcesCritical",Enemy,Hero,source,ValueProp.Move)!,"retained source cannon never borrows sword guaranteed critical");
        Equal((decimal)Call("HisameCloseSlashCombatHelper","GetHpDamageMultiplier",source,Enemy)!,1m,"retained source cannon never borrows sword HP multiplier");
        Equal((decimal)Call("HisameCloseSlashCombatHelper","GetDpDamageMultiplier",source,Enemy)!,1m,"retained source cannon never borrows gun DP multiplier");
        Equal((decimal)Call("HisameCloseSlashCombatHelper","GetDestructionGainMultiplier",source,Enemy,Hero,ValueProp.Move)!,1m,"retained source cannon preserves its own destruction coefficient");
    }
    static bool HisameEnergyLeaf(decimal amount,Player player,ref Task __result)
    {Check(player==Player,"Desire energy belongs to the physical card owner");HisameEnergy.Add(amount);__result=Task.CompletedTask;return false;}
    static bool HisameAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=HisameAttack(__instance);return false;}
    static async Task<AttackCommand> HisameAttack(AttackCommand command)
    {
        HisameCommands++;
        var card=(CardModel)command.ModelSource!;
        await (Task)MisatoHook("BeforeAttack",command)!;
        var targets=(IReadOnlyList<Creature>)Native(command,"GetPossibleTargets")!;
        decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!;
        int hits=(int)decimal.Floor((decimal)MisatoHook("ModifyAttackHitCount",command,(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!)!);
        var rows=(List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!;
        if(HisameThrow)throw new InvalidOperationException("Hisame controlled command failure");
        if(HisameProbeNested)
        {
            var other=new AttackCommand(1);Native(other,"FromCard",card);other.TargetingAllOpponents(Combat);
            var nested=(IReadOnlyList<Creature>)Native(other,"GetPossibleTargets")!;
            Check(nested.Count==2,"an unrelated nested command retaining the same source receives no species filter");
        }
        foreach(var target in targets)
        {
            if(card is SlashIfCloseHbrgoSaki&&((SlashIfCloseHbrgoSaki)card).UpgradeEffectOrder.Contains(4)&&!HBRGOSakiMod.Core.Helpers.HbrEnemyCancerHelper.IsCancer(target))
                Check(target.HasPower<HbrAttackDownPower>(),"sword KillingIntent is already applied before its native attack executes");
            using((IDisposable)Call("HisameCloseSlashCombatHelper","BeginPreview",card,HBRGOSakiMod.Core.Helpers.HbrEnemyCancerHelper.IsCancer(target))!)
                HisameHits.Add(new(target,raw,hits,Hero.HasPower<MindsEyePower>(),Hero.HasPower<HbrAttackUpPower>(),(bool)Call("HisameCloseSlashCombatHelper","ForcesCritical",target,Hero,card,ValueProp.Move)!,
                    (decimal)Call("HisameCloseSlashCombatHelper","GetHpDamageMultiplier",card,target)!,
                    (decimal)Call("HisameCloseSlashCombatHelper","GetDpDamageMultiplier",card,target)!,
                    (decimal)Call("HisameCloseSlashCombatHelper","GetDestructionGainMultiplier",card,target,Hero,ValueProp.Move)!));
            for(int i=0;i<hits;i++)
            {
                rows.Add((await Command(Hero,raw,ValueProp.Move,card,[target])).ToList());
            }
        }
        await (Task)MisatoHook("AfterAttack",command)!;return command;
    }
    static bool HisameBodyLeaf(IEnumerable<Creature> targets,decimal raw,ValueProp props,Creature? dealer,CardModel? card,ref Task<IEnumerable<DamageResult>> __result)
    {
        if(card is not SlashIfCloseHbrgoSaki||AsyncValue("HisameCloseSlashCombatHelper","Current")==null)return true;
        __result=HisameBody(targets,raw,props,dealer!,card);return false;
    }
    static async Task<IEnumerable<DamageResult>> HisameBody(IEnumerable<Creature> targets,decimal raw,ValueProp props,Creature dealer,CardModel card)
    {
        await Task.Yield();var rows=new List<DamageResult>();
        foreach(var target in targets)
        {
            object? command=AsyncValue("HbrEffectSkillDamageContext","CurrentCommand");
            if(HisameInsideNested)
            {
                Check(!(bool)Call("HisameCloseSlashCombatHelper","ForcesCritical",target,dealer,card,props)!,"same-source nested raw effect cannot borrow sword forced critical");
                Equal((decimal)Call("HisameCloseSlashCombatHelper","GetHpDamageMultiplier",card,target)!,1m,"same-source nested raw effect cannot borrow sword HP specialization");
                Equal((decimal)Call("HisameCloseSlashCombatHelper","GetDestructionGainMultiplier",card,target,dealer,props)!,1m,"same-source nested raw effect cannot borrow branch destruction");
                Call("HisameCloseSlashCombatHelper","OnEnemyBroken",dealer,target,card,props,false);
            }
            if(HisameNestedRaw&&!HisameInsideNested)
            {
                HisameInsideNested=true;
                try{await Command(dealer,raw,props,card,[target]);}finally{HisameInsideNested=false;}
            }
            decimal damage=Actual(target,dealer,raw,props,card);
            Hits.Add(new(target,dealer,card,raw,props,damage,Enabled(command),Sample(command),0,false));
            if(HisameZero)damage=0;
            var shell=HisameBreak?target.GetPower<HighDimensionalShellPower>():null;
            decimal blocked=HisameBreak?target.DamageBlockInternal(damage,props):0m;
            damage=Math.Max(0m,damage-blocked);
            var result=new DamageResult(target,props){UnblockedDamage=(int)Math.Min(int.MaxValue,damage),BlockedDamage=(int)Math.Min(int.MaxValue,blocked)};
            Call("ItsMygoIncomingDamageCapture","AppendResults",rows,new List<DamageResult>{result});
            if(shell!=null)await shell.AfterDamageReceived(new ThrowingPlayerChoiceContext(),target,result,props,dealer,card);
        }
        return rows;
    }
    static Creature HisameEnemy(bool cancer)
    {
        var enemy=MisatoEnemy();
        if(!cancer)
        {
            var monster=Blank<TezukaSaki>();Set(monster,"<Id>k__BackingField",ModelDb.GetId(typeof(TezukaSaki)));
            Set(enemy,"<Monster>k__BackingField",monster);Set(monster,"_creature",enemy);Set(monster,"<IsMutable>k__BackingField",true);
        }
        return enemy;
    }
    static (Creature Gun,Creature Sword) HisameReset()
    {
        NewReset();HisameHits.Clear();HisameEnergy.Clear();HisameCommands=0;HisameZero=HisameBreak=HisameThrow=HisameProbeNested=HisameNestedRaw=HisameInsideNested=false;
        Set(RunState,"_players",new List<Player>{Player});Set(Combat,"_enemies",new List<Creature>());
        var gun=HisameEnemy(true);var sword=HisameEnemy(false);typeof(Checks).GetField("Enemy",F)!.SetValue(null,gun);
        var table=Helper("HisameCloseSlashCombatHelper").GetField("Battles",F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);
        CombatInProgressWithTurn();return(gun,sword);
    }
    static async Task HisameCases()
    {
        var (gun,sword)=HisameReset();var card=NewGarden<SlashIfCloseHbrgoSaki>();HisameProbeNested=true;await NewPlay(card);
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),1,"base cost1");Equal(HisameCommands,2,"mixed AoE uses exactly two native commands");
        Check(card is IThirtyGMemberCard member&&member.ThirtyGMember==ThirtyGMember.OgasawaraHisame,"card supplies the sixth30G member");
        var g=HisameHits.Single(h=>h.Target==gun);var s=HisameHits.Single(h=>h.Target==sword);
        Equal(g.Raw,3.6m,"gun raw60percent");Equal(g.Hits,2,"Cancer receives two gun hits only");Equal(g.Destruction,1m/3m,"gun destruction1");
        Equal(s.Raw,19.5m,"sword raw325percent");Equal(s.Hits,1,"nonCancer receives one sword hit only");Equal(s.Hp,1.5m,"sword HP specialization50percent");Equal(s.Destruction,.7m,"sword destruction2point1");
        Power<HighDimensionalShellPower>(gun,100);Equal((decimal)Call("HisameCloseSlashCombatHelper","GetDpDamageMultiplier",card,gun)!,1.3m,"gun DP specialization30percent");
        Power<HighDimensionalShellPower>(sword,100);Equal((decimal)Call("HisameCloseSlashCombatHelper","GetHpDamageMultiplier",card,sword)!,1m,"sword HP multiplier is absent while DP remains");
        Equal((decimal)Call("HisameCloseSlashCombatHelper","GetHpDamageMultiplier",ModelDb.Card<SlashIfCloseHbrgoSaki>(),sword)!,1m,"canonical HP query never reads owner");
        (gun,sword)=HisameReset();card=NewGarden<SlashIfCloseHbrgoSaki>(0,1,2);var gauge=NewGauge();int before=RngCount(RunState.Rng.Niche);
        Check((bool)Call("HisameCloseSlashCombatHelper","ForcesCritical",sword,Hero,card,ValueProp.Move)!,"selected nonCancer sword preview guarantees critical");
        Check(!(bool)Call("HisameCloseSlashCombatHelper","ForcesCritical",gun,Hero,card,ValueProp.Move)!,"Cancer gun preview is not forced critical");
        for(int i=0;i<3;i++)Preview(sword,Hero,19.5m,ValueProp.Move,card);
        Equal(RngCount(RunState.Rng.Niche),before,"repeated deterministic sword previews do not roll RNG");
        using((IDisposable)Call("HisameCloseSlashCombatHelper","BeginPreview",card,true)!)
            Check(!(bool)Call("HisameCloseSlashCombatHelper","ForcesCritical",sword,Hero,card,ValueProp.Move)!,"gun description line remains gun against a selected nonCancer");
        using((IDisposable)Call("HisameCloseSlashCombatHelper","BeginPreview",card,false)!)
            Check((bool)Call("HisameCloseSlashCombatHelper","ForcesCritical",gun,Hero,card,ValueProp.Move)!,"sword description line remains sword against a selected Cancer");
        Check(AsyncValue("HisameCloseSlashCombatHelper","Preview")==null,"both read-only branch previews close cleanly");
        await NewPlay(card);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"Flash cost0");
        Check(HisameHits.All(hit=>hit.Eye),"one preattack MindsEye benefits both species branches");
        Check(HisameHits.Single(h=>h.Target==sword).Forced&&!HisameHits.Single(h=>h.Target==gun).Forced,"only actual sword branch forces critical");
        Equal(HisameHits.Single(h=>h.Target==sword).Hp,2m,"FullForce sword HP specialization100percent");
        Equal(gauge.PreciseCharge,15m*gauge.ChargePerLevel/100m,"connected gun grants one15percent OD despite multiple targets/hits");
        Check(!Hero.HasPower<MindsEyePower>(),"shared logical attack settles its one MindsEye after both commands");
        (gun,sword)=HisameReset();card=NewGarden<SlashIfCloseHbrgoSaki>(2);gauge=NewGauge();HisameZero=true;await NewPlay(card);Equal(gauge.PreciseCharge,0m,"zero actual gun results grant no OD");
        (gun,sword)=HisameReset();Set(Combat,"_enemies",new List<Creature>{gun});card=NewGarden<SlashIfCloseHbrgoSaki>(2);gauge=NewGauge();HisameBreak=true;await HisameShell(gun,100000m);await NewPlay(card);
        Equal(gauge.PreciseCharge,15m*gauge.ChargePerLevel/100m,"real DP-only connected gun results still grant one15percent OD through shared DP receipts");
        (gun,sword)=HisameReset();Set(Combat,"_enemies",new List<Creature>{sword});card=NewGarden<SlashIfCloseHbrgoSaki>(1,2);gauge=NewGauge();await HbrApply<HbrCriticalDamageUpPower>(Hero,50m);before=RngCount(RunState.Rng.Niche);await NewPlay(card);
        Check(!HisameHits.Single().Eye&&HisameHits.Single().Forced,"sword-only play gains no gun MindsEye but remains critical");Equal(gauge.PreciseCharge,0m,"sword-only play grants no gun OD");
        Equal(RngCount(RunState.Rng.Niche),before,"actual guaranteed sword critical also spends no critical RNG");
        decimal criticalBase=(decimal)Call("HbrSkillPower","EvaluateDamageFromUnits",19.5m,100m,150m,true)!;
        Equal(Hits.Single().Final,decimal.Floor(criticalBase*2m*2m),"guaranteed sword uses real critical defense reduction and existing50critical damage plus HP100percent");
        (gun,sword)=HisameReset();card=NewGarden<SlashIfCloseHbrgoSaki>(4);await NewPlay(card);
        var down=gun.GetPower<HbrDefenseDownPower>()!;Equal((decimal)Native(down,"get_EffectPercent")!,28.5m,"gun applies30DefenseDown through common INT100/E150 formula");
        Equal(down.Amount,1,"gun DefenseDown lasts one enemy turn");Check(!sword.HasPower<HbrDefenseDownPower>(),"sword target never receives gun DefenseDown");
        Check(gun.HasPower<HbrAttackDownPower>()&&sword.HasPower<HbrAttackDownPower>(),"drawing the sword applies20AttackDown to all enemies");Equal((decimal)Native(gun.GetPower<HbrAttackDownPower>()!,"get_EffectPercent")!,19m,"sword AttackDown20 through INT100/E150 framework");
        (gun,sword)=HisameReset();card=NewGarden<SlashIfCloseHbrgoSaki>(3);HisameBreak=true;await HisameShell(gun);await HisameShell(sword);await NewPlay(card);
        Equal(HisameEnergy.Sum(),1m,"two real DPbreak branches grant only one energy per turn");
        await HisameShell(gun);await NewPlay(card);Equal(HisameEnergy.Sum(),1m,"same own turn cannot gain the break reward twice");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await HisameShell(gun);await NewPlay(card);Equal(HisameEnergy.Sum(),2m,"next actual own turn renews the break receipt");
        (gun,sword)=HisameReset();card=NewGarden<SlashIfCloseHbrgoSaki>(6);MisatoPermanent(card);await NewPlay(card);await NewPlay(card);
        Equal(HisameHits[0].Raw,3.6m,"StillGrowing first gun remains60percent");Equal(HisameHits[1].Raw,19.5m,"StillGrowing first sword remains325percent");Equal(HisameHits[2].Raw,4.5m,"second gun grows to75percent");Equal(HisameHits[3].Raw,20.4m,"second sword grows to340percent");
        var mirror=(SlashIfCloseHbrgoSaki)card.MutableClone();Set(mirror,"_owner",Player);mirror.DeckVersion=card;await NewPlay(mirror);Equal(HisameHits[^2].Raw,5.4m,"OD mirror uses shared physical90percent growth");
        Equal((decimal)Call("HisameCloseSlashCombatHelper","GetBranchUnits",card,true)!,6.3m,"mirror play advances the same physical growth once");
        (gun,sword)=HisameReset();card=NewGarden<SlashIfCloseHbrgoSaki>(6);await NewPlay(card);HisameThrow=true;
        try{await NewPlay(card);throw new Exception("expected controlled failure");}catch(Exception ex)when(ex.ToString().Contains("controlled command failure")){}
        Check(AsyncValue("HisameCloseSlashCombatHelper","Current")==null,"exception closes species/card receipt");Equal((decimal)Call("HisameCloseSlashCombatHelper","GetBranchUnits",card,true)!,4.5m,"failed attack grants no completed-play growth");
        (gun,sword)=HisameReset();card=NewGarden<SlashIfCloseHbrgoSaki>();Power<HbrComboSmallPower>(Hero,5);await HbrApply<HbrAttackUpPower>(Hero,65m);await NewPlay(card);
        Equal(HisameHits[0].Hits,3,"small combo adds one gun hit");Equal(HisameHits[1].Hits,2,"same logical combo also adds one sword hit");Equal(Hero.GetPowerAmount<HbrComboSmallPower>(),4,"mixed commands consume just one small-combo use");
        Check(HisameHits.All(hit=>hit.AttackUp),"Count1AttackUp remains active for both mixed species commands");Check(!Hero.HasPower<HbrAttackUpPower>(),"logical mixed attack settles Count1AttackUp once at completion");
        (gun,sword)=HisameReset();Set(Combat,"_enemies",new List<Creature>{sword});card=NewGarden<SlashIfCloseHbrgoSaki>(1,2,3);HisameNestedRaw=true;await NewPlay(card);
        Equal(HisameEnergy.Count,0,"nested same-source effect break notification cannot create Desire energy");
        await HisamePermanentCases();
        (gun,sword)=HisameReset();card=NewGarden<SlashIfCloseHbrgoSaki>(1,2);var cannon=(Creature)H("Actor",Blank<HotaruContestCannon>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,cannon});
        try{HisameCannonProbeEnabled=true;await (Task)Call("HotaruCannonStatsHelper","ReactionDamage",new ThrowingPlayerChoiceContext(),cannon,gun,6m,card,null,false,false)!;}
        finally{HisameCannonProbeEnabled=false;}
    }
    static async Task HbrApply<T>(Creature target,decimal basis)where T:HbrEffectPower
        =>await (Task)Invoke(Helper("HbrEffectApplication").GetMethods(F).Single(m=>m.Name=="Apply"&&!m.IsGenericMethod),null,
            new ThrowingPlayerChoiceContext(),(HbrEffectPower)ModelDb.Power<T>().ToMutable(),target,basis,Hero,null,null,null,false,null)!;
    static async Task HisameShell(Creature target,decimal amount=1m)
    {
        if(target.GetPower<BreakPower>() is {} broken)await PowerCmd.Remove(broken);
        if(target.GetPower<HighDimensionalShellPower>() is {} shell)await PowerCmd.Remove(shell);
        await PowerCmd.Apply<HighDimensionalShellPower>(new ThrowingPlayerChoiceContext(),target,amount,target,null,true);
    }
    static object HisameStats()
    {object?[] args={Hero,null};Check((bool)Call("HbrBattleStatsHelper","TryGetStats",args)!,"actual derived stats exist");return args[1]!;}
    static decimal HisameStat(object stats,string name)=>(decimal)stats.GetType().GetProperty(name)!.GetValue(stats)!;
    static async Task HisamePermanentCases()
    {
        HisameReset();var card=NewGarden<SlashIfCloseHbrgoSaki>(5);MisatoPermanent(card);
        Check((bool)Call("HisameCloseSlashCombatHelper","HasMastersAdvice",Player)!,"permanent MastersAdvice enables cached policy");
        var baseline=HisameStats();Power<CrimsonBladeFlowPower>(Hero,1);var crimson=HisameStats();
        Equal(HisameStat(crimson,"Vitality"),HisameStat(baseline,"Vitality")*.5m,"MastersAdvice halves real derived Crimson vitality instead of zero");
        Equal(HisameStat(crimson,"Spirit"),HisameStat(baseline,"Spirit")*.5m,"MastersAdvice halves real derived Crimson spirit instead of zero");
        Equal(HisameStat(crimson,"Strength"),HisameStat(baseline,"Strength")*2m,"Crimson strength doubling stays intact");
        Equal(HisameStat(crimson,"Dexterity"),HisameStat(baseline,"Dexterity")*2m,"Crimson dexterity doubling stays intact");
        Hits.Clear();await (Task)Call("MonakaFriendlyFireHelper","DamageTeam",new ThrowingPlayerChoiceContext(),card)!;
        Equal(Hits.Single().Final,decimal.Floor((decimal)Call("LargeCreatureHealthHelper","GetMaxHp",Hero)!*.125m),"real fixed friendly-fire pipeline is halved to12point5percent HP");
        var parry=NewGarden<HarvestBountifulGrainsHbrgoSaki>(6);MisatoPermanent(parry);Hits.Clear();await (Task)Call("MonakaFriendlyFireHelper","DamageTeam",new ThrowingPlayerChoiceContext(),card)!;
        Equal(Hits.Count,0,"Kura team Parry overrides MastersAdvice friendly-fire half with zero");
        var relic=(ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().ToMutable();Set(relic,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(relic).ToList());Call("ThirtyGProposalState","RestoreIdentity",card,"hisame-advice");
        string State(bool released)=>JsonSerializer.Serialize(new{InBattle=true,Cards=new Dictionary<string,object>{{"hisame-advice",new{GraduatedRemaining=1,Released=released}}}});
        Native(relic,"SetProposalData",State(false));Call("ThirtyGProposalState","Notify",card);Check(!(bool)Call("HisameCloseSlashCombatHelper","HasMastersAdvice",Player)!,"graduation immediately disables MastersAdvice cache");
        Native(relic,"SetProposalData",State(true));Call("ThirtyGProposalState","Notify",card);Check((bool)Call("HisameCloseSlashCombatHelper","HasMastersAdvice",Player)!,"release immediately restores policy without changing deck count");
    }
    public static void RunHisame(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".hisame78");
            foreach(Type type in new[]{typeof(Overclock),typeof(SlashIfCloseHbrgoSaki),typeof(HarvestBountifulGrainsHbrgoSaki),typeof(CrimsonBladeFlowPower),typeof(MindsEyePower),typeof(HbrDefenseDownPower),typeof(HbrAttackDownPower),typeof(HbrAttackUpPower),typeof(HbrCriticalDamageUpPower),typeof(HbrComboSmallPower),typeof(HighDimensionalShellPower),typeof(DestructionRatePower),typeof(BreakPower),typeof(ThirtyGCollectionRelic),typeof(SmallHopper),typeof(TezukaSaki)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string patch in new[]{"HbrEffectApplicationPatch","HighHitDamageAttackScopePatch","HighHitDamageCommandScopePatch","CreatureDamageBlockInternalPatch","HisameCloseSlashTargetPatch","HbrComboVirtualHitPatch","HotaruCannonDamageContextPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+patch,true)!).Patch().Count>0,"installs "+patch);
            h.Patch(Canonical,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(HisameCannonProbe),F)!){priority=0});
            void Patch(MethodBase target,string name,int priority=Priority.First+100)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(name,F)!){priority=priority});
            foreach(Type t in new[]{RunState.GetType(),typeof(CombatState)})Patch(t.GetMethod("IterateHookListeners",F)!,nameof(MisatoListeners));
            Patch(typeof(EffectChecks).GetMethod(nameof(Body),F)!,nameof(HisameBodyLeaf));
            Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,nameof(HisameAttackLeaf),Priority.Last);
            Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.GainEnergy))!,nameof(HisameEnergyLeaf));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,nameof(FormPowerIconLeaf));
            foreach(var wait in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))Patch(wait,nameof(VisualTask));
            HisameCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual78 upgrades, species-target GetPossibleTargets postfix, shared logical Count/combo reservation, real native hooks/canonical damage context and shell break. Attack presentation/terminal resource leaf controlled; no Godot/rendering/network test."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Hisame checks");
    }
}




