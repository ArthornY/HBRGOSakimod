using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

static class SatomiStandalone {public static void Run(string path)=>EffectChecks.RunSatomi(path);}
static partial class EffectChecks
{
    sealed record SatomiHit(decimal Raw,int Hits,decimal Destruction);
    static readonly List<SatomiHit> SatomiHits=[];
    static readonly List<KuraRiceBallHbrgoSaki> SatomiRice=[];
    static int SatomiKills;
    static bool SatomiBreak,SatomiBlock;
    static void SatomiDeathObserve(Creature target,object? __result)
    {
        Type type=Helper("KuraSatomiCombatHelper");object? current=AsyncValue("KuraSatomiCombatHelper","Current");
        object? command=current?.GetType().GetField("Command",F)!.GetValue(current);
        object identity=Call("HighHitDamageVisualHelper","GetCurrentAttackIdentity")!;
        Cases.Add(new{deathTarget=target.Monster?.GetType().Name,captured=__result!=null,current=current!=null,
            sameCommand=ReferenceEquals(command,identity.GetType().GetField("Item1")!.GetValue(identity)),hp=target.CurrentHp,
            exactHp=(decimal)Call("LargeCreatureHealthHelper","GetCurrentHp",target)!});
    }
    static void SatomiCompleteObserve(object receipt)
    {object kills=receipt.GetType().GetField("Kills",F)!.GetValue(receipt)!;Cases.Add(new{completeKills=(int)kills.GetType().GetProperty("Count")!.GetValue(kills)!,heroAlive=Hero.IsAlive,allies=Combat.Allies.Count()});}
    static bool SatomiAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=SatomiAttack(__instance);return false;}
    static async Task<AttackCommand> SatomiAttack(AttackCommand command)
    {
        var source=(CardModel)command.ModelSource!;await (Task)MisatoHook("BeforeAttack",command)!;
        int hits=(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!;decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!;
        SatomiHits.Add(new(raw,hits,(decimal)Call("KuraSatomiCombatHelper","GetDestructionGainMultiplier",source,Enemy,Hero,ValueProp.Move)!));
        var results=(List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!;
        for(int i=0;i<hits;i++)
        {
            var shell=SatomiBreak?Enemy.GetPower<HighDimensionalShellPower>():null;decimal damage=Actual(Enemy,Hero,raw,ValueProp.Move,source);
            if(shell?.TryAbsorb(damage)==true)damage=0;
            var result=new DamageResult(Enemy,ValueProp.Move){UnblockedDamage=(int)Math.Min(int.MaxValue,damage)};results.Add(new(){result});
            if(shell!=null)await shell.AfterDamageReceived(new ThrowingPlayerChoiceContext(),Enemy,result,ValueProp.Move,Hero,source);
        }
        var victims=Enumerable.Range(0,SatomiKills).Select(i=>i==0?Enemy:MisatoEnemy()).ToArray();
        if(victims.Length>0)MisatoEnemy(); // keep the actual battle open for post-kill buffs/return
        for(int i=0;i<victims.Length;i++)
        {if(i>0)Power<MinionPower>(victims[i],1);await CreatureCmd.Kill(victims[i]);}
        await (Task)MisatoHook("AfterAttack",command)!;return command;
    }
    // The ordinary card commands are retained at their exact native entry.
    // Only unavailable hand-layout/history leaves are replaced with real pile
    // mutations. Production delegates its overflow policy to these commands.
    static bool SatomiGenerated(CardModel card,PileType newPileType,ref Task<CardPileAddResult> __result)
    {
        Check(card is KuraRiceBallHbrgoSaki&&newPileType==PileType.Hand,"harvest uses native generated-card-to-hand command");
        SatomiRice.Add((KuraRiceBallHbrgoSaki)card);var hand=card.Owner.PlayerCombatState!.Hand;
        CardPile pile=hand.Cards.Count>=10?card.Owner.PlayerCombatState.DiscardPile:hand;
        card.Pile?.RemoveInternal(card,true);pile.AddInternal(card);
        __result=Task.FromResult(new CardPileAddResult{success=true,cardAdded=card});return false;
    }
    static bool SatomiReturn(CardModel card,PileType newPileType,ref Task<CardPileAddResult> __result)
    {
        if(card is not HarvestBountifulGrainsHbrgoSaki)return true;
        card.Pile?.RemoveInternal(card,true);newPileType.GetPile(card.Owner).AddInternal(card);
        __result=Task.FromResult(new CardPileAddResult{success=true,cardAdded=card});return false;
    }
    static void SatomiAppend(ValueProp props,ref Task<IEnumerable<DamageResult>> __result)=>__result=SatomiAppendAwait(__result,props);
    static async Task<IEnumerable<DamageResult>> SatomiAppendAwait(Task<IEnumerable<DamageResult>> native,ValueProp props)
    {
        var rows=(await native).ToList();
        // The general effect host controls only receiver/HP at its terminal.
        // Restore the command's real provenance before the production append.
        foreach(var row in rows)Set(row,"<Props>k__BackingField",props);
        if(SatomiBlock)foreach(var row in rows){row.BlockedDamage=(int)Math.Max(1,row.UnblockedDamage);Set(row,"<UnblockedDamage>k__BackingField",0);}
        var destination=new List<DamageResult>();Call("ItsMygoIncomingDamageCapture","AppendResults",destination,rows);return destination;
    }
    static void SatomiReset()
    {
        NewReset();SatomiHits.Clear();SatomiRice.Clear();SatomiKills=0;SatomiBreak=SatomiBlock=false;
        Set(RunState,"_players",new List<Player>{Player});
        SatomiFreshTarget();
        foreach(var item in new[]{("KuraSatomiCombatHelper","Battles"),("ThirtyOneBEnemyActionHelper","ParryBattles")})
        {object table=Helper(item.Item1).GetField(item.Item2,F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);}
        CombatInProgressWithTurn();
    }
    static void SatomiFreshTarget()
    {
        Creature fresh=MisatoEnemy();typeof(Checks).GetField("Enemy",F)!.SetValue(null,fresh);
        Set(Combat,"_enemies",new List<Creature>{fresh});
    }
    static async Task SatomiMove(params decimal[] amounts)
        =>await SatomiMoveAgainst(Hero,amounts);
    static async Task SatomiMoveAgainst(Creature receiver,params decimal[] amounts)
    {
        Set(Enemy.Monster!,"<Id>k__BackingField",ModelDb.GetId(Enemy.Monster!.GetType()));
        Set(Enemy.Monster!,"<NextMove>k__BackingField",new MoveState("SATOMI_REAL_MOVE",async _=>
        {foreach(decimal amount in amounts)await Command(Enemy,amount,ValueProp.Move,null,[receiver]);}));
        await Enemy.Monster!.PerformMove();
    }
    static void SatomiToPlay(CardModel card)
    {card.Pile?.RemoveInternal(card,true);Player.PlayerCombatState!.PlayPile.AddInternal(card);}
    static async Task SatomiCases()
    {
        SatomiReset();var card=NewGarden<HarvestBountifulGrainsHbrgoSaki>();await NewPlay(card);var hit=SatomiHits.Single();Equal(hit.Raw,2.16m,"base36percent raw");Equal(hit.Hits,4,"base4hits");Equal(hit.Destruction,5m/3m,"base destruction5normalized");Check(SatomiRice.Count==0,"ordinary hit without break or defeat creates no rice");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>();await PowerCmd.Apply<HighDimensionalShellPower>(new ThrowingPlayerChoiceContext(),Enemy,1m,Enemy,null,true);SatomiBreak=true;await NewPlay(card);
        Check(Enemy.HasPower<BreakPower>()&&SatomiRice.Count==1,"real shell absorption and AfterDamageReceived break generates exactly one rice");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(0,1);Power<BreakPower>(Enemy,1);await NewPlay(card);hit=SatomiHits.Single();Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"Flash native cost0");Equal(hit.Raw,7.98m,"alreadyBroken Festival133percent");Equal(hit.Hits,6,"Festival6hits");Equal(hit.Destruction,7m,"Festival destruction21");Check(Hero.HasPower<HbrDefenseDownPower>()&&Hero.GetPower<HbrDefenseDownPower>()!.Amount==1,"Festival applies its own one-enemy-end defense-down after attack");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(1);await PowerCmd.Apply<HighDimensionalShellPower>(new ThrowingPlayerChoiceContext(),Enemy,1m,Enemy,null,true);SatomiBreak=true;await NewPlay(card);Equal(SatomiHits.Single().Raw,2.16m,"breaking DP during play never retroactively activates Festival");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(2,3,5);SatomiToPlay(card);await PowerCmd.Apply<HighDimensionalShellPower>(new ThrowingPlayerChoiceContext(),Enemy,1m,Enemy,null,true);SatomiBreak=true;SatomiKills=2;await NewPlay(card);
        Check(SatomiRice.Count==1&&SatomiRice[0].ForWholeTeam,"same play breaking and killing two enemies still creates one Tropical rice");Equal(Hero.GetPowerAmount<HbrMoralePower>(),2,"Vanguard counts the actual second minion final kill as well");Check(card.Pile==Player.PlayerCombatState!.Hand,"DivineCatch really invokes return-to-hand after a kill");
        SatomiToPlay(card);SatomiFreshTarget();SatomiKills=1;SatomiBreak=false;await NewPlay(card);Check(card.Pile==Player.PlayerCombatState.PlayPile,"same ordinary turn cannot catch a second time");
        Set(Player.PlayerCombatState,"<TurnNumber>k__BackingField",2);SatomiFreshTarget();await NewPlay(card);Check(card.Pile==Player.PlayerCombatState.Hand,"new native own turn renews one catch");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(4,5);MisatoPermanent(card);await (Task)Call("KuraSatomiCombatHelper","OnCombatStart",Combat)!;await (Task)Call("KuraSatomiCombatHelper","OnCombatStart",Combat)!;Check(SatomiRice.Count==1&&SatomiRice[0].ForWholeTeam,"undrawn permanent LovingRice gives one Tropical rice despite repeated battle-start entry");
        var rice=SatomiRice.Single();Check(rice.MaxUpgradeLevel==0&&rice.Type==CardType.Skill&&rice.Keywords.Contains(CardKeyword.Exhaust)&&rice.EnergyCost.GetWithModifiers(CostModifiers.Local)==0,"derived rice is native0cost nonupgradable exhaust skill");Check((object)rice is not ThirtyOneBGardenCard&&(object)rice is not HBRGOSakiMod.Core.Models.Capabilities.IThirtyGMemberCard,"rice is neither Garden nor a30G member");
        Player peer=SupportTeammate();Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});await NewPlay(rice);Check(Hero.GetPowerAmount<HbrMoralePower>()==1&&peer.Creature.GetPowerAmount<HbrMoralePower>()==1,"Tropical rice grants both actual player recipients one morale");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>();await (Task)Call("KuraSatomiCombatHelper","GenerateRiceBall",card)!;rice=SatomiRice.Single();Check(rice.TargetType==TargetType.Self,"ordinary rice in solo selects only its owner");await NewPlay(rice);Equal(Hero.GetPowerAmount<HbrMoralePower>(),1,"solo rice grants exactly one structural morale level");
        peer=SupportTeammate();Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});Check(rice.TargetType==TargetType.AnyAlly,"ordinary rice in multiplayer exposes ally selection");Power<HbrMoralePower>(peer.Creature,10);var selected=Play(rice);Set(selected,"<Target>k__BackingField",peer.Creature);await (Task)Native(rice,"OnPlay",new ThrowingPlayerChoiceContext(),selected)!;Equal(peer.Creature.GetPowerAmount<HbrMoralePower>(),10,"selected teammate's morale respects its actual level10cap");Equal(Hero.GetPowerAmount<HbrMoralePower>(),1,"selecting teammate never grants the owner an extra level");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(3);SatomiKills=1;
        ulong? oldLocal=LocalContext.NetId;
        try{LocalContext.NetId=Player.NetId;await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(),Enemy,true,default(ResourceInfo),true);}
        finally{LocalContext.NetId=oldLocal;}
        Check(card.Pile==Player.PlayerCombatState!.Hand&&!Player.PlayerCombatState.DiscardPile.Cards.Contains(card),"real native OnPlayWrapper final cleanup preserves DivineCatch in hand");
        await SatomiParryCases();
    }
    static async Task SatomiParryCases()
    {
        SatomiReset();var card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(6);MisatoPermanent(card);
        decimal expected=Preview(Hero,Enemy,8m,ValueProp.Move,null);int start=Hits.Count;await SatomiMove(8m,8m,8m);var first=Hits.Skip(start).ToArray();
        Check(first.Length==3&&first.All(hit=>hit.Final==expected),"real enemy PerformMove gives every command in first attack the same half multiplier");
        start=Hits.Count;await SatomiMove(8m);Check(Hits[start].Final==decimal.Floor(expected*2m)||Hits[start].Final==decimal.Floor(expected*2m)+1,"subsequent real enemy move loses the once-battle half protection");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(6);MisatoPermanent(card);await SatomiMove(0m);expected=Preview(Hero,Enemy,8m,ValueProp.Move,null);start=Hits.Count;await SatomiMove(8m);Equal(Hits[start].Final,expected,"zero damage move never consumes the first connected attack protection");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(6);MisatoPermanent(card);SatomiBlock=true;await SatomiMove(8m);SatomiBlock=false;expected=Preview(Hero,Enemy,8m,ValueProp.Move,null);start=Hits.Count;await SatomiMove(8m);Equal(Hits[start].Final,expected,"a wholly blocked physical result still commits the first attack protection");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(6);MisatoPermanent(card);var peer=SupportTeammate();Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});
        var peerCard=NewGarden<HarvestBountifulGrainsHbrgoSaki>(6);peerCard.Pile!.RemoveInternal(peerCard,true);Set(peerCard,"_owner",peer);peer.Deck.AddInternal(peerCard);
        await SatomiMove(8m);expected=Preview(peer.Creature,Enemy,8m,ValueProp.Move,null);start=Hits.Count;await SatomiMoveAgainst(peer.Creature,8m,8m);Check(Hits.Skip(start).All(hit=>hit.Final==expected),"teammate retains its own entire first-action half protection after owner's quota was consumed");
        start=Hits.Count;await SatomiMoveAgainst(peer.Creature,8m);Check(Hits[start].Final>=expected*2m-1m,"teammate's subsequent attack uses its independently consumed quota");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(6);MisatoPermanent(card);Check((bool)Call("KuraSatomiCombatHelper","HasParry",Player)!,"Parry cache initially enables permanent upgrade");
        var relic=(ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().ToMutable();Set(relic,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(relic).ToList());Call("ThirtyGProposalState","RestoreIdentity",card,"satomi-parry");
        string State(bool released)=>JsonSerializer.Serialize(new{InBattle=true,Cards=new Dictionary<string,object>{{"satomi-parry",new{GraduatedRemaining=1,Released=released}}}});
        Native(relic,"SetProposalData",State(false));Call("ThirtyGProposalState","Notify",card);Check(!(bool)Call("KuraSatomiCombatHelper","HasParry",Player)!,"graduation Changed disables the cached Parry immediately");await SatomiMove(8m);
        Native(relic,"SetProposalData",State(true));Call("ThirtyGProposalState","Notify",card);Check((bool)Call("KuraSatomiCombatHelper","HasParry",Player)!,"release Changed restores Parry within the same physical deck");expected=Preview(Hero,Enemy,8m,ValueProp.Move,null);start=Hits.Count;await SatomiMove(8m);Equal(Hits[start].Final,expected,"disabled attack never consumed the later restored first-action quota");
        SatomiReset();card=NewGarden<HarvestBountifulGrainsHbrgoSaki>(6);MisatoPermanent(card);Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());
        var main=(Creature)H("Actor",Blank<HBRGOSakiMod.Core.Models.Monsters.SmallHopper>(),null,Player)!;Power<PhantomPower>(main,1);
        var pet=(Creature)H("Actor",Blank<HBRGOSakiMod.Core.Models.Monsters.SmallHopper>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,main,pet});
        expected=Preview(pet,Enemy,8m,ValueProp.Move,null);start=Hits.Count;await SatomiMoveAgainst(pet,8m);Equal(Hits[start].Final,expected,"ordinary owned pet gets no30A-holder Parry");
        expected=Preview(main,Enemy,8m,ValueProp.Move,null);start=Hits.Count;await SatomiMoveAgainst(main,8m,8m);Check(Hits.Skip(start).All(hit=>hit.Final==expected),"actual30A main phantom retains the holder's unspent whole-action protection");
        start=Hits.Count;await SatomiMoveAgainst(main,8m);Check(Hits[start].Final>=expected*2m-1m,"main phantom's actual connected hit commits only its owner's first-action quota");
    }
    public static void RunSatomi(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".satomi76");
            foreach(Type type in new[]{typeof(HarvestBountifulGrainsHbrgoSaki),typeof(KuraRiceBallHbrgoSaki),typeof(HbrMoralePower),typeof(HbrDefenseDownPower),typeof(HighDimensionalShellPower),typeof(DestructionRatePower),typeof(BreakPower),typeof(MinionPower),typeof(ThirtyGCollectionRelic),typeof(PhantomPower),typeof(HBRGOSakiMod.Core.Models.Monsters.SmallHopper)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string patch in new[]{"HbrEffectApplicationPatch","HighHitDamageAttackScopePatch","TengenBrilliantMoveDeathPatch","ThirtyOneBEnemyMoveScopePatch","ThirtyOneBEnemyActualTargetPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+patch,true)!).Patch().Count>0,"installs "+patch);
            h.Patch(Helper("KuraSatomiCombatHelper").GetMethod("CaptureDeath",F)!,postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(SatomiDeathObserve),F)!));
            h.Patch(Helper("KuraSatomiCombatHelper").GetMethod("CompleteAttack",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(SatomiCompleteObserve),F)!));
            void Patch(MethodBase target,string name,int priority=Priority.First+100)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(name,F)!){priority=priority});
            foreach(Type t in new[]{RunState.GetType(),typeof(CombatState)})Patch(t.GetMethod("IterateHookListeners",F)!,nameof(MisatoListeners));
            Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,nameof(SatomiAttackLeaf),Priority.Last);
            Patch(typeof(CardPileCmd).GetMethods(F).Single(m=>m.Name=="AddGeneratedCardToCombat"),nameof(SatomiGenerated));
            Patch(typeof(CardPileCmd).GetMethods(F).Single(m=>m.Name=="Add"&&m.GetParameters()[0].ParameterType==typeof(CardModel)&&m.GetParameters()[1].ParameterType==typeof(PileType)),nameof(SatomiReturn));
            h.Patch(Canonical,postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(SatomiAppend),F)!));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,nameof(FormPowerIconLeaf));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec",F)!,nameof(CrackClock));
            foreach(var wait in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))Patch(wait,nameof(VisualTask));
            SatomiCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual76card upgrades/HighHit command identity, real shell TryAbsorb+AfterDamageReceived break, native CreatureCmd.Kill/global death, perplay receipt, morale; real enemy Monster.PerformMove scopes with production AppendResults connected-hit commit. Terminal damage, pile layout/history leaves controlled with actual pile mutations. Full live rendering/transport not exercised."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Satomi checks");
    }
}
