using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

static class IchikoStandalone { public static void Run(string path)=>EffectChecks.RunIchiko(path); }
static partial class EffectChecks
{
    static int NameDraws,NameChoices;
    static CardModel? NameSelected;
    static bool IchikoDrawLeaf(decimal count,ref Task<IEnumerable<CardModel>> __result)
    {NameDraws+=(int)count;__result=Task.FromResult(Enumerable.Empty<CardModel>());return false;}
    static bool IchikoChoiceLeaf(IReadOnlyList<CardModel> cardsIn,ref Task<IEnumerable<CardModel>> __result)
    {Check(cardsIn.Count>0&&cardsIn.All(c=>c is IThirtyOneEMemberCard),"name grid contains only actual31Ecards");NameChoices++;__result=Task.FromResult<IEnumerable<CardModel>>([NameSelected??cardsIn[0]]);return false;}
    static bool IchikoMoveLeaf(CardModel card,[HarmonyArgument(1)] PileType newPileType,ref Task<CardPileAddResult> __result)
    {
        // The selection/upgrade/resource logic remains real; isolate the
        // scene-animated add leaf while using native pile mutation containers.
        Check(newPileType==PileType.Hand,"chosen31Eis requested into hand");card.Pile!.RemoveInternal(card,true);Player.PlayerCombatState!.Hand.AddInternal(card);
        __result=Task.FromResult(Blank<CardPileAddResult>());return false;
    }
    static void IchikoReset()
    {NewReset();NameDraws=NameChoices=0;NameSelected=null;MisatoEnergy.Clear();}
    static decimal Vitality()
    {object?[] args=[Hero,null];Check((bool)Call("HbrBattleStatsHelper","TryGetStats",args)!,"hero has HBR attributes");return (decimal)args[1]!.GetType().GetProperty("Vitality")!.GetValue(args[1])!;}
    static async Task IchikoCoreCases()
    {
        IchikoReset();var card=NewGarden<EldestOshimaIsHereHbrgoSaki>();await NewPlay(card);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),0,"eldest base card costs0");Check(!card.Keywords.Contains(CardKeyword.Exhaust),"eldest base card is not exhaust");var defense=Hero.GetPower<HbrDefenseUpPower>()!;Equal((decimal)Native(defense,"get_EffectPotency")!,30m,"base defense-up uses30INTframework");Equal(defense.Amount,1,"base defense-up lasts one enemy turn");Equal(NameDraws,1,"no31Edraw-pile card falls back to one actual draw command");Equal(NameChoices,0,"empty name list bypasses choiceUI");
        IchikoReset();card=NewGarden<EldestOshimaIsHereHbrgoSaki>();var target=NewGarden<EldestOshimaIsHereHbrgoSaki>();target.Pile!.RemoveInternal(target,true);Player.PlayerCombatState!.DrawPile.AddInternal(target);NameSelected=target;await NewPlay(card);Equal(NameChoices,1,"available31Ein draw triggers real selection boundary once");Check(target.Pile?.Type==PileType.Hand,"chosen physical draw-pile card is moved into hand");Equal(NameDraws,0,"choosing31Enever also performs fallbackDraw");
        IchikoReset();card=NewGarden<EldestOshimaIsHereHbrgoSaki>(0,1,3,4);var gauge=NewGauge();var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;var cannon=(Creature)H("Actor",Blank<HotaruContestCannon>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,phantom,cannon});Power<PhantomPower>(phantom,1);await NewPlay(card);
        Check(Hero.HasPower<HbrDefenseUpPower>()&&phantom.HasPower<HbrDefenseUpPower>(),"loving wave covers self and main phantom");Check(!cannon.HasPower<HbrDefenseUpPower>(),"loving wave excludes floating cannon from ally stat buff targets");Equal(Hero.GetPower<HbrDefenseUpPower>()!.Amount,2,"loving wave defense lasts two enemy turns");Equal((decimal)Native(Hero.GetPower<HbrAttackUpPower>()!,"get_EffectPotency")!,65m,"loving wave gives65attack-up once");Equal(gauge.PreciseCharge,0m,"same first play gains50ODthen signpost spends50OD");Equal(MisatoEnergy.Sum(),2m,"signpost grants two actual energy even at zero finalOD");Check(Enemy.HasPower<FragilePower>(),"loyal oath applies actual fragile");var down=Enemy.GetPower<HbrDefenseDownPower>()!;Check(Native(down,"get_CurrentEffectProfile")!.ToString()!.Contains("None")&&Native(down,"get_CurrentEffectProfile")!.ToString()!.Contains("Eternal"),"halo first defense-down is eternal and outside ordinary slots");await NewPlay(card);Equal(Powers(Enemy).OfType<HbrDefenseDownPower>().Count(),1,"later play does not repeat first halo enemy debuff");Equal(MisatoEnergy.Sum(),4m,"later signpost still grants energy with insufficient OD");
    }
    static async Task IchikoPermanentAndVitalityCases()
    {
        IchikoReset();var card=NewGarden<EldestOshimaIsHereHbrgoSaki>(2,5);var aina=NewGarden<DiveAsDeepAsIWantHbrgoSaki>();Equal(aina.EnergyCost.GetWithModifiers(CostModifiers.All),10,"combat-only copy cannot activate permanent Frugal");Equal((decimal)Call("IchikoEldestCombatHelper","GetGoldBonusPercent",Player)!,0m,"combat-only copy cannot activate military pay");MisatoPermanent(card);Equal(aina.EnergyCost.GetWithModifiers(CostModifiers.All),5,"native base-cost hook halves10before any other modifiers");Power<WeakPower>(Enemy,1);Equal(aina.EnergyCost.GetWithModifiers(CostModifiers.All),4,"Aina negative-status discount runs after Frugal base halving");
        foreach(var row in new[]{(3,2),(4,2),(5,2),(7,3),(10,5),(2,2)})Equal((int)Call("IchikoEldestCombatHelper","GetFrugalBaseCost",aina,row.Item1)!,row.Item2,"frugal mapping "+row.Item1+"to"+row.Item2);
        Equal((decimal)Call("IchikoEldestCombatHelper","GetGoldBonusPercent",Player)!,15m,"permanent military pay is15percentagepoints");
        var other=NewGarden<EldestOshimaIsHereHbrgoSaki>(2,5);MisatoPermanent(other);Equal(aina.EnergyCost.GetWithModifiers(CostModifiers.All),4,"duplicate permanent Frugal does not halve twice");Equal((decimal)Call("IchikoEldestCombatHelper","GetGoldBonusPercent",Player)!,15m,"duplicate permanent pay does not stack");
        var manual=State<ElectronicSoldierManual>("Manual");decimal before=manual.ModifyGoldGained(Player,100m);Check(before>=115m,"actual manual gold hook includes military-pay additive share");
        typeof(ElectronicSoldierManual).GetProperty("Talent",F)!.SetValue(manual,10);
        MethodInfo gold=typeof(Hook).GetMethods(F).Single(m=>m.Name=="ModifyGoldGained");
        object?[] goldArgs=[RunState,Combat,100m,Player,null];decimal goldResult=(decimal)Invoke(gold,null,goldArgs)!;
        Equal(goldResult,115m,"native Hook gold keeps military15 while Talent no longer changes gold");
        card.DowngradeInternal();other.DowngradeInternal();Equal(aina.EnergyCost.GetWithModifiers(CostModifiers.All),9,"downgrade invalidates cachedFrugal without a pile change");Equal((decimal)Call("IchikoEldestCombatHelper","GetGoldBonusPercent",Player)!,0m,"downgrade invalidates cached pay flags");
        Check((bool)Native(card,"PrepareUpgradeEffect",2)!,"permanent cached card accepts Frugal re-upgrade");card.UpgradeInternal();card.FinalizeUpgradeInternal();Equal(aina.EnergyCost.GetWithModifiers(CostModifiers.All),4,"upgrade invalidates and rebuilds permanent policy cache");
        Player.Deck.RemoveInternal(card,true);Equal(aina.EnergyCost.GetWithModifiers(CostModifiers.All),9,"native permanent pile ContentsChanged invalidates Frugal after supplier removal");
        FormReset(false);card=NewGarden<EldestOshimaIsHereHbrgoSaki>(2,5);MisatoPermanent(card);aina=NewGarden<DiveAsDeepAsIWantHbrgoSaki>();Equal((int)Call("IchikoEldestCombatHelper","GetFrugalBaseCost",aina,10)!,10,"legacy numeric mode does not inherit new permanent Frugal");Equal((decimal)Call("IchikoEldestCombatHelper","GetGoldBonusPercent",Player)!,0m,"legacy numeric mode does not inherit new military pay");
        IchikoReset();card=NewGarden<EldestOshimaIsHereHbrgoSaki>(6);decimal initial=Vitality();await NewPlay(card);Equal(Vitality(),initial*1.15m,"real derivedVitality includes current15percent snapshot");await NewPlay(card);Equal(Vitality(),initial*1.3225m,"second15percent snapshots115percentcurrent, producing132point25");var powers=Powers(Hero).OfType<IchikoVitalityUpPower>().ToArray();Check(powers.Length==2,"two current vitality gains are independent framework instances");foreach(var power in powers)await power.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,[Enemy]);Equal(Vitality(),initial*1.3225m,"one enemy end preserves both two-turn gains");foreach(var power in powers)await power.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,[Enemy]);Equal(Vitality(),initial,"second enemy end removes both gains without permanent rollback drift");
    }
    public static void RunIchiko(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".ichiko65");
            foreach(Type type in new[]{typeof(HBRGOSakiMod.Core.Models.Relics.Overclock),typeof(EldestOshimaIsHereHbrgoSaki),typeof(DiveAsDeepAsIWantHbrgoSaki),typeof(HbrDefenseUpPower),typeof(HbrDefenseDownPower),typeof(HbrAttackUpPower),typeof(IchikoVitalityUpPower),typeof(FragilePower),typeof(PhantomPower),typeof(WeakPower),typeof(SmallHopper)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string name in new[]{"HbrEffectApplicationPatch","HbrFragileApplyPatch","HbrFragileStackingPatch","IchikoFrugalBaseCostPatch","IchikoMilitaryPayPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs "+name);
            foreach(Type listeners in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(listeners.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoListeners),F)!){priority=Priority.First+100});
            h.Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.GainEnergy))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoEnergyLeaf),F)!));
            h.Patch(typeof(CardPileCmd).GetMethods(F).Single(m=>m.Name=="Draw"&&m.GetParameters().Length==4),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(IchikoDrawLeaf),F)!));
            h.Patch(typeof(CardSelectCmd).GetMethods(F).Single(m=>m.Name=="FromSimpleGrid"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(IchikoChoiceLeaf),F)!));
            h.Patch(typeof(CardPileCmd).GetMethods(F).Single(m=>m.Name=="Add"&&m.GetParameters()[0].ParameterType==typeof(CardModel)&&m.GetParameters()[1].ParameterType==typeof(PileType)),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(IchikoMoveLeaf),F)!));
            h.Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            foreach(var method in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            IchikoCoreCases().GetAwaiter().GetResult();IchikoPermanentAndVitalityCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual card upgrade/buff/OD/native energy cost/Gold manual/derived-stat logic; hand grid selection and scene-animated pile-add/Draw/energy terminal leaves controlled. No Godot/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Ichiko checks");
    }
}

