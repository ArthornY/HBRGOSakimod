using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class MiyaStandalone { public static void Run(string path)=>EffectChecks.RunMiya(path); }
static partial class EffectChecks
{
    sealed record MiyaHit(CardModel Source,decimal Raw,int Hits,Creature[] Targets,decimal Destruction);
    static readonly List<MiyaHit> MiyaHits=[];
    static bool MiyaAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=MiyaAttack(__instance);return false;}
    static async Task<AttackCommand> MiyaAttack(AttackCommand command)
    {
        await (Task)MisatoHook("BeforeAttack",command)!;
        var targets=((IReadOnlyList<Creature>)Native(command,"GetPossibleTargets")!).ToArray();
        int hits=(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!;
        var source=(CardModel)command.ModelSource!;
        MiyaHits.Add(new(source,(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!,hits,targets,
            (decimal)Call("KiryuExorcisingStringCombatHelper","GetDestructionGainMultiplier",source,targets[0],Hero,ValueProp.Move)!));
        var rows=(List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!;
        foreach(var target in targets)rows.Add(Enumerable.Range(0,hits).Select(_=>new DamageResult(target,ValueProp.Move)).ToList());
        await (Task)MisatoHook("AfterAttack",command)!;return command;
    }
    static void MiyaReset()
    {NewReset();MiyaHits.Clear();var table=Helper("KiryuExorcisingStringCombatHelper").GetField("Battles",F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);}
    static decimal MiyaPercent(PowerModel power)=>(decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",power)!;
    static async Task MiyaTick(ExorcisingStringPower power)=>await power.AfterEnergyReset(Player);
    static string MiyaDefenseSeed()
    {for(int i=0;i<1000;i++){string seed="MIYA-DEFENSE-"+i;var rng=new RunRngSet(seed).CombatTargets;rng.NextInt(0,2);if(rng.NextInt(0,2)==0)return seed;}throw new Exception("No seed");}
    static async Task MiyaCases()
    {
        MiyaReset();var card=NewGarden<ExorcisingStringResoundHbrgoSaki>();await NewPlay(card);var power=Hero.GetPower<ExorcisingStringPower>()!;
        Check(card.Type==CardType.Power&&card.EnergyCost.GetWithModifiers(CostModifiers.Local)==1,"base card is a1energy Power");Check(MiyaHits.Count==0,"base Power play is not an attack");
        await MiyaTick(power);var fragile=Enemy.GetPower<FragilePower>()!;Equal(MiyaPercent(fragile),39.9m,"base42fragile passes through real attribute framework");Check(fragile.Amount==1&&Native(fragile,"get_CurrentEffectProfile")!.ToString()!.Contains("EnemyTurnEnd"),"base fragile lasts to the next enemy end");
        int rng=RngCount(RunState.Rng.CombatTargets);await MiyaTick(power);await (Task)Call("KiryuExorcisingStringCombatHelper","StartInsertedTurn",new ThrowingPlayerChoiceContext(),Player)!;
        Check(RngCount(RunState.Rng.CombatTargets)==rng,"repeated same-turn native and inserted callbacks neither reapply nor consume RNG");
        var fresh=MisatoEnemy();Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await MiyaTick(power);Check(fresh.HasPower<FragilePower>(),"target picker prioritizes the one enemy lacking fragile");
        foreach(var enemy in new[]{Enemy,fresh})foreach(var effect in enemy.Powers.OfType<FragilePower>().ToArray())await effect.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,[Enemy,fresh]);
        Check(!Enemy.HasPower<FragilePower>()&&!fresh.HasPower<FragilePower>(),"real enemy-end callbacks remove both one-turn applications");
        MiyaReset();card=NewGarden<ExorcisingStringResoundHbrgoSaki>(0,1,2,3,6);await NewPlay(card);power=Hero.GetPower<ExorcisingStringPower>()!;fresh=MisatoEnemy();Set(RunState,"<Rng>k__BackingField",new RunRngSet(MiyaDefenseSeed()));
        Check(card.EnergyCost.GetWithModifiers(CostModifiers.Local)==0&&card.Keywords.Contains(CardKeyword.Innate),"Flash0and Bowstring Innate are native card properties");
        await MiyaTick(power);Check(Enemy.HasPower<FragilePower>()&&fresh.HasPower<FragilePower>(),"GrassArrow applies to all living enemies");
        var hit=MiyaHits.Single();Equal(hit.Raw,8.64m,"CursedArrow144percent actual coefficient");Equal(hit.Hits,1,"CursedArrow single hit");Check(hit.Targets.Length==1,"GrassArrow still fires at only its selected enemy");Equal(hit.Destruction,1.25m/3m,"arrow uses its own destruction1point25 scope");Check(hit.Source.Type==CardType.Power,"an automatic arrow never pretends an Attack card was played");
        Check(Enemy.HasPower<HbrDefenseDownPower>()&&fresh.HasPower<HbrDefenseDownPower>()&&!Enemy.HasPower<HbrAttackDownPower>()&&!fresh.HasPower<HbrAttackDownPower>(),"NoRather selects one shared random debuff kind for the whole fragile batch");
        await NewPlay(card);Check(Hero.Powers.OfType<ExorcisingStringPower>().Count()==2,"repeated heart-style play creates a second independent ongoing power");
        await MiyaTick(Hero.Powers.OfType<ExorcisingStringPower>().Last());Check(MiyaHits.Count==2,"new second power has its own turn receipt");
        MiyaReset();card=NewGarden<ExorcisingStringResoundHbrgoSaki>(1);await NewPlay(card);var original=Hero.GetPower<ExorcisingStringPower>()!;
        Check((bool)Native(card,"PrepareUpgradeEffect",2)!,"a source can later acquire GrassArrow");card.UpgradeInternal();card.FinalizeUpgradeInternal();await NewPlay(card);fresh=MisatoEnemy();await MiyaTick(original);
        Check(new[]{Enemy,fresh}.Count(e=>e.HasPower<FragilePower>())==1,"old power retains its original single-target flags after source upgrade");
        await MiyaTick(Hero.Powers.OfType<ExorcisingStringPower>().Last());Check(Enemy.HasPower<FragilePower>()&&fresh.HasPower<FragilePower>(),"new power independently snapshots the later all-target upgrade");
        MiyaReset();card=NewGarden<ExorcisingStringResoundHbrgoSaki>(4,5);MisatoPermanent(card);fresh=MisatoEnemy();await NewPlay(card);hit=MiyaHits.Single();Equal(hit.Raw,6.42m,"IceFlower107percent per hit");Equal(hit.Hits,5,"IceFlower5hits");Equal(hit.Destruction,2.6m/3m,"IceFlower own destruction2point6");Check(hit.Targets.Length==2,"IceFlower targets all enemies");
        foreach(var enemy in new[]{Enemy,fresh})
        {var down=enemy.GetPower<HbrDefenseDownPower>()!;Equal(MiyaPercent(down),28.5m,"permanent Weakening adds10pp to authored20before attributes exactly once");Check(down.HbrUnlimitedInstances&&Native(down,"get_CurrentEffectProfile")!.ToString()!.Contains("Eternal"),"IceFlower down is an unlimited-slot eternal instance");Equal((decimal)Native(enemy.GetPower<HbrRecoveryDownPower>()!,"get_EffectPotency")!,100m,"IceFlower ordinary recovery is fully disabled");}
        await NewPlay(card);Check(MiyaHits.Count==2&&Hero.Powers.OfType<ExorcisingStringPower>().Count()==2,"every repeated play performs IceFlower and adds its own ongoing power");
        Check(new[]{Enemy,fresh}.All(enemy=>enemy.Powers.OfType<HbrDefenseDownPower>().Count()==1&&enemy.Powers.OfType<HbrRecoveryDownPower>().Count()==1),"own eternal pair refreshes rather than stacking on repeated play");
        await MiyaWeakeningCacheCase(card);
        MiyaReset();card=NewGarden<ExorcisingStringResoundHbrgoSaki>(1,2,3);await NewPlay(card);power=Hero.GetPower<ExorcisingStringPower>()!;Set(Enemy,"_currentHp",0);rng=RngCount(RunState.Rng.CombatTargets);await MiyaTick(power);Check(MiyaHits.Count==0&&RngCount(RunState.Rng.CombatTargets)==rng,"no living enemy produces no arrow or selection RNG");
        Check(ModelDb.Power<ExorcisingStringPower>().Description.GetFormattedText().Length>0,"canonical ongoing-power tooltip does not read an owner");
    }
    static async Task MiyaWeakeningCacheCase(ExorcisingStringResoundHbrgoSaki card)
    {
        Check((bool)Call("KiryuExorcisingStringCombatHelper","HasPermanentWeakening",Player)!,"permanent Weakening cache begins enabled");
        var relic=(ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().ToMutable();Set(relic,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(relic).ToList());
        Call("ThirtyGProposalState","RestoreIdentity",card,"miya-cache-identity");
        string Json(bool released)=>JsonSerializer.Serialize(new{InBattle=true,Cards=new Dictionary<string,object>{{"miya-cache-identity",new{GraduatedRemaining=1,Released=released}}}});
        Native(relic,"SetProposalData",Json(false));Call("ThirtyGProposalState","Notify",card);Check(!(bool)Call("KiryuExorcisingStringCombatHelper","HasPermanentWeakening",Player)!,"graduation Changed invalidates the cached passive without a deck mutation");
        Native(relic,"SetProposalData",Json(true));Call("ThirtyGProposalState","Notify",card);Check((bool)Call("KiryuExorcisingStringCombatHelper","HasPermanentWeakening",Player)!,"release Changed restores permanent Weakening without a deck mutation");
    }
    public static void RunMiya(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".miya74");
            foreach(Type type in new[]{typeof(ExorcisingStringResoundHbrgoSaki),typeof(ExorcisingStringPower),typeof(FragilePower),typeof(HbrDefenseDownPower),typeof(HbrAttackDownPower),typeof(HbrRecoveryDownPower),typeof(ThirtyGCollectionRelic),typeof(HBRGOSakiMod.Core.Models.Monsters.SmallHopper)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string patch in new[]{"HbrEffectApplicationPatch","HbrFragileApplyPatch","HbrFragileStackingPatch","HighHitDamageAttackScopePatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+patch,true)!).Patch().Count>0,"installs "+patch);
            void Patch(MethodBase target,string name)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(name,F)!){priority=Priority.First+100});
            foreach(Type t in new[]{RunState.GetType(),typeof(CombatState)})Patch(t.GetMethod("IterateHookListeners",F)!,nameof(MisatoListeners));
            h.Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MiyaAttackLeaf),F)!){priority=Priority.Last});
            Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,nameof(FormPowerIconLeaf));Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));
            foreach(var wait in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))Patch(wait,nameof(VisualTask));
            MiyaCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real74card/independent power instances/native AfterEnergyReset/common fragility and percent-debuff application/side-end removal/high-hit attack identity/eternal refresh/72Changed passive cache. Controlled terminal attack/history/animation leaves; synchronized CombatTargets RNG runs. True coordinator entry is additionally covered by personal-immediate-turns. No Godot/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Miya checks");
    }
}
