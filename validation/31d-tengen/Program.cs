using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class MisatoStandalone { public static void Run(string path) => EffectChecks.RunMisato(path); }
static partial class EffectChecks
{
    sealed record MisatoHit(decimal Raw,int Hits,decimal Amplification,decimal Destruction,bool DefenseDown,bool PriorAttackUp);
    static readonly List<MisatoHit> MisatoHits=[];
    static readonly List<decimal> MisatoEnergy=[];
    static bool MisatoCannonProbe;
    static void MisatoObserveCannon(object[] __args)
    {
        if(!MisatoCannonProbe || __args[5] is not CardModel source)return;
        object?[] query={Enemy,Hero,source,ValueProp.Move,null};
        Check((bool)Call("HotaruCannonStatsHelper","TryGetCurrentCannon",query)!,"real cannon reaction retains the card source while identifying its own cannon dealer");
        Equal((decimal)Call("TengenBrilliantMoveCombatHelper","GetDamageAmplificationPercent",source,Hero,Enemy,ValueProp.Move)!,0m,"card-only OD amplification never leaks into triggered cannon damage");
        Equal((decimal)Call("TengenBrilliantMoveCombatHelper","GetDestructionGainMultiplier",source,Enemy,Hero,ValueProp.Move)!,1m,"card coefficient1 never lowers its triggered cannon's destruction gain");
    }
    static bool MisatoListeners(ref IEnumerable<AbstractModel> __result)
    {
        var enemies=(List<Creature>)typeof(CombatState).GetField("_enemies",F)!.GetValue(Combat)!;
        var allies=(List<Creature>)typeof(CombatState).GetField("_allies",F)!.GetValue(Combat)!;
        __result=enemies.Concat(allies).SelectMany(c=>c.Powers).Cast<AbstractModel>().Concat(Player.Relics).Concat(Player.PlayerCombatState!.AllCards).ToArray();
        return false;
    }
    static object? MisatoHook(string name,AttackCommand command,int count=0)
    {
        MethodInfo method=typeof(Hook).GetMethods(F).Single(m=>m.Name==name);
        object?[] args=method.GetParameters().Select(p=>p.ParameterType.IsInstanceOfType(RunState)?(object)RunState:
            p.ParameterType.IsInstanceOfType(Combat)?Combat:p.ParameterType==typeof(AttackCommand)?command:
            p.ParameterType==typeof(int)?count:typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType)?new ThrowingPlayerChoiceContext():
            throw new Exception("Unknown attack hook parameter "+p)).ToArray();
        return Invoke(method,null,args);
    }
    static bool MisatoAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=MisatoResolveAttack(__instance);return false;}
    static async Task<AttackCommand> MisatoResolveAttack(AttackCommand command)
    {
        CardModel card=(CardModel)command.ModelSource!;
        await (Task)MisatoHook("BeforeAttack",command)!;
        decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!;
        int hits=(int)decimal.Floor((decimal)MisatoHook("ModifyAttackHitCount",command,(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!)!);
        MisatoHits.Add(new(raw,hits,(decimal)Call("TengenBrilliantMoveCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,
            (decimal)Call("TengenBrilliantMoveCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,
            Enemy.HasPower<HbrDefenseDownPower>(),Hero.HasPower<HbrAttackUpPower>()));
        var results=(List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!;
        foreach(var target in Combat.HittableEnemies)
            results.Add(Enumerable.Range(0,hits).Select(_=>new DamageResult(target,ValueProp.Move)).ToList());
        await (Task)MisatoHook("AfterAttack",command)!;
        return command;
    }
    static bool MisatoEnergyLeaf(decimal amount,Player player,ref Task __result)
    {Check(player==Player,"dessert energy belongs only to the card's owner");MisatoEnergy.Add(amount);__result=Task.CompletedTask;return false;}
    static bool MisatoNetId(ref ulong? __result){__result=Player.NetId;return false;}
    static void MisatoReset()
    {
        FormReset();Fate(FateJson(bosses:0));Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",1);
        Set(Combat,"_escapedCreatures",new List<Creature>());
        var table=Helper("TengenBrilliantMoveCombatHelper").GetField("Battles",F)!.GetValue(null)!;
        table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);MisatoHits.Clear();MisatoEnergy.Clear();
    }
    static TengenBrilliantMoveHbrgoSaki Misato(params int[] effects)
    {
        var card=FormCard<TengenBrilliantMoveHbrgoSaki>();
        foreach(int id in effects){Check((bool)Native(card,"PrepareUpgradeEffect",id)!,"Misato accepts choice "+id);card.UpgradeInternal();card.FinalizeUpgradeInternal();}
        return card;
    }
    static void MisatoPermanent(CardModel card){card.Pile?.RemoveInternal(card,true);Player.Deck.AddInternal(card);}
    static decimal MisatoGrowth(TengenBrilliantMoveHbrgoSaki card)=>string.IsNullOrEmpty(card.WinningStreakData)?0m:decimal.Parse(card.WinningStreakData,System.Globalization.CultureInfo.InvariantCulture);
    static async Task MisatoPlay(CardModel card)=>await (Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),Play(card))!;
    static Creature MisatoEnemy()
    {
        var monster=(MonsterModel)ModelDb.Monster<SmallHopper>().ToMutable();
        var creature=(Creature)H("Actor",monster,null,null)!;
        ((List<Creature>)typeof(CombatState).GetField("_enemies",F)!.GetValue(Combat)!).Add(creature);return creature;
    }
    static Task MisatoDeath(Creature target,bool prevented=false,CardModel? source=null)
    {
        MethodInfo hook=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterDeath");
        object?[] args=hook.GetParameters().Select(p=>p.ParameterType.IsInstanceOfType(RunState)?(object)RunState:
            p.ParameterType.IsInstanceOfType(Combat)?Combat:p.ParameterType==typeof(Creature)?target:
            p.ParameterType==typeof(bool)?prevented:p.ParameterType==typeof(float)?0f:p.ParameterType==typeof(CardModel)?source:throw new Exception(p.ToString())).ToArray();
        return (Task)Invoke(hook,null,args)!;
    }
    static IDisposable MisatoExtra(bool od)
    {
        Type coordinator=Helper(od?"OverclockImmediateTurnCoordinator":"PersonalImmediateTurnCoordinator");
        FieldInfo current=coordinator.GetField("_current",F)!;object? old=current.GetValue(null);
        FieldInfo? combat=coordinator.GetField("_combat",F);object? oldCombat=combat?.GetValue(null);
        object session;
        if(od)session=coordinator.GetNestedType("Session",F)!.GetConstructors(F).Single().Invoke([Blank<Overclock>(),Combat,new[]{Player}]);
        else{session=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(coordinator.GetNestedType("Session",F)!);Set(session,"Owner",Player);Set(session,"Combat",Combat);combat!.SetValue(null,Combat);}
        current.SetValue(null,session);Check((bool)Invoke(coordinator.GetMethod("IsActiveFor",F)!,null,Player)!,"fixture has a real "+(od?"OD":"personal")+" membership token");
        return new MisatoExtraScope(()=>{current.SetValue(null,old);combat?.SetValue(null,oldCombat);});
    }
    sealed class MisatoExtraScope(Action restore):IDisposable{public void Dispose()=>restore();}
    static async Task MisatoPipelineCases(Harmony harmony)
    {
        MisatoReset();var card=Misato(2);
        decimal basePower=(decimal)Call("HbrSkillPower","EvaluateDamageFromUnits",2.4m,100m,150m,false)!;
        Equal(Actual(Enemy,Hero,2.4m,ValueProp.Move,card),decimal.Floor(basePower),"the card's raw coefficient enters the shared actual damage pipeline");
        var amplification=(HotaruDamageAmplificationPower)ModelDb.Power<HotaruDamageAmplificationPower>().ToMutable();Set(amplification,"_owner",Hero);
        typeof(HotaruDamageAmplificationPower).GetProperty(nameof(HotaruDamageAmplificationPower.AmplificationPercentData),F)!.SetValue(amplification,"100");Powers(Hero).Add(amplification);
        Equal(Actual(Enemy,Hero,2.4m,ValueProp.Move,card),decimal.Floor(basePower*2m),"ordinary100percent amplification retains its usual shared bucket");
        using(MisatoExtra(true))Equal(Actual(Enemy,Hero,2.4m,ValueProp.Move,card),decimal.Floor(basePower*3m),"OD100points add to the ordinary amplification bucket instead of multiplying it again");
        var relic=new Millsage();Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",Player);Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),relic});
        var cannon=(Creature)H("Actor",Blank<HotaruContestCannon>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,cannon});
        harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HotaruCannonDamageContextPatch",true)!).Patch();
        var probe=typeof(EffectChecks).GetMethod(nameof(MisatoObserveCannon),F)!;harmony.Patch(Canonical,prefix:new HarmonyMethod(probe){priority=0});
        try{MisatoCannonProbe=true;using(MisatoExtra(true))await (Task)Call("HotaruCannonStatsHelper","ReactionDamage",new ThrowingPlayerChoiceContext(),cannon,Enemy,6m,card,null,false,false)!;}
        finally{MisatoCannonProbe=false;harmony.Unpatch(Canonical,probe);}
    }
    static async Task MisatoNativeDeathCases(Harmony harmony)
    {
        MisatoReset();var card=Misato(6);MisatoPermanent(card);var target=MisatoEnemy();
        await CreatureCmd.Kill(target);
        Check(target.IsDead&&MisatoGrowth(card)==1m,"actual native CreatureCmd.Kill reaches the production death hook and grants one point");
        target=MisatoEnemy();await CreatureCmd.Escape(target);
        Check(!target.IsDead&&MisatoGrowth(card)==1m,"actual native Escape dispatches removal rather than a growth-granting death");
        var subject=(MonsterModel)ModelDb.Monster<TestSubject>().ToMutable();
        target=(Creature)H("Actor",subject,null,null)!;((List<Creature>)typeof(CombatState).GetField("_enemies",F)!.GetValue(Combat)!).Add(target);
        var adaptable=Power<AdaptablePower>(target,1);
        MethodInfo deadAnimation=typeof(TestSubject).GetMethod("TriggerDeadState",F)!;
        MethodInfo localNet=typeof(LocalContext).GetProperty("NetId",F)!.GetMethod!;
        harmony.Patch(deadAnimation,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
        harmony.Patch(localNet,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoNetId),F)!));
        try
        {
            await CreatureCmd.Kill(target);
            bool reviving=(bool)typeof(AdaptablePower).GetProperty("IsReviving",F)!.GetValue(adaptable)!;
            Check(reviving&&MisatoGrowth(card)==1m,
                $"actual native Adaptable death enters revival without awarding a permanent kill (reviving={reviving},growth={MisatoGrowth(card)},hp={target.CurrentHp},dead={target.IsDead})");
            Native(adaptable,"DoRevive");Set(target,"_currentHp",100);await PowerCmd.Remove(adaptable);await CreatureCmd.Kill(target);
            Check(target.IsDead&&MisatoGrowth(card)==2m,"native final death after revival ends earns exactly one permanent kill");
        }
        finally{harmony.Unpatch(deadAnimation,typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!);harmony.Unpatch(localNet,typeof(EffectChecks).GetMethod(nameof(MisatoNetId),F)!);}
    }
    static async Task MisatoCardCases()
    {
        MisatoReset();var card=Misato();Check(card.Type==CardType.Attack&&card.Rarity==CardRarity.Rare&&card.TargetType==TargetType.AllEnemies&&card.EnergyCost.Canonical==1,"Misato starts as a one-energy rare AoE attack");
        Equal(card.DynamicVars.Damage.BaseValue,2.4m,"forty-percent coefficient is stored before all multipliers");
        await MisatoPlay(card);var hit=MisatoHits.Single();Equal(hit.Raw,2.4m,"base attack issues40percent raw skill units");Equal(hit.Hits,3,"base card issues three native hit-count units");Equal(hit.Destruction,1m/3m,"authored destruction coefficient1 normalizes against shared default3");
        Check(!hit.PriorAttackUp&&Hero.GetPower<HbrAttackUpPower>()?.Amount==1,"one-use attack-up is granted only after the attack");
        Equal((decimal)Native(Hero.GetPower<HbrAttackUpPower>()!,"get_EffectPotency")!,35m,"base self buff uses framework35percent at100INT");
        MisatoReset();card=Misato(1);await MisatoPlay(card);Equal((decimal)Native(Hero.GetPower<HbrAttackUpPower>()!,"get_EffectPotency")!,75m,"Cooldown replaces35with75basis");
        MisatoReset();card=Misato(2,3);await MisatoPlay(card);Check(MisatoHits.Single().Amplification==0m&&MisatoHits.Single().Hits==3,"normal turn gains neither OD amplification nor large combo");
        MisatoReset();card=Misato(2,3);using(MisatoExtra(true))await MisatoPlay(card);
        hit=MisatoHits.Single();Equal(hit.Amplification,100m,"OD contributes100ordinary amplification points");Equal(hit.Hits,6,"pre-attack large combo doubles this card's own three hits through native hooks");Check(!Hero.HasPower<HbrComboLargePower>(),"the combo's one use is consumed after the actual attack hook");
        MisatoReset();card=Misato(2,3);using(MisatoExtra(false))await MisatoPlay(card);Check(MisatoHits.Single().Amplification==0m&&MisatoHits.Single().Hits==3,"other extra turns never impersonateOD");
        MisatoReset();card=Misato(4);await MisatoPlay(card);Check(MisatoEnergy.SequenceEqual(new[]{2m}),"first ordinary own turn grants two energy");await MisatoPlay(card);Equal(MisatoEnergy.Count,2,"another first-turn play qualifies rather than consuming an invented once-per-card receipt");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await MisatoPlay(card);Equal(MisatoEnergy.Count,2,"later ordinary turns grant no dessert energy");
        foreach(bool od in new[]{true,false}){MisatoReset();card=Misato(4);using(MisatoExtra(od))await MisatoPlay(card);Check(MisatoEnergy.Count==0,"native TurnNumber1 in an inserted "+(od?"OD":"personal")+" turn is not an ordinary first turn");}
        MisatoReset();card=Misato(5);var other=MisatoEnemy();await MisatoPlay(card);
        Check(MisatoHits.Single().DefenseDown&&Enemy.GetPower<HbrDefenseDownPower>()?.Amount==2&&other.GetPower<HbrDefenseDownPower>()?.Amount==2,"Peerless gives all enemies two-turn defense-down before attacking");
        Equal((decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",Enemy.GetPower<HbrDefenseDownPower>()!)!,19m,"twenty basis uses the normal INT/LUCK formula");
        MisatoReset();card=Misato(0,1,2,3,4,5,6);Check(card.CurrentUpgradeLevel==7&&!card.IsUpgradable&&card.EnergyCost.GetWithModifiers(CostModifiers.Local)==0,"all seven effects remain independently obtainable and Flash applies once");
        Check(((IThirtyOneDMemberCard)card).ThirtyOneDMember==ThirtyOneDMember.NikaidoMisato,"card declares the correct31D member");MisatoPermanent(card);
        Equal((int)Invoke(typeof(IThirtyOneDMemberCard).Assembly.GetType("HBRGOSakiMod.Core.Models.Capabilities.ThirtyOneDKeywordRules",true)!.GetMethod("GetPermanentDistinctCount",F)!,null,Player)!,1m,"permanent31D kinds count Misato once");
    }
    static async Task MisatoGrowthCases()
    {
        MisatoReset();var card=Misato();MisatoPermanent(card);var dead=MisatoEnemy();Set(dead,"_currentHp",0);await MisatoDeath(dead);Equal(MisatoGrowth(card),0,"kills before upgrade6 grant no retroactive growth");
        Check((bool)Native(card,"PrepareUpgradeEffect",6)!,"growth can be selected after earlier deaths");card.UpgradeInternal();card.FinalizeUpgradeInternal();await MisatoDeath(dead);Equal(MisatoGrowth(card),0,"a repeated prior death notification cannot become retroactive growth");
        var ordinary=(TengenBrilliantMoveHbrgoSaki)Combat.CloneCard(card);ordinary.DeckVersion=card;Player.PlayerCombatState!.Hand.AddInternal(ordinary);await ordinary.BeforeCombatStart();
        var od=(TengenBrilliantMoveHbrgoSaki)Combat.CloneCard(card);od.DeckVersion=card;Player.PlayerCombatState.Hand.AddInternal(od);await od.BeforeCombatStart();
        ordinary.DynamicVars.Damage.BaseValue+=2m;
        dead=MisatoEnemy();Set(dead,"_currentHp",0);await MisatoDeath(dead);
        Equal(MisatoGrowth(card),1m,"one real nonminion death grants one permanent percentage point");Equal(MisatoGrowth(ordinary),1m,"ordinary mirror shares the physical growth");Equal(MisatoGrowth(od),1m,"OD mirror does not multiply the physical kill reward");
        Equal(ordinary.DynamicVars.Damage.BaseValue,4.46m,"growth increments the raw coefficient without erasing another base-damage modifier");
        await MisatoDeath(dead);Equal(MisatoGrowth(card),1m,"the global native death event is idempotent per creature");
        var peer=SupportTeammate();var peerSource=Owned<MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad>();Set(peerSource,"_owner",peer);
        var peerVictim=MisatoEnemy();Set(peerVictim,"_currentHp",0);await MisatoDeath(peerVictim,false,peerSource);
        Equal(MisatoGrowth(card),2m,"a teammate-owned source also awards the player's permanent kill growth");
        dead=MisatoEnemy();Power<MinionPower>(dead,1);Set(dead,"_currentHp",0);await MisatoDeath(dead);Equal(MisatoGrowth(card),2m,"native MinionPower excludes a dead enemy");
        await PowerCmd.Remove(dead.GetPower<MinionPower>()!);await MisatoDeath(dead);Equal(MisatoGrowth(card),2m,"removing a dead minion marker does not manufacture a second eligible death");
        dead=MisatoEnemy();await MisatoDeath(dead);Equal(MisatoGrowth(card),2m,"a living or escaping enemy cannot qualify merely from a dispatched notification");
        Set(dead,"_currentHp",0);await MisatoDeath(dead,true);Equal(MisatoGrowth(card),2m,"native removal-prevented revival notification grants no kill");
        await MisatoDeath(dead);Equal(MisatoGrowth(card),3m,"a later actual final death after a prevented removal can count once");
        var copy=(TengenBrilliantMoveHbrgoSaki)Combat.CloneCard(card);Call("CardCopyContextHelper","RecordCopy",copy);copy.DeckVersion=card;Player.PlayerCombatState.Hand.AddInternal(copy);
        Equal(MisatoGrowth(copy),3m,"a temporary copy starts from the original's current growth snapshot");
        var preview=(TengenBrilliantMoveHbrgoSaki)card.MutableClone();
        dead=MisatoEnemy();Set(dead,"_currentHp",0);await MisatoDeath(dead);Equal(MisatoGrowth(card),4m,"new death gives permanent origin only one reward despite mirrors and copy");Equal(MisatoGrowth(copy),4m,"temporary copy grows independently after its snapshot");Equal(MisatoGrowth(preview),3m,"an unregistered preview never earns live kill growth");
        Native(copy,"SetGrowth",900m);Equal(MisatoGrowth(card),4m,"temporary copy's growth never writes back to permanent origin");
        for(int i=0;i<51;i++){dead=MisatoEnemy();Set(dead,"_currentHp",0);await MisatoDeath(dead);}
        Equal(MisatoGrowth(card),55m,"growth continues past the obsolete fifty-point cap");Equal(card.DynamicVars.Damage.BaseValue,5.7m,"permanent growth is part of raw per-hit coefficient before all multipliers");
        Native(card,"SetGrowth",999m);card.DowngradeInternal();Equal(MisatoGrowth(card),999m,"downgrading an upgrade does not erase already permanent growth");Equal(card.DynamicVars.Damage.BaseValue,62.34m,"native downgrade reconstruction restores the growth coefficient once");
        string face=card.GetDescriptionForPile(PileType.Deck,null);Check(!face.Contains("1039%")&&DesuwaFace(card,true).Contains("1039%"),"67: normal face shows damage while detail shows the grown1039percent coefficient");
        Type? cache=typeof(SavedProperties).Assembly.GetTypes().SingleOrDefault(t=>t.Name=="SavedPropertiesTypeCache");
        if(cache?.GetMethod("InjectTypeIntoCache",F)is{}inject)Invoke(inject,null,typeof(TengenBrilliantMoveHbrgoSaki));
        else{cache=typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;Invoke(cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!,null,typeof(TengenBrilliantMoveHbrgoSaki));cache.GetField("_initialized",F)!.SetValue(null,true);}
        var jsonOptions=new JsonSerializerOptions{IncludeFields=true};string json=JsonSerializer.Serialize(SavedProperties.From(card),jsonOptions);
        var restored=Misato();JsonSerializer.Deserialize<SavedProperties>(json,jsonOptions)!.Fill(restored);Native(restored,"AfterDeserialized");
        Equal(MisatoGrowth(restored),999m,"native SavedProperties JSON retains permanent growth after reload");Equal(restored.DynamicVars.Damage.BaseValue,62.34m,"deserialization materializes saved growth into the base coefficient once");
    }
    public static void RunMisato(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".misato57");
            foreach(Type type in new[]{typeof(TengenBrilliantMoveHbrgoSaki),typeof(HbrAttackUpPower),typeof(HbrDefenseDownPower),typeof(HbrComboLargePower),typeof(HotaruDamageAmplificationPower),typeof(MinionPower),typeof(SmallHopper),typeof(TestSubject),typeof(AdaptablePower)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string name in new[]{"TengenBrilliantMoveDeathPatch","HbrEffectApplicationPatch","HbrFragileApplyPatch","HbrFragileStackingPatch","HbrComboVirtualHitPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs production "+name);
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch",true)!).Patch().Count>0,"installs production skill-description parameters");
            foreach(Type listeners in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(listeners.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoListeners),F)!){priority=Priority.First+100});
            h.Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoAttackLeaf),F)!));
            h.Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.GainEnergy),[typeof(decimal),typeof(Player)])!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoEnergyLeaf),F)!));
            h.Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            foreach(var method in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            MisatoCardCases().GetAwaiter().GetResult();MisatoPipelineCases(h).GetAwaiter().GetResult();MisatoNativeDeathCases(h).GetAwaiter().GetResult();MisatoGrowthCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real card upgrade and raw coefficient ledgers, native attack/buff/combo hooks, shared buff application, global native AfterDeath dispatch and captured eligibility, physical/OD/copy growth, native saved-property JSON. Attack presentation/terminal energy writes and dead HP fixture inputs are controlled; extra-turn membership uses real coordinator sessions. No Godot, package, Git or upload."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Misato checks");
    }
}
