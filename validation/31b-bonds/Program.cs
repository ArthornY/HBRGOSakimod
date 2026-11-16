using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class BondStandalone { public static void Run(string path)=>EffectChecks.RunBonds(path); }
// ID-only pickup fixtures keep arbitrary native food on-pickup effects outside
// these tests while executing the real RelicCmd.Obtain and receipt patch.
public sealed class BondFoodFixture : RelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Event;
    public override bool IsStackable=>true;
}
public sealed class BondHiddenDebuffFixture : PowerModel
{
    public override PowerType Type=>PowerType.Debuff;
    public override PowerStackType StackType=>PowerStackType.Counter;
    protected override bool IsVisibleInternal=>false;
}

static partial class EffectChecks
{
    static bool BondNotLocal(ref bool __result){__result=false;return false;}
    static void BondSavedCache(params Type[] types)
    {
        Assembly native=typeof(Creature).Assembly;
        if(native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is{} legacy)
        {foreach(Type type in types)legacy.GetMethod("InjectTypeIntoCache",F)!.Invoke(null,[type]);return;}
        native.GetType("MegaCrit.Sts2.Core.Modding.AssemblyInfo",true)!.GetMethod("Init",F)!.Invoke(null,null);
        Type cache=native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;
        cache.GetMethod("Init",F)!.Invoke(null,null);
        foreach(Type type in types)cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!.Invoke(null,[type]);
    }
    static void BondReset()
    {
        RanaReset();
        Set(RunState,"_allCards",new List<CardModel>());
        Set(Player,"<RelicGrabBag>k__BackingField",new MegaCrit.Sts2.Core.Runs.RelicGrabBag());
        Set(RunState,"<SharedRelicGrabBag>k__BackingField",new MegaCrit.Sts2.Core.Runs.RelicGrabBag());
        Set(Manual,"<MemoryGardenSharedUpgrades>k__BackingField",string.Empty);
        Set(Manual,"<MemoryGardenMetCharacters>k__BackingField",string.Empty);
    }
    static T BondRelic<T>() where T:RelicModel
    {
        var relic=(T)ModelDb.GetById<RelicModel>(ModelDb.GetId(typeof(T))).ToMutable();
        Player.AddRelicInternal(relic,silent:true);return relic;
    }
    static T BondPermanent<T>() where T:CardModel,new()
    {
        var card=FormCard<T>();Player.PlayerCombatState!.Hand.RemoveInternal(card);Player.Deck.AddInternal(card);
        ((List<CardModel>)RunState.GetType().GetField("_allCards",F)!.GetValue(RunState)!).Add(card);return card;
    }
    static void BondMove(CardModel card,PileType target)
    {
        var cards=(List<CardModel>)RunState.GetType().GetField("_allCards",F)!.GetValue(RunState)!;
        if(!cards.Contains(card))cards.Add(card);
        card.Pile?.RemoveInternal(card);target.GetPile(card.Owner).AddInternal(card);
    }
    static RelicModel Food(string id)
    {
        var relic=(RelicModel)ModelDb.Relic<BondFoodFixture>().ToMutable();
        Set(relic,"<Id>k__BackingField",new ModelId("RELIC",id));return relic;
    }
    static async Task ObtainFood(string id)=>await RelicCmd.Obtain(Food(id),Player);
    static bool BondActive(RelicModel relic)=>(bool)relic.GetType().GetProperty("IsActive",F)!.GetValue(relic)!;
    static void SquadDeck(int count)
    {
        Type[] types=[typeof(ByakkoRoarHbrgoSaki),typeof(PeacefulMemoriesHbrgoSaki),typeof(HellfireBurnHbrgoSaki),typeof(FrozenMistMeowHbrgoSaki),typeof(FreshDeathHbrgoSaki),typeof(InnocentRequiemHbrgoSaki)];
        foreach(Type type in types.Take(count))
        {
            var card=(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();Set(card,"_owner",Player);Player.Deck.AddInternal(card);
        }
    }
    static Task Charm(decimal amount)=>PowerCmd.Apply<ProtectiveCharmPower>(new ThrowingPlayerChoiceContext(),Hero,amount,Hero,null,true);
    static async Task Weak(decimal amount=1)=>await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(),Hero,amount,Enemy,null,true);
    static decimal LiveStat(string name)
    {
        object?[] args=[Hero,null];Check((bool)Call("HbrBattleStatsHelper","TryGetStats",args)!,"hero exposes live HBR stats");
        return (decimal)args[1]!.GetType().GetProperty(name,F)!.GetValue(args[1])!;
    }
    static int Charms()=>Hero.GetPowerAmount<ProtectiveCharmPower>();
    static async Task MarkerCases()
    {
        BondReset();BondPermanent<MatchaParfaitHbrgoSaki>();
        await (Task)Call("TeamCollectionRelics","EnsureObtained",Player)!;
        var snack=Player.GetRelic<TigersMouthSnackBond>();
        Check(snack!=null&&snack.DisplayAmount==1&&!BondActive(snack),"actual team-marker acquisition awards the inactive snack relic for the first permanent partner");
        BondPermanent<ByakkoRoarHbrgoSaki>();
        await (Task)Call("TeamCollectionRelics","EnsureObtained",Player)!;
        Check(BondActive(snack!)&&Player.GetRelic<ThirtyOneBCollection>()?.DisplayAmount==1,"adding Byakko activates snacks and awards the real31B marker independently");
        await (Task)Call("TeamCollectionRelics","EnsureObtained",Player)!;
        Check(Player.Relics.OfType<TigersMouthSnackBond>().Count()==1&&Player.Relics.OfType<ThirtyOneBCollection>().Count()==1,"repeated acquisition notifications do not duplicate either marker relic");
        BondReset();BondPermanent<HellfireBurnHbrgoSaki>();
        await (Task)Call("TeamCollectionRelics","EnsureObtained",Player)!;
        var sisters=Player.GetRelic<FamilyIsLikeThisBond>();
        Check(sisters!=null&&sisters.DisplayAmount==1&&!BondActive(sisters),"the first permanent sister awards the inactive family marker through its actual acquisition path");
        BondPermanent<FrozenMistMeowHbrgoSaki>();BondPermanent<FrozenMistMeowHbrgoSaki>();
        await (Task)Call("TeamCollectionRelics","EnsureObtained",Player)!;
        Check(BondActive(sisters!)&&sisters!.DisplayAmount==2&&Player.GetRelic<ThirtyOneBCollection>()?.DisplayAmount==2,"a second distinct sister activates the family bond while physical duplicates never count as another31B member");
    }
    static async Task SquadCases()
    {
        foreach(int count in new[]{0,1,2,4,6})
        {
            BondReset();SquadDeck(count);var squad=BondRelic<ThirtyOneBCollection>();
            await squad.BeforeCombatStart();
            Equal(squad.DisplayAmount,count,"the31B relic counts distinct permanent member cards");
            Equal(Charms(),count>=2?count:0,"the starting protective-charm grant matches the current31B tier, count="+count);
            decimal expected=count>=4?100m*(1m+count*0.04m):100m;
            Equal(LiveStat("Vitality"),expected,"protective charms affect live Vitality only at the four-member tier");
            Equal(LiveStat("Spirit"),expected,"protective charms affect live Spirit only at the four-member tier");
            if(count>=2)
            {
                await Weak();Check(!Hero.HasPower<WeakPower>(),"one actual incoming Weak application is blocked by protective charm");
                Equal(Charms(),count-1,"one debuff application consumes exactly one charm rather than its debuff stack count");
                if(count>=4)
                {
                    Equal(LiveStat("Vitality"),100m*(1m+(count-1)*0.04m),"Vitality changes immediately when one charm is consumed");
                    Equal(LiveStat("Spirit"),100m*(1m+(count-1)*0.04m),"Spirit changes immediately when one charm is consumed");
                }
            }
        }
        BondReset();SquadDeck(6);var six=BondRelic<ThirtyOneBCollection>();await six.BeforeCombatStart();
        await PowerCmd.Remove(Hero.GetPower<ProtectiveCharmPower>()!);
        await six.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Equal(Charms(),1,"the six-member tier replenishes an empty charm counter to one");
        await six.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Equal(Charms(),1,"the six-member tier never adds a charge while a charm remains");
        await Weak(3);Equal(Charms(),0,"a three-stack native Weak application is a single blocked debuff");
        await six.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Equal(Charms(),1,"a later turn replenishes the newly exhausted charm again");
        BondReset();SquadDeck(4);var four=BondRelic<ThirtyOneBCollection>();await four.BeforeCombatStart();
        CardModel removed=Player.Deck.Cards.Last();Player.Deck.RemoveInternal(removed);four.GetType().GetMethod("RefreshCounter",F)!.Invoke(four,null);
        Equal(LiveStat("Vitality"),100m,"falling below four31B kinds disables the charm percentage bonus without deleting its charges");
        Equal(Charms(),4,"losing the stat tier preserves the Artifact-like charm resource");
    }
    static async Task CharmCases()
    {
        BondReset();await Charm(2);await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),Hero,1m,Hero,null,true);
        Equal(Charms(),2,"a positive Strength grant does not consume protective charm");
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),Hero,-1m,Enemy,null,true);
        Equal(Charms(),1,"negative Strength uses native GetTypeForAmount and consumes one charm");
        Equal(Hero.GetPowerAmount<StrengthPower>(),1,"blocked negative Strength leaves the existing buff unchanged");
        await PowerCmd.Apply<BondHiddenDebuffFixture>(new ThrowingPlayerChoiceContext(),Hero,1m,Enemy,null,true);
        Equal(Charms(),1,"an invisible debuff is not blocked, matching native Artifact visibility rules");
        foreach(bool artifactFirst in new[]{false,true})
        {
            BondReset();
            if(artifactFirst)await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(),Hero,1m,Hero,null,true);
            await Charm(2);
            if(!artifactFirst)await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(),Hero,1m,Hero,null,true);
            await Weak();Equal(Hero.GetPowerAmount<ArtifactPower>(),0,"native Artifact is consumed before protective charm in either listener order");
            Equal(Charms(),2,"coexisting shields do not both consume a charge for the same debuff");
            await Weak();Equal(Charms(),1,"protective charm takes over after native Artifact is exhausted");
        }
    }
    static async Task SnackCases()
    {
        BondReset();var snack=BondRelic<TigersMouthSnackBond>();BondPermanent<MatchaParfaitHbrgoSaki>();
        Check(!BondActive(snack),"one partner card awards the snack relic marker but leaves the bond inactive");
        await ObtainFood("STRAWBERRY");Equal(snack.RanaFoodBonuses+snack.ByakkoFoodBonuses,0,"food obtained before the partner arrives gives no bonus");
        BondPermanent<ByakkoRoarHbrgoSaki>();Check(BondActive(snack),"both permanent partner cards activate the snack bond");
        Equal(snack.RanaFoodBonuses+snack.ByakkoFoodBonuses,0,"activating the bond never retroactively scans old food");
        int rng=RngCount(RunState.Rng.Niche);await ObtainFood("NOT_A_FOOD");
        Equal(RngCount(RunState.Rng.Niche),rng,"a relic outside the food whitelist neither grants a coefficient nor consumes selection RNG");
        object foods=typeof(TigersMouthSnackBond).GetProperty("Foods",F)!.GetValue(null)!;
        string[] ids=((System.Collections.IEnumerable)foods).Cast<object>().Select(food=>(string)food.GetType().GetProperty("Id")!.GetValue(food)!).ToArray();
        Check(ids.Length==20&&ids.Distinct().Count()==20,"one authoritative food list contains exactly the twenty specified IDs");
        foreach(string id in ids)await ObtainFood(id);
        Equal(snack.RanaFoodBonuses+snack.ByakkoFoodBonuses,20,"each whitelisted actual Obtain command grants one permanent ten-percentage-point reward");
        Equal(RngCount(RunState.Rng.Niche)-rng,20,"each newly obtained food consumes exactly one deterministic recipient selection");
        var save=SavedProperties.From(snack)??throw new Exception("Native saved-property cache missed the new snack relic");var restored=(TigersMouthSnackBond)ModelDb.Relic<TigersMouthSnackBond>().ToMutable();save.Fill(restored);
        Check(restored.RanaFoodBonuses==snack.RanaFoodBonuses&&restored.ByakkoFoodBonuses==snack.ByakkoFoodBonuses,"both per-card-type reward ledgers survive the actual native SavedProperties roundtrip");
        foreach(Type type in new[]{typeof(MatchaParfaitHbrgoSaki),typeof(ByakkoRoarHbrgoSaki)})
        {
            var card=(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();Set(card,"_owner",Player);
            decimal bonus=(decimal)typeof(TigersMouthSnackBond).GetMethod("GetDamageBonusUnits",F)!.Invoke(null,[Hero,card])!;
            int count=type==typeof(MatchaParfaitHbrgoSaki)?snack.RanaFoodBonuses:snack.ByakkoFoodBonuses;
            Equal(bonus,count*0.6m,"a newly created physical copy reads all already-earned type-specific base units");
        }
        var byakko=Player.Deck.Cards.OfType<ByakkoRoarHbrgoSaki>().Single();Player.Deck.RemoveInternal(byakko);
        var rana=FormCard<MatchaParfaitHbrgoSaki>();
        Equal((decimal)typeof(TigersMouthSnackBond).GetMethod("GetDamageBonusUnits",F)!.Invoke(null,[Hero,rana])!,snack.RanaFoodBonuses*0.6m,"earned coefficient rewards remain after a partner card is removed");
        Check(!BondActive(snack),"removing the partner stops future food grants");
    }
    static async Task SnackDamageCases()
    {
        foreach(bool rana in new[]{false,true})
        {
            BondReset();var snack=BondRelic<TigersMouthSnackBond>();Set(snack,"<RanaFoodBonuses>k__BackingField",2);Set(snack,"<ByakkoFoodBonuses>k__BackingField",3);
            CardModel card=rana?FormCard<MatchaParfaitHbrgoSaki>():FormCard<ByakkoRoarHbrgoSaki>();
            decimal raw=card.DynamicVars.Damage.BaseValue,bonus=rana?1.2m:1.8m;
            decimal enemy=(decimal)Call("HbrEnemyProfileHelper","GetEnemyParameter",Enemy)!;
            decimal expected=decimal.Floor((decimal)Call("HbrSkillPower","EvaluateDamageFromUnits",raw+bonus,100m,enemy,false)!);
            Equal(Preview(Enemy,Hero,raw,ValueProp.Move,card),expected,"selected-target preview adds the earned coefficient before exactly one shared attribute calculation");
            string face=DesuwaFace(card,false),detail=DesuwaFace(card,true);
            string percent=((raw+bonus)/6m*100m).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
            Check(!face.Contains("倍率")&&!face.Contains("（")&&detail.Contains("（"+percent+"%）"),"the normal card/inspect presentation shows numeric damage and the increased coefficient only in parentheses");
            await RanaPlay(card);Equal(Hits[0].Final,expected,"actual native attack receives the same coefficient bonus once, matching its target preview");
            Equal(card.DynamicVars.Damage.BaseValue,raw,"preview and execution never rewrite the authored DamageVar with the earned modifier");
        }
    }
    static (FamilyIsLikeThisBond Relic,HellfireBurnHbrgoSaki Ichigo,FrozenMistMeowHbrgoSaki Sumomo) Sisters(bool both=true)
    {
        BondReset();BondPermanent<HellfireBurnHbrgoSaki>();if(both)BondPermanent<FrozenMistMeowHbrgoSaki>();
        var relic=BondRelic<FamilyIsLikeThisBond>();var ichigo=FormCard<HellfireBurnHbrgoSaki>();var sumomo=FormCard<FrozenMistMeowHbrgoSaki>();
        BondMove(ichigo,PileType.Play);BondMove(sumomo,PileType.Draw);return(relic,ichigo,sumomo);
    }
    static async Task SisterPlay(FamilyIsLikeThisBond relic,CardModel card,bool automatic=false,int replay=0)
    {var play=Play(card);Set(play,"<IsAutoPlay>k__BackingField",automatic);Set(play,"<PlayIndex>k__BackingField",replay);await relic.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(),play);}
    static async Task SisterCases()
    {
        var s=Sisters(false);await SisterPlay(s.Relic,s.Ichigo);Check(s.Sumomo.Pile?.Type==PileType.Draw&&!s.Relic.PlayerTurnUsed,"a generated combat partner does not activate a bond missing its permanent card");
        s=Sisters();var discard=FormCard<FrozenMistMeowHbrgoSaki>();BondMove(discard,PileType.Discard);
        await SisterPlay(s.Relic,s.Ichigo);Check(s.Sumomo.Pile?.Type==PileType.Hand&&discard.Pile?.Type==PileType.Discard,"the real pile command retrieves the first draw-pile partner before any discarded copy");
        Check(s.Relic.PlayerTurnUsed,"a successful player-turn retrieval consumes the shared once-per-turn use");
        BondMove(s.Sumomo,PileType.Exhaust);await SisterPlay(s.Relic,s.Ichigo,true,1);
        Check(discard.Pile?.Type==PileType.Discard,"an automatic replay cannot retrieve a second partner in the same player turn");
        await s.Relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);await SisterPlay(s.Relic,s.Ichigo,true);
        Check(discard.Pile?.Type==PileType.Hand,"a later player turn can retrieve a discarded partner through an automatic play");
        s=Sisters();BondMove(s.Sumomo,PileType.Hand);await SisterPlay(s.Relic,s.Ichigo);
        Check(!s.Relic.PlayerTurnUsed,"a partner already in hand does not spend the turn's use");
        BondMove(s.Sumomo,PileType.Exhaust);await SisterPlay(s.Relic,s.Ichigo);
        Check(!s.Relic.PlayerTurnUsed,"an exhausted partner is not recovered and does not spend the turn's use");
        BondMove(s.Sumomo,PileType.Discard);await SisterPlay(s.Relic,s.Ichigo);
        Check(s.Sumomo.Pile?.Type==PileType.Hand&&s.Relic.PlayerTurnUsed,"after failed attempts, the still-unused turn can retrieve a partner once it is available");
        BondMove(s.Ichigo,PileType.Draw);BondMove(s.Sumomo,PileType.Play);Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Enemy);
        await s.Relic.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,new[]{Enemy},Combat);
        await SisterPlay(s.Relic,s.Sumomo,true);
        Check(s.Ichigo.Pile?.Type==PileType.Hand&&s.Relic.EnemyTurnUsed&&s.Relic.PlayerTurnUsed,"Sumomo's enemy-turn automatic play retrieves Ichigo using an independent enemy-turn allowance");
    }
    static async Task SisterExtraTurnCase()
    {
        var s=Sisters();await SisterPlay(s.Relic,s.Ichigo);BondMove(s.Sumomo,PileType.Discard);
        Type coordinator=Helper("PersonalImmediateTurnCoordinator");FieldInfo field=coordinator.GetField("_current",F)!,combatField=coordinator.GetField("_combat",F)!;
        object? previous=field.GetValue(null),previousCombat=combatField.GetValue(null);
        object session=RuntimeHelpers.GetUninitializedObject(coordinator.GetNestedType("Session",F)!);Set(session,"Owner",Player);Set(session,"Combat",Combat);
        try
        {
            combatField.SetValue(null,Combat);field.SetValue(null,session);
            Check(Call("OverclockImmediateTurnCoordinator","GetTurnToken",Player)!=null,"the real turn-token selector recognizes a personal inserted turn");
            await s.Relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);
            Check(s.Relic.PlayerTurnUsed,"entering an inserted turn does not clear the suspended normal turn's consumed allowance");
            await SisterPlay(s.Relic,s.Ichigo);Check(s.Sumomo.Pile?.Type==PileType.Hand,"an inserted turn receives its own successful retrieval");
        }
        finally{field.SetValue(null,previous);combatField.SetValue(null,previousCombat);}
        BondMove(s.Sumomo,PileType.Discard);await SisterPlay(s.Relic,s.Ichigo);
        Check(s.Sumomo.Pile?.Type==PileType.Discard&&s.Relic.PlayerTurnUsed,"returning to the normal turn restores its existing once-per-turn restriction");
    }
    public static void RunBonds(string path)
    {
        bool ok=false;string? error=null;ReportDirectory=Path.GetDirectoryName(Path.GetFullPath(path))!;
        try
        {
            Install();Localization();
            foreach(Type type in new[]{typeof(ThirtyACollection),typeof(ThirtyOneACollection),typeof(CryChicCollection),typeof(TigersMouthSnackBond),typeof(ThirtyOneBCollection),typeof(FamilyIsLikeThisBond),typeof(ProtectiveCharmPower),typeof(ArtifactPower),typeof(WeakPower),typeof(StrengthPower),typeof(MindsEyePower),typeof(BondHiddenDebuffFixture),typeof(BondFoodFixture),typeof(MatchaParfaitHbrgoSaki),typeof(ByakkoRoarHbrgoSaki),typeof(PeacefulMemoriesHbrgoSaki),typeof(HellfireBurnHbrgoSaki),typeof(FrozenMistMeowHbrgoSaki),typeof(FreshDeathHbrgoSaki),typeof(InnocentRequiemHbrgoSaki)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            BondSavedCache(typeof(TigersMouthSnackBond),typeof(FamilyIsLikeThisBond));
            var h=new Harmony(ProductionId+".bonds");
            foreach(Type type in new[]{typeof(MegaCrit.Sts2.Core.Runs.RunState),typeof(CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RanaListeners),F)!){priority=Priority.First+2});
            h.Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormNotEnding),F)!));
            h.Patch(AccessTools.PropertyGetter(typeof(PowerModel),"Icon"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            h.Patch(typeof(LocalContext).GetMethod("IsMe",F,[typeof(Player)])!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BondNotLocal),F)!));
            foreach(var method in typeof(Cmd).GetMethods(F).Where(method=>method.Name is "Wait" or "CustomScaledWait"))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            foreach(var method in typeof(CreatureCmd).GetMethods(F).Where(method=>method.Name=="TriggerAnim"&&method.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            foreach(var method in typeof(VfxCmd).GetMethods(F).Where(method=>method.Name is "PlayOnCreatureCenter" or "PlayOnSide"))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            var history=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory",true)!;
            foreach(var method in history.GetMethods(F).Where(method=>method.Name is "CreatureAttacked" or "PowerReceived" or "PowerRemoved" or "CardChangedPiles"))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            foreach(string name in new[]{"TigersMouthSnackObtainPatch","ThirtyOneBBondDescriptionPatch","CardDescriptionLocPatch"})h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches."+name,true)!).Patch();
            h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrEffectApplicationPatch",true)!).Patch();
            DamageCallsiteCatalogCases().GetAwaiter().GetResult();MarkerCases().GetAwaiter().GetResult();SquadCases().GetAwaiter().GetResult();CharmCases().GetAwaiter().GetResult();SnackCases().GetAwaiter().GetResult();SnackDamageCases().GetAwaiter().GetResult();SisterCases().GetAwaiter().GetResult();SisterExtraTurnCase().GetAwaiter().GetResult();
            ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real native RelicCmd.Obtain with ID-only inert pickup fixtures; actual native power application/Artifact interaction and live attributes; real CardPileCmd transfer via production sibling hook; native attack commands and target previews; native SavedProperties roundtrip. Visual/history/terminal HP writes are isolated. Extra-turn admission uses a controlled real production session token without transport or UI. No Godot, package or upload."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" bond checks");if(!ok)throw new Exception("Inspect "+path);
    }
}
