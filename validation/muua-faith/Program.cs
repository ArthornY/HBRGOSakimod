using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock=HBRGOSakiMod.Core.Models.Relics.Overclock;

static class MuuaStandalone { public static void Run(string path)=>EffectChecks.RunMuua(path); }
static partial class EffectChecks
{
    static readonly List<(decimal Raw,int Hits,decimal Destruction,decimal Actual)> MuuaCounters=[];
    static int MuuaDamageMode,MuuaEnergy,MuuaDraws;
    static Task? MuuaGate;
    static Creature? MuuaRedirect;
    static bool MuuaDrawLeaf(object[] __args,ref Task<IEnumerable<CardModel>> __result)
    {MuuaDraws+=(int)(decimal)__args[1];__result=Task.FromResult<IEnumerable<CardModel>>([]);return false;}
    static bool MuuaEnergyLeaf(decimal amount,ref Task __result)
    {MuuaEnergy+=(int)amount;__result=Task.CompletedTask;return false;}
    static bool MuuaDamageLeaf(object[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {
        __result=MuuaDamageBody(__args);return false;
    }
    static async Task<IEnumerable<DamageResult>> MuuaDamageBody(object[] args)
    {
        if(MuuaGate!=null)await MuuaGate;
        Type api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!;
        var targets=(IEnumerable<Creature>)Invoke(api.GetMethod("ReadTargets",F)!,null,new object?[]{args})!;
        var results=new List<DamageResult>();
        foreach(Creature target in targets)
        {
            if(MuuaDamageMode==4)
            {
                var zero=new DamageResult(target,(ValueProp)args[3]);var actual=new DamageResult(MuuaRedirect!,(ValueProp)args[3]);Set(actual,"<UnblockedDamage>k__BackingField",12);
                Call("ItsMygoIncomingDamageCapture","AppendResults",results,new[]{zero,actual});continue;
            }
            var result=new DamageResult(target,(ValueProp)args[3]);
            if(MuuaDamageMode==1)Set(result,"<BlockedDamage>k__BackingField",12);
            else if(MuuaDamageMode!=2&&MuuaDamageMode!=3)Set(result,"<UnblockedDamage>k__BackingField",12);
            // This is the actual native append helper and immutable result receipt.
            // Mode2 is a dodged zero-hit; mode3 is fully absorbed by DP.
            if(MuuaDamageMode==2)Call("HpOnlyDamageContext","RecordDodge",target);
            if(MuuaDamageMode==3){decimal spent=target.Player!.GetRelic<DeflectorProtection>()!.AbsorbDp(12m);Call("HpOnlyDamageContext","RecordDpAbsorption",target,spent);}
            Call("ItsMygoIncomingDamageCapture","AppendResults",results,new[]{result});
        }
        return results;
    }
    static bool MuuaCounterLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {
        if(__instance.ModelSource is MakeAChangeKillYourselfHbrgoSaki){__result=Task.FromResult(__instance);return false;}
        if(__instance.ModelSource is not IWillNotFallHbrgoSaki)return true;
        __result=MuuaCounterBody(__instance);return false;
    }
    static async Task<AttackCommand> MuuaCounterBody(AttackCommand command)
    {
        object scope=Call("HighHitDamageVisualHelper","BeginAttack",command)!;
        try
        {
            await (Task)MisatoHook("BeforeAttack",command)!;
            decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!;
            int hits=(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!;
            decimal destruction=(decimal)Call("MuuaFaithCombatHelper","GetDestructionGainMultiplier",command.ModelSource,Enemy,Hero,ValueProp.Move)!;
            MuuaCounters.Add((raw,hits,destruction,Actual(Enemy,Hero,raw,ValueProp.Move,(CardModel)command.ModelSource!)));
            Check(command.IsMultiTargeted&&command.Attacker==Hero,"71 counter is one normal all-opponent attack by its holder");
            Equal((decimal)Call("MuuaFaithCombatHelper","GetDestructionGainMultiplier",command.ModelSource,Enemy,Hero,ValueProp.Unpowered)!,1m,"71 counter coefficient cannot leak to unpowered reactions");
            var result=new DamageResult(Enemy,ValueProp.Move);Set(result,"<UnblockedDamage>k__BackingField",1);
            ((List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!).Add([result]);
            Call("CombatAttackHitHelper","Bind",new[]{result},Hero,command.ModelSource,null,0m,null);await NewGiven((CardModel)command.ModelSource!,result);
            await (Task)MisatoHook("AfterAttack",command)!;return command;
        }
        finally{Call("HighHitDamageVisualHelper","CloseAttack",scope);}
    }
    static void MuuaReset()
    {
        NewReset();Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki>());var niinaLedger=Helper("NiinaMakeupCombatHelper").GetField("Battles",F)!.GetValue(null)!;niinaLedger.GetType().GetMethod("Remove")!.Invoke(niinaLedger,[Combat]);MuuaCounters.Clear();MuuaEnergy=MuuaDraws=MuuaDamageMode=0;MuuaGate=null;MuuaRedirect=null;
        Set(Hero,"_currentHp",5000);Set(Hero,"_maxHp",5000);Set(Enemy,"_currentHp",5000);Set(Enemy,"_maxHp",5000);
        Set(Combat,"<RoundNumber>k__BackingField",1);Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Enemy);
        Helper("OverclockImmediateTurnCoordinator").GetField("_current",F)!.SetValue(null,null);
        Helper("PersonalImmediateTurnCoordinator").GetField("_current",F)!.SetValue(null,null);
    }
    static async Task<MuuaIWillNotFallPower> MuuaApply(params int[] upgrades)
    {var card=NewGarden<IWillNotFallHbrgoSaki>(upgrades);await NewPlay(card);return Hero.GetPower<MuuaIWillNotFallPower>()!;}
    static Task MuuaMove(int commands,ValueProp props=ValueProp.Move)
    {
        Set(Enemy.Monster!,"<Id>k__BackingField",ModelDb.GetId(Enemy.Monster!.GetType()));
        Set(Enemy.Monster!,"<NextMove>k__BackingField",new MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState("MUUA_TRUE_MOVE",async _=>
        {for(int i=0;i<commands;i++)await Command(Enemy,1m,props,null,[Hero]);}));
        return Enemy.Monster!.PerformMove();
    }
    static void FaithEquals(BigInteger amount,string label)=>Check(HbrFaithResource.Get(Hero)==amount,label+": "+HbrFaithResource.Get(Hero)+" = "+amount);
    static void MuuaSaveType(Type type)
    {
        Assembly native=typeof(SavedProperties).Assembly;
        if(native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is {} legacy){legacy.GetMethod("InjectTypeIntoCache",F)!.Invoke(null,[type]);return;}
        Type cache=native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;
        cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!.Invoke(null,[type]);cache.GetField("_initialized",F)!.SetValue(null,true);
    }
    static async Task MuuaResourceCases()
    {
        MuuaReset();FaithEquals(0,"71 new combat has no faith resource");var card=NewGarden<IWillNotFallHbrgoSaki>();Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),1,"71 base cost1");Check(card.Type==CardType.Power,"71 card remains a Power");
        await NewPlay(card);Check(Hero.HasPower<MuuaIWillNotFallPower>(),"71 actual OnPlay applies persistent ability");FaithEquals(0,"71 playing base ability grants no immediate faith");
        HbrFaithResource.Gain(Hero,7);FaithEquals(7,"71 common exact resource API gains7");
        var faith=Hero.GetPower<HbrFaithPower>()!;faith.SetAmount(10);FaithEquals(10,"71 native counter SetAmount synchronizes exact resource");
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(),faith,2m,Hero,null);FaithEquals(12,"71 native ModifyAmount participates in common resource");
        var huge=BigInteger.Parse("1000000000000000000000000000000");HbrFaithResource.Gain(Hero,huge);FaithEquals(huge+12,"71 resource does not inherit int or combat-stat cap");
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(),faith,1m,Hero,null);FaithEquals(huge+13,"71 native positive counter offset preserves huge exact resource beyond int proxy");
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(),faith,-7m,Hero,null);FaithEquals(huge+6,"71 native negative offset preserves large exact resource");
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(),faith,2_000_000_000m,Hero,null);FaithEquals(huge+2_000_000_006,"71 native logical delta above stat proxy cap is not itself capped");
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(),faith,-1_999_999_993m,Hero,null);FaithEquals(huge+13,"71 large native subtraction retains exact arithmetic");
        string saved=JsonSerializer.Serialize(SavedProperties.From(faith),new JsonSerializerOptions{IncludeFields=true});
        var restored=(HbrFaithPower)ModelDb.Power<HbrFaithPower>().ToMutable();JsonSerializer.Deserialize<SavedProperties>(saved,new JsonSerializerOptions{IncludeFields=true})!.Fill(restored);
        Check(restored.ExactAmount==huge+13,"71 actual native SavedProperties JSON Fill retains exact huge resource");
        var twice=FormCard<DoublePowersHbrgoSaki>();await NewPlay(twice);FaithEquals((huge+13)*2,"71 actual DoublePowers doubles exact resource beyond native proxy cap");
        Check(HbrFaithResource.TrySpend(Hero,huge+13),"71 first half exact spend succeeds after double");
        Check(HbrFaithResource.TrySpend(Hero,huge+13),"71 exact resource can be consumed without truncation");FaithEquals(0,"71 exact spend removes exhausted counter");Check(!Hero.HasPower<HbrFaithPower>(),"71 zero faith has no stale visible counter");
        HbrFaithResource.Gain(Hero,12);await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(),Hero.GetPower<HbrFaithPower>()!, -12m,Hero,null);FaithEquals(0,"71 native exact-zero offset clears the common resource");Check(!Hero.HasPower<HbrFaithPower>(),"71 native exact-zero offset leaves no stale counter");
        HbrFaithResource.Gain(Hero,3);Call("MuuaFaithCombatHelper","Clear",Combat);FaithEquals(0,"71 battle-end clear resets faith");
        MuuaReset();var power=await MuuaApply(0,1,2,3,4,5,6);Check(power.UpgradeMask==127,"71 all7 upgrades are copied to persistent ability");
        Native(power,"ClaimRescue");saved=JsonSerializer.Serialize(SavedProperties.From(power),new JsonSerializerOptions{IncludeFields=true});
        var copy=(MuuaIWillNotFallPower)ModelDb.Power<MuuaIWillNotFallPower>().ToMutable();JsonSerializer.Deserialize<SavedProperties>(saved,new JsonSerializerOptions{IncludeFields=true})!.Fill(copy);
        Check(copy.UpgradeMask==127&&copy.DpRescueUsed,"71 real ability save retains upgrade mask and once-combat rescue spent state");
    }
    static async Task MuuaHitCases()
    {
        foreach(int mode in new[]{0,1,2,3})
        {
            MuuaReset();await MuuaApply(4);MuuaDamageMode=mode;if(mode==3)NewDp();await MuuaMove(3);
            FaithEquals(mode==2?0:1,"71 real native multicommand enemy action grants once; mode"+mode);
            Equal(MuuaCounters.Count,mode==2?0:1,"71 counter executes once per connected enemy action; mode"+mode);
            Check(AsyncValue("ThirtyOneBEnemyActionHelper","Current")==null,"71 native action scope is closed before counter and restored afterward");
            if(mode!=2){Equal(MuuaCounters[0].Raw,4.62m,"71 counter uses77percent raw coefficient");Equal(MuuaCounters[0].Hits,1,"71 counter is one segment");Equal(MuuaCounters[0].Destruction,1.6m/3m,"71 counter uses total destruction1point6 once");}
            await MuuaMove(1);FaithEquals(mode==2?0:2,"71 subsequent action has its own faith receipt; mode"+mode);
        }
        MuuaReset();await MuuaApply(4);await MuuaMove(2,ValueProp.Unpowered);FaithEquals(0,"71 unpowered enemy HP effects do not count as an attack action");Equal(MuuaCounters.Count,0,"71 unpowered HP effects never counter");
        MuuaReset();await MuuaApply(4);var pending=new TaskCompletionSource();MuuaGate=pending.Task;Task move=MuuaMove(2);FaithEquals(0,"71 pending first damage command has no premature action reward");pending.SetResult();await move;FaithEquals(1,"71 async enemy action retains one holder receipt through awaits");
        MuuaReset();var gauge=NewGauge();await MuuaApply(4);await MuuaMove(3);Equal(gauge.PreciseCharge,2.5m,"71 one77percent counter's actual after-damage hook grants normal2point5OD per hit");
        MuuaReset();await MuuaApply(4);Power<HbrAttackUpPower>(Hero,1);Native(Hero.GetPower<HbrAttackUpPower>()!,"ConfigureEffect",50m,null);await MuuaMove(1);decimal boosted=MuuaCounters.Single().Actual;
        MuuaReset();await MuuaApply(4);await MuuaMove(1);Check(boosted>MuuaCounters.Single().Actual,"71 counter uses holder's ordinary offensive buff pipeline");
    }
    static async Task MuuaRedirectCases()
    {
        MuuaReset();await MuuaApply();var peer=SupportTeammate();Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});
        var peerCard=(IWillNotFallHbrgoSaki)ModelDb.Card<IWillNotFallHbrgoSaki>().ToMutable();Set(peerCard,"_owner",peer);await (Task)Native(peerCard,"OnPlay",new ThrowingPlayerChoiceContext(),Play(peerCard))!;
        MuuaRedirect=peer.Creature;MuuaDamageMode=4;await MuuaMove(1);FaithEquals(0,"71 logical hit's zero-damage original player earns no faith after cross-player redirection");
        Check(HbrFaithResource.Get(peer.Creature)==1,"71 redirected physical hit awards faith only to the connected second holder");
    }
    static IDisposable MuuaExtra(bool od)
    {
        Type coordinator=Helper(od?"OverclockImmediateTurnCoordinator":"PersonalImmediateTurnCoordinator"),sessionType=coordinator.GetNestedType("Session",F)!;
        FieldInfo field=coordinator.GetField("_current",F)!;object? old=field.GetValue(null);object session;
        if(od)session=Activator.CreateInstance(sessionType,F,null,new object[]{Player.GetRelic<Overclock>()!,Combat,new[]{Player}},null)!;
        else{session=Activator.CreateInstance(sessionType,true)!;sessionType.GetField("Owner",F)!.SetValue(session,Player);sessionType.GetField("Combat",F)!.SetValue(session,Combat);coordinator.GetField("_combat",F)!.SetValue(null,Combat);}
        field.SetValue(null,session);return new MisatoExtraScope(()=>field.SetValue(null,old));
    }
    static async Task MuuaTurnCases()
    {
        MuuaReset();Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Player);var gauge=NewGauge();var ability=await MuuaApply();HbrFaithResource.Gain(Hero,7);await ability.AfterEnergyReset(Player);FaithEquals(2,"71 ordinary turn spends one complete set of5");Equal(gauge.PreciseCharge,gauge.ChargePerLevel*.5m,"71 one exchange grants50percent OD");
        await ability.AfterEnergyReset(Player);FaithEquals(2,"71 duplicate reset callback does not exchange twice in same ordinary turn");
        HbrFaithResource.Gain(Hero,8);using(MuuaExtra(false)){await (Task)Call("HbrInsertedTurnStartHelper","Start",new ThrowingPlayerChoiceContext(),Player)!;FaithEquals(0,"71 personal extra turn independently exchanges all complete sets");Equal(gauge.PreciseCharge,gauge.ChargePerLevel*1.5m,"71 personal turn grants two explicit50percent exchanges");}
        HbrFaithResource.Gain(Hero,5);await ability.AfterEnergyReset(Player);FaithEquals(5,"71 returning to interrupted ordinary turn retains its receipt");
        using(MuuaExtra(true)){await (Task)Call("HbrInsertedTurnStartHelper","Start",new ThrowingPlayerChoiceContext(),Player)!;FaithEquals(0,"71 OD extra turn independently exchanges faith");}
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);HbrFaithResource.Gain(Hero,10);await ability.AfterEnergyReset(Player);FaithEquals(0,"71 next ordinary turn can exchange twice again");
        MuuaReset();gauge=NewGauge();ability=await MuuaApply();HbrFaithResource.Gain(Hero,BigInteger.Pow(10,40)+2);
        await ability.AfterEnergyReset(Player);FaithEquals(2,"71 huge faith exchanges preserve exact remainder without iterating empty gauge gains");
        Equal(gauge.PreciseCharge,gauge.MaxCharge,"71 huge faith exchanges reach the same finite ordinary native gauge capacity");
        MuuaReset();gauge=NewGauge();ability=await MuuaApply(1,2,3);var dp=NewDp(1000);int before=dp.Dp;HbrFaithResource.Gain(Hero,4);await ability.AfterEnergyReset(Player);FaithEquals(0,"71 SummerGrace gain occurs before five-point exchange");
        Equal(MuuaEnergy,1,"71 one NewDay exchange grants1energy");Equal(MuuaDraws,1,"71 one NewDay exchange draws1");Check(dp.Dp>before,"71 DelightfulSprint heals the real DP pool");Check(Hero.HasPower<MindsEyePower>(),"71 DelightfulSprint gives one-count MindEye");
        MuuaReset();Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Player);NewGauge();ability=await MuuaApply(5);await ability.AfterEnergyReset(Player);Check(!Hero.HasPower<HbrAttackUpPower>(),"71 SweetHealing is absent on ordinary start");
        using(MuuaExtra(false)){await (Task)Call("HbrInsertedTurnStartHelper","Start",new ThrowingPlayerChoiceContext(),Player)!;Check(!Hero.HasPower<HbrAttackUpPower>(),"71 SweetHealing is absent on personal start");}
        using(MuuaExtra(true)){await (Task)Call("HbrInsertedTurnStartHelper","Start",new ThrowingPlayerChoiceContext(),Player)!;Check(Hero.HasPower<HbrAttackUpPower>()&&Hero.HasPower<HbrCriticalDamageUpPower>(),"71 actual OD membership grants SweetHealing pair even without faith exchange");Equal(Hero.GetPower<HbrAttackUpPower>()!.Amount,1,"71 SweetHealing attack buff has one attack count");}
    }
    static async Task MuuaRescueCases()
    {
        MuuaReset();var dp=NewDp();Set(dp,"_dp",dp.MaxDp);var niina=NewGarden<NarcissismIsFineHbrgoSaki>(5);MisatoPermanent(niina);await (Task)Call("NiinaMakeupCombatHelper","OnCombatStart",Combat)!;Check(Hero.HasPower<NiinaMakeupPower>(),"71 setup grants real permanent-deck Makeup");var ability=await MuuaApply(6);
        Native(dp,"set_Dp",0);Equal(dp.Dp,decimal.Floor(dp.MaxDp*.2m),"71 real DP setter immediately restores20percent once");FaithEquals(3,"71 first DP zero gains3faith");Check(!Hero.HasPower<NiinaMakeupPower>(),"71 rescue clears Makeup before DP rises again");
        await (Task)Call("NiinaMakeupCombatHelper","OnCombatStart",Combat)!;Check(!Hero.HasPower<NiinaMakeupPower>(),"71 later DP recovery cannot resurrect lost Makeup");
        Native(dp,"set_Dp",0);Equal(dp.Dp,0,"71 second DP zero does not rescue");FaithEquals(3,"71 second DP zero does not award more faith");Check(ability.DpRescueUsed,"71 once-combat rescue receipt is consumed synchronously");
        MuuaReset();dp=NewDp(0);ability=await MuuaApply(6);Native(dp,"set_Dp",0);FaithEquals(0,"71 existing zero DP never fabricates a new zero transition");Check(!ability.DpRescueUsed,"71 starting zero DP does not spend unused rescue");
        MuuaReset();dp=NewDp();Set(dp,"_dp",1);await MuuaApply(6);var fubuki=NewGarden<MakeAChangeKillYourselfHbrgoSaki>();await NewPlay(fubuki);FaithEquals(3,"71 actual Fubuki self-DP cost can trigger NeverFall");Check(dp.Dp>0,"71 Fubuki self-DP cost receives immediate20percent rescue");
    }
    static async Task MuuaShellCases()
    {
        foreach(bool remove in new[]{false,true})
        {
            MuuaReset();Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());
            var reign=(MomentOfReign)ModelDb.Relic<MomentOfReign>().ToMutable();Set(reign,"_owner",Player);Set(Player,"_relics",Player.Relics.Concat(new[]{reign}).ToList());
            Creature phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});
            var shell=(PhantomHighDimensionalShellPower)ModelDb.Power<PhantomHighDimensionalShellPower>().ToMutable();
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(),shell,phantom,1m,Hero,null);decimal maximum=(decimal)Native(shell,"get_MaxAmount")!;Native(shell,"SetDpAmount",maximum,false);
            var niina=NewGarden<NarcissismIsFineHbrgoSaki>(5);MisatoPermanent(niina);await (Task)Call("NiinaMakeupCombatHelper","OnCombatStart",Combat)!;
            Check(Hero.HasPower<NiinaMakeupPower>(),"71 real30A phantomDP can support Makeup before depletion");await MuuaApply(6);
            if(remove)await PowerCmd.Remove(shell);else Native(shell,"SetDpAmount",0m,false);
            var pool=phantom.GetPower<HighDimensionalShellPower>();Check(pool!=null,"71 DP rescue leaves an actual phantom resource shell after removal="+remove);
            Equal(pool!.CurrentDp,decimal.Floor(maximum*.2m),"71 actual native shell zero/removal restores20percent; remove="+remove);FaithEquals(3,"71 shell zero/removal awards exactly3faith; remove="+remove);
            Check(!Hero.HasPower<NiinaMakeupPower>(),"71 shell rescue clears the owner's Makeup before recreating itsDP pool");
            await PowerCmd.Remove(pool);FaithEquals(3,"71 later explicit shell removal cannot reuse the rescue receipt");
        }
    }
    public static void RunMuua(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".muua71");
            foreach(Type t in new[]{typeof(IWillNotFallHbrgoSaki),typeof(HbrFaithPower),typeof(MuuaIWillNotFallPower),typeof(MindsEyePower),typeof(HbrAttackUpPower),typeof(HbrCriticalDamageUpPower),typeof(NarcissismIsFineHbrgoSaki),typeof(NiinaMakeupPower),typeof(SmallHopper),typeof(DoublePowersHbrgoSaki),typeof(Overclock),typeof(MakeAChangeKillYourselfHbrgoSaki),typeof(PhantomHighDimensionalShellPower),typeof(PhantomPower),typeof(MomentOfReign),typeof(HbrDefenseDownPower)})
            {if(!ModelDb.Contains(t))ModelDb.Inject(t);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(t)),"<Id>k__BackingField",ModelDb.GetId(t));}
            foreach(Type t in new[]{typeof(HbrFaithPower),typeof(MuuaIWillNotFallPower),typeof(IWillNotFallHbrgoSaki)})MuuaSaveType(t);
            foreach(string name in new[]{"HbrEffectApplicationPatch","HbrEffectInstanceStackingPatch","MuuaFaithDeflectorPatch","MuuaFaithShellPatch","MuuaFaithShellRemovalPatch","FaithNativeCounterPatch","MuuaFaithCombatEndPatch","HighDimensionalShellNativeAmountPatch"})
                Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"71 installs actual production patch "+name);
            h.CreateClassProcessor(Mod.GetTypes().Single(t=>t.Name=="IntegerStatAmountPatch")).Patch();
            Type action=Mod.GetTypes().Single(t=>t.Name=="ThirtyOneBEnemyMoveScopePatch");h.CreateClassProcessor(action).Patch();
            foreach(Type t in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(t.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoListeners),F)!){priority=Priority.First+100});
            h.Unpatch(Canonical,typeof(EffectChecks).GetMethod(nameof(Terminal),F)!);
            h.Patch(Canonical,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MuuaDamageLeaf),F)!){priority=Priority.Last});
            h.Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MuuaCounterLeaf),F)!){priority=Priority.Last});
            h.Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.GainEnergy))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MuuaEnergyLeaf),F)!));
            h.Patch(typeof(CardPileCmd).GetMethods(F).Single(m=>m.Name=="Draw"&&m.GetParameters().Length==4),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MuuaDrawLeaf),F)!));
            MuuaResourceCases().GetAwaiter().GetResult();MuuaHitCases().GetAwaiter().GetResult();MuuaTurnCases().GetAwaiter().GetResult();MuuaRescueCases().GetAwaiter().GetResult();MuuaShellCases().GetAwaiter().GetResult();MuuaRedirectCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real card OnPlay/native PowerCmd/native SavedProperties/real Monster.PerformMove/action scopes and production connected-result append; damage and attack animation leaves controlled. Native ordinary callback and actual OD/personal coordinator session membership; real DP setter and Fubuki DP cost/Makeup invariant. No rendered Godot or live network session."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Muua71 checks");
    }
}
