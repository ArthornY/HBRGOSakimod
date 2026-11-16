using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Networking.ManagedActions;

static class MachinaStandalone { public static void Run(string path)=>EffectChecks.RunMachina(path); }
public abstract class MachinaMemberFixture : CardModel,IThirtyOneCMemberCard
{
    protected MachinaMemberFixture():base(0,CardType.Skill,CardRarity.Token,TargetType.Self){}
    public abstract ThirtyOneCMember ThirtyOneCMember {get;}
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>Task.CompletedTask;
}
public sealed class MachinaYamawakiFixture:MachinaMemberFixture {public override ThirtyOneCMember ThirtyOneCMember=>ThirtyOneCMember.YamawakiVonIvar;}
public sealed class MachinaBungoFixture:MachinaMemberFixture {public override ThirtyOneCMember ThirtyOneCMember=>ThirtyOneCMember.BungoYayoi;}
public sealed class MachinaAdelheidFixture:MachinaMemberFixture {public override ThirtyOneCMember ThirtyOneCMember=>ThirtyOneCMember.KanzakiAdelheid;}
public sealed class MachinaAmaneFixture:MachinaMemberFixture {public override ThirtyOneCMember ThirtyOneCMember=>ThirtyOneCMember.AmaneMiko;}
public sealed class MachinaSeiraFixture:MachinaMemberFixture {public override ThirtyOneCMember ThirtyOneCMember=>ThirtyOneCMember.SakurabaSeira;}
public sealed class MachinaMariFixture:MachinaMemberFixture {public override ThirtyOneCMember ThirtyOneCMember=>ThirtyOneCMember.SatsukiMari;}
public sealed class ProbedMachinaMemberFixture:MachinaMemberFixture
{
    public static int Reads;
    public override ThirtyOneCMember ThirtyOneCMember{get{Reads++;return ThirtyOneCMember.YamawakiVonIvar;}}
}
public sealed class MachinaHookProbePower:PowerModel
{
    public static int BeforeAttacks,AfterAttacks,PlayedCards;
    public static AttackCommand? LastAttack;
    public override PowerType Type=>PowerType.Buff;
    public override PowerStackType StackType=>PowerStackType.Counter;
    public override Task BeforeAttack(AttackCommand command){BeforeAttacks++;LastAttack=command;return Task.CompletedTask;}
    public override Task AfterAttack(PlayerChoiceContext context,AttackCommand command){AfterAttacks++;return Task.CompletedTask;}
    public override Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play){PlayedCards++;return Task.CompletedTask;}
}
static partial class EffectChecks
{
    static readonly Type[] MachinaMembers=[typeof(MachinaYamawakiFixture),typeof(MachinaBungoFixture),typeof(MachinaAdelheidFixture),typeof(MachinaAmaneFixture),typeof(MachinaSeiraFixture),typeof(MachinaMariFixture)];
    static readonly List<object> MachinaEvidence=[];
    static bool MachinaDestruction,MachinaNested;
    static string? MachinaFailure;
    static int MachinaDepth;
    static bool MachinaFalse(ref bool __result){__result=false;return false;}
    static void MachinaDeck(Player owner,int count)
    {
        foreach(Type type in MachinaMembers.Take(count))
        {var card=(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();Set(card,"_owner",owner);owner.Deck.AddInternal(card);}
    }
    static ThirtyOneCCollection MachinaReset(int count=2)
    {
        BondReset();MachinaEvidence.Clear();MachinaDestruction=MachinaNested=false;MachinaDepth=0;MachinaFailure=null;Bond50Leaf=false;
        MachinaHookProbePower.BeforeAttacks=MachinaHookProbePower.AfterAttacks=MachinaHookProbePower.PlayedCards=0;MachinaHookProbePower.LastAttack=null;
        Set(Player.PlayerCombatState!,"_phase",PlayerTurnPhase.Play);Set(Player.PlayerCombatState!,"_energy",3);
        MachinaDeck(Player,count);var relic=BondRelic<ThirtyOneCCollection>();Power<MachinaHookProbePower>(Hero,1);return relic;
    }
    static Task<bool> MachinaPress(Player owner,int receipt=0)=>(Task<bool>)Call("ThirtyOneCMachinaHelper","TryActivate",new ThrowingPlayerChoiceContext(),owner,receipt)!;
    static bool MachinaCan(Player owner)=>(bool)Call("ThirtyOneCMachinaHelper","CanActivate",owner)!;
    static decimal MachinaE(Creature target)=>(decimal)Call("HbrEnemyProfileHelper","GetEnemyParameter",target)!;
    static bool MachinaTerminal(object[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {
        Type api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!;
        var targets=(IEnumerable<Creature>)Invoke(api.GetMethod("ReadTargets",F)!,null,new object?[]{__args})!;
        __result=MachinaBody(targets,(decimal)__args[2],(ValueProp)__args[3],__args[4]as Creature,__args[5]as CardModel);return false;
    }
    static async Task<IEnumerable<DamageResult>> MachinaBody(IEnumerable<Creature> targets,decimal raw,ValueProp props,Creature? dealer,CardModel? source)
    {
        int depth=MachinaDepth++;var results=new List<DamageResult>();
        try
        {
            Creature[] snapshot=targets.ToArray();await Task.Yield();
            if(MachinaFailure=="fault")throw new InvalidOperationException("controlled-machina-fault");
            if(MachinaFailure=="cancel")throw new OperationCanceledException("controlled-machina-cancel");
            if(MachinaNested&&depth==0)await Command(dealer,raw,props,source,snapshot);
            foreach(Creature target in snapshot)
            {
                bool forced=(bool)Call("ThirtyOneCMachinaHelper","ForcesCritical",dealer,source,props)!;
                decimal scaled=(decimal)Call("ThirtyOneCMachinaHelper","ScaleSkillUnits",dealer,source,props,raw)!;
                decimal destruction=(decimal)Call("ThirtyOneCMachinaHelper","DestructionMultiplier",dealer,source,props)!;
                HighDimensionalShellPower? shell=Bond50Leaf?target.GetPower<HighDimensionalShellPower>():null;
                decimal final=Actual(target,dealer,raw,props,source);
                if(shell?.TryAbsorb(final)==true)final=0m;
                Hits.Add(new(target,dealer,source,raw,props,final,false,null,depth,false));
                MachinaEvidence.Add(new{target=ReferenceEquals(target,Enemy)?"enemy":"peer-target",raw,scaled,forced,destruction,depth,final});
                var result=new DamageResult(target,props){UnblockedDamage=(int)Math.Min(int.MaxValue,Math.Max(0,final))};results.Add(result);
                if(shell!=null)await shell.AfterDamageReceived(new ThrowingPlayerChoiceContext(),target,result,props,dealer!,source!);
                if(MachinaDestruction&&target.GetPower<DestructionRatePower>()is{} rate)
                    await rate.AfterDamageReceived(new ThrowingPlayerChoiceContext(),target,result,props,dealer,source);
            }
            return results;
        }
        finally{MachinaDepth--;}
    }
    static int MachinaCount(Player owner)=>(int)Call("ThirtyOneCMachinaHelper","ServantCount",owner)!;
    static void MachinaCountCacheCases()
    {
        MachinaReset(2);var probe=(ProbedMachinaMemberFixture)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(ProbedMachinaMemberFixture))).ToMutable();Set(probe,"_owner",Player);Player.Deck.AddInternal(probe);
        Equal(MachinaCount(Player),2,"31C cache observes a native permanent-pile addition without inventing another duplicate member");
        int reads=ProbedMachinaMemberFixture.Reads;bool unchanged=true;for(int index=0;index<100;index++)unchanged&=MachinaCount(Player)==2;Check(unchanged,"100 cached31C count reads retain the exact same membership");
        Equal(ProbedMachinaMemberFixture.Reads,reads,"100 unchanged membership reads never rescan per-card member getters");
        CardModel bungo=Player.Deck.Cards.Single(card=>card is MachinaBungoFixture);int count=Player.Deck.Cards.Count;
        Player.Deck.RemoveInternal(bungo);MachinaDeck(Player,1);
        Check(Player.Deck.Cards.Count==count&&MachinaCount(Player)==1,"31C native ContentsChanged invalidates even a same-count replacement of Bungo with a duplicate Yamawaki");
        var adelheid=(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(MachinaAdelheidFixture))).ToMutable();Set(adelheid,"_owner",Player);Player.Deck.AddInternal(adelheid);
        Equal(MachinaCount(Player),2,"31C cache immediately recognizes a distinct new permanent member");Player.Deck.RemoveInternal(adelheid);
        Equal(MachinaCount(Player),1,"31C cache immediately drops a natively removed permanent member");
        var deck=Player.Deck;Player peer=MachinaPeer(0);var empty=peer.Deck;Set(peer,"<Deck>k__BackingField",deck);
        Equal(MachinaCount(peer),0,"a shared inventory viewed by another owner never borrows the first owner's member count");
        Equal(MachinaCount(Player),1,"switching back to the real owner recomputes that same pile's valid ownership");
        Set(Player,"<Deck>k__BackingField",empty);Equal(MachinaCount(Player),0,"replacing the player's entire inventory selects a fresh independent count cache");
        Set(Player,"<Deck>k__BackingField",deck);Equal(MachinaCount(Player),1,"restoring the original inventory returns its exact native membership");
    }
    static async Task MachinaBaseCases()
    {
        foreach(int count in new[]{0,1,2,4,6})
        {
            var relic=MachinaReset(count);
            Equal((int)Call("ThirtyOneCMachinaHelper","ServantCount",Player)!,count,"31C counts distinct explicit permanent member kinds");
            Check(!(bool)Call("ThirtyOneCMachinaHelper","IsVisible",Player)!,"the button does not show before its combat-start ledger is prepared");
            await relic.BeforeCombatStart();Equal((int)Call("ThirtyOneCMachinaHelper","RemainingUses",Player)!,count>=4?2:count>=2?1:0,"the combat ledger has the specified tier use budget");
            if(count<2){Check(!MachinaCan(Player)&&!await MachinaPress(Player),"below two servants cannot dispatch or spend the battle skill");continue;}
            Check(MachinaCan(Player),"a living owner in its own play phase can activate the unlocked button");
            int energy=Player.PlayerCombatState!.Energy,rng=RngCount(RunState.Rng.Niche),deck=Player.Deck.Cards.Count;
            Check(await MachinaPress(Player),"the authoritative activation executes successfully");
            Check(Hits.Count==6&&Hits.All(hit=>hit.Card==null&&hit.Dealer==Hero),"one button press produces six native actor attacks with no synthetic card source");
            decimal smallUnits=count*.6m;
            for(int index=0;index<6;index++)
            {
                decimal units=smallUnits*(index==5?5m:1m);
                decimal expected=decimal.Floor((decimal)Call("HbrSkillPower","EvaluateDamageFromUnits",units,100m,150m,true)!*1.5m);
                Equal(Hits[index].Final,expected,"segment uses the shared ordinary forced-native-critical attribute formula at index"+index);
            }
            Equal(RngCount(RunState.Rng.Niche),rng,"guaranteed native critical hits do not consume random critical samples or SoulRoll dice");
            Equal(Player.PlayerCombatState.Energy,energy,"the button does not spend or grant energy");Equal(Player.Deck.Cards.Count,deck,"the button neither creates nor plays a card");
            Check(MachinaHookProbePower.BeforeAttacks==1&&MachinaHookProbePower.AfterAttacks==1&&MachinaHookProbePower.PlayedCards==0&&MachinaHookProbePower.LastAttack?.ModelSource==relic,"one real BeforeAttack/AfterAttack pair owns all weighted segments and no card-play hook runs");
            Equal(relic.UsesSpent,1,"the activation consumes its owner receipt exactly once");
            Check(!await MachinaPress(Player,0),"a duplicate queued expected-use receipt cannot activate a second time");
            Equal(relic.UsesSpent,1,"duplicate activation leaves the existing ledger intact");
            if(count==2)Check(!MachinaCan(Player),"the two-member battle budget is exhausted after one use");
            else{Check(await MachinaPress(Player,1),"the four/six-member budget admits its distinct second receipt");Check(!MachinaCan(Player)&&!await MachinaPress(Player,2),"the third press remains unavailable at four/six members");}
            await relic.AfterCombatEnd(null!);Check(!(bool)Call("ThirtyOneCMachinaHelper","IsVisible",Player)!,"combat end hides the button and its finished ledger");
            await relic.BeforeCombatStart();Equal(relic.UsesSpent,0,"the next combat resets only this relic's own spent-use count");
        }
        var duplicate=MachinaReset(2);MachinaDeck(Player,1);Equal((int)Call("ThirtyOneCMachinaHelper","ServantCount",Player)!,2,"another physical copy of one member never adds a servant kind");
        Check(!(bool)Invoke(Mod.GetType("HBRGOSakiMod.Core.Models.Capabilities.ThirtyOneCKeywordRules",true)!.GetMethod("IsThirtyOneCCard",F)!,null,FormCard<MatchaParfaitHbrgoSaki>())!,"an existing non31C garden card is never implicitly assigned to the new squad");
        await duplicate.BeforeCombatStart();Set(Player.PlayerCombatState!,"_phase",PlayerTurnPhase.Start);
        Check(!MachinaCan(Player)&&!await MachinaPress(Player),"execution revalidates the owner's own play phase after a request was queued");
        Equal(duplicate.UsesSpent,0,"an invalid phase does not consume a receipt");
        var all=MachinaReset(4);await all.BeforeCombatStart();
        Creature second=(Creature)H("Actor",Blank<HBRGOSakiMod.Core.Models.Monsters.SmallHopper>(),null,null)!;
        Set(Combat,"_enemies",new List<Creature>{Enemy,second});
        await MachinaPress(Player);
        Check(Hits.Count==12&&Hits.Count(hit=>hit.Target==Enemy)==6&&Hits.Count(hit=>hit.Target==second)==6,"one real all-opponent command applies every weighted segment to both native enemy targets");
        Check(Enemy.GetPower<FearOfDevilPower>()?.Amount==1&&second.GetPower<FearOfDevilPower>()?.Amount==1,"the post-attack Fear applies to every surviving opponent");
    }
    static Task MachinaEffect<T>(decimal basis)where T:HbrEffectPower=>(Task)Invoke(Helper("HbrEffectApplication").GetMethods(F).Single(method=>method.Name=="Apply"&&method.IsGenericMethodDefinition).MakeGenericMethod(typeof(T)),null,new ThrowingPlayerChoiceContext(),Hero,basis,Hero,null,null,null,false,null)!;
    static async Task MachinaModifierCases()
    {
        var relic=MachinaReset(2);await relic.BeforeCombatStart();
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),Hero,10m,Hero,null,true);
        await MachinaEffect<HbrAttackUpPower>(30m);var attackUp=Hero.GetPower<HbrAttackUpPower>()!;
        await MachinaEffect<HbrCriticalDamageUpPower>(50m);
        await MachinaPress(Player);
        decimal skill=(decimal)Call("HbrSkillPower","EvaluateDamageFromUnits",1.2m,120m,150m,true)!;
        Equal(Hits[0].Final,decimal.Floor(skill*2m*1.3m),"native guaranteed critical receives original critical-damage buffs, the ordinary attack-buff bucket and real Strength-scaled STR/DEX");
        Check(attackUp.Amount==0&&!Hero.Powers.Contains(attackUp),"one counted attack buff survives all six segments then consumes exactly once");
        relic=MachinaReset(2);await relic.BeforeCombatStart();await PowerCmd.Apply<HbrComboLargePower>(new ThrowingPlayerChoiceContext(),Hero,1m,Hero,null,true);var combo=Hero.GetPower<HbrComboLargePower>()!;
        await MachinaPress(Player);Equal(Hits.Count,12,"the ordinary large-combo hook doubles the whole six-segment native attack");
        for(int index=0;index<6;index++)Equal(Hits[index+6].Final,Hits[index].Final,"large combo repeats the complete one-one-one-one-one-five distribution");
        Check(combo.Amount==0&&!Hero.Powers.Contains(combo),"large combo pays one attack use rather than one use per weighted segment");
    }
    static async Task MachinaDestructionCases()
    {
        var relic=MachinaReset(2);await relic.BeforeCombatStart();Power<BreakPower>(Enemy,3);var rate=Power<DestructionRatePower>(Enemy,DestructionRatePower.BaselineAmount);MachinaDestruction=true;
        await MachinaPress(Player);Equal(rate.Amount-DestructionRatePower.BaselineAmount,12,"all six HP hits share one original destruction coefficient6 budget: enemy coefficient2 times6, never default3 times6");
        Equal((decimal)Call("ThirtyOneCMachinaHelper","DestructionMultiplier",Hero,null,ValueProp.Move)!,1m,"leaving the native attack closes its destruction multiplier scope");
        relic=MachinaReset(2);await relic.BeforeCombatStart();MachinaNested=true;await MachinaPress(Player);
        Check(Hits.Count==12&&Hits.Count(hit=>hit.Depth==1)==6,"controlled nested native commands resolve inside the outer attack without being promoted to its segments");
        decimal ordinary=decimal.Floor((decimal)Call("HbrSkillPower","EvaluateDamageFromUnits",1.2m,100m,150m,false)!);
        Check(Hits.Where(hit=>hit.Depth==1).All(hit=>hit.Final==ordinary),"nested reactions inherit neither the forced native critical nor the final segment's fivefold authored units");
        Check(MachinaEvidence.Where(row=>(int)row.GetType().GetProperty("depth")!.GetValue(row)! ==1).All(row=>(decimal)row.GetType().GetProperty("destruction")!.GetValue(row)! ==1m),"nested reaction commands never inherit the original destruction coefficient6 scope");
    }
    static Task MachinaFear(Creature target,bool permanent,Creature? source=null)=>(Task)Invoke(typeof(FearOfDevilPower).GetMethod("Apply",F)!,null,new ThrowingPlayerChoiceContext(),target,source??Hero,permanent)!;
    static async Task MachinaFearCases()
    {
        MachinaReset();var context=new ThrowingPlayerChoiceContext();
        await MachinaFear(Enemy,false);Equal(MachinaE(Enemy),120m,"a temporary Fear lowers enemy E by exactly30 independently of damage or attributes");
        await MachinaFear(Enemy,false);Check(Enemy.Powers.OfType<FearOfDevilPower>().Count()==1&&Enemy.GetPower<FearOfDevilPower>()!.Amount==1,"reapplying temporary Fear refreshes one visible layer instead of stacking");
        await Enemy.GetPower<FearOfDevilPower>()!.AfterSideTurnEnd(context,CombatSide.Player,new[]{Hero});Equal(MachinaE(Enemy),120m,"Fear does not expire on the player's side end");
        await Enemy.GetPower<FearOfDevilPower>()!.AfterSideTurnEnd(context,CombatSide.Enemy,new[]{Enemy});Equal(MachinaE(Enemy),150m,"temporary Fear expires at the first actual enemy side end");
        await MachinaFear(Enemy,true);await MachinaFear(Enemy,true);
        var fear=Enemy.GetPower<FearOfDevilPower>()!;Check(fear.Permanent&&fear.Amount==2,"each accepted six-member Fear application adds one permanent shared layer");Equal(MachinaE(Enemy),90m,"two permanent Fear layers lower E by60");
        await MachinaFear(Enemy,false);fear=Enemy.GetPower<FearOfDevilPower>()!;
        Check(fear.Permanent&&fear.Amount==2&&Enemy.Powers.OfType<FearOfDevilPower>().Count()==1,"a lower-tier teammate refresh never downgrades existing permanent Fear or adds a free shared layer");
        await fear.AfterSideTurnEnd(context,CombatSide.Enemy,new[]{Enemy});Check(Enemy.GetPower<FearOfDevilPower>()?.Amount==2,"permanent Fear survives every enemy turn end");
        await MachinaFear(Enemy,true);Equal(Enemy.GetPower<FearOfDevilPower>()!.Amount,3,"a later six-member owner can continue the same enemy's permanent Fear counter");
        Power<HbrCalamityPower>(Enemy,10);Equal(MachinaE(Enemy),50m,"Fear combines with existing independent Calamity in the actual enemy-E getter");
        await MachinaFear(Enemy,true);await MachinaFear(Enemy,true);Equal(MachinaE(Enemy),0m,"total state-value reduction keeps the existing E floor at zero");
        MachinaReset();Power<ArtifactPower>(Enemy,1);await MachinaFear(Enemy,true);
        Check(!Enemy.HasPower<FearOfDevilPower>()&&Enemy.GetPowerAmount<ArtifactPower>()==0,"native Artifact blocks a Fear application before any layer or permanent-state mutation");
        await MachinaFear(Enemy,false);Power<ArtifactPower>(Enemy,1);await MachinaFear(Enemy,true);
        Check(Enemy.GetPower<FearOfDevilPower>() is{Permanent:false,Amount:1},"a blocked six-member application cannot upgrade an existing temporary Fear to permanent");
    }
    static Task MachinaSerialized<T>(object rawDescriptor,object rawRequest,Player owner)
    {
        var descriptor=(RitsuLibManagedNetActionDescriptor<T>)rawDescriptor;
        Check(descriptor.ActionType==GameActionType.CombatPlayPhaseOnly,"31C network requests are real managed own-play-phase actions");
        byte[] bytes=descriptor.Serialize((T)rawRequest);T received=descriptor.Deserialize(bytes);
        Check(typeof(T).GetProperty("ExpectedUsed")!.GetValue(received)!.Equals(typeof(T).GetProperty("ExpectedUsed")!.GetValue(rawRequest)),"the use receipt survives the actual Ritsu descriptor serialization roundtrip");
        return descriptor.Execute(new RitsuLibManagedNetActionContext<T>(received,owner,null!,Blank<GameActionPlayerChoiceContext>()));
    }
    static Task MachinaNet(Player owner,int used)
    {
        Type net=Mod.GetType("HBRGOSakiMod.Core.Integration.HbrThirtyOneCMachinaNetActions",true)!;
        Type request=net.GetNestedType("ActivationRequest",F)!;object message=Activator.CreateInstance(request)!;
        request.GetProperty("ExpectedUsed")!.SetValue(message,used);
        object descriptor=net.GetField("Descriptor",F)!.GetValue(null)!;
        return (Task)Invoke(typeof(EffectChecks).GetMethod(nameof(MachinaSerialized),F)!.MakeGenericMethod(request),null,descriptor,message,owner)!;
    }
    static async Task MachinaNetworkCases()
    {
        var relic=MachinaReset(4);await relic.BeforeCombatStart();await MachinaNet(Player,0);Equal(relic.UsesSpent,1,"serialized managed activation spends the sender's own relic receipt");
        int hits=Hits.Count;await MachinaNet(Player,0);Check(relic.UsesSpent==1&&Hits.Count==hits,"a duplicate serialized request neither spends a second use nor repeats its attack");
        Set(Player.PlayerCombatState!,"_phase",PlayerTurnPhase.Start);await MachinaNet(Player,1);Equal(relic.UsesSpent,1,"a queued network request is rejected after the owner's play phase has ended");
        Set(Player.PlayerCombatState!,"_phase",PlayerTurnPhase.Play);
        var other=Blank<Player>();Set(other,"_relics",new List<RelicModel>());Set(other,"_runState",RunState);
        await MachinaNet(other,1);Equal(relic.UsesSpent,1,"an unrelated sender cannot use the local owner's relic through a payload owner override");
        await MachinaNet(Player,1);Equal(relic.UsesSpent,2,"the owner's distinct second serialized receipt remains usable");
        BondSavedCache(typeof(ThirtyOneCCollection));var save=MegaCrit.Sts2.Core.Saves.Runs.SavedProperties.From(relic)!;
        var copy=(ThirtyOneCCollection)ModelDb.Relic<ThirtyOneCCollection>().ToMutable();save.Fill(copy);
        Check(copy.UsesSpent==2&&copy.BattleReady,"native saved properties preserve only the actual battle-use ledger and ready state");
    }
    static Player MachinaPeer(int count)
    {
        var peer=Blank<Player>();Set(peer,"_runState",RunState);Set(peer,"<NetId>k__BackingField",202UL);
        Creature hero=(Creature)H("Actor",null,peer,null)!;Set(peer,"<Creature>k__BackingField",hero);
        Set(peer,"<Character>k__BackingField",Blank<HBRGOSakiMod.Core.Models.Characters.HbrgoSaki>());
        var manual=Blank<ElectronicSoldierManual>();Set(manual,"<IsMutable>k__BackingField",true);Set(manual,"_owner",peer);typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData",F)!.SetValue(manual,"{\"Schema\":1,\"Enabled\":true,\"HealthScaleApplied\":true,\"Level\":1}");Set(peer,"_relics",new List<RelicModel>{manual});
        Set(peer,"<Deck>k__BackingField",new CardPile(PileType.Deck));
        var state=Blank<PlayerCombatState>();Set(state,"_player",peer);Set(state,"_pets",new List<Creature>());Set(state,"_phase",PlayerTurnPhase.Play);
        foreach(var row in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})Set(state,"<"+row.Item1+">k__BackingField",new CardPile(row.Item2));
        Set(peer,"<PlayerCombatState>k__BackingField",state);MachinaDeck(peer,count);
        var relic=(ThirtyOneCCollection)ModelDb.Relic<ThirtyOneCCollection>().ToMutable();peer.AddRelicInternal(relic,silent:true);
        Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,hero});return peer;
    }
    static async Task MachinaCoopCases()
    {
        var own=MachinaReset(2);Set(Player,"<NetId>k__BackingField",101UL);await own.BeforeCombatStart();
        Player peer=MachinaPeer(6);var theirs=peer.GetRelic<ThirtyOneCCollection>()!;await theirs.BeforeCombatStart();
        await MachinaNet(peer,0);Check(theirs.UsesSpent==1&&own.UsesSpent==0&&Enemy.GetPower<FearOfDevilPower>()is{Permanent:true,Amount:1},"the six-member peer spends only its own use and creates shared permanent Fear");
        await MachinaNet(Player,0);Check(own.UsesSpent==1&&theirs.UsesSpent==1&&Enemy.GetPower<FearOfDevilPower>()is{Permanent:true,Amount:1},"the two-member owner has its separate one-use budget and cannot downgrade or add to the peer's permanent Fear");
        await MachinaNet(peer,1);Check(theirs.UsesSpent==2&&own.UsesSpent==1&&Enemy.GetPower<FearOfDevilPower>()is{Permanent:true,Amount:2},"the same peer's second receipt adds exactly one further permanent Fear layer");
        int hits=Hits.Count;await MachinaNet(Player,1);Check(Hits.Count==hits&&own.UsesSpent==1,"the exhausted two-member owner cannot borrow the six-member teammate's second-use tier");
    }
    static async Task MachinaFailureCases()
    {
        foreach(string failure in new[]{"fault","cancel"})
        {
            var relic=MachinaReset(4);await relic.BeforeCombatStart();MachinaFailure=failure;
            try{await MachinaPress(Player);throw new Exception("Expected controlled Machina failure");}
            catch(Exception error)when(error.Message=="controlled-machina-"+failure){Check(true,"a native attack "+failure+" propagates its original error");}
            Check(relic.UsesSpent==1&&!(bool)typeof(ThirtyOneCCollection).GetProperty("IsResolving",F)!.GetValue(relic)!,"a "+failure+" clears the in-flight lease while preserving the already-spent authoritative use");
            Equal((decimal)Call("ThirtyOneCMachinaHelper","DestructionMultiplier",Hero,null,ValueProp.Move)!,1m,"a "+failure+" releases the scoped destruction multiplier");
            Check(!(bool)Call("ThirtyOneCMachinaHelper","ForcesCritical",Hero,null,ValueProp.Move)!,"a "+failure+" releases the forced-critical scope");
            MachinaFailure=null;Check(await MachinaPress(Player,1),"a "+failure+" leaves the separately budgeted second receipt usable");
        }
    }
    public static void RunMachina(string path)
    {
        bool ok=false;string? error=null;ReportDirectory=Path.GetDirectoryName(Path.GetFullPath(path))!;
        try
        {
            Install();Localization();
            Type[] models=[..MachinaMembers,typeof(MomentOfReign),typeof(ProbedMachinaMemberFixture),typeof(StarsGuideYouHbrgoSaki),typeof(KozueSpiritPower),typeof(KozueSpiritShellPower),typeof(DisabledPower),typeof(LastStandPower),typeof(FutureDuoBondFixture),typeof(PhantomPower),typeof(PhantomHighDimensionalShellPower),typeof(DeflectorProtection),typeof(HbrDefenseUpPower),typeof(HbrCriticalRateUpPower),typeof(HbrAttackDownPower),typeof(HellfireBurnHbrgoSaki),typeof(FrozenMistMeowHbrgoSaki),typeof(ByakkoRoarHbrgoSaki),typeof(DemonKingAndFamiliarBond),typeof(WorldIsMyLaboratoryHbrgoSaki),typeof(WorldConquestHbrgoSaki),typeof(HighDimensionalShellPower),typeof(HotaruDamageAmplificationPower),typeof(ThirtyOneCCollection),typeof(FearOfDevilPower),typeof(MachinaHookProbePower),typeof(ArtifactPower),typeof(StrengthPower),typeof(BreakPower),typeof(DestructionRatePower),typeof(HbrAttackUpPower),typeof(HbrCriticalDamageUpPower),typeof(HbrComboLargePower),typeof(HbrCalamityPower),typeof(MatchaParfaitHbrgoSaki),typeof(ThirtyACollection),typeof(ThirtyOneACollection),typeof(ThirtyOneBCollection),typeof(CryChicCollection),typeof(TigersMouthSnackBond),typeof(FamilyIsLikeThisBond)];
            foreach(Type type in models){if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            BondSavedCache(typeof(MomentOfReign),typeof(ProbedMachinaMemberFixture),typeof(StarsGuideYouHbrgoSaki),typeof(KozueSpiritPower),typeof(KozueSpiritShellPower),typeof(DisabledPower),typeof(LastStandPower),typeof(FutureDuoBondFixture),typeof(PhantomPower),typeof(PhantomHighDimensionalShellPower),typeof(DeflectorProtection),typeof(HbrDefenseUpPower),typeof(HbrCriticalRateUpPower),typeof(HbrAttackDownPower),typeof(HellfireBurnHbrgoSaki),typeof(FrozenMistMeowHbrgoSaki),typeof(ByakkoRoarHbrgoSaki),typeof(DemonKingAndFamiliarBond),typeof(WorldIsMyLaboratoryHbrgoSaki),typeof(WorldConquestHbrgoSaki),typeof(HighDimensionalShellPower),typeof(HotaruDamageAmplificationPower),typeof(ThirtyOneCCollection),typeof(FearOfDevilPower));
            var h=new Harmony(ProductionId+".machina");
            h.Patch(typeof(CardPileCmd).GetMethods(F).Single(method=>method.Name=="Draw"&&method.GetParameters().Length==4),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(SeiraDrawLeaf),F)!));
            h.Patch(typeof(RollGlossaryCapability).GetMethod("NextRawRoll",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(UnifiedFixedRaw),F)!));
            h.Patch(typeof(RollGlossaryCapability).GetMethod("HasEffectiveRollKeyword",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(UnifiedEligibleCard),F)!));
            h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch",true)!).Patch();
            foreach(Type type in new[]{typeof(RunState),typeof(CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RanaListeners),F)!){priority=Priority.First+2});
            h.Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormNotEnding),F)!));
            h.Patch(AccessTools.PropertyGetter(typeof(CombatManager),"PlayerActionsDisabled"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MachinaFalse),F)!));
            h.Patch(AccessTools.PropertyGetter(typeof(PowerModel),"Icon"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            foreach(MethodInfo method in typeof(Cmd).GetMethods(F).Where(method=>method.Name is"Wait"or"CustomScaledWait"))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            var history=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory",true)!;
            foreach(MethodInfo method in history.GetMethods(F).Where(method=>method.Name is"CreatureAttacked"or"PowerReceived"or"PowerRemoved"))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            foreach(string name in new[]{"HighHitDamageAttackScopePatch","HighHitDamageResolvedCountPatch","HighHitDamageCommandScopePatch","HbrEffectApplicationPatch","HighDimensionalShellInitialAmountPatch","HighDimensionalShellNativeAmountPatch"})h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch();
            h.Unpatch(Canonical,typeof(EffectChecks).GetMethod(nameof(Terminal),F)!);
            h.Patch(Canonical,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MachinaTerminal),F)!){priority=Priority.Last,after=[ProductionId]});
            MachinaCountCacheCases();MachinaBaseCases().GetAwaiter().GetResult();MachinaModifierCases().GetAwaiter().GetResult();MachinaDestructionCases().GetAwaiter().GetResult();MachinaFearCases().GetAwaiter().GetResult();MachinaNetworkCases().GetAwaiter().GetResult();MachinaCoopCases().GetAwaiter().GetResult();MachinaFailureCases().GetAwaiter().GetResult();SeiraCases().GetAwaiter().GetResult();Bond50Cases().GetAwaiter().GetResult();
            ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),nativeSourceSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),boundary="Real native AttackCommand Execute and attack hooks; ordinary HBR attribute/native-critical/buff/destruction scope paths, terminal native damage/visual/history writes controlled. Member cards are explicit validation-only models, never production cards. UI, live multiplayer transport, Godot, packaging and upload are not exercised."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Machina checks");if(!ok)throw new Exception("Inspect "+path);
    }
}
