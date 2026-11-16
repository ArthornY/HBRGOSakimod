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
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock=HBRGOSakiMod.Core.Models.Relics.Overclock;
static class MaruyamaStandalone{public static void Run(string path)=>EffectChecks.RunMaruyama(path);}
static partial class EffectChecks
{
    internal static readonly List<int> MPaid=[];
    sealed record MAttack(decimal Raw,int Hits,bool Eye,int Combo,int DpDown,int Down,int Morale);
    static readonly List<MAttack> MAttacks=[];
    static readonly List<(decimal Destruction,decimal DpFactor,decimal DpDownFactor,decimal DownFactor,decimal Final)> MSegments=[];
    static readonly List<CardModel[]> MChoices=[]; static CardModel? MSelected; static bool MFailAttack;
    static void MReset(bool hbr=true,int kinds=0)
    {FReset(kinds,hbr);Player.MaxEnergy=3;MPaid.Clear();MAttacks.Clear();MSegments.Clear();MChoices.Clear();MSelected=null;MFailAttack=false;ExtraServingProbe.Plays=0;Power<ExtraServingHookPower>(Hero,1);}
    static LetMeShowYouClassHbrgoSaki MCard(params int[] upgrades)=>NewGarden<LetMeShowYouClassHbrgoSaki>(upgrades);
    static int MDebt(Player? owner=null)=>(owner??Player).Creature.GetPowerAmount<HbrEnergyDebtPower>();
    static async Task MResetEnergy(Player? owner=null,bool inserted=false)
    {owner??=Player;owner.PlayerCombatState!.ResetEnergy();if(inserted)await (Task)Call("HbrInsertedTurnStartHelper","Start",new ThrowingPlayerChoiceContext(),owner)!;else await Hook.AfterEnergyReset(Combat,owner);}
    static bool MAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=MResolveAttack(__instance);return false;}
    static async Task<AttackCommand> MResolveAttack(AttackCommand command)
    {
        await Hook.BeforeAttack(Combat,command);
        decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!;
        int hits=(int)Hook.ModifyAttackHitCount(Combat,command,(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!);
        MAttacks.Add(new(raw,hits,Hero.HasPower<MindsEyePower>(),Hero.GetPowerAmount<HbrComboMediumPower>(),Enemy.GetPowerAmount<HbrDpDefenseDownPower>(),Enemy.GetPowerAmount<HbrDefenseDownPower>(),Hero.GetPowerAmount<HbrMoralePower>()));
        if(MFailAttack)throw new InvalidOperationException("81 controlled failed main attack");
        for(int i=0;i<hits;i++)
        {
            CardModel card=(CardModel)command.ModelSource!;
            var results=(await Command(command.Attacker,raw,ValueProp.Move,card,Combat.HittableEnemies)).ToArray();
            command.AddResultsInternal(results);
            foreach(DamageResult result in results)await NewGiven(card,result);
        }
        await Hook.AfterAttack(Combat,new ThrowingPlayerChoiceContext(),command);return command;
    }
    static bool MDamageLeaf(object[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {__result=MResolveDamage(__args);return false;}
    static async Task<IEnumerable<DamageResult>> MResolveDamage(object[] args)
    {
        CardModel? card=args[5]as CardModel;Creature? dealer=args[4]as Creature;ValueProp props=(ValueProp)args[3];
        decimal destruction=1,dp=1,dpdown=1,down=1;
        if(card is LetMeShowYouClassHbrgoSaki)
        {
            destruction=(decimal)Call("MaruyamaStyleCombatHelper","GetDestructionGainMultiplier",card,Enemy,dealer,props)!;
            dp=(decimal)Call("MaruyamaStyleCombatHelper","GetDpDamageMultiplier",card,Enemy,dealer,props)!;
            if(Enemy.GetPower<HbrDpDefenseDownPower>()is{} d)dpdown=d.ModifyDamageMultiplicative(Enemy,1m,ValueProp.Move,dealer!,card);
            if(Enemy.GetPower<HbrDefenseDownPower>()is{} n)down=n.ModifyDamageMultiplicative(Enemy,1m,ValueProp.Move,dealer!,card);
        }
        var results=await FDamageBody(args);
        if(card is LetMeShowYouClassHbrgoSaki)MSegments.Add((destruction,dp,dpdown,down,FDamage.Last().Final));return results;
    }
    static void MSkipPileVisuals(ref bool skipVisuals)=>skipVisuals=true;
    static bool MGrid(object[] __args,ref Task<IEnumerable<CardModel>> __result)
    {CardModel[] choices=__args.OfType<IEnumerable<CardModel>>().Single().ToArray();MChoices.Add(choices);__result=Task.FromResult<IEnumerable<CardModel>>(MSelected==null?choices.Take(1):new[]{MSelected});return false;}
    static void MRegister(Type type)
    {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
    static void MMetadata()
    {
        MReset();var card=MCard();Check(card is IExtraServingCard&&card is IThirtyOneFMemberCard m&&m.ThirtyOneFMember==ThirtyOneFMember.MaruyamaKanata,"81 real card has generic ExtraServing and Kanata31F capabilities");
        Check(card.EnergyCost.Canonical==4&&card.Type==CardType.Attack&&card.Rarity==CardRarity.Rare&&card.TargetType==TargetType.AllEnemies,"81 actual rare four-energy all-enemy attack");
        Equal(card.DynamicVars.Damage.BaseValue,10.44m,"81 exact174percent retained as10point44 native units");
        Check(card.Keywords.Contains(CardKeyword.Exhaust)&&card.MaxUpgradeLevel==7,"81 base is Exhaust with seven independent upgrades");
        foreach(int id in Enumerable.Range(0,7).Reverse())
        {Check((bool)Native(card,"PrepareUpgradeEffect",id)!,"81 unique choice accepted "+id);card.UpgradeInternal();card.FinalizeUpgradeInternal();Check(card.Keywords.Contains(CardKeyword.Exhaust),"81 upgrade retains Exhaust "+id);}
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),3,"81 Flash4to3 once after all upgrades");
        Check(((IReadOnlyList<int>)Native(card,"GetAvailableUpgradeEffects")!).Count==0,"81 no repeated or invented eighth upgrade");
        var copy=(LetMeShowYouClassHbrgoSaki)card.MutableClone();Check(copy.UpgradeEffectOrder.SequenceEqual(card.UpgradeEffectOrder),"81 actual native clone retains all physical upgrades");
        Check((string)Call("HbrMemoryGarden","CharacterIdOf",card)! == "kanata","81 garden character identity is Kanata");
    }
    static async Task MPaymentCases()
    {
        foreach(int available in new[]{0,1,3,4,7})
        {
            MReset();var card=MCard();Set(Player.PlayerCombatState!,"_energy",available);
            Check(card.CanPlay(out var reason,out _)&&reason==UnplayableReason.None,"81 real CanPlay permits exact energy shortage "+available);
            var paid=await card.SpendResources();Equal(paid.Item1,Math.Min(4,available),"81 native resource tuple reports actual paid "+available);
            Equal(Player.PlayerCombatState!.Energy,Math.Max(0,available-4),"81 actual native LoseEnergy stays nonnegative "+available);
            Equal(MDebt(),Math.Max(0,4-available),"81 exact unpaid remainder visible debt "+available);
            Check(MPaid.SequenceEqual(new[]{Math.Min(4,available)}),"81 real AfterEnergySpent observes actual paid once "+available);
        }
        MReset();var generic=FCard(typeof(ExtraServingProbe));Set(Player.PlayerCombatState!,"_energy",1);Check(generic.CanPlay(),"81 generic second card is eligible independent of Maruyama type");await generic.SpendResources();Equal(MDebt(),3,"81 second authored card uses generic debt route");
        await FCard(typeof(ExtraServingProbe)).SpendResources();Equal(MDebt(),7,"81 multiple manual payments accumulate exact debt");
        MReset();var zero=MCard();zero.EnergyCost.SetThisTurn(0);Set(Player.PlayerCombatState!,"_energy",0);await zero.SpendResources();Equal(MDebt(),0,"81 actual zero cost produces no debt");
        foreach(var row in new[]{(Offset:1m,Factor:1m),(Offset:0m,Factor:.5m),(Offset:0m,Factor:0m)})
        {
            MReset();var card=MCard();var power=Power<FGlobalCostPower>(Hero,1);power.Offset=row.Offset;power.Factor=row.Factor;
            Set(Player.PlayerCombatState!,"_energy",1);int cost=FCost(card);var paid=await card.SpendResources();
            Equal(MDebt(),Math.Max(0,cost-1),"81 debt uses real modified global cost "+row);Equal(paid.Item1,Math.Min(1,cost),"81 adjusted native paid amount "+row);
        }
        MReset();Set(Player.PlayerCombatState!,"_energy",0);var blocked=FCard(typeof(ExtraServingBlocked));Check(!blocked.CanPlay(out var why,out _)&&why.HasFlag(UnplayableReason.BlockedByCardLogic)&&!why.HasFlag(UnplayableReason.EnergyCostTooHigh),"81 energy waiver retains card logic block");
        var notAllowed=MCard();notAllowed.AddKeyword(CardKeyword.Unplayable);Check(!notAllowed.CanPlay(out why,out _)&&why.HasFlag(UnplayableReason.HasUnplayableKeyword),"81 native Unplayable is never waived");
        var ordinary=FCard(typeof(FOtherCard));Check(!ordinary.CanPlay(out why,out _)&&why.HasFlag(UnplayableReason.EnergyCostTooHigh),"81 unrelated native card retains energy prohibition");
        var hook=Hero.GetPower<ExtraServingHookPower>()!;hook.Block=true;Check(!MCard().CanPlay(out why,out _)&&why.HasFlag(UnplayableReason.BlockedByHook),"81 native Hook.ShouldPlay remains authoritative");
        MReset();Set(Player.PlayerCombatState!,"_energy",0);var star=FCard(typeof(ExtraServingStars));Check(!star.CanPlay(out why,out _)&&why.HasFlag(UnplayableReason.StarCostTooHigh),"81 missing native star cost not waived");
        Set(Player.PlayerCombatState!,"_stars",2);Check(star.CanPlay(),"81 enough native stars plus energy debt is playable");var resources=await star.SpendResources();Equal(resources.Item2,2,"81 native SpendStars retained");Equal(Player.PlayerCombatState.Stars,0,"81 stars actually deducted");Equal(MDebt(),4,"81 star payment does not erase separate energy debt");
        MReset();Set(Player.PlayerCombatState!,"_energy",1);hook=Hero.GetPower<ExtraServingHookPower>()!;hook.PayStars=true;var alt=MCard();Set(Player.PlayerCombatState!,"_stars",5);Check(!alt.CanPlay(out why,out _)&&why.HasFlag(UnplayableReason.StarCostTooHigh),"81 native excess-energy-to-stars rule still blocks insufficient stars");
        Set(Player.PlayerCombatState!,"_stars",6);Check(alt.CanPlay(),"81 native alternative payment with six stars allowed");resources=await alt.SpendResources();Check(resources.Item1==1&&resources.Item2==6&&MDebt()==0&&Player.PlayerCombatState.Stars==0,"81 native alternative stars pays deficit without duplicate debt");
        MReset(false);Set(Player.PlayerCombatState!,"_energy",0);Check(!MCard().CanPlay(out why,out _)&&why.HasFlag(UnplayableReason.EnergyCostTooHigh),"81 legacy mode never gains generic HBR debt waiver");
        MReset(kinds:4);Set(Player.PlayerCombatState!,"_energy",0);var discounted=MCard();FCard(typeof(FMember1));await FTurnDraw();Equal(FCost(discounted),1,"81 actual31F starting-draw discount reduces4to1 before payment");await discounted.SpendResources();Equal(MDebt(),1,"81 debt uses actual31F final discounted cost1 rather than base4");
        MReset();var x=FCard(typeof(ExtraServingX));Check(!(bool)Call("ExtraServingCombatHelper","Eligible",x)!,"81 X-cost excluded from fixed deficit calculation");
    }
    static async Task MSettlementCases()
    {
        MReset();Set(Player.PlayerCombatState!,"_energy",1);await MCard().SpendResources();Equal(MDebt(),3,"81 settlement fixture borrows3");
        await Hook.AfterEnergyReset(Combat,Player);Equal(MDebt(),3,"81 same-turn callback cannot collect newly created debt");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await FSetupTurn();Equal(Player.PlayerCombatState!.Energy,0,"81 actual native SetupPlayerTurn fills then pays3");Equal(MDebt(),0,"81 ordinary next turn fully clears debt");
        await MResetEnergy();Equal(Player.PlayerCombatState.Energy,3,"81 duplicate same-turn reset callback does not recollect settled amount");
        MReset();Set(Player.PlayerCombatState!,"_energy",0);await MCard().SpendResources();await MCard().SpendResources();Equal(MDebt(),8,"81 large accumulated debt before next turn");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await MResetEnergy();Equal(Player.PlayerCombatState!.Energy,0,"81 debt beyond full energy clamps to zero");Equal(MDebt(),0,"81 unpaid excess expires instead of carrying");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",3);await MResetEnergy();Equal(Player.PlayerCombatState.Energy,3,"81 following turn recovers full energy with no residual debt");
        foreach(bool od in new[]{false,true})
        {
            MReset();Set(Player.PlayerCombatState!,"_energy",1);await MCard().SpendResources();
            using(MisatoExtra(od))
            {await MResetEnergy(inserted:true);Equal(Player.PlayerCombatState!.Energy,0,"81 actual inserted common start collects debt after full energy od="+od);Equal(MDebt(),0,"81 inserted reset removes debt od="+od);await MCard().SpendResources();Equal(MDebt(),4,"81 newly borrowed inserted-turn debt waits od="+od);await (Task)Call("HbrInsertedTurnStartHelper","Start",new ThrowingPlayerChoiceContext(),Player)!;Equal(MDebt(),4,"81 duplicate inserted token cannot collect new same-turn debt od="+od);}
            Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await MResetEnergy();Equal(MDebt(),0,"81 next ordinary turn can settle inserted-turn debt od="+od);Equal(Player.PlayerCombatState!.Energy,0,"81 excess inserted debt still never carries od="+od);
        }
        MReset();Set(Player.PlayerCombatState!,"_energy",1);await MCard().SpendResources();var peer=SupportTeammate();Set(peer.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await MResetEnergy(peer);Equal(MDebt(),3,"81 teammate energy reset cannot collect holder debt");
        var debt=Hero.GetPower<HbrEnergyDebtPower>()!;Check(debt.Type==MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff&&debt.Description.GetFormattedText().Contains("energy_icon")&&debt.Description.GetFormattedText().Contains("下个"),"81 visible native debt description includes actual energy icon");
    }
    static async Task MReplayCases()
    {
        MReset();var probe=FCard(typeof(ExtraServingProbe));Set(Player.PlayerCombatState!,"_energy",1);Hero.GetPower<ExtraServingHookPower>()!.Replays=2;
        await ChasePlay(probe);Equal(ExtraServingProbe.Plays,3,"81 real native OnPlayWrapper performs three play-count resolutions");Equal(MDebt(),3,"81 replay series borrows exactly once at actual payment");Check(MPaid.SequenceEqual(new[]{1}),"81 replay does not call native SpendEnergy again");
        MReset();probe=FCard(typeof(ExtraServingProbe));Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(probe,true);Equal(ExtraServingProbe.Plays,1,"81 real native AutoPlay resolves extra-serving body");Equal(MDebt(),0,"81 unpaid native autoplay creates no debt");
        MReset();var card=MCard();Set(Player.PlayerCombatState!,"_energy",1);await ChasePlay(card);Check(card.Pile==Player.PlayerCombatState!.ExhaustPile,"81 real native wrapper sends base card to Exhaust");Equal(MDebt(),3,"81 genuine attack wrapper pays exactly once");
        MReset();card=MCard(0,1,2,3,4,5,6);Set(Player.PlayerCombatState!,"_energy",0);await ChasePlay(card);Check(card.Pile==Player.PlayerCombatState!.ExhaustPile&&MDebt()==3,"81 seven-upgrade wrapper still Exhausts and uses Flash cost3");
    }
    public static void RunMaruyama(string path)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();Localization();LocalContext.NetId=Player.NetId;var h=new Harmony(ProductionId+".maruyama81");
            foreach(Type type in FMembers.Concat(new[]{typeof(ExtraServingProbe),typeof(ExtraServingBlocked),typeof(ExtraServingX),typeof(ExtraServingStars),typeof(ExtraServingHookPower),typeof(FGlobalCostPower),typeof(FOtherCard),typeof(ThirtyOneFCollection),typeof(LetMeShowYouClassHbrgoSaki),typeof(HbrEnergyDebtPower),typeof(HbrDpDefenseDownPower),typeof(HbrDefenseDownPower),typeof(HbrMoralePower),typeof(HbrComboMediumPower),typeof(MindsEyePower),typeof(HighDimensionalShellPower),typeof(PhantomPower),typeof(MinionPower),typeof(ArtifactPower),typeof(DoublePowersHbrgoSaki),typeof(Overclock)}))MRegister(type);
            foreach(string name in new[]{"ExtraServingCanPlayPatch","ExtraServingSpendResourcesPatch","ExtraServingSpendEnergyPatch","HbrEffectApplicationPatch","HbrEffectInstanceStackingPatch","ThirtyOneFStartingHandDrawPatch","HbrComboVirtualHitPatch","HighHitDamageAttackScopePatch","HighHitDamageCommandScopePatch","CreatureDamageBlockInternalPatch","PhantomBeforeDamageReceivedPatch","PhantomDamageTargetPatch","ModeRestrictedCardPlayPatch","CardPlayContextPatch","MoreThanOneTruthPlayScopePatch"})
            {var type=Mod.GetTypes().Single(t=>t.Name==name);Check(h.CreateClassProcessor(type).Patch().Count>0,"81 installs production "+name);}
            void Patch(MethodBase method,string prefix)=>h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix,F)!));
            foreach(Type type in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FListeners),F)!){priority=Priority.First+200});
            Patch(AccessTools.PropertyGetter(typeof(CombatManager),"IsOverOrEnding"),nameof(CostLive));Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState",F)!,nameof(Skip));Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent",F)!,nameof(VisualTask));Patch(AccessTools.PropertyGetter(typeof(PowerModel),"Icon"),nameof(FormPowerIconLeaf));
            foreach(MethodInfo wait in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))Patch(wait,nameof(VisualTask));
            foreach(MethodInfo m in typeof(SfxCmd).GetMethods(F).Where(m=>m.Name=="Play"))Patch(m,nameof(Skip));foreach(MethodInfo m in typeof(ThinkCmd).GetMethods(F).Where(m=>m.Name=="Play"))Patch(m,nameof(Skip));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec",F)!,nameof(CrackClock));Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay",F)!,nameof(ChaseManualPile));
            foreach(MethodInfo add in typeof(CardPileCmd).GetMethods(F).Where(m=>m.Name=="Add"))Patch(add,nameof(MSkipPileVisuals));
            foreach(MethodInfo select in typeof(CardSelectCmd).GetMethods(F).Where(m=>m.Name=="FromSimpleGrid"))Patch(select,nameof(MGrid));
            Patch(typeof(AttackCommand).GetMethod("Execute",F)!,nameof(MAttackLeaf));h.Unpatch(Canonical,typeof(EffectChecks).GetMethod(nameof(Terminal),F)!);Patch(Canonical,nameof(MDamageLeaf));
            MMetadata();MPaymentCases().GetAwaiter().GetResult();MSettlementCases().GetAwaiter().GetResult();MReplayCases().GetAwaiter().GetResult();MCardCases().GetAwaiter().GetResult();MDpDownCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary="Real native CanPlay/HasEnoughResourcesFor/SpendResources/SpendEnergy/History and hooks, OnPlayWrapper/GeneratePlayCount/AutoPlay/Exhaust; native SetupPlayerTurn and actual inserted-start debt handler; actual card body/buffs/debuff slots and shared damage arithmetic. Attack animation, canonical damage scene terminal and selection/pile VFX leaves controlled; no Godot or live multiplayer."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Maruyama81 checks");if(!ok)throw new Exception("Inspect "+path);
    }
}
