using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;

static class MikoStandalone { public static void Run(string path)=>EffectChecks.RunMiko(path); }
static partial class EffectChecks
{
    static int MikoFactories;
    static bool MikoListeners(ref IEnumerable<AbstractModel> __result)
    {__result=Combat.Creatures.SelectMany(c=>c.Powers).Cast<AbstractModel>().Concat(Player.Relics).Concat(Player.PlayerCombatState!.AllCards).ToArray();return false;}
    static bool MikoFactoryLeaf(Player player,Rng rng,ref PotionModel __result)
    {MikoFactories++;rng.NextInt(0,2);__result=ModelDb.Potion<StrengthPotion>();return false;}
    static ImmortalRecollectionHbrgoSaki Miko(params int[] ids)
    {
        var card=FormCard<ImmortalRecollectionHbrgoSaki>();
        foreach(int id in ids){Check((bool)Native(card,"PrepareUpgradeEffect",id)!,"Miko garden accepts independent choice "+id);card.UpgradeInternal();card.FinalizeUpgradeInternal();}
        return card;
    }
    static void MikoReset()
    {BungoReset();MikoFactories=0;Set(Player,"_potionSlots",new List<PotionModel?>{null,null,null});}
    static decimal MikoPercent(HbrDefenseDownPower power)=>(decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",power)!;
    static async Task MikoPlay(CardModel card)
    {var play=Play(card);Set(play,"<Target>k__BackingField",Enemy);await (Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),play)!;}
    static async Task MikoCases()
    {
        MikoReset();var card=Miko();Check(card.Type==CardType.Skill&&card.EnergyCost.Canonical==1&&card.TargetType==TargetType.AnyEnemy&&card.Rarity==CardRarity.Rare,"Miko base is one-energy rare single-target skill");
        await MikoPlay(card);var down=Enemy.GetPower<HbrDefenseDownPower>()!;Equal(MikoPercent(down),28.5m,"base30percent gets the ordinary100INT versus150E debuff formula");
        Check(down.Amount==2&&(int)Call("HbrPercentDebuffHelper","GetModifierAmount",down)! == 1,"base is one independent two-turn application, not30modifier layers");
        MikoReset();card=Miko(1);Creature other=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;Set(Combat,"_enemies",new List<Creature>{Enemy,other});
        await MikoPlay(card);Check(card.TargetType==TargetType.AllEnemies&&Enemy.GetPower<HbrDefenseDownPower>()!=null&&other.GetPower<HbrDefenseDownPower>()!=null,"DestructionPlus needs no target and applies to every opponent");
        Equal(MikoPercent(Enemy.GetPower<HbrDefenseDownPower>()!),38m,"DestructionPlus starts from40percent basis");
        MikoReset();card=Miko(3);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),1,"CatMagic leaves base cost without enemy defense-down");
        await MikoPlay(card);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),0,"actual native cost hook sees enemy defense-down and discounts one");
        await PowerCmd.Remove(Enemy.GetPower<HbrDefenseDownPower>()!);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),1,"removing the last enemy defense-down restores native cost");
        MikoReset();card=Miko(0,3);await MikoPlay(card);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),0,"Flash and CatMagic never make a negative energy cost");
        MikoReset();card=Miko(1,4);other=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;Set(Combat,"_enemies",new List<Creature>{Enemy,other});await MikoPlay(card);
        foreach(Creature target in new[]{Enemy,other})
        {
            var fragile=target.GetPower<FragilePower>()!;Check(fragile!=null&&fragile.Amount==1,"CrimsonWitch applies Fragile to the same AoE target with the current framework default duration");
            Equal((decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",fragile)!,46.875m*.95m,"Fragile uses3modifiers and its standard46point875percent base");
        }
        MikoReset();card=Miko(5);
        for(int i=0;i<6;i++)
        {
            decimal basis=30m+Math.Min(i*10,40);await MikoPlay(card);
            Equal(Powers(Enemy).OfType<HbrDefenseDownPower>().Max(MikoPercent),basis*.95m,"NecroDance uses pre-play basis at iteration"+i);
            Equal((int)Call("ImmortalRecollectionCombatHelper","Growth",card)!,Math.Min((i+1)*10,40),"NecroDance advances only after play and caps at40, iteration"+i);
        }
        BungoPermanent(card);var mirror=(ImmortalRecollectionHbrgoSaki)card.MutableClone();mirror.DeckVersion=card;Player.PlayerCombatState!.Hand.AddInternal(mirror);await mirror.BeforeCombatStart();
        Equal((int)Call("ImmortalRecollectionCombatHelper","Growth",mirror)!,40,"OD mirror preserves its physical card's accumulated growth through repeated initialization");await MikoPlay(mirror);
        var generated=Miko(5);Equal((int)Call("ImmortalRecollectionCombatHelper","Growth",generated)!,0,"a different physical card does not inherit growth");
        MikoReset();card=Miko(2);Power<HbrAttackDownPower>(Hero,2);Power<HbrDefenseDownPower>(Hero,2);await MikoPlay(card);
        Equal(Powers(Hero).Count(p=>p.Type==MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff),1,"Antidote removes exactly one existing debuff using the shared Tama cleanser");
        MikoReset();card=Miko(6);Set(Player,"_potionSlots",new List<PotionModel?>{ModelDb.Potion<StrengthPotion>().ToMutable(),ModelDb.Potion<StrengthPotion>().ToMutable(),ModelDb.Potion<StrengthPotion>().ToMutable()});
        int rng=RngCount(RunState.Rng.CombatPotionGeneration);await MikoPlay(card);
        Check(MikoFactories==0&&RngCount(RunState.Rng.CombatPotionGeneration)==rng,"full potion bar skips factory and all potion RNG");
        Set(Player,"_potionSlots",new List<PotionModel?>{null,null,null});await MikoPlay(card);Equal(MikoFactories,0,"full first play still consumes that physical card's first-play potion opportunity");
        MikoReset();card=Miko(6);await MikoPlay(card);Check(MikoFactories==1&&Player.Potions.Count()==1,"first recipe uses native PotionCmd to procure exactly one random potion");
        BungoPermanent(card);mirror=(ImmortalRecollectionHbrgoSaki)card.MutableClone();mirror.DeckVersion=card;Player.PlayerCombatState!.Hand.AddInternal(mirror);await mirror.BeforeCombatStart();await MikoPlay(mirror);
        Check(MikoFactories==1&&Player.Potions.Count()==1,"OD mirror and repeated initialization cannot renew recipe's first-play opportunity");
        MikoReset();card=Miko(0,1,2,3,4,5,6);Check(card.CurrentUpgradeLevel==7&&!card.IsUpgradable&&((IReadOnlyList<int>)Native(card,"GetAvailableUpgradeEffects")!).Count==0,"all seven Miko upgrades are independently reachable exactly once");
    }
    public static void RunMiko(string path)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".miko52");
            foreach(Type type in new[]{typeof(ImmortalRecollectionHbrgoSaki),typeof(HbrDefenseDownPower),typeof(HbrAttackDownPower),typeof(FragilePower),typeof(StrengthPotion)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(Type type in new[]{typeof(MegaCrit.Sts2.Core.Runs.RunState),typeof(MegaCrit.Sts2.Core.Combat.CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MikoListeners),F)!){priority=Priority.First+3});
            foreach(string name in new[]{"HbrFragileApplyPatch","HbrFragileStackingPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs production "+name);
            h.Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            foreach(MethodInfo method in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            h.Patch(typeof(MegaCrit.Sts2.Core.Factories.PotionFactory).GetMethod("CreateRandomPotionInCombat",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MikoFactoryLeaf),F)!));
            MikoCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual Miko OnPlay, native cost Hook, common HBR debuff/Fragile applications and instance slots, shared Tama cleanse and native PotionCmd inventory writes execute. Only registered potion pool choice is a deterministic factory leaf; full bar invokes no factory/RNG. No Godot, packaging or delivered-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Miko checks");
    }
}
