using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
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
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock=HBRGOSakiMod.Core.Models.Relics.Overclock;

static class YanagiStandalone {public static void Run(string path)=>EffectChecks.RunYanagi(path);}
static partial class EffectChecks
{
    static CardModel? YanagiSelected;
    static readonly List<string> YanagiOrder=[];
    static readonly List<(Player Owner,decimal Amount)> YanagiEnergy=[];
    static readonly List<(Creature Target,decimal Amount)> YanagiHeals=[];
    static readonly List<(decimal Raw,int Hits,decimal Coeff)> YanagiAttacks=[];
    static int YanagiPileSelectors,YanagiGridSelectors;
    static bool YanagiOverflow,YanagiThrow,YanagiUseDp;
    static bool YanagiCannonProbeEnabled;
    static void YanagiCannonProbe(object[] __args)
    {
        if(!YanagiCannonProbeEnabled||__args[5] is not CardModel source)return;
        Equal(Prepared(source),0m,"actual triggered cannon retaining a prepared card source receives no preparation");
    }
    static decimal YanagiControlledDamage=2m;
    static bool YanagiEnergyLeaf(decimal amount,Player player,ref Task __result)
    {YanagiOrder.Add("energy");YanagiEnergy.Add((player,amount));__result=Task.CompletedTask;return false;}
    static bool YanagiSelectPile(CardPile pile,Func<CardModel,bool> filter,ref Task<IEnumerable<CardModel>> __result)
    {YanagiPileSelectors++;YanagiOrder.Add("select");Check(pile.Type==PileType.Draw,"base selector is the native Draw-pile selector");Check(YanagiSelected!=null&&filter(YanagiSelected),"native filter admits selected Draw card");__result=Task.FromResult<IEnumerable<CardModel>>([YanagiSelected!]);return false;}
    static bool YanagiSelectGrid(IReadOnlyList<CardModel> __1,ref Task<IEnumerable<CardModel>> __result)
    {YanagiGridSelectors++;YanagiOrder.Add("select");Check(YanagiSelected!=null&&__1.Contains(YanagiSelected),"combined native Grid contains the selected physical entity");__result=Task.FromResult<IEnumerable<CardModel>>([YanagiSelected!]);return false;}
    static bool YanagiPileAdd(CardModel card,PileType newPileType,ref Task<CardPileAddResult> __result)
    {
        Check(newPileType==PileType.Hand,"fetch delegates transfer to native Hand command");
        card.Pile?.RemoveInternal(card,true);(YanagiOverflow?card.Owner.PlayerCombatState!.DiscardPile:card.Owner.PlayerCombatState!.Hand).AddInternal(card);
        __result=Task.FromResult(new CardPileAddResult{success=true,cardAdded=card});return false;
    }
    static bool YanagiHealLeaf(Creature creature,decimal amount,ref Task __result)
    {YanagiHeals.Add((creature,amount));__result=Task.CompletedTask;return false;}
    static bool YanagiHealInternalLeaf(Creature __instance,decimal __0)
    {YanagiHeals.Add((__instance,__0));return false;}
    static bool YanagiAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=YanagiAttack(__instance);return false;}
    static async Task<AttackCommand> YanagiAttack(AttackCommand command)
    {
        YanagiOrder.Add("attack");await (Task)MisatoHook("BeforeAttack",command)!;
        decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!;
        int count=(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!;
        if(YanagiThrow)throw new Exception("controlled Yanagi attack failure");
        var rows=(List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!;
        decimal coefficient=1m;
        for(int i=0;i<count;i++)
        {
            var results=(await Command(command.Attacker,raw,command.DamageProps,(CardModel)command.ModelSource!,Combat.HittableEnemies)).ToList();rows.Add(results);
            coefficient=YanagiLastCoefficient;
        }
        YanagiAttacks.Add((raw,count,coefficient));await (Task)MisatoHook("AfterAttack",command)!;return command;
    }
    static decimal YanagiLastCoefficient;
    static bool YanagiBodyLeaf(IEnumerable<Creature> targets,decimal raw,ValueProp props,Creature? dealer,CardModel? card,ref Task<IEnumerable<DamageResult>> __result)
    {
        if(card is not AsTheYoungLadyCommandsHbrgoSaki)return true;
        __result=YanagiBody(targets,raw,props,dealer!,card);return false;
    }
    static async Task<IEnumerable<DamageResult>> YanagiBody(IEnumerable<Creature> targets,decimal raw,ValueProp props,Creature dealer,CardModel card)
    {
        await Task.Yield();var rows=new List<DamageResult>();
        foreach(var target in targets)
        {
            YanagiLastCoefficient=(decimal)Call("YanagiButlerCombatHelper","GetDestructionGainMultiplier",card,target,dealer,props)!;
            decimal blocked=YanagiUseDp?target.DamageBlockInternal(YanagiControlledDamage,props):0m;
            var result=new DamageResult(target,props){UnblockedDamage=(int)Math.Max(0m,YanagiControlledDamage-blocked),BlockedDamage=YanagiUseDp?(int)blocked:99,OverkillDamage=999};
            Call("ItsMygoIncomingDamageCapture","AppendResults",rows,new List<DamageResult>{result});
            await NewGiven(card,result);
        }
        return rows;
    }
    static void YanagiReset()
    {
        NewReset();YanagiSelected=null;YanagiOrder.Clear();YanagiEnergy.Clear();YanagiHeals.Clear();YanagiAttacks.Clear();
        YanagiPileSelectors=YanagiGridSelectors=0;YanagiOverflow=YanagiThrow=YanagiUseDp=false;YanagiControlledDamage=2m;
        Set(RunState,"_players",new List<Player>{Player});Set(Combat,"_allies",new List<Creature>{Hero});
        Set(Player.PlayerCombatState!,"_energy",10);SetNativeBlock(0);
        foreach(string field in new[]{"Battles","CompletedTurns"})
        {object table=Helper("YanagiButlerCombatHelper").GetField(field,F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);}
    }
    static void YanagiMove(CardModel card,PileType pile)
    {card.Pile?.RemoveInternal(card,true);pile.GetPile(card.Owner).AddInternal(card);}
    static decimal Prepared(CardModel card,Creature? dealer=null)=> (decimal)Call("YanagiButlerCombatHelper","GetPreparedBonusPercent",card,dealer??Hero,ValueProp.Move)!;
    static Task YanagiPlay(CardModel card,Creature? target=null)
    {var play=Play(card);Set(play,"<Target>k__BackingField",target??Hero);return(Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),play)!;}
    static async Task YanagiFetchCases()
    {
        YanagiReset();var card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>();CardModel selected=FormCard<StrikeIronclad>();YanagiMove(selected,PileType.Draw);YanagiSelected=selected;
        await YanagiPlay(card);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),1,"base Skill costs one energy");
        Check(card is IThirtyOneFMemberCard{ThirtyOneFMember:ThirtyOneFMember.YanagiMion},"card contributes Yanagi31F member");
        Check(selected.Pile?.Type==PileType.Hand&&YanagiPileSelectors==1,"base retrieves same Draw entity through native selector");Equal(selected.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"base native ThisTurn lowers one to zero");
        Check(YanagiResultPile(card)==PileType.Exhaust,"base native result rule exhausts card");
        YanagiReset();card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(0,3);selected=FormCard<StrikeIronclad>();YanagiMove(selected,PileType.Discard);YanagiSelected=selected;
        await YanagiPlay(card);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"Flash is zero-cost");Check(YanagiGridSelectors==1&&selected.Pile?.Type==PileType.Hand,"AlwaysAtHand searches Draw plus Discard together");
        YanagiReset();card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>();await YanagiPlay(card);Check(YanagiPileSelectors==0&&YanagiGridSelectors==0,"empty Draw neither opens selector nor shuffles Discard");
        YanagiReset();card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(5);var lady=NewGarden<LetMeShowYouClassHbrgoSaki>();YanagiMove(lady,PileType.Discard);await YanagiPlay(card);
        Check(lady.Pile?.Type==PileType.Hand&&YanagiGridSelectors==0&&YanagiPileSelectors==0,"LadyFirst finds garden-kanata in Discard without pocket upgrade or popup");Equal(lady.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"LadyFirst replaces reduction with native ThisTurn zero");
        YanagiReset();card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(1);selected=FormCard<Whirlwind>();YanagiMove(selected,PileType.Draw);YanagiSelected=selected;await YanagiPlay(card);
        Check(selected.EnergyCost.CostsX&&selected.Pile?.Type==PileType.Hand,"X card remains a valid fetch and retains X cost");Equal(Prepared(selected),20m,"X selection still receives Preparation");
        YanagiReset();card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(1);selected=FormCard<Wound>();YanagiMove(selected,PileType.Draw);YanagiSelected=selected;int before=selected.EnergyCost.GetWithModifiers(CostModifiers.Local);await YanagiPlay(card);
        Equal(selected.EnergyCost.GetWithModifiers(CostModifiers.Local),before,"unplayable selection receives no cost mutation");Equal(Prepared(selected),20m,"unplayable selection still receives Preparation");
        YanagiReset();card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(1);selected=FormCard<StrikeIronclad>();YanagiMove(selected,PileType.Draw);YanagiSelected=selected;YanagiOverflow=true;await YanagiPlay(card);
        Check(selected.Pile?.Type==PileType.Discard,"native full-hand overflow destination is retained");Equal(selected.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"successful overflow entity retains ThisTurn reduction");Equal(Prepared(selected),20m,"successful overflow entity retains preparation");
        for(int id=0;id<7;id++){YanagiReset();card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(id);Check(YanagiResultPile(card)==PileType.Exhaust,"upgrade"+id+" preserves exhaust");}
    }
    static PileType YanagiResultPile(CardModel card)
    {
#if HBR_BETA_NATIVE
        return ((CardLocation)Native(card,"GetResultLocationForCardPlay")!).pileType;
#else
        return (PileType)Native(card,"GetResultPileTypeForCardPlay")!;
#endif
    }
    static async Task YanagiPreparedCases()
    {
        YanagiReset();var noPile=Blank<StrikeIronclad>();Set(noPile,"<IsMutable>k__BackingField",true);Set(noPile,"_owner",Player);
        Equal(Prepared(noPile),0m,"unprepared sparse card without native pile initialization follows O1 fast path");
        YanagiReset();var attack=FormCard<StrikeIronclad>();Call("YanagiButlerCombatHelper","Prepare",attack);Equal(Prepared(attack),20m,"one preparation is twenty percent");Call("YanagiButlerCombatHelper","Prepare",attack);Call("YanagiButlerCombatHelper","Prepare",attack);Equal(Prepared(attack),40m,"Default limits repeated preparation to two copies");
        Equal(Prepared(noPile),0m,"another prepared card does not force an unmarked sparse card to query native piles");
        var copy=(CardModel)attack.MutableClone();Call("CardCopyContextHelper","RecordCopy",copy);YanagiMove(copy,PileType.Hand);Equal(Prepared(copy),40m,"temporary native clone receives active snapshot");
        Call("YanagiButlerCombatHelper","OnTurnEnd",Player,null);Equal(Prepared(attack),0,"ordinary end clears physical preparation");Equal(Prepared(copy),0,"ordinary end also expires independent inherited snapshot");
        YanagiReset();attack=FormCard<StrikeIronclad>();Call("YanagiButlerCombatHelper","Prepare",attack);
        Type coordinator=Helper("PersonalImmediateTurnCoordinator");FieldInfo current=coordinator.GetField("_current",F)!,combatField=coordinator.GetField("_combat",F)!;object? old=current.GetValue(null),oldCombat=combatField.GetValue(null);
        object session=RuntimeHelpers.GetUninitializedObject(coordinator.GetNestedType("Session",F)!);Set(session,"Owner",Player);Set(session,"Combat",Combat);
        try{combatField.SetValue(null,Combat);current.SetValue(null,session);Equal(Prepared(attack),0,"normal preparation is suspended inside personal extra turn");Call("YanagiButlerCombatHelper","Prepare",attack);Equal(Prepared(attack),20m,"extra turn owns independent preparation");Call("YanagiButlerCombatHelper","OnTurnEnd",Player,session);Equal(Prepared(attack),0,"ending extra removes only extra-scope marks");}
        finally{current.SetValue(null,old);combatField.SetValue(null,oldCombat);}Equal(Prepared(attack),20m,"returning to normal restores its paused preparation");
        MethodInfo end=typeof(Hook).GetMethods(F).Single(method=>method.Name is "AfterTurnEnd" or "AfterSideTurnEnd");
        object?[] endArgs=end.GetParameters().Select(parameter=>parameter.ParameterType==typeof(CombatSide)?(object)CombatSide.Player:
            parameter.ParameterType.IsInstanceOfType(Combat)?Combat:parameter.ParameterType.IsInstanceOfType(RunState)?RunState:
            typeof(PlayerChoiceContext).IsAssignableFrom(parameter.ParameterType)?new ThrowingPlayerChoiceContext():
            typeof(IEnumerable<Creature>).IsAssignableFrom(parameter.ParameterType)?new[]{Hero}:throw new Exception(parameter.ToString())).ToArray();
        await(Task)Invoke(end,null,endArgs)!;Equal(Prepared(attack),0,"real native AfterTurnEnd expires normal preparation");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);Call("YanagiButlerCombatHelper","Prepare",attack);Equal(Prepared(attack),20m,"next actual native turn can prepare again");
        YanagiReset();var origin=FormCard<StrikeIronclad>();MisatoPermanent(origin);var mirror=(CardModel)origin.MutableClone();Set(mirror,"_owner",Player);mirror.DeckVersion=origin;YanagiMove(mirror,PileType.Hand);Call("YanagiButlerCombatHelper","Prepare",mirror);
        Equal(Prepared(mirror),20m,"ordinary/OD mirror uses its physical origin's preparation ledger");Equal(Prepared(origin),0m,"physical permanent deck source never exposes a battle preparation");
        YanagiReset();attack=FormCard<StrikeIronclad>();decimal baseline=Preview(Enemy,Hero,6m,ValueProp.Move,attack);Call("YanagiButlerCombatHelper","Prepare",attack);Equal(Preview(Enemy,Hero,6m,ValueProp.Move,attack),decimal.Floor(baseline*1.2m),"real shared damage preview includes prepared twenty percent");
        await (Task)Invoke(Helper("HbrEffectApplication").GetMethods(F).Single(m=>m.Name=="Apply"&&!m.IsGenericMethod),null,new ThrowingPlayerChoiceContext(),(HbrEffectPower)ModelDb.Power<HbrAttackUpPower>().ToMutable(),Hero,50m,Hero,null,null,null,false,50m)!;
        Equal(Preview(Enemy,Hero,6m,ValueProp.Move,attack),decimal.Floor(Base(6m)*1.7m),"prepared20 and original AttackUp50 share one additive bucket before final flooring");
        var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});Equal(Prepared(attack,phantom),0m,"automatic owned phantom retaining card source cannot borrow Preparation");
        var cannon=(Creature)H("Actor",Blank<HotaruContestCannon>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,cannon});
        try{YanagiCannonProbeEnabled=true;await(Task)Call("HotaruCannonStatsHelper","ReactionDamage",new ThrowingPlayerChoiceContext(),cannon,Enemy,6m,attack,null,false,false)!;}finally{YanagiCannonProbeEnabled=false;}
        await YanagiRecoveryCases();
    }
    static async Task YanagiRecoveryCases()
    {
        YanagiReset();var card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(2);await YanagiPlay(card);Check(YanagiEnergy.Single()==(Player,1m),"solo maintenance gives owner one energy before selection");
        YanagiReset();card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(2);var peer=SupportTeammate();Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});Check(card.TargetType==TargetType.AnyAlly&&card.IsValidTarget(Hero)&&card.IsValidTarget(peer.Creature),"coop maintenance permits self or teammate");
        var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Check(!card.IsValidTarget(phantom)&&!card.IsValidTarget(Enemy),"maintenance rejects phantom and enemy recipient");await YanagiPlay(card,peer.Creature);Check(YanagiEnergy.Single()==(peer,1m),"coop selected player receives maintenance energy");
        YanagiReset();var dp=NewDp(0);int max=dp.MaxDp;card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(4);await YanagiPlay(card);Equal(dp.Dp,decimal.Floor(max*.15m),"HighBeams restores actual maxDP fifteen percent");await YanagiPlay(card);Equal(dp.Dp,decimal.Floor(max*.15m),"second play of same physical card cannot restore twice");
        var mirror=(AsTheYoungLadyCommandsHbrgoSaki)card.MutableClone();Set(mirror,"_owner",Player);mirror.DeckVersion=card;await YanagiPlay(mirror);Equal(dp.Dp,decimal.Floor(max*.15m),"OD mirror shares first-play HighBeams receipt");
        for(int i=0;i<11;i++)await YanagiPlay(NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(4));Equal(dp.Dp,decimal.Floor(max*1.5m),"independent physical beams can reach150percent maxDP ceiling");
        await(Task)Call("SeiraDpRecoveryHelper","Restore",new ThrowingPlayerChoiceContext(),Hero,.1m,true,null)!;Equal(dp.Dp,decimal.Floor(max*1.5m),"old120recovery never truncates an existing150pool");
        Check(dp.DpRecoveryOverflowPercent==150,"actual native DP relic stores150ceiling");YanagiSaved(dp);
        YanagiReset();dp=NewDp(0);max=dp.MaxDp;await(Task)Call("SeiraDpRecoveryHelper","Restore",new ThrowingPlayerChoiceContext(),Hero,2m,true,null)!;Equal(dp.Dp,decimal.Floor(max*1.2m),"default existing Restore remains120percent");
        YanagiReset();dp=NewDp(0);max=dp.MaxDp;var heal=FormCard<DefendIronclad>();Call("YanagiButlerCombatHelper","Prepare",heal);await(Task)Call("SeiraDpRecoveryHelper","Restore",new ThrowingPlayerChoiceContext(),Hero,.1m,false,heal)!;Equal(dp.Dp,decimal.Floor(max*.12m),"prepared DP recovery travels real shared resource entry");
        await YanagiHealCases();
    }
    static void YanagiSaved(AbstractModel model)
    {
        Type? cache=typeof(SavedProperties).Assembly.GetTypes().SingleOrDefault(type=>type.Name=="SavedPropertiesTypeCache");
        if(cache!=null)Invoke(cache.GetMethod("InjectTypeIntoCache",F)!,null,model.GetType());else{cache=typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;Invoke(cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!,null,model.GetType());cache.GetField("_initialized",F)!.SetValue(null,true);}
        Set(model,"<Id>k__BackingField",ModelDb.GetId(model.GetType()));
        var options=new JsonSerializerOptions{IncludeFields=true};string json=JsonSerializer.Serialize(SavedProperties.From(model),options);var copy=model.MutableClone();JsonSerializer.Deserialize<SavedProperties>(json,options)!.Fill(copy);
        Equal((int)Native(copy,"get_DpRecoveryOverflowPercent")!,150,"native SavedProperties JSON/Fill preserves150ceiling");
    }
    static async Task YanagiHealCases()
    {
        // The catalog itself is part of the evidence and installed at real
        // safe nongeneric card call sites, never on a global ambient Heal.
        Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.YanagiPreparedHealCallsitePatch",true)!;
        object coverage=patch.GetProperty("Coverage",F)!.GetValue(null)!;
        var sites=((System.Collections.IEnumerable)coverage.GetType().GetProperty("Sites")!.GetValue(coverage)!).Cast<object>().ToArray();
        Check(sites.Length>2,"prepared Heal covers native and mod healing cards");
        foreach(var site in sites){MethodInfo caller=(MethodInfo)site.GetType().GetProperty("Caller")!.GetValue(site)!;Check(!caller.ContainsGenericParameters,"healing callsite uses no generic detour: "+caller.DeclaringType!.FullName);}
        Cases.Add(new{healingCallsites=sites.Select(site=>new{caller=((MethodInfo)site.GetType().GetProperty("Caller")!.GetValue(site)!).DeclaringType!.FullName,card=((Type)site.GetType().GetProperty("CardType")!.GetValue(site)!).FullName,count=site.GetType().GetProperty("Count")!.GetValue(site)}).ToArray(),scanMs=coverage.GetType().GetProperty("Milliseconds")!.GetValue(coverage)});
        // Use a real original healing card discovered from its own body.
        Type nativeCard=sites.Select(site=>(Type)site.GetType().GetProperty("CardType")!.GetValue(site)!).First(type=>type.Assembly==typeof(CardModel).Assembly&&type.Name=="NotYet");
        if(!ModelDb.Contains(nativeCard))ModelDb.Inject(nativeCard);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(nativeCard)),"<Id>k__BackingField",ModelDb.GetId(nativeCard));
        YanagiReset();var card=(CardModel)Invoke(typeof(EffectChecks).GetMethod(nameof(FormCard),F)!.MakeGenericMethod(nativeCard),null,false)!;await YanagiPlay(card);decimal plain=YanagiHeals.Last().Amount;YanagiHeals.Clear();Call("YanagiButlerCombatHelper","Prepare",card);await YanagiPlay(card);Equal(YanagiHeals.Last().Amount,plain*1.2m,"actual native NotYet body binds its exact prepared source after fixed units");
        object scope=Call("CardPlayContextHelper","Begin",card,false)!;
        try
        {
            await CreatureCmd.Heal(Hero,100m,false);Equal(YanagiHeals.Last().Amount,100m,"unrelated raw/relic/potion Heal inside the prepared play never borrows its ambient source");
            await CreatureCmd.GainMaxHp(Hero,100m);Equal(YanagiHeals.Last().Amount,100m,"native maxHP structural healing inside the same play never borrows Preparation");
        }
        finally{Call("CardPlayContextHelper","Close",scope);}
    }
    static async Task YanagiCleaningCases()
    {
        YanagiReset();var dp=NewDp(0);var card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(6);await YanagiPlay(card);var attack=YanagiAttacks.Single();Equal(attack.Raw,4.56m,"cleaning76percent uses4point56units");Equal(attack.Hits,2,"cleaning uses one native command with two hits");Equal(attack.Coeff,.4m,"cleaning destruction1point2 normalized by common3");Equal(dp.Dp,1m,"actual two HP hits total4 restore floor1 once excluding block99and overkill999");
        Check(card.Type==CardType.Skill,"cleaning is not an attack-card play");
        YanagiReset();var gauge=NewGauge();dp=NewDp(0);card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(6);var damageShell=Power<HighDimensionalShellPower>(Enemy,2);Set(damageShell,"_pendingExtraDpDamage",new Stack<decimal>());YanagiUseDp=true;await YanagiPlay(card);
        Equal(dp.Dp,1m,"actual primary DP2 plus later HP2 produce one floor4times30percent recovery");
        Equal(gauge.PreciseCharge,gauge.ChargePerLevel*.05m,"two actual cleaning powered hits earn normal five percent OD despite Skill source");
        YanagiReset();dp=NewDp(0);card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(2,4,6);var selected=FormCard<StrikeIronclad>();YanagiMove(selected,PileType.Draw);YanagiSelected=selected;await YanagiPlay(card);Check(YanagiOrder.SequenceEqual(new[]{"energy","attack","select"}),"maintenance precedes beams/cleaning and retrieval comes last");
        YanagiReset();card=NewGarden<AsTheYoungLadyCommandsHbrgoSaki>(6);YanagiThrow=true;try{await YanagiPlay(card);throw new Exception("expected failure");}catch(Exception error)when(error.Message.Contains("controlled Yanagi")){}Check(AsyncValue("YanagiButlerCombatHelper","Cleaning")==null,"failed native cleaning closes its source receipt");
    }
    public static void RunYanagi(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".yanagi80");
            foreach(Type type in new[]{typeof(AsTheYoungLadyCommandsHbrgoSaki),typeof(LetMeShowYouClassHbrgoSaki),typeof(BindingRedemptionHbrgoSaki),typeof(EmberRecoveryHbrgoSaki),typeof(DeflectorProtection),typeof(Overclock),typeof(StrikeIronclad),typeof(DefendIronclad),typeof(Whirlwind),typeof(Wound),typeof(HighDimensionalShellPower),typeof(PhantomHighDimensionalShellPower),typeof(HbrAttackUpPower),typeof(HbrRecoveryDownPower),typeof(PhantomPower),typeof(SmallHopper)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string patch in new[]{"YanagiEnergyTargetPatch","YanagiPreparedClonePatch","YanagiPreparedTurnEndPatch","YanagiCleaningDamagePatch","YanagiPreparedHealCallsitePatch","HighHitDamageAttackScopePatch","HighHitDamageCommandScopePatch","CreatureDamageBlockInternalPatch","AinaRecoveryDownDpPatch","AinaRecoveryDownOvercapDpPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+patch,true)!).Patch().Count>0,"installs "+patch);
            void Patch(MethodBase target,string name,int priority=Priority.First+100)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(name,F)!){priority=priority});
            foreach(Type t in new[]{RunState.GetType(),typeof(CombatState)})Patch(t.GetMethod("IterateHookListeners",F)!,nameof(MisatoListeners));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,nameof(FormPowerIconLeaf));
            Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,nameof(YanagiAttackLeaf),Priority.Last);Patch(typeof(EffectChecks).GetMethod(nameof(Body),F)!,nameof(YanagiBodyLeaf));
            h.Patch(Canonical,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(YanagiCannonProbe),F)!){priority=0});
            Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.GainEnergy))!,nameof(YanagiEnergyLeaf));
            Patch(typeof(Creature).GetMethods(F).Single(method=>method.Name=="HealInternal"),nameof(YanagiHealInternalLeaf));
            Patch(typeof(CardPileCmd).GetMethods(F).Single(method=>method.Name=="Add"&&method.GetParameters()[0].ParameterType==typeof(CardModel)&&method.GetParameters()[1].ParameterType==typeof(PileType)),nameof(YanagiPileAdd));
            Patch(typeof(CardSelectCmd).GetMethods(F).Single(method=>method.Name=="FromCombatPile"&&method.GetParameters().Any(parameter=>parameter.ParameterType==typeof(Func<CardModel,bool>))),nameof(YanagiSelectPile));
            Patch(typeof(CardSelectCmd).GetMethods(F).Single(method=>method.Name=="FromSimpleGrid"),nameof(YanagiSelectGrid));
            YanagiInstallResourceLeaves(h);YanagiFetchCases().GetAwaiter().GetResult();YanagiPreparedCases().GetAwaiter().GetResult();YanagiCleaningCases().GetAwaiter().GetResult();YanagiNativeBlockCases().GetAwaiter().GetResult();YanagiRecoveryDownCases();YanagiSharedRecoveryCases().GetAwaiter().GetResult();YanagiShellCeilingCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual80 upgraded card bodies and native selection entry, native cost modifiers/target validation/ordinary turn hook; real shared damage and resource entries, typed healing-card callsite transpilers. Choice UI/hand-layout/energy animation and terminal damage/Heal writes controlled. No Godot/rendering/network/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Yanagi checks");
    }
}
