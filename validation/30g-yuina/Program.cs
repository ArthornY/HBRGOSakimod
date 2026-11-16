using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class YuinaStandalone { public static void Run(string path) => EffectChecks.RunYuina(path); }
static partial class EffectChecks
{
    sealed record YuinaHit(decimal Raw,int Hits,decimal Destruction,decimal Hp,bool AttackUp,int DiceBefore);
    static readonly List<YuinaHit> YuinaHits=[];
    static readonly List<decimal> YuinaEnergy=[];
    static RollGlossaryCapability? YuinaRoll;
    static bool YuinaEnergyLeaf(decimal amount,Player player,ref Task __result)
    {Check(player==Player,"Purge energy belongs to the card owner");YuinaEnergy.Add(amount);__result=Task.CompletedTask;return false;}
    static bool YuinaAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=YuinaAttack(__instance);return false;}
    static async Task<AttackCommand> YuinaAttack(AttackCommand command)
    {
        var card=(NoblesseObligeHbrgoSaki)command.ModelSource!;
        YuinaRoll?.EnsureCurrentRollFor(card);
        await (Task)MisatoHook("BeforeAttack",command)!;
        YuinaHits.Add(new((decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!,
            (int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!,
            (decimal)Call("NoblesseObligeCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,
            (decimal)Call("NoblesseObligeCombatHelper","GetHpDamageMultiplier",card,Enemy)!,
            Hero.HasPower<HbrAttackUpPower>(),UnifiedRawReads));
        var rows=(List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!;
        rows.Add(new(){new DamageResult(Enemy,ValueProp.Move)});
        await (Task)MisatoHook("AfterAttack",command)!;return command;
    }
    static void YuinaReset()
    {NewReset();YuinaRoll=DesuwaPipeline=null;YuinaEnergy.Clear();YuinaHits.Clear();UnifiedDice();UnifiedRawFailure=null;}
    static PileType YuinaResultPile(CardModel card)
    {
#if HBR_BETA_NATIVE
        return ((CardLocation)Native(card,"GetResultLocationForCardPlay")!).pileType;
#else
        return (PileType)Native(card,"GetResultPileTypeForCardPlay")!;
#endif
    }
    static void YuinaRegister(CardModel card,CardModel? physical=null)
    {
        Set(card,"_owner",Player);if(physical!=null)card.DeckVersion=physical;
        Player.PlayerCombatState!.Hand.AddInternal(card);
        ((List<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Add(card);
    }
    static BigInteger YuinaGrowth(NoblesseObligeHbrgoSaki card)=>string.IsNullOrEmpty(card.CourageData)?0:BigInteger.Parse(card.CourageData);
    static async Task YuinaCases()
    {
        YuinaReset();var card=NewGarden<NoblesseObligeHbrgoSaki>();await NewPlay(card);var hit=YuinaHits.Single();
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),3,"base cost3");Equal(hit.Raw,51.3m,"855percent raw coefficient");Equal(hit.Hits,1,"base single hit");
        Equal(hit.Destruction,1.2m,"base destruction3point6 normalized by shared3");Equal(hit.Hp,1.5m,"HP specialization adds50percent");
        Check(YuinaResultPile(card)==PileType.Exhaust,"native result rule exhausts the base card");
        Power<HighDimensionalShellPower>(Enemy,100);Equal((decimal)Call("NoblesseObligeCombatHelper","GetHpDamageMultiplier",card,Enemy)!,1m,"live DP target gets no HP specialization");
        Equal((decimal)Call("NoblesseObligeCombatHelper","GetHpDamageMultiplier",ModelDb.Card<NoblesseObligeHbrgoSaki>(),Enemy)!,1m,"canonical preview safely reads no owner");
        YuinaReset();card=NewGarden<NoblesseObligeHbrgoSaki>(0,1,2,3);await NewPlay(card);hit=YuinaHits.Single();
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),2,"Flash lowers cost to2");Check(hit.AttackUp,"ShameBrand is applied before its own attack");Equal(hit.Destruction,6m,"ThunderJudgment destruction18 normalized");
        Check(YuinaResultPile(card)==PileType.Exhaust,"selected upgrades never remove exhaust");
        var rate=Hero.GetPower<HbrCriticalRateUpPower>()!;var damage=Hero.GetPower<HbrCriticalDamageUpPower>()!;
        Equal((decimal)Native(rate,"get_EffectPotency")!,100m,"AzureIris rate100 uses common INT formula");Equal((decimal)Native(damage,"get_EffectPotency")!,130m,"AzureIris damage130 uses framework");
        Check(Native(rate,"get_CurrentEffectProfile")!.ToString()!.Contains("Only")&&(int)Native(rate,"get_EffectRemaining")! ==5,"AzureIris is Only and five player-side turns");
        await NewPlay(card);Check(Hero.Powers.OfType<HbrCriticalRateUpPower>().Count()==1&&Hero.Powers.OfType<HbrCriticalDamageUpPower>().Count()==1,"repeat play refreshes Only pair without a second copy");
        var peer=SupportTeammate();var phantom=(Creature)H("Actor",Blank<HBRGOSakiMod.Core.Models.Monsters.SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);
        Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature,phantom});await NewPlay(card);
        Check(peer.Creature.HasPower<HbrCriticalRateUpPower>()&&phantom.HasPower<HbrCriticalDamageUpPower>(),"AzureIris reaches the living teammate and actual main phantom");
        YuinaReset();card=NewGarden<NoblesseObligeHbrgoSaki>(4);YuinaRoll=DesuwaAttach(card);UnifiedDice(10,51);await NewPlay(card);
        Check(YuinaHits.Single().DiceBefore==1&&UnifiedRawReads==2,"Purge requests a fresh ordinary die only after the attack");Equal(YuinaEnergy.Sum(),1m,"new final51 grants one energy");Equal(YuinaRoll.CurrentRoll,10,"additional roll never overwrites original attack roll");
        YuinaReset();card=NewGarden<NoblesseObligeHbrgoSaki>(4);YuinaRoll=DesuwaAttach(card);UnifiedDice(90,50);await NewPlay(card);Equal(YuinaEnergy.Count,0,"new50grants no energy even though the attack rolled90");
        YuinaReset();card=NewGarden<NoblesseObligeHbrgoSaki>(4);YuinaRoll=DesuwaAttach(card);await UnifiedApply(false,50,3);await UnifiedApply(true,1,3);UnifiedDice(10,20,30,40);await NewPlay(card);
        Equal(YuinaEnergy.Sum(),1m,"additional advantage winner40with50percent bonus becomes60");Equal(UnifiedRawReads,4,"attack and additional die each independently take their one advantage die");
        Equal(UnifiedUses(UnifiedPowers().Single()),2,"additional roll consumes only its own bonus use");Equal(UnifiedUses(UnifiedPowers(true).Single()),2,"additional roll consumes only its own advantage use");
        Equal(YuinaRoll.CurrentRoll,30,"original attack's modified advantage result remains published");await UnifiedFinishCardRoll(YuinaRoll,card);
        Equal(UnifiedUses(UnifiedPowers().Single()),1,"finishing original play consumes its preserved bonus reservation once");Equal(UnifiedUses(UnifiedPowers(true).Single()),1,"finishing original play consumes its preserved advantage reservation once");
        YuinaReset();card=NewGarden<NoblesseObligeHbrgoSaki>(4);YuinaRoll=DesuwaAttach(card);var strengthen=Power<StrengthenPower>(Hero,3);UnifiedDice(10,20,30,50);await NewPlay(card);
        Equal(strengthen.Amount,2,"additional normal die consumes just its one legacy Strengthen charge");Equal((int)Native(strengthen,"get_ReservedRollCount")!,1,"original Strengthen reservation remains owned by the attack");
        await UnifiedFinishCardRoll(YuinaRoll,card);Equal(strengthen.Amount,1,"ordinary cleanup later consumes its original Strengthen once");
        YuinaReset();card=NewGarden<NoblesseObligeHbrgoSaki>(4);YuinaRoll=DesuwaAttach(card);UnifiedDice(10,100);await NewPlay(card);
        Check(Hero.GetPowerAmount<SwordheartPower>()>0,"the additional raw critical really runs the normal Swordheart reward");
        Equal(YuinaRoll.CurrentRoll,10,"critical additional reward still leaves the original card roll intact");
        await YuinaGrowthCases();
    }
    static async Task YuinaGrowthCases()
    {
        YuinaReset();var card=NewGarden<NoblesseObligeHbrgoSaki>();MisatoPermanent(card);var target=FormCard<StrikeIronclad>();
        Call("NoblesseObligeCombatHelper","OnProposal",Player,target);Check(YuinaGrowth(card).IsZero,"proposals before Courage never give retroactive growth");
        Check((bool)Native(card,"PrepareUpgradeEffect",6)!,"Courage can be acquired later");card.UpgradeInternal();card.FinalizeUpgradeInternal();
        var ordinary=(NoblesseObligeHbrgoSaki)card.MutableClone();YuinaRegister(ordinary,card);var od=(NoblesseObligeHbrgoSaki)card.MutableClone();YuinaRegister(od,card);
        Call("NoblesseObligeCombatHelper","OnProposal",Player,target);Check(YuinaGrowth(card)==40&&YuinaGrowth(ordinary)==40&&YuinaGrowth(od)==40,"one confirmed proposal grants physical40pp once and synchronizes ordinary/OD mirrors");
        Equal(card.DynamicVars.Damage.BaseValue,53.7m,"895percent coefficient is actual raw damage");
        var copy=(NoblesseObligeHbrgoSaki)ordinary.MutableClone();Call("CardCopyContextHelper","RecordCopy",copy);YuinaRegister(copy);
        var preview=(NoblesseObligeHbrgoSaki)card.MutableClone();
        Call("NoblesseObligeCombatHelper","OnProposal",Player,target);Check(YuinaGrowth(card)==80&&YuinaGrowth(copy)==80&&YuinaGrowth(preview)==40,"temporary live copy grows independently while a detached preview never grows");
        Native(copy,"SetGrowth",new BigInteger(999));Check(YuinaGrowth(card)==80,"temporary copy growth never writes back to permanent origin");
        var huge=BigInteger.Pow(10,50);Native(card,"SetGrowth",huge);Call("NoblesseObligeCombatHelper","OnProposal",Player,target);
        Check(YuinaGrowth(card)==huge+40,"exact permanent growth has no decimal/native layer cap");
        var cloned=(NoblesseObligeHbrgoSaki)card.MutableClone();Check(YuinaGrowth(cloned)==huge+40,"native MutableClone preserves all unbounded growth digits");
        Type native=typeof(SavedProperties);Type? cache=native.Assembly.GetTypes().SingleOrDefault(t=>t.Name=="SavedPropertiesTypeCache");
        if(cache?.GetMethod("InjectTypeIntoCache",F)is{}inject)Invoke(inject,null,typeof(NoblesseObligeHbrgoSaki));
        else{cache=native.Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;Invoke(cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!,null,typeof(NoblesseObligeHbrgoSaki));cache.GetField("_initialized",F)!.SetValue(null,true);}
        var options=new JsonSerializerOptions{IncludeFields=true};string json=JsonSerializer.Serialize(SavedProperties.From(card),options);
        var restored=NewGarden<NoblesseObligeHbrgoSaki>();JsonSerializer.Deserialize<SavedProperties>(json,options)!.Fill(restored);Native(restored,"AfterDeserialized");Check(YuinaGrowth(restored)==huge+40,"native SavedProperties JSON/Fill retains exact unbounded Courage");
        await card.BeforeCombatStart();Check(YuinaGrowth(card)==huge+40,"new combat never clears permanent Courage growth");
        card.DowngradeInternal();Check(YuinaGrowth(card)==huge+40,"downgrade keeps already earned permanent growth");
        YuinaReset();card=NewGarden<NoblesseObligeHbrgoSaki>(5);Check(!(bool)Call("NoblesseObligeCombatHelper","HasMoonlight",Player)!,"Moonlight outside permanent deck does not enable proposal selection");MisatoPermanent(card);Check((bool)Call("NoblesseObligeCombatHelper","HasMoonlight",Player)!,"permanent Moonlight enables the72selection policy");
    }
    public static void RunYuina(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".yuina73");
            foreach(Type type in new[]{typeof(NoblesseObligeHbrgoSaki),typeof(HbrAttackUpPower),typeof(HbrCriticalRateUpPower),typeof(HbrCriticalDamageUpPower),typeof(HighDimensionalShellPower),typeof(SwordheartPower),typeof(StrikeIronclad),typeof(StrengthenPower),typeof(PhantomPower),typeof(HBRGOSakiMod.Core.Models.Monsters.SmallHopper)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrEffectApplicationPatch",true)!).Patch().Count>0,"installs real common effect application");
            void Patch(MethodBase target,string name)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(name,F)!){priority=Priority.First+100});
            foreach(Type t in new[]{RunState.GetType(),typeof(CombatState)})Patch(t.GetMethod("IterateHookListeners",F)!,nameof(MisatoListeners));
            Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,nameof(YuinaAttackLeaf));Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.GainEnergy))!,nameof(YuinaEnergyLeaf));
            Patch(typeof(RollGlossaryCapability).GetMethod("HasEffectiveRollKeyword",F)!,nameof(DesuwaKeyword));Patch(typeof(RollGlossaryCapability).GetMethod("NextRawRoll",F)!,nameof(UnifiedFixedRaw));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,nameof(FormPowerIconLeaf));
            foreach(var wait in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))Patch(wait,nameof(VisualTask));
            IsolateCenterVisuals();YuinaCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real73card upgrades/OnPlay, common buff framework, native result-pile rule, Ritsu capability lookup and full normal additional-roll formula/reservations, MutableClone/SavedProperties/physical growth; fixed raw-die, terminal attack results and energy animation writes controlled.72confirmed proposal callback tested directly; its UI/transport belongs to72suite. No Godot/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Yuina checks");
    }
}
