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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock = HBRGOSakiMod.Core.Models.Relics.Overclock;

static class MinoriStandalone { public static void Run(string path)=>EffectChecks.RunMinori(path); }
static partial class EffectChecks
{
    sealed record MinoriHit(decimal Raw,int Hits,decimal Conditional,decimal Destruction,bool Eye,decimal Rate,decimal Crit);
    static readonly List<MinoriHit> MinoriHits=[];
    static readonly List<decimal> MinoriLosses=[];
    static int MinoriDraws;
    static bool MinoriDrawLeaf(decimal count,ref Task<IEnumerable<CardModel>> __result)
    {MinoriDraws+=(int)count;__result=Task.FromResult(Enumerable.Empty<CardModel>());return false;}
    static bool MinoriLossLeaf(decimal amount,Player player,ref Task __result)
    {Check(player==Player,"energy debt only belongs to the card owner");MinoriLosses.Add(amount);__result=Task.CompletedTask;return false;}
    static void MinoriReset()
    {NewReset();MinoriHits.Clear();MinoriLosses.Clear();MinoriDraws=0;var table=Helper("MinoriDeliveryCombatHelper").GetField("Battles",F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);}
    static bool MinoriAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=MinoriResolve(__instance);return false;}
    static async Task<AttackCommand> MinoriResolve(AttackCommand command)
    {
        CardModel card=(CardModel)command.ModelSource!;await (Task)MisatoHook("BeforeAttack",command)!;
        int hits=(int)decimal.Floor((decimal)MisatoHook("ModifyAttackHitCount",command,(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!)!);
        MinoriHits.Add(new((decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!,hits,
            (decimal)Call("MinoriDeliveryCombatHelper","GetConditionalDamageMultiplier",card,Hero,Enemy,ValueProp.Move)!,
            (decimal)Call("MinoriDeliveryCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,Hero.HasPower<MindsEyePower>(),
            Hero.Powers.OfType<HbrCriticalRateUpPower>().Select(p=>(decimal)Native(p,"get_EffectPotency")!).DefaultIfEmpty(0).Max(),
            Hero.Powers.OfType<HbrCriticalDamageUpPower>().Select(p=>(decimal)Native(p,"get_EffectPotency")!).DefaultIfEmpty(0).Max()));
        var rows=(List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!;
        foreach(var enemy in Combat.HittableEnemies)rows.Add(Enumerable.Range(0,hits).Select(_=>new DamageResult(enemy,ValueProp.Move)).ToList());
        await (Task)MisatoHook("AfterAttack",command)!;return command;
    }
    static async Task MinoriEnergyReset()
    {
        MethodInfo method=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterEnergyReset");
        object?[] args=method.GetParameters().Select(p=>p.ParameterType.IsInstanceOfType(Combat)?(object)Combat:
            p.ParameterType==typeof(Player)?Player:p.ParameterType.IsInstanceOfType(RunState)?RunState:throw new Exception(p.ToString())).ToArray();
        await (Task)Invoke(method,null,args)!;
    }
    static PileType MinoriResultPile(CardModel card)
    {
#if HBR_BETA_NATIVE
        return ((CardLocation)Native(card,"GetResultLocationForCardPlay")!).pileType;
#else
        return (PileType)Native(card,"GetResultPileTypeForCardPlay")!;
#endif
    }
    static async Task MinoriCases()
    {
        MinoriReset();var card=NewGarden<YourCombatDeliveryIsHereHbrgoSaki>();await NewPlay(card);var hit=MinoriHits.Single();Equal(hit.Raw,3.3m,"Minori base55percent raw perhit");Equal(hit.Hits,2,"Minori base two hits");Equal(hit.Conditional,1.3m,"target with no DP gets30percent HP specialization");Equal(hit.Destruction,1m/3m,"Minori authored destruction1");Check(MinoriResultPile(card) == PileType.Discard,"real native result-pile rule is ordinary discard, never draw-top");
        typeof(CardModel).GetProperty("ExhaustOnNextPlay",F)!.SetValue(card,true);
        Check(MinoriResultPile(card)==PileType.Exhaust,"external next-play exhaust still follows the native result rule");
        Check(MinoriResultPile(card)==PileType.Discard,"native one-play exhaust is consumed without a top-deck override");
        Power<HighDimensionalShellPower>(Enemy,100);Equal((decimal)Call("MinoriDeliveryCombatHelper","GetConditionalDamageMultiplier",card,Hero,Enemy,ValueProp.Move)!,1m,"target currently retaining DP gets no HP bonus");
        MinoriReset();card=NewGarden<YourCombatDeliveryIsHereHbrgoSaki>(0,1,5);await NewPlay(card);hit=MinoriHits.Single();Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"Flash lowers native1cost to0");Equal(hit.Raw,23.76m,"Hurricane changes skill to396percent");Equal(hit.Hits,1,"Hurricane single hit");Equal(hit.Destruction,2.2m/3m,"Hurricane destruction2point2");Equal(MinoriDraws,1,"Newspaper invokes one real draw command after attack");Check(Hero.HasPower<MinoriNextTurnEnergyDebtPower>(),"Hurricane gives a real future energy-debt status");await NewPlay(card);Equal(Hero.GetPower<MinoriNextTurnEnergyDebtPower>()!.Amount,2,"same-turn hurricane debts stack");await MinoriEnergyReset();Equal(MinoriLosses.Count,0,"reset in same native own turn cannot prematurely pay future debt");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);using(MisatoExtra(true))await MinoriEnergyReset();Equal(MinoriLosses.Count,0,"OD energy reset leaves ordinary-turn debt pending");using(MisatoExtra(false))await MinoriEnergyReset();Equal(MinoriLosses.Count,0,"personal extra-turn reset also leaves ordinary-turn debt pending");await MinoriEnergyReset();Equal(MinoriLosses.Sum(),2m,"next ordinary own-turn reset settles both charges once");Check(!Hero.HasPower<MinoriNextTurnEnergyDebtPower>(),"settled energy debt is removed");await MinoriEnergyReset();Equal(MinoriLosses.Sum(),2m,"another reset cannot charge the same debt twice");
        MinoriReset();card=NewGarden<YourCombatDeliveryIsHereHbrgoSaki>(2,3,6);var gauge=NewGauge();var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,phantom});Power<PhantomPower>(phantom,1);await NewPlay(card);hit=MinoriHits.Single();Check(hit.Eye,"first play grants mindseye before its own attack");Equal(hit.Rate,40m,"firstredleaf uses40critical-rate basis at100INT");Equal(hit.Crit,50m,"firstredleaf uses50critical-damage basis");Equal(gauge.PreciseCharge,0m,"insufficient OD still grants critical buffs while clamping gauge0");Check(phantom.HasPower<MindsEyePower>()&&phantom.HasPower<HbrCriticalRateUpPower>(),"all-party support reaches the main phantom");Equal(phantom.GetPower<HbrCriticalRateUpPower>()!.Amount,1,"party critical effect grants one attack use");Check(Native(phantom.GetPower<HbrCriticalRateUpPower>()!,"get_CurrentEffectProfile")!.ToString()!.Contains("Count"),"redleaf critical lifetime counts attacks, not turns");Equal(Hero.GetPower<HbrMoralePower>()!.Amount,1,"first play grants whole-party one morale level");await NewPlay(card);Check(!MinoriHits.Last().Eye,"same turn second play does not regrant first-play mindseye");Equal(Hero.GetPower<HbrMoralePower>()!.Amount,1,"same turn second play does not regrant morale");Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await NewPlay(card);Check(MinoriHits.Last().Eye,"new native own turn renews first-play mindseye receipt");Equal(Hero.GetPower<HbrMoralePower>()!.Amount,2,"new turn first play grants another morale level");
        MinoriReset();gauge=NewGauge();card=NewGarden<YourCombatDeliveryIsHereHbrgoSaki>(4);MisatoPermanent(card);await (Task)Call("MinoriDeliveryCombatHelper","OnCombatStart",Combat)!;Equal(gauge.PreciseCharge,10m*gauge.ChargePerLevel/100m,"permanent Ollie gives10percent level at battle start even undrawn");await (Task)Call("MinoriDeliveryCombatHelper","OnCombatStart",Combat)!;Equal(gauge.PreciseCharge,10m*gauge.ChargePerLevel/100m,"repeated battle-start/OD mirror entry does not repeat Ollie");
        Check(ModelDb.Power<MinoriNextTurnEnergyDebtPower>().Description.GetFormattedText().Length>0,"canonical future-debt tooltip formats safely without reading an owner");
    }
    public static void RunMinori(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".minori68");
            foreach(Type type in new[]{typeof(YourCombatDeliveryIsHereHbrgoSaki),typeof(MinoriNextTurnEnergyDebtPower),typeof(MindsEyePower),typeof(HbrCriticalRateUpPower),typeof(HbrCriticalDamageUpPower),typeof(HbrMoralePower),typeof(HighDimensionalShellPower),typeof(PhantomPower),typeof(SmallHopper),typeof(Overclock)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string name in new[]{"HbrEffectApplicationPatch","HbrComboVirtualHitPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs "+name);
            foreach(Type listeners in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(listeners.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoListeners),F)!){priority=Priority.First+100});
            h.Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MinoriAttackLeaf),F)!));
            h.Patch(typeof(CardPileCmd).GetMethods(F).Single(m=>m.Name=="Draw"&&m.GetParameters().Length==4),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MinoriDrawLeaf),F)!));
            h.Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.LoseEnergy))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MinoriLossLeaf),F)!));
            h.Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            foreach(var method in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            MinoriCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real card upgrades, normal native result-pile rule, framework attack/crit/mindseye/morale listeners, native AfterEnergyReset and real debt removal; attack animations, energy write and Draw terminal leaves controlled. No Godot/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Minori checks");
    }
}
