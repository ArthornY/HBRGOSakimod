using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class HotaruRelicStandalone { public static void Run(string path)=>EffectChecks.RunHotaru98(path).GetAwaiter().GetResult(); }
static partial class EffectChecks
{
    static Millsage HM=null!;
    static readonly List<(Creature Cannon,bool Suppressed)> HPhases=[];
    static int HDraws;static bool HObserveOnly,HAttemptNestedCritical;
    static T HRelic<T>() where T:RelicModel
    {var relic=(T)ModelDb.GetById<RelicModel>(ModelDb.GetId(typeof(T))).ToMutable();Set(relic,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(relic).ToList());((List<RelicModel>)typeof(Checks).GetField("ExtraListeners",F)!.GetValue(null)!).Add(relic);return relic;}
    static Creature HCannon<T>() where T:HotaruCannonModel
    {var cannon=(Creature)H("Actor",Blank<T>(),null,Player)!;Set(Combat,"_allies",Combat.Allies.Append(cannon).ToList());return cannon;}
    static bool HDrawLeaf(object[] __args,ref Task<IEnumerable<CardModel>> __result)
    {HDraws+=(int)System.Convert.ToDecimal(__args[1]);__result=Task.FromResult<IEnumerable<CardModel>>(Array.Empty<CardModel>());return false;}
    static bool HPhaseLeaf(Creature cannon,bool suppressCritical,ref Task __result)
    {
        HPhases.Add((cannon,suppressCritical));
        if(!HObserveOnly)return true;
        if(HAttemptNestedCritical)Native(HM,"QueueCriticalCannonReward");
        __result=Task.CompletedTask;return false;
    }
    static bool HIconLeaf(ref Godot.Texture2D? __result) { __result=null;return false; }
    static bool HListeners(ref IEnumerable<AbstractModel> __result)
    {__result=Combat.Creatures.SelectMany(c=>c.Powers).Cast<AbstractModel>().Concat(RunState.Players.SelectMany(p=>p.Relics)).ToArray();return false;}
    static void HReset()
    {
        NumericReset();Set(Player,"<Character>k__BackingField",Blank<HbrgoHotaru>());
        Set(RunState,"<Rng>k__BackingField",new RunRngSet("HOTARU-98"));HDraws=0;HObserveOnly=HAttemptNestedCritical=false;HPhases.Clear();
        HM=HRelic<Millsage>();Set(Player,"<ExtraFields>k__BackingField",System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Player).GetProperty("ExtraFields",F)!.PropertyType));
        Call("HotaruClassRelicCombatHelper","ResetBattle",Player);
    }
    static void HSetup()
    {
        NumericSetup();
        NumericHarmony.Patch(AccessTools.PropertyGetter(typeof(PowerModel),"Icon"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(HIconLeaf),F)!));
        foreach(Type t in new[]{typeof(Millsage),typeof(CheersByTheSea),typeof(KanatasHomework),typeof(GiftFromAbove),typeof(TheLastStage),typeof(SmilingPanKeychain),typeof(StreetPiano),typeof(ChargePower),typeof(HotaruHappinessCannon),typeof(HotaruCoffinCannon),typeof(HotaruContestCannon),typeof(HotaruLongingCannon),typeof(HotaruAbsolutionCannon)})
        {if(!ModelDb.Contains(t))ModelDb.Inject(t);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(t)),"<Id>k__BackingField",ModelDb.GetId(t));}
        NumericHarmony.Patch(typeof(Millsage).GetMethod("RefreshIntent",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
        NumericHarmony.Patch(typeof(Millsage).GetMethod("ExecutePhase",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(HPhaseLeaf),F)!));
        foreach(var method in typeof(CardPileCmd).GetMethods(F).Where(m=>m.Name=="Draw"&&m.ReturnType==typeof(Task<IEnumerable<CardModel>>)&&m.GetParameters().Any(p=>p.ParameterType==typeof(decimal))))
            NumericHarmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(HDrawLeaf),F)!));
        foreach(string name in new[]{"HotaruCannonBlockScopePatch","HotaruCannonBlockRoundingPatch"})
        {Type t=Mod.GetTypes().Single(t=>t.Name==name);Check(NumericHarmony.CreateClassProcessor(t).Patch().Count>0,"installs actual cannon block path "+name);}
        Type history=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory",true)!;
        NumericHarmony.Patch(history.GetMethod("CreatureAttacked",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
#if HBR_BETA_NATIVE
        typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Modding.AssemblyInfo",true)!.GetMethod("Init",F)!.Invoke(null,null);
        typeof(ModelIdSerializationCache).GetMethod("Init",F)!.Invoke(null,null);
        typeof(ModelIdSerializationCache).GetMethod("CacheSavedPropertiesForTypeDebug",F)!.Invoke(null,new object[]{typeof(StreetPiano)});
#else
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(StreetPiano));
#endif
        HReset();
    }
    static async Task HActivationAndStartCases()
    {
        HReset();Creature coffin=HCannon<HotaruCoffinCannon>();var cheers=HRelic<CheersByTheSea>();
        await HM.TriggerAllEffects(coffin,new ThrowingPlayerChoiceContext());
        Equal(HDraws,1,"actual cannon activation issues one draw");Equal(Hero.GetPower<ChargePower>()?.Amount??0,1,"actual native power application gives holder Charge1");
        await HM.TriggerAllEffects(coffin,new ThrowingPlayerChoiceContext());Equal(HDraws,1,"subsequent activation does not repeat Cheers");
        Check(cheers.UsedThisCombat,"first activation receipt is persistent relic state");
        Call("HotaruClassRelicCombatHelper","ResetBattle",Player);await HM.TriggerAllEffects(coffin,new ThrowingPlayerChoiceContext());Equal(HDraws,2,"new battle reset permits exactly one new activation award");
        HReset();coffin=HCannon<HotaruCoffinCannon>();HRelic<TheLastStage>();HRelic<CheersByTheSea>();
        typeof(Millsage).GetProperty("RetiredCannonsMask",F)!.SetValue(HM,29);
        await HM.BeforeCombatStart();
        Check(HPhases.Count(p=>ReferenceEquals(p.Cannon,coffin))==1,"real combat start performs one additional Coffin full action after existing roster is summoned");
        Check(Combat.Allies.Count(c=>Millsage.IsOwnedCannon(c,Player))==1,"retired four cannons do not return at start");
        Check(Hero.Block>0&&HDraws==1,"startup complete action grants real native Block and activates Cheers once");
        HReset();coffin=HCannon<HotaruCoffinCannon>();var piano=HRelic<StreetPiano>();await (Task)Call("HotaruClassRelicCombatHelper","OnCannonsReady",HM)!;
        Check(piano.FollowupPending&&piano.SelectedKind==1&&HPhases.Count==1,"one-cannon Piano automatically selects exact current entity without grid");
        await HM.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Check(piano.OwnTurns==1&&piano.FollowupPending,"first own turn does not consume second-turn followup");
        int first=HPhases.Count;await HM.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Equal(HPhases.Count,first,"duplicate native first-turn callback is ignored");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await HM.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);
        Check(piano.OwnTurns==2&&!piano.FollowupPending,"second own turn claims Piano followup once");
        Equal(HPhases.Count-first,2,"second turn gives one Piano action plus the ordinary start action");
        HReset();coffin=HCannon<HotaruCoffinCannon>();piano=HRelic<StreetPiano>();await (Task)Call("HotaruClassRelicCombatHelper","OnCannonsReady",HM)!;
        await HM.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Set(coffin,"_currentHp",0);Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);
        first=HPhases.Count;await HM.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Equal(HPhases.Count,first,"missing selected cannon safely skips second-turn action");
    }
    static async Task HGiftCases()
    {
        foreach(int count in new[]{1,2,3})
        {
            HReset();HRelic<GiftFromAbove>();HCannon<HotaruCoffinCannon>();if(count>1)HCannon<HotaruHappinessCannon>();if(count>2)HCannon<HotaruContestCannon>();HObserveOnly=HAttemptNestedCritical=true;
            Native(HM,"QueueCriticalCannonReward");
            IEnumerable queue=(IEnumerable)typeof(Millsage).GetField("_pendingCriticalCannons",F)!.GetValue(HM)!;object receipt=queue.Cast<object>().Single();
            Creature[] selected=(Creature[])receipt.GetType().GetField("Item1")!.GetValue(receipt)!;
            Equal(selected.Length,Math.Min(2,count)+1,"Gift adds one cannon to actual critical queue with available="+count);
            if(count==3)Equal(selected.Distinct().Count(),3,"Gift prefers the cannon outside handbook's distinct two");
            var soulBefore=selected.Distinct().ToDictionary(c=>c,c=>HM.GetCannonDamagePercent(((HotaruCannonModel)c.Monster).Kind));
            int handbook=(int)receipt.GetType().GetField("Item2")!.GetValue(receipt)!;
            await (Task)Native(HM,"ResolveCriticalCannonRewards",new ThrowingPlayerChoiceContext())!;
            foreach(Creature cannon in selected.Distinct())Equal(HM.GetCannonDamagePercent(((HotaruCannonModel)cannon.Monster).Kind)-soulBefore[cannon],
                selected.Take(handbook).Contains(cannon)?1:0,"only original handbook cannon receives soul growth, not extra Gift activation");
            Check(HPhases.All(p=>p.Suppressed),"all critical-reward activation phases preserve recursion suppression");
            Check(!queue.Cast<object>().Any(),"nested attempted critical rewards do not enqueue recursively");
            int phases=HPhases.Count;Native(HM,"QueueCriticalCannonReward");await (Task)Native(HM,"ResolveCriticalCannonRewards",new ThrowingPlayerChoiceContext())!;
            Equal(HPhases.Count,phases*2,"Gift can trigger again on every separate critical success");
        }
    }
    static DeflectorProtection HShield(int dp)
    {var shield=HRelic<DeflectorProtection>();Set(shield,"_cancerMaxDp",100);Set(shield,"_hasInitializedDp",true);Set(shield,"_dp",dp);return shield;}
    static async Task<List<DamageResult>> HNativeHits(int hits,decimal amount)
    {
        AttackCommand attack=DamageCmd.Attack(amount).FromMonster(Enemy.Monster)
            .Unpowered().WithNoAttackerAnim().WithHitCount(hits);
        await attack.Execute(new ThrowingPlayerChoiceContext());
        return attack.Results.SelectMany(rows=>rows).ToList();
    }
    static async Task HDpCases()
    {
        HReset();HCannon<HotaruCoffinCannon>();var key=HRelic<SmilingPanKeychain>();var shield=HShield(10);Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Enemy);
        var result=await HNativeHits(2,30m);
        Equal(shield.Dp,0,"real native first incoming hit depletes positive DP");
        Equal(HPhases.Count,1,"native per-hit callback runs all present cannons once before second incoming hit");
        Check(result.Count==2&&result[1].BlockedDamage>=30&&result[1].UnblockedDamage==0,"Block granted in first hit callback protects the next hit of the same native command");
        SetNative(Hero,0);Set(shield,"_dp",10);await HNativeHits(1,30m);Equal(HPhases.Count,1,"second enemy-side DP break in same turn does not retrigger");
        Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Player);Set(shield,"_dp",10);await HNativeHits(1,30m);Equal(HPhases.Count,2,"own side turn has independent DP-break allowance");
        Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Enemy);Set(Combat,"<RoundNumber>k__BackingField",2);SetNative(Hero,0);Set(shield,"_dp",10);await HNativeHits(1,30m);Equal(HPhases.Count,3,"next enemy turn has a new allowance");
        HReset();HCannon<HotaruCoffinCannon>();HRelic<SmilingPanKeychain>();shield=HShield(10);shield.LoseDp(10);await HM.AfterDamageReceived(new ThrowingPlayerChoiceContext(),Hero,Blank<DamageResult>(),ValueProp.Unpowered,Enemy,null!);Equal(HPhases.Count,0,"DP payment/direct loss cannot queue a break reaction");
        Set(shield,"_dp",10);SetNative(Hero,100);await HNativeHits(1,30m);Equal(HPhases.Count,0,"native full Block prevents DP loss and reaction");
    }
    static async Task HHomeworkCases()
    {
        HReset();var homework=HRelic<KanatasHomework>();var manual=Player.GetRelic<ElectronicSoldierManual>()!;
        foreach(var kind in new[]{RoomType.Monster,RoomType.Elite,RoomType.Boss})
        {
            string encounterName=kind==RoomType.Elite?"BygoneEffigyElite":kind==RoomType.Boss?"AeonglassBoss":"BowlbugsWeak";
            var encounter=(EncounterModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Models.Encounters."+encounterName,true)!);
            Set(encounter,"<IsMutable>k__BackingField",true);Set(encounter,"<Id>k__BackingField",ModelDb.GetId(encounter.GetType()));
            Set(Combat,"_encounter",encounter);var room=new CombatRoom(Combat);
            int before=manual.MemoryGardenUpgradePoints;await homework.AfterCombatVictory(room);int gain=manual.MemoryGardenUpgradePoints-before;
            Equal(gain,kind==RoomType.Monster?0:1,"actual victory hook extra shared point for "+kind);
            await homework.AfterCombatVictory(room);Equal(manual.MemoryGardenUpgradePoints-before,gain,"same physical combat victory cannot double-claim homework "+kind);
        }
    }
    static async Task HIsolationCases()
    {
        HReset();Creature cannon=HCannon<HotaruCoffinCannon>();HRelic<CheersByTheSea>();
        var peer=Blank<Player>();Set(peer,"_runState",RunState);Set(peer,"<Character>k__BackingField",Blank<HbrgoHotaru>());
        Creature peerHero=(Creature)H("Actor",null,peer,null)!;Set(peer,"<Creature>k__BackingField",peerHero);
        var pc=Blank<PlayerCombatState>();Set(pc,"_player",peer);Set(pc,"<TurnNumber>k__BackingField",1);Set(peer,"<PlayerCombatState>k__BackingField",pc);
        Set(peer,"<Deck>k__BackingField",new MegaCrit.Sts2.Core.Entities.Cards.CardPile(MegaCrit.Sts2.Core.Entities.Cards.PileType.Deck));
        var peerManual=(ElectronicSoldierManual)ModelDb.Relic<ElectronicSoldierManual>().ToMutable();Set(peerManual,"_owner",peer);
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData",F)!.SetValue(peerManual,Player.GetRelic<ElectronicSoldierManual>()!.HbrBattleProgressData);
        var peerMill=(Millsage)ModelDb.Relic<Millsage>().ToMutable();Set(peerMill,"_owner",peer);Set(peer,"_relics",new List<RelicModel>{peerManual,peerMill});
        ((List<RelicModel>)typeof(Checks).GetField("ExtraListeners",F)!.GetValue(null)!).Add(peerMill);
        Creature peerCannon=(Creature)H("Actor",Blank<HotaruCoffinCannon>(),null,peer)!;
        Set(Combat,"_allies",new List<Creature>{Hero,cannon,peerHero,peerCannon});Set(RunState,"_players",new List<Player>{Player,peer});
        HObserveOnly=true;await peerMill.TriggerAllEffects(peerCannon,new ThrowingPlayerChoiceContext());Equal(HDraws,0,"teammate activation cannot consume holder Cheers");
        await HM.TriggerAllEffects(cannon,new ThrowingPlayerChoiceContext());Equal(HDraws,1,"holder still receives first activation award after teammate action");
        Check(peerHero.GetPower<ChargePower>()==null&&Hero.GetPower<ChargePower>()?.Amount==1,"Charge applies only to relic owner");
        var piano=HRelic<StreetPiano>();await HM.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),peer);Equal(piano.OwnTurns,0,"teammate turn callback does not advance holder Piano");
        Set(RunState,"_players",new List<Player>{Player});
        HReset();cannon=HCannon<HotaruCoffinCannon>();HRelic<CheersByTheSea>();H("Mode",false,1);HObserveOnly=true;
        await HM.TriggerAllEffects(cannon,new ThrowingPlayerChoiceContext());Equal(HDraws,0,"new class relic effects are disabled in legacy numeric mode");
        foreach(var row in new (Type Type,MegaCrit.Sts2.Core.Entities.Relics.RelicRarity Rarity)[]{
          (typeof(CheersByTheSea),MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Common),(typeof(KanatasHomework),MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Uncommon),
          (typeof(GiftFromAbove),MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Rare),(typeof(TheLastStage),MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Uncommon),
          (typeof(SmilingPanKeychain),MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Rare),(typeof(StreetPiano),MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Shop)})
        {
            var relic=ModelDb.GetById<RelicModel>(ModelDb.GetId(row.Type));Check(relic.Rarity==row.Rarity,"effective98b rarity: "+row.Type.Name);
            Check(relic is HBRGOSakiMod.Core.Models.RelicPools.IHotaruClassRelic,"dedicated Hotaru pool marker: "+row.Type.Name);
            Check(relic.HoverTips.ToArray().Length>1,"canonical native hover includes required localized rules: "+row.Type.Name);
        }
    }
    static object HEnterPersonal()
    {
        Type coordinator=Helper("PersonalImmediateTurnCoordinator"),type=coordinator.GetNestedType("Session",F)!;
        object session=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        type.GetField("Owner",F)!.SetValue(session,Player);type.GetField("Combat",F)!.SetValue(session,Combat);
        coordinator.GetField("_combat",F)!.SetValue(null,Combat);coordinator.GetField("_current",F)!.SetValue(null,session);
        Check(ReferenceEquals(Call("OverclockImmediateTurnCoordinator","GetTurnToken",Player),session),"real shared token getter recognizes controlled live personal session");return session;
    }
    static void HLeavePersonal()
    {Type coordinator=Helper("PersonalImmediateTurnCoordinator");coordinator.GetField("_current",F)!.SetValue(null,null);coordinator.GetField("_combat",F)!.SetValue(null,null);}
    static async Task HInsertedTurnCases()
    {
        HReset();HCannon<HotaruCoffinCannon>();var piano=HRelic<StreetPiano>();await (Task)Call("HotaruClassRelicCombatHelper","OnCannonsReady",HM)!;
        await HM.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);int before=HPhases.Count;object session=HEnterPersonal();
        try
        {
            await (Task)Native(HM,"TriggerOverclockTurnStart",new ThrowingPlayerChoiceContext(),session)!;
            Check(piano.OwnTurns==2&&!piano.FollowupPending,"real inserted-start bridge advances Piano to second own turn");
            Equal(HPhases.Count-before,2,"inserted turn performs Piano action and existing natural-start action");
            int after=HPhases.Count;await (Task)Native(HM,"TriggerOverclockTurnStart",new ThrowingPlayerChoiceContext(),session)!;Equal(HPhases.Count,after,"same inserted session cannot repeat start or Piano followup");
        }
        finally{HLeavePersonal();}
        before=HPhases.Count;await HM.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Equal(HPhases.Count,before,"resuming suspended normal turn does not create a new own start");
        HReset();HCannon<HotaruCoffinCannon>();HRelic<SmilingPanKeychain>();var shield=HShield(10);
        await HNativeHits(1,30m);Equal(HPhases.Count,1,"ordinary own turn has its first DP reaction");
        session=HEnterPersonal();
        try
        {
            SetNative(Hero,0);Set(shield,"_dp",10);await HNativeHits(1,30m);Equal(HPhases.Count,2,"personal extra turn has an independent DP reaction receipt");
            SetNative(Hero,0);Set(shield,"_dp",10);await HNativeHits(1,30m);Equal(HPhases.Count,2,"same personal extra turn cannot react twice");
        }
        finally{HLeavePersonal();}
        SetNative(Hero,0);Set(shield,"_dp",10);await HNativeHits(1,30m);Equal(HPhases.Count,2,"return from extra turn preserves already-used ordinary receipt");
    }
    static StreetPiano HReloadPiano(SavedProperties saved)
    {
        var options=new JsonSerializerOptions{IncludeFields=true};
        var reloaded=(StreetPiano)ModelDb.Relic<StreetPiano>().ToMutable();Set(reloaded,"_owner",Player);
        JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(saved,options),options)!.FillInternal(reloaded);
        return reloaded;
    }
    static async Task HPianoPhysicalSaveCases()
    {
        HReset();Creature original=HCannon<HotaruCoffinCannon>();original.CombatId=77;var piano=HRelic<StreetPiano>();
        await (Task)Call("HotaruClassRelicCombatHelper","OnCannonsReady",HM)!;Native(piano,"AdvanceTurn",HM);
        var saved=SavedProperties.From(piano)!;var restored=HReloadPiano(saved);
        Check(ReferenceEquals(Native(restored,"AdvanceTurn",HM),original),"loaded Piano recognizes same physical cannon by persisted CombatId");
        restored=HReloadPiano(saved);Set(original,"_currentHp",0);Creature replacement=HCannon<HotaruCoffinCannon>();replacement.CombatId=78;
        Check(Native(restored,"AdvanceTurn",HM)==null&&!restored.FollowupPending,"loaded Piano does not borrow a replacement cannon of the same kind");
        Check((string)typeof(StreetPiano).GetProperty("SelectedCombatId",F)!.GetValue(restored)! == "77","native saved-property JSON roundtrip preserves selected combat identity");
    }
    public static async Task RunHotaru98(string path)
    {
        string? error=null;try{HSetup();await HActivationAndStartCases();await HGiftCases();await HDpCases();await HHomeworkCases();await HIsolationCases();await HInsertedTurnCases();await HPianoPhysicalSaveCases();}catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual Millsage activation and start/turn phase, native Charge apply/GainBlock, critical reward queue/drain and victory callback. Full native AttackCommand with two hits and its two canonical CreatureCmd.Damage calls, actual DP absorption and native per-hit AfterDamageReceived, real Block protects later hit. Draw presentation terminal and gift phase effects controlled. No Godot rendering, actual grid selection or live multiplayer. Controlled active personal session validates shared token and real Millsage inserted-start bridge, not a full coordinator transition. Native SavedProperties JSON roundtrip checks selected cannon identity; full game save loading is outside the host."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count+" Hotaru class relic checks");if(error!=null)throw new Exception("See "+path);
    }
}
