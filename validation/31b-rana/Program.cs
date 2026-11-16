using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.CardPools;
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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class RanaByakkoStandalone { public static void Run(string reportPath) => EffectChecks.RunRanaByakko(reportPath); }

static partial class EffectChecks
{
    static readonly List<CardModel> RanaAutoPlays=[];
    static bool RanaRecursiveCritical;
    static bool RanaListeners(ref IEnumerable<AbstractModel> __result)
    {
        __result=Combat.Creatures.SelectMany(creature=>creature.Powers).Cast<AbstractModel>()
            .Concat(Player.Relics).Concat(Player.PlayerCombatState!.AllCards).Distinct().ToArray();
        return false;
    }
    static bool RanaAutoLeaf(object[] __args,ref Task __result)
    {
        CardModel card=__args.OfType<CardModel>().Single();RanaAutoPlays.Add(card);
        if(RanaRecursiveCritical)Call("RanaByakkoCombatHelper","QueueRawCritical",Player);
        // Isolate the native visual/card wrapper; command requests still come
        // from the production card's OnPlay body and real AttackCommand.
        __result=(Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),Play(card))!;
        return false;
    }
    static void RanaReset()
    {
        FormReset();Fate(FateJson(bosses:0));RanaAutoPlays.Clear();RanaRecursiveCritical=false;
        Set(RunState,"_players",new List<Player>{Player});
        // Each fixture case represents a new battle, not another card within
        // the previous case. Preserve the production state for repeat plays.
        object table=Helper("RanaByakkoCombatHelper").GetField("Battles",F)!.GetValue(null)!;
        table.GetType().GetMethod("Remove")!.Invoke(table,[Player]);
    }
    static void RanaUpgrade(CardModel card,params int[] ids)
    {
        foreach(int id in ids)
        {
            Check((bool)Native(card,"PrepareUpgradeEffect",id)!,"garden accepts selected upgrade "+card.Id+"/"+id);
            card.UpgradeInternal();card.FinalizeUpgradeInternal();
        }
    }
    static async Task RanaPlay(CardModel card)=>await (Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),Play(card))!;
    static T RanaCard<T>(params int[] ids) where T:CardModel,new()
    {var card=FormCard<T>();RanaUpgrade(card,ids);return card;}
    static decimal FragilePercent(FragilePower power)=>(decimal)typeof(FragilePower).GetProperty("HbrEffectPercent",F)!.GetValue(power)!;
    static void RanaFragile(FragilePower power,decimal amount)=>Native(power,"ConfigureHbr",amount,1);

    static async Task RanaCoreCases()
    {
        RanaReset();var card=RanaCard<MatchaParfaitHbrgoSaki>();
        Check(card.Type==CardType.Attack&&card.EnergyCost.Canonical==1&&card.Rarity==CardRarity.Rare&&card.TargetType==TargetType.RandomEnemy,"Matcha is now a one-energy rare random-target attack");
        Equal(card.DynamicVars.Damage.BaseValue,6m,"Matcha stores exact100percent native damage units");
        Creature other=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;
        Set(Combat,"_enemies",new List<Creature>{Enemy,other});
        var expectedRng=new RunRngSet(NonCriticalSeed).CombatTargets;
        Creature[] expected=[new[]{Enemy,other}[expectedRng.NextInt(2)],new[]{Enemy,other}[expectedRng.NextInt(2)]];
        int rng=RngCount(RunState.Rng.CombatTargets);
        await RanaPlay(card);
        Check(Hits.Count==2&&Hits.Select(hit=>hit.Target).SequenceEqual(expected),"actual native random AttackCommand independently chooses each of the two shot targets");
        Equal(RngCount(RunState.Rng.CombatTargets)-rng,2,"basic twin pistols consume two native target selections");
        Check(Hits.All(hit=>hit.Raw==6m),"both shots retain the authored100percent skill before shared calculations");
        RanaReset();card=RanaCard<MatchaParfaitHbrgoSaki>(5);await RanaPlay(card);
        Equal(Hits.Count,4,"Black Gun adds two actual shots to the same native attack command");
        RanaReset();card=RanaCard<MatchaParfaitHbrgoSaki>(3,5);
        FragilePower a=Power<FragilePower>(Enemy,2),b=Power<FragilePower>(Enemy,1);
        RanaFragile(a,46.9m);RanaFragile(b,20m);
        await RanaPlay(card);
        Equal(FragilePercent(a),56.9m,"Paw Print adds ten percentage points to the first independent Fragile");
        Equal(FragilePercent(b),30m,"Paw Print adds ten percentage points to the second independent Fragile");
        Check(a.Amount==2&&b.Amount==1&&Powers(Enemy).OfType<FragilePower>().Count()==2,"Paw Print preserves both durations and instance count");
        await RanaPlay(card);Equal(FragilePercent(a),66.9m,"a later play can enhance the same target once again");
        Equal(FragilePercent(b),40m,"four hits per play never enhance an instance four times");
        RanaReset();card=RanaCard<MatchaParfaitHbrgoSaki>(3);await RanaPlay(card);
        Check(!Enemy.HasPower<FragilePower>(),"Paw Print does not create Fragile on a target without it");
        RanaReset();card=RanaCard<MatchaParfaitHbrgoSaki>(1);
        var protection=(DeflectorProtection)ModelDb.Relic<DeflectorProtection>().ToMutable();Set(protection,"_owner",Player);Set(protection,"_hasInitializedDp",true);Set(protection,"_dp",0);
        Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),protection});
        int recovery=(int)decimal.Floor(protection.MaxDp*0.2m);await RanaPlay(card);
        Equal(protection.Dp,recovery,"White Gun executes the real20percent maximum-DP recovery");
        await RanaPlay(card);Equal(protection.Dp,recovery,"White Gun only recovers once in the battle");
        RanaReset();card=RanaCard<MatchaParfaitHbrgoSaki>(1);
        Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());
        var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;
        Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});
        var shell=(PhantomHighDimensionalShellPower)ModelDb.Power<PhantomHighDimensionalShellPower>().ToMutable();
        Set(shell,"_owner",phantom);Set(shell,"_amount",1000);Powers(phantom).Add(shell);
        decimal expectedDp=Math.Min(shell.MaximumDp,1000m+decimal.Floor(shell.MaximumDp*0.2m));
        await RanaPlay(card);Equal(shell.CurrentDp,expectedDp,"30A White Gun recovers the currently living phantom's20percent maximum DP through the real recovery helper");
        RanaReset();card=RanaCard<MatchaParfaitHbrgoSaki>(4);
        await card.BeforeCombatStart();int effect=card.ImprovisationEffect;
        Check(effect is >=0 and <=2,"Improvisation chooses exactly one valid effect at battle start");
        int counter=RngCount(RunState.Rng.Niche);await RanaPlay(card);await RanaPlay(card);
        Check(card.ImprovisationEffect==effect,"the chosen Improvisation effect remains stable across repeat plays");
        Equal(RngCount(RunState.Rng.Niche)-counter,4,"four native damage commands use four natural critical samples without rerolling Improvisation");
        Equal((decimal)Call("RanaByakkoCombatHelper","GetDestructionMultiplier",card)!,effect==2?2m:1m,"Improvisation doubles destruction only for its destruction branch");
        RanaReset();card=RanaCard<MatchaParfaitHbrgoSaki>(6);
        Equal(card.ModifyDamageMultiplicative(Enemy,1m,ValueProp.Move,Hero,card),1m,"I Love It has no bonus against a target without buffs");
        Power<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(Enemy,1);
        Equal(card.ModifyDamageMultiplicative(Enemy,1m,ValueProp.Move,Hero,card),1.5m,"I Love It checks the actual target's positive buff");
        await RanaPlay(card);
        Equal(Hits[0].Final,decimal.Floor(Base(6m)*1.5m),"I Love It's hidden stable-signature method participates in the actual shared damage pipeline");
    }
    static MatchaParfaitHbrgoSaki RanaDeckCopy(MatchaParfaitHbrgoSaki origin)
    {
        var copy=(MatchaParfaitHbrgoSaki)origin.MutableClone();copy.DeckVersion=origin;
        Player.PlayerCombatState!.Hand.AddInternal(copy);
        ((List<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Add(copy);
        return copy;
    }
    static async Task RanaCloneStateCases()
    {
        RanaReset();var origin=RanaCard<MatchaParfaitHbrgoSaki>(1,4);
        Player.PlayerCombatState!.Hand.RemoveInternal(origin);Player.Deck.AddInternal(origin);
        var current=RanaDeckCopy(origin);await current.BeforeCombatStart();
        Check(origin.ImprovisationEffect==current.ImprovisationEffect&&origin.ImprovisationEffect>=0,
            "combat start publishes Improvisation to the permanent origin before Matcha is ever played");
        var earlyExtra=RanaDeckCopy(origin);
        Check(earlyExtra.ImprovisationEffect==current.ImprovisationEffect,
            "a newly rebuilt personal-turn deck immediately displays the same Improvisation before either copy has played");
        var protection=(DeflectorProtection)ModelDb.Relic<DeflectorProtection>().ToMutable();Set(protection,"_owner",Player);
        Set(protection,"_hasInitializedDp",true);Set(protection,"_dp",0);
        Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),protection});
        await RanaPlay(current);int once=protection.Dp;
        Check(origin.WhiteGunUsedThisCombat,"White Gun publishes its once-per-battle use to the permanent origin");
        var lateExtra=RanaDeckCopy(origin);
        Check(lateExtra.WhiteGunUsedThisCombat&&lateExtra.ImprovisationEffect==current.ImprovisationEffect,
            "native clones of the permanent origin preserve White Gun use and Improvisation for later personal turns");
        await RanaPlay(earlyExtra);await RanaPlay(lateExtra);
        Equal(protection.Dp,once,"both early and late personal-turn copies share the physical permanent card's one White Gun use");
        var nextBattle=RanaDeckCopy(origin);await nextBattle.BeforeCombatStart();
        Check(!nextBattle.WhiteGunUsedThisCombat&&!origin.WhiteGunUsedThisCombat,
            "the following combat resets White Gun on both combat copy and permanent origin");
        await RanaPlay(nextBattle);Equal(protection.Dp,once*2,"White Gun can recover again after the new-combat reset");
    }
    sealed class RanaRoom(RoomType type) : CombatRoom(Combat)
    { public override RoomType RoomType => type; }
    static void RanaRewardCases()
    {
        RanaReset();var card=RanaCard<MatchaParfaitHbrgoSaki>(2);Player.Deck.AddInternal(card);
        foreach(RoomType roomType in new[]{RoomType.Monster,RoomType.Elite,RoomType.Boss})
        {
            var rewards=new List<Reward>();var room=new RanaRoom(roomType);
            Call("RanaByakkoCombatHelper","AddWildCatRewards",Player,rewards,room);
            Check(rewards.Count==1&&rewards[0] is CardReward,"Wild Cat constructs one real optional native card reward for "+roomType);
            Check(ReferenceEquals(rewards[0].Player,Player),"Wild Cat reward retains its owning player for "+roomType);
            Equal((int)typeof(CardReward).GetProperty("OptionCount",F)!.GetValue(rewards[0])!,3,"Wild Cat keeps three native card choices for "+roomType);
            Call("RanaByakkoCombatHelper","AddWildCatRewards",Player,rewards,room);
            Equal(rewards.Count,1,"repeated reward-hook evaluation cannot add a duplicate Wild Cat reward");
        }
    }
    static async Task RanaCriticalCases()
    {
        RanaReset();var a=RanaCard<MatchaParfaitHbrgoSaki>(0);var b=RanaCard<MatchaParfaitHbrgoSaki>(0);
        var noUpgrade=RanaCard<MatchaParfaitHbrgoSaki>();
        var draw=RanaCard<MatchaParfaitHbrgoSaki>(0);Player.PlayerCombatState!.Hand.RemoveInternal(draw);
        Player.PlayerCombatState.DrawPile.AddInternal(draw);
        RanaRecursiveCritical=true;
        Call("RanaByakkoCombatHelper","QueueRawCritical",Player);
        await (Task)Call("RanaByakkoCombatHelper","ResolvePendingRawCritical",new ThrowingPlayerChoiceContext(),Player)!;
        Check(RanaAutoPlays.SequenceEqual(new[]{a,b}),"one queued raw critical automatically plays each eligible initial hand card once, excluding unupgraded and draw-pile cards");
        Equal(Hits.Count,4,"the free plays still execute each production card attack body");
        await (Task)Call("RanaByakkoCombatHelper","ResolvePendingRawCritical",new ThrowingPlayerChoiceContext(),Player)!;
        Equal(RanaAutoPlays.Count,2,"critical rolls queued by the automatic batch do not leave pending recursion");
        RanaReset();Call("RanaByakkoCombatHelper","QueueRawCritical",Player);
        var lateDraw=RanaCard<MatchaParfaitHbrgoSaki>(0);
        await (Task)Call("RanaByakkoCombatHelper","ResolvePendingRawCritical",new ThrowingPlayerChoiceContext(),Player)!;
        Equal(RanaAutoPlays.Count,0,"a Matcha drawn after a raw critical cannot join that earlier trigger's hand snapshot");
        RanaReset();var oldHand=RanaCard<MatchaParfaitHbrgoSaki>(0);
        Call("RanaByakkoCombatHelper","QueueRawCritical",Player);
        var extraDraw=RanaCard<MatchaParfaitHbrgoSaki>(0);
        await (Task)Call("RanaByakkoCombatHelper","ResolvePendingRawCritical",new ThrowingPlayerChoiceContext(),Player)!;
        Check(RanaAutoPlays.SequenceEqual(new[]{oldHand}),"a queued batch plays only the physical card present when the die resolved, excluding a later draw");
        RanaReset();var departed=RanaCard<MatchaParfaitHbrgoSaki>(0);
        Call("RanaByakkoCombatHelper","QueueRawCritical",Player);
        Player.PlayerCombatState!.Hand.RemoveInternal(departed);Player.PlayerCombatState.DiscardPile.AddInternal(departed);
        await (Task)Call("RanaByakkoCombatHelper","ResolvePendingRawCritical",new ThrowingPlayerChoiceContext(),Player)!;
        Equal(RanaAutoPlays.Count,0,"a card that was in the trigger snapshot but has left hand before draining is not automatically played");
    }
    static async Task RanaRawEntryCases()
    {
        foreach(bool shieldEntry in new[]{false,true})
        foreach(int raw in new[]{1,99,100})
        {
            RanaReset();var card=RanaCard<MatchaParfaitHbrgoSaki>(0);
            // A final absolute critical with low raw dice must not trigger the
            // raw-critical-only upgrade. Conversely raw100 remains critical
            // even if its modified final result falls to20.
            var roll=new SoulRollBreakdown(raw,raw==100?20:999,"fixture raw critical");
            if(shieldEntry)
            {
                var shield=Power<StrongestShieldPower>(Hero,1);
                Native(shield,"RegisterCriticalSuccess",roll);
                await (Task)Native(shield,"CompleteIncomingRoll",new ThrowingPlayerChoiceContext(),null)!;
            }
            else
            {
                var pipeline=RollPipeline(card);Native(pipeline,"RegisterResolvedRoll",roll);
                await (Task)Native(pipeline,"ResolveHotaruCriticalRewards",new ThrowingPlayerChoiceContext())!;
            }
            Equal(RanaAutoPlays.Count,raw==100?1:0,(shieldEntry?"Strongest Shield defense roll":"normal RollGlossary roll")+" queues and drains Matcha only for the actual raw critical interval, raw="+raw);
        }
        RanaReset();var suppressedCard=RanaCard<MatchaParfaitHbrgoSaki>(0);
        var suppressed=(SoulRollBreakdown)StaticCall(RollRules,"WithoutCritical",new SoulRollBreakdown(100,100,"suppressed cannon"))!;
        var suppressedPipeline=RollPipeline(suppressedCard);Native(suppressedPipeline,"RegisterResolvedRoll",suppressed);
        await (Task)Native(suppressedPipeline,"ResolveHotaruCriticalRewards",new ThrowingPlayerChoiceContext())!;
        Equal(RanaAutoPlays.Count,0,"a cannon action explicitly stripped of critical resolution never queues a free Matcha play even with raw100");
    }
    static async Task ByakkoCases()
    {
        string keyword=STS2RitsuLib.Content.ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod","31B");
        foreach(string suffix in new[]{"title","cardText","description"})
        {
            var text=new MegaCrit.Sts2.Core.Localization.LocString("card_keywords",keyword+"."+suffix);
            Check(text.Exists()&&text.GetFormattedText().Contains("31B"),"the actual Ritsu-qualified31B "+suffix+" localization key exists and formats through native LocString");
        }
        RanaReset();
        Set(Manual,"<MemoryGardenMetCharacters>k__BackingField",string.Empty);Set(Manual,"_memoryGardenUpgradePoints",1);
        using var library=JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,"story/events/library_hbrgo_saki.json")));
        string[] met=library.RootElement.GetProperty("pages")[1].GetProperty("metCharacters").EnumerateArray().Select(item=>item.GetString()!).ToArray();
        Check(met.Contains("byakko"),"the existing real library storyline page records a Byakko encounter");
        Call("HbrMemoryGarden","RecordEncounters",Player,met);
        Check(Manual.MemoryGardenMetCharacters.Split(',').Contains("byakko"),"the real encounter recorder retains the library's Byakko unlock");
        var choices=((System.Collections.IEnumerable)Call("HbrMemoryGarden","GetChoices",Player)!).Cast<object>().ToArray();
        // The same library page now also records the newly implemented 31B
        // members. Keep its real page unlocks; Byakko must remain available.
        var characters=((System.Collections.IEnumerable)Helper("HbrMemoryGarden").GetField("Characters",F)!.GetValue(null)!).Cast<object>();
        int expectedChoices=characters.Count(entry=>met.Contains((string)entry.GetType().GetProperty("Id")!.GetValue(entry)!));
        Check(choices.Length==expectedChoices&&choices.Any(choice=>choice.GetType().GetProperty("PreviewCard")!.GetValue(choice) is ByakkoRoarHbrgoSaki&&
            (bool)choice.GetType().GetProperty("IsAvailable")!.GetValue(choice)!),"the real garden choice builder offers Byakko and the other implemented library encounters, with an upgrade point");
        RanaReset();var card=RanaCard<ByakkoRoarHbrgoSaki>();
        Check(card.Type==CardType.Attack&&card.TargetType==TargetType.AllEnemies&&card.EnergyCost.Canonical==2,"Byakko starts as a two-energy all-enemy attack");
        Equal(card.DynamicVars.Damage.BaseValue,5m,"Byakko stores exact five native units, not rounded83point33percent");
        await RanaPlay(card);Equal(Hits.Count,2,"basic Byakko issues two actual native hits against the sole enemy");
        Equal((decimal)Call("RanaByakkoCombatHelper","GetDestructionMultiplier",card)!,1.5m,"Byakko always contributes150percent destruction gain");
        Type squad=Mod.GetType("HBRGOSakiMod.Core.Models.Capabilities.ThirtyOneBKeywordRules",true)!;
        Check((bool)Invoke(squad.GetMethod("IsThirtyOneBCard",F)!,null,card)!,"Byakko participates in the new31B affiliation");
        Player.PlayerCombatState!.Hand.RemoveInternal(card);Player.Deck.AddInternal(card);
        Equal((int)Invoke(squad.GetMethod("GetPermanentDistinctCount",F)!,null,Player)!,1,"the permanent31B counter recognizes Byakko");
        var copy=RanaCard<ByakkoRoarHbrgoSaki>();Player.PlayerCombatState.Hand.RemoveInternal(copy);Player.Deck.AddInternal(copy);
        Equal((int)Invoke(squad.GetMethod("GetPermanentDistinctCount",F)!,null,Player)!,1,"two copies of Byakko still count as one31B member");
        RanaReset();card=RanaCard<ByakkoRoarHbrgoSaki>(3);await RanaPlay(card);
        Equal(Hits.Count,5,"Rage Claw adds three hits when exactly one enemy is alive");
        Creature other=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;
        Set(Combat,"_enemies",new List<Creature>{Enemy,other});Hits.Clear();await RanaPlay(card);
        Equal(Hits.Count,4,"Rage Claw adds no hits with two enemies: two hits on each");
        RanaReset();card=RanaCard<ByakkoRoarHbrgoSaki>(1,2,4,5);await RanaPlay(card);
        Equal(Hits.Count,6,"Beast Cage adds four actual hit segments to the base two");
        Check(Hero.Powers.OfType<HbrAttackUpPower>().Any(),"Beast Cage grants its attack-up before the card attacks");
        Check(Hero.Powers.OfType<HbrDefenseDownPower>().Count()==2,"Innocence and Beast Cage produce two independent self-defense-down effects");
        Check(Enemy.Powers.OfType<HbrDefenseDownPower>().Any(),"Destruction Queen applies actual enemy defense down");
        Equal(Hero.GetPowerAmount<HbrMoralePower>(),2,"Sunny Relaxation grants two real Morale levels");
        var buff=Hero.Powers.OfType<HbrAttackUpPower>().Single();Equal(buff.Amount,5,"Beast Cage attack-up lasts five turns");
        await buff.AfterSideTurnEnd(null!,CombatSide.Player,new[]{Hero});Equal(buff.Amount,4,"Beast Cage attack-up ticks on its declared player-side turn end");
        RanaReset();card=RanaCard<ByakkoRoarHbrgoSaki>(6);
        Player.PlayerCombatState!.Hand.RemoveInternal(card);Player.PlayerCombatState.DrawPile.AddInternal(card);
        await card.AfterAutoPrePlayPhaseEnteredEarly(new ThrowingPlayerChoiceContext(),Player);
        await card.AfterAutoPrePlayPhaseEnteredEarly(new ThrowingPlayerChoiceContext(),Player);
        Check(RanaAutoPlays.SequenceEqual(new[]{card})&&Hits.Count==2,"Fifty Kilometers per Hour plays a draw-pile card once at battle start and resolves normal OnPlay");
    }
    public static void RunRanaByakko(string reportPath)
    {
        bool ok=false;string? error=null;ReportDirectory=Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        try
        {
            Install();Localization();
            foreach(Type type in new[]{typeof(MatchaParfaitHbrgoSaki),typeof(ByakkoRoarHbrgoSaki),typeof(FragilePower),typeof(HbrDefenseDownPower),typeof(HbrAttackUpPower),typeof(HbrMoralePower),typeof(MindsEyePower),typeof(DeflectorProtection),typeof(HBRGOSaki31OCardPool),typeof(PhantomHighDimensionalShellPower),typeof(PeacefulMemoriesHbrgoSaki),typeof(HellfireBurnHbrgoSaki),typeof(FrozenMistMeowHbrgoSaki),typeof(InnocentRequiemHbrgoSaki),typeof(FreshDeathHbrgoSaki),typeof(BoostUpHbrgoSaki)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            // The library unlocks the current garden catalog, including later
            // cards. Register current catalog models without changing old card
            // behavior assertions or hard-coding a frozen number of choices.
            foreach(object entry in ((System.Collections.IEnumerable)Helper("HbrMemoryGarden").GetField("Characters",F)!.GetValue(null)!).Cast<object>())
            {
                Type type=(Type)entry.GetType().GetProperty("CardType")!.GetValue(entry)!;
                if(!ModelDb.Contains(type))ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));
            }
            if(Mod.GetType("HBRGOSakiMod.Core.Models.Relics.ThirtyOneCCollection") is { } collection&&!ModelDb.Contains(collection))ModelDb.Inject(collection);
            var h=new Harmony(ProductionId+".rana-byakko");
            foreach(Type type in new[]{typeof(RunState),typeof(CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RanaListeners),F)!){priority=Priority.First+2});
            h.Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormNotEnding),F)!));
            h.Patch(AccessTools.PropertyGetter(typeof(PowerModel),"Icon"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            foreach(var method in typeof(CreatureCmd).GetMethods(F).Where(m=>m.Name=="TriggerAnim"&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            foreach(var method in typeof(VfxCmd).GetMethods(F).Where(m=>m.Name is "PlayOnCreatureCenter" or "PlayOnSide"))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            foreach(var method in typeof(Cmd).GetMethods(F).Where(m=>m.Name is "Wait" or "CustomScaledWait"))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            var history=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory",true)!;
            h.Patch(history.GetMethod("CreatureAttacked",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            h.Patch(typeof(CardCmd).GetMethod("AutoPlay",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RanaAutoLeaf),F)!));
            h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.MatchaPawPrintDamagePatch",true)!).Patch();
            // Keep the false-returning terminal substitution after every real
            // production prefix, including the late Paw Print mutation.
            var terminal=new Harmony("hbr.effect-skill.checks.terminal");
            terminal.Unpatch(Canonical,HarmonyPatchType.Prefix,terminal.Id);
            terminal.Patch(Canonical,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Terminal),F)!){priority=Priority.Last-1000});
            h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrEffectApplicationPatch",true)!).Patch();
            RanaCoreCases().GetAwaiter().GetResult();RanaCloneStateCases().GetAwaiter().GetResult();RanaRewardCases();RanaCriticalCases().GetAwaiter().GetResult();RanaRawEntryCases().GetAwaiter().GetResult();ByakkoCases().GetAwaiter().GetResult();
            foreach(Type type in new[]{typeof(MatchaParfaitHbrgoSaki),typeof(ByakkoRoarHbrgoSaki)})
            {
                RanaReset();var c=(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();Set(c,"_owner",Player);
                RanaUpgrade(c,0,1,2,3,4,5,6);Check(c.CurrentUpgradeLevel==7&&!c.IsUpgradable,"all seven upgrades are obtainable and the eighth is unavailable for "+type.Name);
            }
            ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary="Production card OnPlay and real native AttackCommand target RNG/hit loops, real shared damage hooks, exact Fragile mutation, real DP recovery and effect application/durations. Terminal damage writes, visuals/history and native autoplay visual/card wrapper are isolated. No in-game UI, transport, rendering, package or upload."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Rana/Byakko checks");if(!ok)throw new Exception("Inspect "+reportPath);
    }
}
