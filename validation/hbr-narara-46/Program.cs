using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class NararaStandalone { public static void Run(string path) => EffectChecks.RunNarara(path); }
static partial class EffectChecks
{
    static NararaShibaBossMonster NararaBoss = null!;
    static NararaNullificationPower NararaNull = null!;
    static PowerModel NararaPressure = null!;
    static readonly List<decimal> NararaHeals = [];
    static decimal NararaUpdatedMax;
    static bool NararaHealLeaf(Creature creature, decimal amount, ref Task __result)
    { NararaHeals.Add(amount); __result = Task.CompletedTask; return false; }
    static bool NararaMaxLeaf(Creature creature, decimal amount, ref Task<decimal> __result)
    { NararaUpdatedMax = amount; Set(creature, "_maxHp", (int)amount); __result = Task.FromResult(amount); return false; }
    static bool NararaNoPresentation() => false;
    static bool NararaNoStartup(ref Task __result) { __result=Task.CompletedTask;return false; }
    static void NararaInject(Type type)
    {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
    static Type NararaPressureType => Mod.GetType("HBRGOSakiMod.Core.Models.Powers.NararaPressurePower",true)!;
    static int PressureNow => (int)NararaPressureType.GetProperty("Pressure",F)!.GetValue(NararaPressure)!;
    static void NararaReset(bool hbr=true,bool phantom=false)
    {
        FormReset(hbr); Fate(FateJson(bosses:0)); UnifiedDice(); NararaHeals.Clear(); NararaUpdatedMax=0;
        NararaBoss=Blank<NararaShibaBossMonster>();Set(NararaBoss,"<IsMutable>k__BackingField",true);Set(NararaBoss,"_creature",Enemy);
        Set(Enemy,"<Monster>k__BackingField",NararaBoss);Set(Enemy,"_petOwner",phantom?Player:null);Set(Enemy,"<Side>k__BackingField",phantom?CombatSide.Player:CombatSide.Enemy);
        Set(Enemy,"_maxHp",10000);Set(Enemy,"_currentHp",5000);Set(Enemy,"_block",0);Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",1);
        NararaBoss.SetUpForCombat();
        Set(NararaBoss,"<NextMove>k__BackingField",NararaBoss.MoveStateMachine!.States[NararaShibaBossMonster.GoodSleepMoveId]);
        NararaNull=(NararaNullificationPower)ModelDb.Power<NararaNullificationPower>().ToMutable();Set(NararaNull,"_owner",Enemy);Set(NararaNull,"_amount",1);Powers(Enemy).Add(NararaNull);
        NararaPressure=(PowerModel)ModelDb.GetById<PowerModel>(ModelDb.GetId(NararaPressureType)).ToMutable();Set(NararaPressure,"_owner",Enemy);Set(NararaPressure,"_amount",1);Powers(Enemy).Add(NararaPressure);
    }
    static bool NararaNullified(CardModel card) => (bool)Call("NararaNullificationHelper","IsCardNullified",Enemy,card)!;
    static async Task NararaPlay(CardModel card, int raw, Func<CardPlay,Task>? inside=null, bool automatic=false)
    {
        if(card.Pile==null)Player.PlayerCombatState!.Hand.AddInternal(card);
        UnifiedDice(raw);
        var play=Play(card);Set(play,"<IsAutoPlay>k__BackingField",automatic);
        object cs=Call("CardPlayContextHelper","Begin",card,automatic)!;
        object ns=Call("NararaNullificationHelper","Begin",card)!;
        try
        {
            Type adapter=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.NararaContestOrderPatch",true)!;
            await (Task)Invoke(adapter.GetMethod("BeforeCardPlayed",F)!,null,Combat,play)!;
            if(inside!=null)await inside(play);
        }
        finally { Call("NararaNullificationHelper","Close",ns);Call("CardPlayContextHelper","Close",cs); }
    }
    static async Task NararaDispatchHit(Creature dealer,CardModel? card,decimal hit=1m)
    {
        DamageResult result=new(Enemy,ValueProp.Move){BlockedDamage=(int)hit};
        MethodInfo hook=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterDamageReceived");
        object?[] args=hook.GetParameters().Select(p=>p.ParameterType==typeof(Creature)?(p.Name=="dealer"?dealer:Enemy):
            p.ParameterType==typeof(DamageResult)?result:p.ParameterType==typeof(ValueProp)?(object)ValueProp.Move:
            p.ParameterType==typeof(CardModel)?card:p.ParameterType==typeof(CardPlay)?null:
            p.ParameterType==typeof(PlayerChoiceContext)?new ThrowingPlayerChoiceContext():
            p.ParameterType.IsAssignableFrom(typeof(CombatState))?Combat:p.ParameterType.IsAssignableFrom(RunState.GetType())?RunState:throw new Exception("Unknown native hit param "+p)).ToArray();
        await (Task)Invoke(hook,null,args)!;
    }
    static async Task NararaCases()
    {
        NararaReset();var card=Owned<DefendIronclad>();
        await NararaPlay(card,100,async play=>
        {
            Check(!NararaNullified(card),"100 versus an unmodified boss die wins strictly");
            Check((bool)Call("NararaNullificationHelper","IsSuccessful",Enemy,Hero,card)!,"success binds the exact hero/card/boss");
            Equal(NararaNull.ModifyDamageMultiplicative(Enemy,100m,ValueProp.Move,Hero,card),1.5m,"successful card gains50percent damage amplification");
            var other=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;
            Equal(NararaNull.ModifyDamageMultiplicative(other,100m,ValueProp.Move,Hero,card),1m,"success does not amplify another target");
            Equal(NararaNull.ModifyDamageMultiplicative(Enemy,100m,ValueProp.Move,other,card),1m,"success does not amplify an independent summon dealer");
            await NararaNull.AfterCardPlayed(new ThrowingPlayerChoiceContext(),play);
        });
        Equal(UnifiedRawReads,1,"comparison takes its own single ordinary raw die");Check(NararaHeals.Count==0,"winning comparison does not heal");
        await NararaPlay(Owned<DefendIronclad>(),1,play=>{Check(UnifiedRawReads==0,"second manual card has no comparison");return Task.CompletedTask;});
        NararaReset();card=Owned<DefendIronclad>();
        await NararaPlay(card,1,play=>{Check(UnifiedRawReads==0,"autoplay rolls no comparison");return Task.CompletedTask;},true);
        await NararaPlay(card,1,async play=>
        {
            Check(NararaNullified(card),"first manual card still compares after autoplay and loses");
            Equal(NararaNull.ModifyDamageCap(Enemy,ValueProp.Move,Hero,card),0m,"failed card has zero boss damage cap");
            Check((bool)Call("NararaNullificationHelper","BlocksPowerChange",ModelDb.Power<HbrAttackDownPower>(),Enemy,1m,card)!,"failure blocks new HBR debuff application");
            await (Task)StaticCall(Helper("HbrPercentDebuffApplication"),"ApplyAttackDown",new ThrowingPlayerChoiceContext(),Enemy,1m,Hero,card,true)!;
            Check(!Enemy.HasPower<HbrAttackDownPower>(),"real common attack-down application is blocked on failed target");
            var shell=Power<HighDimensionalShellPower>(Enemy,1000);
            decimal spent=await (Task<decimal>)Native(shell,"DamageDpOnly",new ThrowingPlayerChoiceContext(),100m,Hero,card,null)!;
            Equal(spent,0m,"failed card cannot spend DP through the real DP-only command");
            Equal(shell.CurrentDp,1000m,"failed DP-only command preserves exact shell ledger");
            Equal(Actual(Enemy,Hero,6m,ValueProp.Move,card),0m,"real native shared damage Hook applies failed comparison's zero cap");
            await HbrApplyOwnBuff(card);
            Check(Hero.HasPower<HbrAttackUpPower>(),"real native/common self buff remains effective on a failed card");
            Check(!(bool)Call("NararaNullificationHelper","BlocksPowerChange",ModelDb.Power<HbrAttackUpPower>(),Hero,1m,card)!,"failure preserves self buffs");
            await NararaNull.AfterCardPlayed(new ThrowingPlayerChoiceContext(),play);await NararaNull.AfterCardPlayed(new ThrowingPlayerChoiceContext(),play);
            Equal(NararaHeals.Single(),200m,"failure heals2percent exact maxHP once across repeats");Equal(Enemy.MaxHp,10000,"failure never grows maximumHP");
        });
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);
        await NararaPlay(Owned<DefendIronclad>(),1,play=>{Equal(UnifiedRawReads,1,"next ordinary turn restores each player's comparison slot");return Task.CompletedTask;});
        NararaReset();int tie=new RunRngSet(NonCriticalSeed).Niche.NextInt(1,101);
        await NararaPlay(Owned<DefendIronclad>(),tie,play=>{Check(NararaNullified(play.Card),"equal final dice strictly favor the boss");return Task.CompletedTask;});
        NararaTurnSlotCases();
        NararaReset(false);Check(!(bool)typeof(NararaNullificationPower).GetProperty("IsActive",F)!.GetValue(NararaNull)!,"legacy enemy contest remains inactive");
        NararaReset(true,true);Check(!(bool)typeof(NararaNullificationPower).GetProperty("IsActive",F)!.GetValue(NararaNull)!,"phantom contest remains inactive");

        NararaReset();Power<NararaSleepingPower>(Enemy,5);Power<TourGuidePower>(Enemy,1);await UnifiedApply(false,50,3);await UnifiedApply(true,1,3);
        UnifiedDice(20,80);int before=FateValue<int>("Swordheart");
        var contestTask=(Task)Invoke(typeof(RollGlossaryCapability).GetMethod("RollForContest",F)!,null,new ThrowingPlayerChoiceContext(),Player,CardType.Skill)!;
        await contestTask;var contest=contestTask.GetType().GetProperty("Result")!.GetValue(contestTask)!;
        var winner=(SoulRollBreakdown)contest.GetType().GetField("Item1")!.GetValue(contest)!;
        Equal(winner.FinalRoll,80,"comparison gets+50percent bonus and sleeping−50percent, takes higher advantage die");
        Equal(UnifiedRawReads,2,"comparison rolls one extra advantage die");UnifiedGroups(false,(50,2));UnifiedGroups(true,(1,2));
        Equal(FateValue<int>("Swordheart"),before,"comparison gives no swordheart");
        Check(!Hero.HasPower<SwordheartPower>(),"comparison does not apply swordheart power");

        NararaReset();Power<NararaSleepingPower>(Enemy,5);card=Owned<DefendIronclad>();
        object cs=Call("CardPlayContextHelper","Begin",card,false)!;
        try{await NararaDispatchHit(Hero,card);await NararaDispatchHit(Hero,card);Equal(PressureNow,1,"native multi-hit callbacks add one pressure for a whole card");}
        finally{Call("CardPlayContextHelper","Close",cs);}
        Creature phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;
        for(int action=0;action<6;action++)
        {
            using var actionScope=(IDisposable)StaticCall(Helper("PhantomPlayerBenefitContext"),"Begin",phantom,new Creature[]{Enemy})!;
            await NararaDispatchHit(phantom,null);await NararaDispatchHit(phantom,null);
        }
        Check(!Enemy.HasPower<NararaSleepingPower>()&&PressureNow==0,"seventh distinct hit action clears sleep and pressure");
        Check(NararaBoss.NextMove.Id==NararaShibaBossMonster.WorkMoveId,"pressure wake immediately selects Work, skipping both earlier moves");
        Equal((int)Native(NararaBoss,"GetWorkHitCount")!,4,"pressure wake reduces next normal Work to4hits");Equal(NararaBoss.ForcedWakeCount,1,"forced wake counter increments once");
        Check(Powers(Enemy).OfType<HbrAttackUpPower>().Count()==1&&Powers(Enemy).OfType<HbrDefenseUpPower>().Count()==1,"forced wake grants independent attack and defense buff instances");
        for(int n=2;n<=3;n++)
        {
            await PowerCmd.Apply<NararaSleepingPower>(new ThrowingPlayerChoiceContext(),Enemy,5,Enemy,null,true);
            await (Task)Native(NararaBoss,"WakeFromPressure",new ThrowingPlayerChoiceContext())!;
        }
        Equal(NararaBoss.ForcedWakeCount,3,"three forced wakes advance resentment exactly three times");
        var attack=Powers(Enemy).OfType<HbrAttackUpPower>().Select(p=>(decimal)p.GetType().GetProperty("EffectPotency",F)!.GetValue(p)!).Order().ToArray();
        Check(attack.Length==2&&attack[1]==attack[0]*2,"default two-slot rule retains latest two doubled resentment strengths");
        foreach(PowerModel p in Powers(Enemy).OfType<HbrAttackUpPower>().Concat<PowerModel>(Powers(Enemy).OfType<HbrDefenseUpPower>()))
            Check(p.GetType().GetProperty("CurrentEffectProfile",F)!.GetValue(p)!.ToString()!.Contains("Eternal"),"resentment is truly Eternal and not count-triggered");
        Set(NararaBoss,"<NextWorkReducedHits>k__BackingField",false);
        await PowerCmd.Apply<NararaSleepingPower>(new ThrowingPlayerChoiceContext(),Enemy,1,Enemy,null,true);
        await PowerCmd.Remove(Enemy.GetPower<NararaSleepingPower>()!);
        Check(NararaBoss.NextMove.Id==NararaShibaBossMonster.DoNotWantToWorkMoveId&&NararaBoss.ForcedWakeCount==3,"natural wake returns to old cycle without resentment");
        await NararaDispatchHit(Hero,card);Equal(PressureNow,0,"awake hits do not add pressure");

        NararaReset();for(int i=0;i<4;i++){var status=Owned<Wound>();Player.PlayerCombatState!.Hand.AddInternal(status);}
        int current=Enemy.CurrentHp;await (Task)Native(NararaBoss,"UpdateMove",Array.Empty<Creature>())!;
        Equal(NararaUpdatedMax,10200m,"Update snapshots maxHP and adds0point5percent per four hand statuses without compounding");
        Equal(Enemy.CurrentHp,current,"Update does not heal currentHP");
        NararaReset(false);for(int i=0;i<4;i++)Player.PlayerCombatState!.Hand.AddInternal(Owned<Wound>());
        await (Task)Native(NararaBoss,"UpdateMove",Array.Empty<Creature>())!;Equal(NararaUpdatedMax,10400,"legacy Update retains100perstatus");
    }
    static async Task NararaStartupAndDpCases()
    {
        NararaReset();Powers(Enemy).Clear();await NararaBoss.AfterAddedToRoom();
        Check(Enemy.HasPower<NararaNullificationPower>()&&Enemy.GetPower<NararaPressurePower>()?.Pressure==0&&
            Enemy.GetPower<NararaLayeredHpPower>() is { Amount: >0 }&&!Enemy.HasPower<HardenedShellPower>(),
            "real HBR enemy entry installs contest, pressure0 and partitioned HP without the retired turn cap");
        var hpLayers=Enemy.GetPower<NararaLayeredHpPower>()??throw new Exception("HP phase marker missing");int hpLayersBefore=hpLayers.Amount;
        Power<HardenedShellPower>(Enemy,25);await NararaBoss.AfterAddedToRoom();
        Check(!Enemy.HasPower<HardenedShellPower>()&&ReferenceEquals(hpLayers,Enemy.GetPower<NararaLayeredHpPower>())&&hpLayers.Amount==hpLayersBefore,
            "old mutable boss reentry removes retired HardenedShell without resetting HP phases");
        NararaReset(false);Powers(Enemy).Clear();await NararaBoss.AfterAddedToRoom();
        Check(!Enemy.HasPower<NararaNullificationPower>()&&!Enemy.HasPower<NararaPressurePower>()&&!Enemy.HasPower<HardenedShellPower>()&&
            Enemy.GetPower<NararaLayeredHpPower>() is { Amount: >0 },
            "real legacy enemy has the requested four-HP mechanic while HBR-only contest and pressure remain disabled");
        NararaReset(true,true);Powers(Enemy).Clear();await NararaBoss.AfterAddedToRoom();
        Check(!Enemy.HasPower<NararaNullificationPower>()&&!Enemy.HasPower<NararaPressurePower>()&&!Enemy.HasPower<HardenedShellPower>()&&
            !Enemy.HasPower<NararaLayeredHpPower>(),
            "real allied phantom entry never grants enemy-only HP phases, contest, pressure or hardened shell");
        NararaReset();Power<NararaSleepingPower>(Enemy,5);var shell=(HighDimensionalShellPower)ModelDb.Power<HighDimensionalShellPower>().ToMutable();Set(shell,"_owner",Enemy);Set(shell,"_amount",1000);Powers(Enemy).Add(shell);var card=Owned<DefendIronclad>();
        object cs=Call("CardPlayContextHelper","Begin",card,false)!;
        try
        {
            decimal amount=await (Task<decimal>)Native(shell,"DamageDpOnly",new ThrowingPlayerChoiceContext(),50m,Hero,card,null)!;
            Equal(amount,50,"real DP-only command reports actual resource spend");Equal(PressureNow,1,"actual DP-only receipt adds pressure");
            await (Task<decimal>)Native(shell,"DamageDpOnly",new ThrowingPlayerChoiceContext(),50m,Hero,card,null)!;
            Equal(PressureNow,1,"multiple DP-only effects from one card still count once");
        }
        finally{Call("CardPlayContextHelper","Close",cs);}
        NararaReset();Power<NararaSleepingPower>(Enemy,5);card=Owned<DefendIronclad>();
        await NararaPlay(card,1,async play=>{await NararaDispatchHit(Hero,card);Equal(PressureNow,0,"failed comparison cannot add pressure even if a later receipt callback is observed");});
        NararaReset();
        Creature stalePhantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;
        var staleAction=(IDisposable)StaticCall(Helper("PhantomPlayerBenefitContext"),"Begin",stalePhantom,new Creature[]{Enemy})!;
        var captured=System.Threading.ExecutionContext.Capture()!;staleAction.Dispose();object? staleToken=new object();
        System.Threading.ExecutionContext.Run(captured,_=>staleToken=Call("PhantomPlayerBenefitContext","GetActionToken",stalePhantom),null);
        Check(staleToken==null,"a captured context cannot reuse a closed phantom action token");
        NararaReset();Power<NararaSleepingPower>(Enemy,5);
        Creature cannon=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;
        Type phaseType=Mod.GetType("HBRGOSakiMod.Core.Helpers.HotaruCannonPhase",true)!;
        for(int action=0;action<2;action++)
        {
            using var phase=(IDisposable)Call("HotaruCannonSupportUpgradeHelper","BeginPhase",cannon,Enum.Parse(phaseType,"End"),false)!;
            await NararaDispatchHit(Hero,null);await NararaDispatchHit(Hero,null);
            Equal(PressureNow,action*2+1,"owner-attributed native cannon hit uses its full phase identity, action"+action);
            var automatic=Owned<DefendIronclad>();object automaticScope=Call("CardPlayContextHelper","Begin",automatic,true)!;
            try{await NararaDispatchHit(Hero,automatic);await NararaDispatchHit(Hero,automatic);Equal(PressureNow,action*2+2,"an automatic card nested inside a cannon action retains its own card identity");}
            finally{Call("CardPlayContextHelper","Close",automaticScope);}
            await NararaDispatchHit(Hero,null);Equal(PressureNow,action*2+2,"returning from nested automatic card resumes the already-counted cannon action");
        }

    }
    static async Task HbrApplyOwnBuff(CardModel card)
    {
        MethodInfo apply=Helper("HbrEffectApplication").GetMethods(F).Single(m=>m.Name=="Apply"&&m.IsGenericMethodDefinition);
        await (Task)Invoke(apply.MakeGenericMethod(typeof(HbrAttackUpPower)),null,new ThrowingPlayerChoiceContext(),Hero,10m,Hero,card,null,null,true,null)!;
    }
    static void NararaTurnSlotCases()
    {
        NararaReset();Check((bool)Native(NararaNull,"TryClaimTurn",Player)!,"ordinary own turn initially owns one comparison slot");
        Type od=Helper("OverclockImmediateTurnCoordinator"),personal=Helper("PersonalImmediateTurnCoordinator");
        FieldInfo odCurrent=od.GetField("_current",F)!,personalCurrent=personal.GetField("_current",F)!;
        object? oldOd=odCurrent.GetValue(null),oldPersonal=personalCurrent.GetValue(null),oldCombat=personal.GetField("_combat",F)!.GetValue(null);
        try
        {
            var relic=Blank<HBRGOSakiMod.Core.Models.Relics.Overclock>();Type session=od.GetNestedType("Session",F)!;
            object inserted=session.GetConstructors(F).Single().Invoke([relic,Combat,new[]{Player}]);
            odCurrent.SetValue(null,inserted);
            Check(ReferenceEquals(Call("OverclockImmediateTurnCoordinator","GetTurnToken",Player),inserted),"comparison reads the actual active OD session token");
            Check((bool)Native(NararaNull,"TryClaimTurn",Player)!&&!(bool)Native(NararaNull,"TryClaimTurn",Player)!,"one OD session provides one separate slot");
            odCurrent.SetValue(null,null);
            Check(!(bool)Native(NararaNull,"CanClaimTurn",Player)!,"returning from OD preserves the consumed ordinary slot");
            object individual=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(personal.GetNestedType("Session",F)!);
            Set(individual,"Owner",Player);Set(individual,"Combat",Combat);personal.GetField("_combat",F)!.SetValue(null,Combat);personalCurrent.SetValue(null,individual);
            Check(ReferenceEquals(Call("OverclockImmediateTurnCoordinator","GetTurnToken",Player),individual),"comparison reads the actual personal extra-turn token");
            Check((bool)Native(NararaNull,"TryClaimTurn",Player)!,"non-OD personal turn receives its own comparison");
            personalCurrent.SetValue(null,null);
            Check(!(bool)Native(NararaNull,"CanClaimTurn",Player)!,"returning from personal turn also preserves ordinary slot");
            var peer=Blank<Player>();Set(peer,"_runState",RunState);Creature peerHero=(Creature)H("Actor",null,peer,null)!;Set(peer,"<Creature>k__BackingField",peerHero);
            var pcs=Blank<PlayerCombatState>();Set(pcs,"<TurnNumber>k__BackingField",1);Set(peer,"<PlayerCombatState>k__BackingField",pcs);
            Check((bool)Native(NararaNull,"TryClaimTurn",peer)!&&!(bool)Native(NararaNull,"TryClaimTurn",peer)!,"teammate's first comparison slot is independent of holder ordinary and extra turns");
        }
        finally{odCurrent.SetValue(null,oldOd);personalCurrent.SetValue(null,oldPersonal);personal.GetField("_combat",F)!.SetValue(null,oldCombat);}
    }
    public static void RunNarara(string path)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".narara46");
            foreach(Type type in new[]{typeof(NararaNullificationPower),NararaPressureType,typeof(NararaSleepingPower),typeof(TourGuidePower),typeof(NararaOverworkPower),typeof(NararaGiveYouPower),typeof(NararaLayeredDpPower),typeof(NararaLayeredHpPower),typeof(LargeHealthPower),typeof(HbrAttackDownPower),typeof(HbrAttackUpPower),typeof(HbrDefenseUpPower),typeof(PlatingPower),typeof(StrengthPower),typeof(HardenedShellPower),typeof(HighDimensionalShellPower)})NararaInject(type);
            foreach(string name in new[]{"NararaContestOrderPatch","NararaPressureAttackScopePatch","NararaPressureDpOnlyPatch","HbrEffectApplicationPatch","HbrEffectInstanceStackingPatch","HbrFragileApplyPatch","HbrFragileStackingPatch"})
                Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs production "+name);
            h.Patch(typeof(RollGlossaryCapability).GetMethod("CreateCannonPipeline",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(UnifiedCannonRegistryLeaf),F)!));
            h.Patch(typeof(RollGlossaryCapability).GetMethod("NextRawRoll",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(UnifiedFixedRaw),F)!));
            foreach(MethodInfo wait in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))h.Patch(wait,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            h.Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            h.Patch(typeof(CreatureCmd).GetMethod(nameof(CreatureCmd.Heal))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NararaHealLeaf),F)!));
            h.Patch(typeof(CreatureCmd).GetMethod(nameof(CreatureCmd.SetMaxHp))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NararaMaxLeaf),F)!));
            h.Patch(Helper("NararaContestPresentation").GetMethod("Show",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NararaNoPresentation),F)!));
            h.Patch(typeof(PowerModel).GetMethods(F).Single(m=>m.Name=="Flash"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NararaNoPresentation),F)!));
            NararaCases().GetAwaiter().GetResult();NararaStartupAndDpCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real native BeforeCardPlayed/AfterDamageReceived, shared dice with only registry/raw-face leaves, native PowerCmd instances/removals and actual monster cycle execute; HP heal/set commands and Godot/VFX are terminal leaves. No Godot, package or delivered-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Narara checks");
    }
}

