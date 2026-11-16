using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

public sealed class LockExpensiveFixture:CardModel{public LockExpensiveFixture():base(5,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy){}}
public sealed class LockLogicBlockedFixture:CardModel{protected override bool IsPlayable=>false;public LockLogicBlockedFixture():base(9,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class LockSisterA:CardModel,IThirtyOneEMemberCard{public ThirtyOneEMember ThirtyOneEMember=>ThirtyOneEMember.OshimaIchiko;public LockSisterA():base(1,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class LockSisterB:CardModel,IThirtyOneEMemberCard{public ThirtyOneEMember ThirtyOneEMember=>ThirtyOneEMember.OshimaNiina;public LockSisterB():base(1,CardType.Skill,CardRarity.Common,TargetType.Self){}}
static class IsuzuStandalone{public static void Run(string path)=>EffectChecks.RunIsuzu(path);}
static partial class EffectChecks
{
    static readonly List<(decimal Raw,int Hits)> IsuzuAttacks=[];
    static int LockChoices;
    static CardModel? LockChoice;
    static bool LockAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {
        Check(__instance.ModelSource is OpenTheLockHbrgoSaki && __instance.IsMultiTargeted,"70 actual native attack command retains own card source and all-opponent scope");
        IsuzuAttacks.Add(((decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(__instance)!,
            (int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(__instance)!));
        __result=Task.FromResult(__instance);return false;
    }
    static bool LockSelectLeaf(Func<CardModel,bool> filter,ref Task<IEnumerable<CardModel>> __result)
    {
        LockChoices++;Check(LockChoice!=null&&filter(LockChoice),"70 real hand selector offers the tied highest-cost physical card");
        __result=Task.FromResult<IEnumerable<CardModel>>([LockChoice!]);return false;
    }
    static void LockReset(){NewReset();IsuzuAttacks.Clear();LockChoices=0;LockChoice=null;Set(Player.PlayerCombatState!,"_energy",0);}
    static async Task LockCoreCases()
    {
        LockReset();var card=NewGarden<OpenTheLockHbrgoSaki>();await NewPlay(card);
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),2,"70 base actual cost2");
        Check(IsuzuAttacks.Single()==(3.06m,3),"70 base actual card issues51percent times3");Equal((decimal)Call("IsuzuOpenTheLockCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,.4m,"70 destruction1point2 normalized by existing default3");
        Equal(LockChoices,0,"70 empty other-hand list needs no selection or RNG");
        LockReset();card=NewGarden<OpenTheLockHbrgoSaki>();var cheap=FormCard<StrikeIronclad>();var expensive=FormCard<LockExpensiveFixture>();FormCard<Whirlwind>();FormCard<LockLogicBlockedFixture>();FormCard<Injury>();
        await NewPlay(card);Equal(expensive.EnergyCost.GetWithModifiers(CostModifiers.All),0,"70 highest-cost card becomes0 despite current energy0");Equal(cheap.EnergyCost.GetWithModifiers(CostModifiers.All),1,"70 cheaper card is unchanged");Equal(LockChoices,0,"70 unique highest candidate bypasses choice screen");
        expensive.EnergyCost.EndOfTurnCleanup();Equal(expensive.EnergyCost.GetWithModifiers(CostModifiers.All),5,"70 native turn cleanup restores original cost");
        LockReset();card=NewGarden<OpenTheLockHbrgoSaki>();var first=FormCard<LockExpensiveFixture>();var second=FormCard<LockExpensiveFixture>();LockChoice=second;
        await NewPlay(card);Equal(LockChoices,1,"70 tied highest cards use exactly one native choice");Equal(first.EnergyCost.GetWithModifiers(CostModifiers.All),5,"70 unchosen tied card retains cost");Equal(second.EnergyCost.GetWithModifiers(CostModifiers.All),0,"70 selected physical tied card is free");
        LockReset();card=NewGarden<OpenTheLockHbrgoSaki>();var aina=NewGarden<DiveAsDeepAsIWantHbrgoSaki>();Power<WeakPower>(Enemy,1);await NewPlay(card);Equal(aina.EnergyCost.GetWithModifiers(CostModifiers.All),0,"70 native local zero wins over Aina's dynamic negative-status discount");
        LockReset();card=NewGarden<OpenTheLockHbrgoSaki>(0,3);MisatoPermanent(card);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),1,"70 Flash alone makes printed2to1");
        MisatoPermanent(FormCard<LockSisterA>());MisatoPermanent(FormCard<LockSisterB>());
        var origin=card;card=(OpenTheLockHbrgoSaki)origin.MutableClone();Set(card,"_owner",Player);card.DeckVersion=origin;Player.PlayerCombatState!.Hand.AddInternal(card);
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),0,"70 at three distinct permanent31E members Flash and HellfireGate reduce actual cost to0");
        LockReset();card=NewGarden<OpenTheLockHbrgoSaki>(1,2,4);Power<HbrBindPower>(Hero,2);Power<HbrImprisonPower>(Hero,2);
        Check(!card.CanPlay(out UnplayableReason locked,out _)&&(locked&UnplayableReason.BlockedByHook)!=0,"70 EscapeArtist does not bypass native Imprison manual-play restriction");
        await NewPlay(card);Check(IsuzuAttacks.Single()==(4.62m,3),"70 AngryRift changes real damage to77percent times3");Check(!Hero.HasPower<HbrBindPower>()&&!Hero.HasPower<HbrImprisonPower>(),"70 EscapeArtist removes actual Bind and Imprison before attack");
        Equal((decimal)Call("IsuzuOpenTheLockCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,1.6m/3m,"70 upgraded destruction1point6 normalized once");
        Check(Enemy.HasPower<HbrDefenseDownPower>()&&Enemy.HasPower<FragilePower>(),"70 actual AngryRift and Mirage append distinct percentage debuffs");
        foreach(PowerModel power in Enemy.Powers.Where(p=>p is HbrDefenseDownPower or FragilePower))Equal(power.Amount,1,"70 ordinary after-hit debuffs expire at next enemy end");
        var unpowered=(decimal)Call("IsuzuOpenTheLockCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Unpowered)!;Equal(unpowered,1m,"70 unpowered reaction does not inherit card destruction");
        var kinds=new HashSet<Type>();
        for(int seed=0;seed<24;seed++)
        {
            LockReset();Set(RunState,"<Rng>k__BackingField",new MegaCrit.Sts2.Core.Runs.RunRngSet("isuzu-mischief-"+seed));card=NewGarden<OpenTheLockHbrgoSaki>(5);
            await NewPlay(card);PowerModel effect=Enemy.Powers.Single(p=>p is HbrAttackDownPower or HbrDefenseDownPower or FragilePower);kinds.Add(effect.GetType());Equal(effect.Amount,2,"70 Mischief actual independent effect lasts2enemyends");
            Check(Enemy.Powers.Count(p=>p is HbrAttackDownPower or HbrDefenseDownPower or FragilePower)==1,"70 Mischief gives exactly one of three allowed statuses and no stun/confusion");
        }
        Check(kinds.Count==3,"70 deterministic native RNG seeds exercise all three Mischief options");
        LockReset();card=NewGarden<OpenTheLockHbrgoSaki>();
        for(int i=0;i<7;i++){Check((bool)Native(card,"PrepareUpgradeEffect",i)!,"70 independent upgrade accepted"+i);card.UpgradeInternal();card.FinalizeUpgradeInternal();}
        Check(card.CurrentUpgradeLevel==7 && ((int[])Native(card,"get_UpgradeEffectOrder")!).SequenceEqual(Enumerable.Range(0,7)),"70 all seven physical upgrade slots acquired once");
    }
    public static void RunIsuzu(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".isuzu70");
            foreach(Type t in new[]{typeof(OpenTheLockHbrgoSaki),typeof(LockExpensiveFixture),typeof(LockLogicBlockedFixture),typeof(LockSisterA),typeof(LockSisterB),typeof(Whirlwind),typeof(Injury),typeof(StrikeIronclad),typeof(DiveAsDeepAsIWantHbrgoSaki),typeof(HbrDefenseDownPower),typeof(HbrAttackDownPower),typeof(FragilePower),typeof(HbrBindPower),typeof(HbrImprisonPower),typeof(SmallHopper)})
            {if(!ModelDb.Contains(t))ModelDb.Inject(t);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(t)),"<Id>k__BackingField",ModelDb.GetId(t));}
            foreach(string name in new[]{"HbrEffectApplicationPatch","HbrEffectInstanceStackingPatch"})h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch();
            foreach(Type t in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(t.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoListeners),F)!){priority=Priority.First+100});
            h.Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(LockAttackLeaf),F)!));
            h.Patch(typeof(CardSelectCmd).GetMethods(F).Single(m=>m.Name=="FromHand"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(LockSelectLeaf),F)!));
            LockCoreCases().GetAwaiter().GetResult();LockTreasureCases(h).GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual card OnPlay/native cost/upgrade/independent debuff/native bag service; attack executor, hand/relic user-choice and scene obtain leaves recorded. Native treasure callsite transpiler audited. No Godot rendering/live multiplayer/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Isuzu70 checks");
    }
}
