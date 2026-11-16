using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.RelicPools;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;

static class ClassRelicsStandalone { public static void Run(string path) => EffectChecks.RunClassRelics(path).GetAwaiter().GetResult(); }
static partial class EffectChecks
{
    static readonly Harmony ClassHarmony = new("hbr.31o.class-relics.checks");
    static readonly List<(Player Owner, decimal Count)> ClassDraws = [];
    static readonly List<(Player Owner, decimal Count)> ClassEnergy = [];
    static CardModel? ClassSelected;
    static int ClassSelectors;
    static readonly List<(CardModel Card, int Before, decimal After)> ClassAttacks = [];
    static readonly Type[] ThirtyORelics = [typeof(MetronomeThirtyOneO),typeof(MarkOfSeven),typeof(BoneChinaTeacup),typeof(SakuraSwordTassel),typeof(ScoreOfHaruhikage),typeof(MusicStand)];
    static bool ClassDraw(Player player, decimal count, ref Task<IEnumerable<CardModel>> __result)
    { ClassDraws.Add((player,count)); __result = Task.FromResult<IEnumerable<CardModel>>([]); return false; }
    static bool ClassGainEnergy(Player player,decimal amount,ref Task __result)
    { ClassEnergy.Add((player,amount)); __result=Task.CompletedTask; return false; }
    static bool ClassSelect(CardPile pile,Player player,CardSelectorPrefs prefs,Func<CardModel,bool> filter,ref Task<IEnumerable<CardModel>> __result)
    {
        ClassSelectors++;
        Check(player==Player && pile==Player.PlayerCombatState!.DrawPile,"MusicStand forwards exact holder and draw pile to actual native selector");
        Check(pile.Cards.Any(filter)&&ClassSelected!=null&&filter(ClassSelected),"MusicStand native selector predicate admits actual chosen form card");
        Check(pile.Cards.Where(filter).All(card=>card is SlideStepHbrgoSaki or SwallowReversalHbrgoSaki),"MusicStand predicate excludes normal and scythe cards in mixed draw pile");
        __result=Task.FromResult<IEnumerable<CardModel>>([ClassSelected!]);return false;
    }
    static bool ClassAdd(CardModel card,PileType newPileType,ref Task<CardPileAddResult> __result)
    { Check(newPileType==PileType.Hand,"MusicStand requests native Hand transfer");card.Pile?.RemoveInternal(card,true);card.Owner.PlayerCombatState!.Hand.AddInternal(card);__result=Task.FromResult(new CardPileAddResult{success=true,cardAdded=card});return false; }
    static bool ClassAttack(AttackCommand __instance,ref Task<AttackCommand> __result)
    {
        var source=(CardModel)__instance.ModelSource!;
        int count=(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(__instance)!;
        decimal hits=Hook.ModifyAttackHitCount(Combat,__instance,count);
        ClassAttacks.Add((source,count,hits));__result=Task.FromResult(__instance);return false;
    }
    static void ClassPatch(MethodBase target,string prefix)=>ClassHarmony.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix,F)!));
    static T ClassRelic<T>() where T:RelicModel
    {
        T relic=(T)ModelDb.Relic<T>().ToMutable();Set(relic,"_owner",Player);
        Set(Player,"_relics",Player.Relics.Append(relic).ToList());return relic;
    }
    static async Task ClassEnterMode(Type type)
    {
        MethodInfo method=typeof(PowerCmd).GetMethods(F).Single(method=>method.Name=="Apply"&&method.IsGenericMethodDefinition&&method.GetParameters() is var p&&p.Length==6&&p[1].ParameterType==typeof(Creature));
        await (Task)Invoke(method.MakeGenericMethod(type),null,new ThrowingPlayerChoiceContext(),Hero,1m,Hero,null,false)!;
    }
    static void ClassReset(bool hbr=true,bool thirtyO=true)
    {
        FormReset(hbr,thirtyOneO:thirtyO);Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",1);
        Set(RunState,"_allCards",new List<CardModel>());Set(Combat,"<RoundNumber>k__BackingField",1);
        ClassDraws.Clear();ClassEnergy.Clear();ClassSelectors=0;ClassSelected=null;ClassAttacks.Clear();
        var manual=Player.GetRelic<ElectronicSoldierManual>()!;
        foreach(string name in new[]{"MemoryGardenDeckRuleVersion","MemoryGardenInitialDeckSize"})
            if(manual.GetType().GetProperty(name,F) is {} property)property.SetValue(manual,1);
    }
    static async Task ClassSetup()
    {
        H("Setup");Localization();NonCriticalSeed=FindSeed(false);
        foreach(Type type in Mod.GetTypes().Concat(typeof(PowerModel).Assembly.GetTypes()).Where(type=>!type.IsAbstract&&!type.ContainsGenericParameters&&
            (typeof(PowerModel).IsAssignableFrom(type)||typeof(RelicModel).IsAssignableFrom(type)||typeof(CardPoolModel).IsAssignableFrom(type)||typeof(RelicPoolModel).IsAssignableFrom(type)||type.Assembly==Mod&&typeof(CardModel).IsAssignableFrom(type))))
        { if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type)); }
        var state=Blank<PlayerCombatState>();Set(state,"_player",Player);Set(state,"_pets",new List<Creature>());Set(state,"<TurnNumber>k__BackingField",1);Set(Player,"<PlayerCombatState>k__BackingField",state);
        Set(RunState,"_allCards",new List<CardModel>());Set(Combat,"_allCards",new List<CardModel>());ClassReset();
        foreach(MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method=>(method.Name is "Wait" or "CustomScaledWait")&&method.ReturnType==typeof(Task)))ClassPatch(wait,nameof(VisualTask));
        ClassPatch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(FormNotEnding));
        ClassPatch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState",F)!,nameof(Skip));
        ClassPatch(typeof(CardPileCmd).GetMethods(F).Single(method=>method.Name=="Draw"&&method.GetParameters().Length==4),nameof(ClassDraw));
        ClassPatch(typeof(PlayerCmd).GetMethods(F).Single(method=>method.Name=="GainEnergy"&&method.GetParameters().Length==2),nameof(ClassGainEnergy));
        ClassPatch(typeof(CardSelectCmd).GetMethods(F).Single(method=>method.Name=="FromCombatPile"&&method.GetParameters().Any(p=>p.ParameterType==typeof(Func<CardModel,bool>))),nameof(ClassSelect));
        foreach(MethodInfo method in typeof(CardPileCmd).GetMethods(F).Where(method=>method.Name=="Add"&&method.GetParameters() is var p&&p[0].ParameterType==typeof(CardModel)&&p[1].ParameterType==typeof(PileType)))ClassPatch(method,nameof(ClassAdd));
        ClassPatch(typeof(AttackCommand).GetMethod("Execute",F)!,nameof(ClassAttack));
        foreach(string name in new[]{"ThirtyOneOSakuraSwordTasselHitPatch","ThirtyOneBAoiAttachedBuffRemovalPatch","MuuaFaithShellRemovalPatch","NararaNullificationRemovePowerPatch"})
            Check(ClassHarmony.CreateClassProcessor(Mod.GetTypes().Single(type=>type.Name==name)).Patch().Count>0,"installs real production "+name);
        await Task.CompletedTask;
    }
    static void ClassPools()
    {
        Type pack=Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiContentPack",true)!;
        Type[] types=(Type[])pack.GetField("RewardRelicTypes",F)!.GetValue(null)!;
        MethodInfo route=pack.GetMethod("GetRewardRelicPool",F)!;
        foreach(Type type in types)ModHelper.AddModelToPool((Type)Invoke(route,null,type)!,type);
        var thirtyO=ModelDb.RelicPool<HBRGOSaki31ORelicPool>();var thirtyA=ModelDb.RelicPool<HBRGOSaki30ARelicPool>();var hotaru=ModelDb.RelicPool<HbrgoHotaruRelicPool>();
        var original=ModelDb.RelicPool<HBRGOSakiRelicPool>();
        Check(ThirtyORelics.All(types.Contains),"actual root content plan registers all six 31O relics");
        Check(thirtyO.AllRelics.OfType<IThirtyOneOClassRelic>().Count()==6,"native 31O AllRelics appends exactly six class entries via actual root routing");
        Check(thirtyA.AllRelics.OfType<IThirtyAClassRelic>().Count()==6&&hotaru.AllRelics.OfType<IHotaruClassRelic>().Count()==6,"native other two class pools retain their own six entries");
        Check(!thirtyO.AllRelics.Any(r=>r is IThirtyAClassRelic or IHotaruClassRelic)&&!thirtyA.AllRelics.Any(r=>r is IThirtyOneOClassRelic or IHotaruClassRelic)&&!hotaru.AllRelics.Any(r=>r is IThirtyOneOClassRelic or IThirtyAClassRelic),"all three real pools reject each other's class relics");
        Check(!original.AllRelics.Any(r=>r is IThirtyOneOClassRelic or IThirtyAClassRelic or IHotaruClassRelic),"old common Saki pool exposes none of the 18 class relics");
        foreach(var pair in ThirtyORelics.Zip(new[]{RelicRarity.Common,RelicRarity.Uncommon,RelicRarity.Uncommon,RelicRarity.Rare,RelicRarity.Rare,RelicRarity.Shop}))
            Check(ModelDb.GetById<RelicModel>(ModelDb.GetId(pair.First)).Rarity==pair.Second,"native rarity correct: "+pair.First.Name);
        Check(new HbrgoSaki31O().RelicPool==thirtyO&&new HbrgoSaki30A().RelicPool==thirtyA,"actual character getters select their separate pools");
        Check(original.AllRelics.All(r=>thirtyO.AllRelics.Contains(r)&&thirtyA.AllRelics.Contains(r)),"new Saki pools preserve all previous starter/ancient/event entries");
    }
    static async Task ClassModeCases()
    {
        ClassReset();var metro=ClassRelic<MetronomeThirtyOneO>();await metro.BeforeCombatStart();
        foreach(Type type in new[]{typeof(ReptureBowPower),typeof(ReptureDualSwordsPower),typeof(ReptureBowPower)})await ClassEnterMode(type);
        Check(ClassDraws.SequenceEqual(new[]{(Player,1m)}),"actual three mode AfterApplied chains draw once in ordinary turn");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await ClassEnterMode(typeof(ReptureDualSwordsPower));
        Check(ClassDraws.Count==2,"new native owner turn restores first-switch draw");
        Type coordinator=Helper("PersonalImmediateTurnCoordinator");FieldInfo current=coordinator.GetField("_current",F)!,combat=coordinator.GetField("_combat",F)!;
        object? old=current.GetValue(null),oldCombat=combat.GetValue(null);Type sessionType=coordinator.GetNestedType("Session",F)!;
        try
        {
            for(int i=0;i<2;i++)
            {object session=Activator.CreateInstance(sessionType,true)!;Set(session,"Owner",Player);Set(session,"Combat",Combat);current.SetValue(null,session);combat.SetValue(null,Combat);
                await ClassEnterMode(typeof(ReptureBowPower));await ClassEnterMode(typeof(ReptureDualSwordsPower));
                Check(ClassDraws.Count==3+i,"distinct personal extra session has independent first switch without repeated draw "+i);}
        }
        finally{current.SetValue(null,old);combat.SetValue(null,oldCombat);}
        await ClassEnterMode(typeof(ReptureBowPower));Check(ClassDraws.Count==4,"resuming suspended ordinary turn does not reset its used allowance");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",3);await ClassEnterMode(typeof(ReptureDualSwordsPower));
        Check(ClassDraws.Count==5,"native extra turn's increased owner TurnNumber has separate allowance");
        Type od=Helper("OverclockImmediateTurnCoordinator");FieldInfo odCurrent=od.GetField("_current",F)!;object? previousOd=odCurrent.GetValue(null);Type odType=od.GetNestedType("Session",F)!;
        try
        {
            object session=odType.GetConstructors(F).Single().Invoke(new object?[]{ModelDb.Relic<HBRGOSakiMod.Core.Models.Relics.Overclock>().ToMutable(),Combat,new[]{Player}});
            odCurrent.SetValue(null,session);await ClassEnterMode(typeof(ReptureBowPower));await ClassEnterMode(typeof(ReptureDualSwordsPower));
            Check(ClassDraws.Count==6,"actual OD session identity supplies exactly one independent switch draw");
        }
        finally{odCurrent.SetValue(null,previousOd);}
        await ClassEnterMode(typeof(ReptureBowPower));Check(ClassDraws.Count==6,"leaving OD preserves already-used suspended ordinary receipt");
        Type? savedCache=typeof(SavedProperties).Assembly.GetTypes().SingleOrDefault(type=>type.Name=="SavedPropertiesTypeCache");
        if(savedCache?.GetMethod("InjectTypeIntoCache",F) is{} inject)Invoke(inject,null,typeof(MetronomeThirtyOneO));
        else{savedCache=typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;Invoke(savedCache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!,null,typeof(MetronomeThirtyOneO));savedCache.GetField("_initialized",F)!.SetValue(null,true);}
        var options=new JsonSerializerOptions{IncludeFields=true};var restored=(MetronomeThirtyOneO)ModelDb.Relic<MetronomeThirtyOneO>().ToMutable();
        JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(metro),options),options)!.Fill(restored);Set(restored,"_owner",Player);
        Set(Player,"_relics",Player.Relics.Where(r=>r is not MetronomeThirtyOneO).Append(restored).ToList());await ClassEnterMode(typeof(ReptureDualSwordsPower));
        Check(restored.LastOrdinaryTurn==3&&restored.OrdinaryTurnSides==1&&ClassDraws.Count==6,"true native SavedProperties JSON roundtrip keeps ordinary-turn draw receipt");
        foreach(bool hbr in new[]{false,true})
        {ClassReset(hbr,false);ClassRelic<MetronomeThirtyOneO>();await ClassEnterMode(typeof(ReptureBowPower));Check(ClassDraws.Count==0,"MetronomeThirtyOneO has no effect outside exact HBR 31O owner "+hbr);}
        ClassReset();ClassRelic<MetronomeThirtyOneO>();var swift=(SwiftPower)FormPower(typeof(SwiftPower));
        await ClassEnterMode(typeof(ReptureBowPower));await ClassEnterMode(typeof(ReptureDualSwordsPower));
        Check(swift.CompletedSwitches==0&&Hero.GetPower<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>()?.Amount==1&&ClassDraws.Count==1,"shared mode switch keeps Swift's two-switch strength alongside relic's one draw");
    }
    static async Task ClassRollCases()
    {
        for(int raw=7;raw<=98;raw+=7)
        {ClassReset();ClassRelic<MarkOfSeven>();Call("ThirtyOneOClassRelicHelper","QueueResolvedRoll",Player,new SoulRollBreakdown(raw,raw,"test"));await (Task)Call("ThirtyOneOClassRelicHelper","ResolvePendingRollRewards",new ThrowingPlayerChoiceContext(),Player)!;
            Check(Hero.GetPower<ChargePower>()?.Amount==1,"actual raw multiple-of-seven grants exactly one Charge "+raw);}
        foreach(int raw in new[]{0,1,6,8,99,100,105})
        {ClassReset();ClassRelic<MarkOfSeven>();Call("ThirtyOneOClassRelicHelper","QueueResolvedRoll",Player,new SoulRollBreakdown(raw,raw,"test"));await (Task)Call("ThirtyOneOClassRelicHelper","ResolvePendingRollRewards",new ThrowingPlayerChoiceContext(),Player)!;Check(!Hero.HasPower<ChargePower>(),"nonmatching/out-of-range raw roll has no mark reward "+raw);}
        ClassReset();var mark=ClassRelic<MarkOfSeven>();
        for(int i=0;i<4;i++)Call("ThirtyOneOClassRelicHelper","QueueResolvedRoll",Player,new SoulRollBreakdown(14,20,"test"));
        await (Task)Call("ThirtyOneOClassRelicHelper","ResolvePendingRollRewards",new ThrowingPlayerChoiceContext(),Player)!;
        Check(Hero.GetPower<ChargePower>()?.Amount==4&&mark.PendingCharge==0,"repeated real rolls have no turn cap and accumulated pending reward drains once");
        await (Task)Call("ThirtyOneOClassRelicHelper","ResolvePendingRollRewards",new ThrowingPlayerChoiceContext(),Player)!;Check(Hero.GetPower<ChargePower>()?.Amount==4,"second drain does not grant again");
        await Hero.GetPower<ChargePower>()!.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(),Hero.Side,new[]{Hero},Combat);
        Check(Hero.GetPower<ChargePower>()?.Amount==3,"mark uses original Charge whose real owner-turn hook decays by one");
        foreach(SoulRollBreakdown roll in new[]{new SoulRollBreakdown(100,100,"raw"),new SoulRollBreakdown(1,1000,"absolute"),new SoulRollBreakdown(1,1,"forced"){IsForcedCritical=true},new SoulRollBreakdown(1,1,"treated"){ResolvedTier=SoulRollTier.Critical}})
        {ClassReset();var score=ClassRelic<ScoreOfHaruhikage>();var pipeline=RollPipeline(ClarityCard<StrikeIronclad>());
            Invoke(typeof(RollGlossaryCapability).GetMethod("RegisterResolvedRoll",F)!,pipeline,roll);await (Task)Invoke(typeof(RollGlossaryCapability).GetMethod("ResolveHotaruCriticalRewards",F)!,pipeline,new ThrowingPlayerChoiceContext())!;
            Check(score.TriggeredThisCombat&&!score.RewardPending&&ClassEnergy.SequenceEqual(new[]{(Player,1m)})&&ClassDraws.SequenceEqual(new[]{(Player,1m)}),"real register/flush delivers first critical energy+draw for "+roll.DiceSource);
            Invoke(typeof(RollGlossaryCapability).GetMethod("RegisterResolvedRoll",F)!,pipeline,roll);await (Task)Invoke(typeof(RollGlossaryCapability).GetMethod("ResolveHotaruCriticalRewards",F)!,pipeline,new ThrowingPlayerChoiceContext())!;Check(ClassEnergy.Count==1&&ClassDraws.Count==1,"same battle cannot retrigger score "+roll.DiceSource);
            await score.BeforeCombatStart();Invoke(typeof(RollGlossaryCapability).GetMethod("RegisterResolvedRoll",F)!,pipeline,roll);await (Task)Invoke(typeof(RollGlossaryCapability).GetMethod("ResolveHotaruCriticalRewards",F)!,pipeline,new ThrowingPlayerChoiceContext())!;Check(ClassEnergy.Count==2,"new battle resets first-critical receipt "+roll.DiceSource);}
        ClassReset();ClassRelic<MarkOfSeven>();ClassRelic<ScoreOfHaruhikage>();
        var normal=new SoulRollBreakdown(98,98,"normal");Call("ThirtyOneOClassRelicHelper","QueueResolvedRoll",Player,normal);await (Task)Call("ThirtyOneOClassRelicHelper","ResolvePendingRollRewards",new ThrowingPlayerChoiceContext(),Player)!;
        Check(ClassEnergy.Count==0&&ClassDraws.Count==0&&Hero.GetPower<ChargePower>()?.Amount==1,"normal multiple7 does not become retroactively critical because its reward adds Charge");
        foreach(bool hbr in new[]{false,true})
        {ClassReset(hbr,false);ClassRelic<MarkOfSeven>();ClassRelic<ScoreOfHaruhikage>();Call("ThirtyOneOClassRelicHelper","QueueResolvedRoll",Player,new SoulRollBreakdown(98,1000,"wrong-role"));await (Task)Call("ThirtyOneOClassRelicHelper","ResolvePendingRollRewards",new ThrowingPlayerChoiceContext(),Player)!;Check(ClassEnergy.Count==0&&!Hero.HasPower<ChargePower>(),"class roll rewards reject wrong role/mode "+hbr);}
        ClassReset();ClassRelic<MarkOfSeven>();ClassRelic<ScoreOfHaruhikage>();var peer=SupportTeammate();
        Call("ThirtyOneOClassRelicHelper","QueueResolvedRoll",peer,new SoulRollBreakdown(98,1000,"peer"));await (Task)Call("ThirtyOneOClassRelicHelper","ResolvePendingRollRewards",new ThrowingPlayerChoiceContext(),peer)!;
        Check(ClassEnergy.Count==0&&ClassDraws.Count==0&&!Hero.HasPower<ChargePower>()&&!peer.Creature.HasPower<ChargePower>(),"teammate's own critical roll cannot borrow holder's relics");
        Call("ThirtyOneOClassRelicHelper","QueueResolvedRoll",Player,new SoulRollBreakdown(98,1000,"owner"));await (Task)Call("ThirtyOneOClassRelicHelper","ResolvePendingRollRewards",new ThrowingPlayerChoiceContext(),Player)!;
        Check(ClassEnergy.All(row=>row.Owner==Player)&&ClassDraws.All(row=>row.Owner==Player)&&Hero.GetPower<ChargePower>()?.Amount==1&&!peer.Creature.HasPower<ChargePower>(),"owner's roll pays only owner's Charge/energy/draw while peer stays unchanged");
        ClassReset();ClassRelic<MarkOfSeven>();var source=ClarityCard<StrikeIronclad>();var real=RollPipeline(source);Set(real,"_currentEnergySpent",1);
        var seven=new SoulRollBreakdown(7,7,"single-selected-roll");Invoke(typeof(RollGlossaryCapability).GetMethod("RegisterResolvedRoll",F)!,real,seven);
        Invoke(typeof(RollGlossaryCapability).GetMethod("QueueCardSwordheart",F)!,real,seven,source);await (Task)Invoke(typeof(RollGlossaryCapability).GetMethod("ApplyPendingSwordheart",F)!,real,new ThrowingPlayerChoiceContext(),source)!;
        await (Task)Invoke(typeof(RollGlossaryCapability).GetMethod("ResolveHotaruCriticalRewards",F)!,real,new ThrowingPlayerChoiceContext())!;
        Check(Hero.GetPower<SwordheartPower>()?.Amount==1&&Hero.GetPower<ChargePower>()?.Amount==1,"one real selected raw7 independently grants existing Swordheart and new Mark Charge");
    }
    static async Task ClassOtherCases()
    {
        ClassReset();var tea=ClassRelic<BoneChinaTeacup>();var manual=Player.GetRelic<ElectronicSoldierManual>()!;int before=manual.SixSensePoints;
        await tea.AfterRestSiteHeal(Player,true);Equal(manual.SixSensePoints,before,"mimicked healing grants no rest-point bonus");
        var peer=Blank<Player>();await tea.AfterRestSiteHeal(peer,false);Equal(manual.SixSensePoints,before,"teammate rest does not grant holder's bonus");
        await tea.AfterRestSiteHeal(Player,false);Equal(manual.SixSensePoints,before+1,"actual holder Rest hook grants one existing allocatable manual point");
        ClassReset();ClassRelic<SakuraSwordTassel>();var card=ClarityCard<MagicSwordLuckySevenHbrgoSaki>();card.SetMagicSwordModeHitCount(7);Power<HbrComboLargePower>(Hero,1);
        await (Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),Play(card))!;
        Check(ClassAttacks.Single().Before==7&&ClassAttacks.Single().After==15,"actual LuckySeven OnPlay attack is doubled first then receives final +1");
        var opening=ClarityCard<MagicSwordLuckySevenOpeningHbrgoSaki>();var command=new AttackCommand(1);Set(command,"<Attacker>k__BackingField",Hero);Set(command,"<ModelSource>k__BackingField",opening);
        Equal(Hook.ModifyAttackHitCount(Combat,command,7),14,"LuckySeven opening does not receive tassel's body-only bonus");
        ClassReset();ClassRelic<SakuraSwordTassel>();card=ClarityCard<MagicSwordLuckySevenHbrgoSaki>();card.SetMagicSwordModeHitCount(0);await (Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),Play(card))!;Check(ClassAttacks.Single().After==1,"zero base LuckySeven body still gains its one extra relic hit");
        ClassReset();card=ClarityCard<MagicSwordLuckySevenHbrgoSaki>();card.SetMagicSwordModeHitCount(0);await (Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),Play(card))!;Check(ClassAttacks.Count==0,"zero-hit finisher without relic still performs no attack");
        ClassReset();var stand=ClassRelic<MusicStand>();await stand.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Check(stand.OfferedThisCombat&&ClassSelectors==0,"empty draw pile safely uses no selection screen");
        ClassReset();stand=ClassRelic<MusicStand>();
        foreach(CardModel c in new CardModel[]{FormCard<StrikeIronclad>(),FormCard<SlideStepHbrgoSaki>(),FormCard<SwallowReversalHbrgoSaki>(),FormCard<SoulHarvestHbrgoSaki>()}){c.Pile?.RemoveInternal(c,true);Player.PlayerCombatState!.DrawPile.AddInternal(c);}
        ClassSelected=Player.PlayerCombatState!.DrawPile.Cards.OfType<SlideStepHbrgoSaki>().Single();
        await stand.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),peer);Check(ClassSelectors==0&&!stand.OfferedThisCombat,"teammate turn does not consume initial selection");
        await stand.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Check(ClassSelectors==1&&ClassSelected.Pile==Player.PlayerCombatState.Hand,"real MusicStand body moves chosen bow card to holder Hand");
        await stand.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Check(ClassSelectors==1,"same initial-turn callback cannot open selection again");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await stand.BeforeCombatStart();await stand.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Check(ClassSelectors==1&&!stand.OfferedThisCombat,"later normal/extra turn never counts as opening draw");
    }
    public static async Task RunClassRelics(string path)
    {
        string? error=null;try{await ClassSetup();ClassPools();await ClassModeCases();await ClassRollCases();await ClassOtherCases();}catch(Exception exception){error=exception.ToString();Console.Error.WriteLine(exception);}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real native model pools with production content routing; real mode Apply/AfterApplied cleanup and shared switch hook; real roll register/flush and Charge Apply/decay; actual relic Rest and MusicStand bodies; real LuckySeven OnPlay plus native Hook hit modifiers and production postfix. Engine draw/energy/selector/attack animation terminals controlled; no Godot, multiplayer transport or in-game test."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count+" 31O class relic checks");if(error!=null)throw new Exception("See "+path);
    }
}
