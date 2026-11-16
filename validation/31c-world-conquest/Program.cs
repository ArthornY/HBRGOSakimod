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
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

static class BungoStandalone { public static void Run(string path)=>EffectChecks.RunBungo(path); }
static partial class EffectChecks
{
    sealed record BungoHit(decimal Raw,int Hits,decimal Conditional,decimal Amplification,decimal Final);
    static readonly List<BungoHit> BungoHits=[];
    static readonly List<CardModel> BungoMoves=[];
    static readonly List<decimal> BungoEnergy=[];
    static bool BungoAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {
        var card=(CardModel)__instance.ModelSource!;decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(__instance)!;
        int hits=(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(__instance)!;
        BungoHits.Add(new(raw,hits,(decimal)Call("WorldConquestCombatHelper","GetConditionalDamageMultiplier",card,Hero,Enemy,ValueProp.Move)!,
            (decimal)Call("WorldConquestCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,Actual(Enemy,Hero,raw,ValueProp.Move,card)));
        __result=Task.FromResult(__instance);return false;
    }
    static bool BungoMoveLeaf(CardModel card,object[] __args,ref Task<CardPileAddResult> __result)
    {
        BungoMoves.Add(card);card.Pile?.RemoveInternal(card,true);Player.PlayerCombatState!.Hand.AddInternal(card);
        __result=Task.FromResult(new CardPileAddResult{success=true,cardAdded=card});return false;
    }
    static bool BungoEnergyLeaf(decimal amount,Player player,ref Task __result)
    {BungoEnergy.Add(amount);__result=Task.CompletedTask;return false;}
    static void BungoReset()
    {
        FormReset();Fate(FateJson(bosses:0));BungoHits.Clear();BungoMoves.Clear();BungoEnergy.Clear();
        UnifiedKeywordCard=null;Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",1);
    }
    static WorldConquestHbrgoSaki Bungo(params int[] upgrades)
    {
        var card=FormCard<WorldConquestHbrgoSaki>();
        foreach(int id in upgrades)
        {Check((bool)Native(card,"PrepareUpgradeEffect",id)!,"Bungo garden choice eligible "+id);card.UpgradeInternal();card.FinalizeUpgradeInternal();}
        return card;
    }
    static void BungoPermanent(CardModel card)
    {card.Pile?.RemoveInternal(card,true);Player.Deck.AddInternal(card);}
    static async Task BungoPlay(CardModel card)
    {var play=Play(card);Set(play,"<Target>k__BackingField",Enemy);await (Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),play)!;}
    sealed class BungoMemberStub:CardModel,IThirtyOneCMemberCard
    {
        public ThirtyOneCMember Member;
        public ThirtyOneCMember ThirtyOneCMember=>Member;
        public BungoMemberStub():base(0,CardType.Skill,CardRarity.Rare,TargetType.Self){}
    }
    static void BungoSixMembers()
    {
        foreach(ThirtyOneCMember member in Enum.GetValues<ThirtyOneCMember>())
        {var c=Blank<BungoMemberStub>();Set(c,"_owner",Player);Set(c,"<IsMutable>k__BackingField",true);c.Member=member;Player.Deck.AddInternal(c);}
    }
    static WorldConquestHbrgoSaki BungoMirror(WorldConquestHbrgoSaki physical)
    {
        var card=(WorldConquestHbrgoSaki)physical.MutableClone();card.DeckVersion=physical;
        Player.PlayerCombatState!.Hand.AddInternal(card);((List<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Add(card);return card;
    }
    static async Task BungoCases()
    {
        BungoReset();var card=Bungo();
        Check(card.Type==CardType.Attack&&card.Rarity==CardRarity.Rare&&card.TargetType==TargetType.AnyEnemy&&card.EnergyCost.Canonical==1,"Bungo base is one-energy rare single-target attack");
        Equal(card.DynamicVars.Damage.BaseValue,12,"Bungo stores200percent as12native damage units");Check(card is IThirtyOneCMemberCard member&&member.ThirtyOneCMember==ThirtyOneCMember.BungoYayoi,"Bungo is exactly the Bungo31C member");
        await BungoPlay(card);Check(BungoHits.Count==1&&BungoHits[0].Raw==12m&&BungoHits[0].Hits==1&&BungoHits[0].Conditional==1m,"base command issues one200percent hit without DP bonus");
        Power<HighDimensionalShellPower>(Enemy,1000);await BungoPlay(card);Equal(BungoHits[^1].Conditional,1.3m,"basic DP target receives130percent conditional factor");
        BungoReset();card=Bungo(0);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"Flash reduces actual card cost by one");
        BungoReset();card=Bungo(1);Power<HighDimensionalShellPower>(Enemy,1000);BungoSixMembers();
        await BungoPlay(card);Equal(BungoHits.Single().Conditional,2m,"SuperDestroyer replaces DP bonus with100percent");Equal(BungoHits.Single().Amplification,60m,"six distinct permanent31C members give60percent additive amplification");
        var duplicate=Blank<BungoMemberStub>();Set(duplicate,"_owner",Player);Set(duplicate,"<IsMutable>k__BackingField",true);duplicate.Member=ThirtyOneCMember.BungoYayoi;Player.Deck.AddInternal(duplicate);
        Equal((decimal)Call("WorldConquestCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,60m,"duplicate member never exceeds six-kind60percent cap");
        Equal((decimal)Call("WorldConquestCombatHelper","GetDamageAmplificationPercent",card,Enemy,Hero,ValueProp.Move)!,0m,"another dealer cannot inherit card-only amplification");
        BungoReset();card=Bungo(6);await BungoPlay(card);Equal(BungoHits.Single().Conditional,2m,"HolyAnthem doubles actual first play only");
        var down=Enemy.GetPower<HbrDefenseDownPower>()!;Check(down!=null,"HolyAnthem applies a real defense-down instance");
        Check(down!.GetType().GetProperty("CurrentEffectProfile",F)!.GetValue(down)!.ToString()!.Contains("Eternal"),"HolyAnthem defense-down lasts the battle");
        decimal expected=20m*(1m+(100m-(decimal)Call("HbrEnemyProfileHelper","GetEnemyParameter",Enemy)!)*.001m);
        Equal((decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",down)!,expected,"20percent basis uses the real normal debuff attribute formula");
        await BungoPlay(card);Equal(BungoHits[^1].Conditional,1m,"second play loses HolyAnthem double");Equal(Powers(Enemy).OfType<HbrDefenseDownPower>().Count(),1,"second play creates no extra HolyAnthem defense down");
        BungoPermanent(card);var mirror=BungoMirror(card);await mirror.BeforeCombatStart();await BungoPlay(mirror);
        Equal(BungoHits[^1].Conditional,1m,"OD mirror and repeated combat initialization cannot renew physical first-play receipt");
        BungoReset();card=Bungo(3);var pipeline=DesuwaAttach(card);UnifiedKeywordCard=card;
        LandRoll(pipeline,card,30);int rng=RngCount(RunState.Rng.Niche);await BungoPlay(card);Check(BungoHits.Count==0,"finalroll30 misses without issuing any attack command");Equal(RngCount(RunState.Rng.Niche),rng,"miss reuses the already-resolved normal card die without another random roll");
        LandRoll(pipeline,card,31);await BungoPlay(card);Check(BungoHits.Count==1&&BungoHits[0].Conditional==2m,"finalroll31 doubles the actual attack");Equal(RngCount(RunState.Rng.Niche),rng,"success also reuses the same normal card die");
        BungoReset();card=Bungo(3,6);pipeline=DesuwaAttach(card);UnifiedKeywordCard=card;LandRoll(pipeline,card,30);await BungoPlay(card);
        Check(BungoHits.Count==0&&Enemy.GetPower<HbrDefenseDownPower>()!=null,"a miss still counts as first play and applies HolyAnthem's independent first-play debuff");
        LandRoll(pipeline,card,31);await BungoPlay(card);Equal(BungoHits.Single().Conditional,2m,"later success gets only Trick double, not a second HolyAnthem double");
        BungoReset();card=Bungo(4);var friend=FormCard<WorldIsMyLaboratoryHbrgoSaki>();friend.Pile!.RemoveInternal(friend,true);Player.PlayerCombatState!.DrawPile.AddInternal(friend);
        var unrelated=Owned<StrikeIronclad>();Player.PlayerCombatState!.DrawPile.AddInternal(unrelated);
        await BungoPlay(card);Check(BungoMoves.SequenceEqual(new CardModel[]{friend})&&friend.Pile?.Type==PileType.Hand&&unrelated.Pile?.Type==PileType.Draw,"CompanionDance moves only another31C draw card into the real hand");
        await BungoPlay(card);Equal(BungoMoves.Count,1,"no other31C draw candidate means no move");
        BungoReset();card=Bungo(2);Creature other=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;Set(Combat,"_enemies",new List<Creature>{Enemy,other});
        Power<StrengthPower>(Enemy,10);Power<StrengthPower>(other,7);Power<StrengthPower>(other,9);
        await BungoPlay(card);Check(!Enemy.HasPower<StrengthPower>()&&!other.HasPower<StrengthPower>(),"ThreeColorDango removes one full buff kind from each enemy including duplicate instances");
        BungoReset();card=Bungo(5);BungoPermanent(card);
        MethodInfo hook=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterEnergyReset");
        object?[] args=hook.GetParameters().Select(p=>p.ParameterType==typeof(Player)?(object)Player:p.ParameterType.IsAssignableFrom(typeof(CombatState))?Combat:p.ParameterType.IsAssignableFrom(RunState.GetType())?RunState:throw new Exception(p.ToString())).ToArray();
        await (Task)Invoke(hook,null,args)!;Check(BungoEnergy.SequenceEqual(new[]{1m}),"real global energy-reset hook grants a permanent deck card's opening energy");
        mirror=BungoMirror(card);await mirror.BeforeCombatStart();await mirror.AfterEnergyReset(Player);await (Task)Invoke(hook,null,args)!;
        Equal(BungoEnergy.Count,1,"permanent card, OD mirror and global fallback share one opening-energy receipt");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await (Task)Call("WorldConquestCombatHelper","OnEnergyReset",Player)!;Equal(BungoEnergy.Count,1,"a later ordinary turn grants no opening energy");
        BungoReset();card=Bungo(6);var closed=(IDisposable)Call("WorldConquestCombatHelper","Begin",card,0)!;
        var staleContext=System.Threading.ExecutionContext.Capture()!;closed.Dispose();decimal staleFactor=0m;
        System.Threading.ExecutionContext.Run(staleContext,_=>staleFactor=(decimal)Call("WorldConquestCombatHelper","GetConditionalDamageMultiplier",card,Hero,Enemy,ValueProp.Move)!,null);
        Equal(staleFactor,1m,"captured ExecutionContext cannot reuse a closed HolyAnthem play scope");
        BungoReset();card=Bungo(0,1,2,3,4,5,6);Check(card.CurrentUpgradeLevel==7&&!card.IsUpgradable&&((IReadOnlyList<int>)Native(card,"GetAvailableUpgradeEffects")!).Count==0,"all seven independent upgrades are reachable exactly once");
    }
    public static void RunBungo(string path)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".bungo49");
            foreach(Type type in new[]{typeof(WorldConquestHbrgoSaki),typeof(WorldIsMyLaboratoryHbrgoSaki),typeof(HbrDefenseDownPower),typeof(HighDimensionalShellPower),typeof(StrengthPower)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string name in new[]{"WorldConquestOpeningEnergyPatch","HbrEffectApplicationPatch","HbrFragileApplyPatch","HbrFragileStackingPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs production "+name);
            h.Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BungoAttackLeaf),F)!));
            h.Patch(typeof(RollGlossaryCapability).GetMethod("HasEffectiveRollKeyword",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(UnifiedEligibleCard),F)!));
            h.Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            foreach(MethodInfo method in typeof(CardPileCmd).GetMethods(F).Where(m=>m.Name=="Add"&&m.GetParameters()[0].ParameterType==typeof(CardModel)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BungoMoveLeaf),F)!));
            foreach(MethodInfo method in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            h.Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.GainEnergy),new[]{typeof(decimal),typeof(Player)})!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BungoEnergyLeaf),F)!));
            BungoCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual physical-card upgrade ledger, OnPlay bodies, common global damage pipeline, native PowerCmd and global AfterEnergyReset execute. Attack/VFX, pile animation, energy write and existing Ritsu registry leaves are isolated. No Godot, package or delivered artifact changes."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Bungo checks");
    }
}
