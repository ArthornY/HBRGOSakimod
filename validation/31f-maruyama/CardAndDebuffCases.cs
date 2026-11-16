using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
static partial class EffectChecks
{
    static HighDimensionalShellPower MShell(int amount)
    {var power=(HighDimensionalShellPower)ModelDb.Power<HighDimensionalShellPower>().MutableClone();power.ApplyInternal(Enemy,amount);return power;}
    static async Task MCardCases()
    {
        MReset();var card=MCard();await NewPlay(card);
        Check(MAttacks.Single()is{Raw:10.44m,Hits:4,Eye:false,Combo:0},"81 actual base requests174percent four times with no invented initial buffs");
        Check(MSegments.Count==4&&MSegments.All(s=>s.Destruction==8m&&s.DpFactor==1m),"81 all four direct segments carry original total24 coefficient, no DP multiplier without upgrade");
        Equal((decimal)Call("MaruyamaStyleCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Unpowered)!,1,"81 unpowered reactions never inherit coefficient24");
        MReset();card=MCard(1);var shell=MShell(100000);Native(shell,"SetDpAmount",100000m,false);await NewPlay(card);
        Check(MSegments.All(s=>s.DpFactor==1.3m),"81 Little High Class multiplies every still-DP segment1point3");
        MReset();card=MCard(1);shell=MShell(1);Native(shell,"SetDpAmount",1m,false);await NewPlay(card);
        Check(MSegments.First().DpFactor==1.3m&&MSegments.Skip(1).All(s=>s.DpFactor==1m),"81 actual first DPbreak changes later three segments to HP without1point3");
        MReset();card=MCard(2,3);await NewPlay(card);
        Check(MAttacks.Single().DpDown==1&&MAttacks.Single().Down==1,"81 both debuffs exist before native BeforeAttack and the first damage");
        var dpDown=Enemy.GetPower<HbrDpDefenseDownPower>()!;var down=Enemy.GetPower<HbrDefenseDownPower>()!;
        Equal(MPotency(dpDown),MResolved(dpDown,50m),"81 DPdown50 uses shared attribute formula snapshot");
        Equal(MPotency(down),MResolved(down,30m),"81 normal down30 uses shared attribute formula snapshot");
        Check(dpDown.Amount==1&&down.Amount==1,"81 both authored debuffs have one enemy-end remaining turn");
        MReset();card=MCard(4,6);var peer=SupportTeammate();
        Creature phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);
        Creature peerPhantom=(Creature)H("Actor",Blank<SmallHopper>(),null,peer)!;Power<PhantomPower>(peerPhantom,1);
        Creature minion=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(minion,1);Power<MinionPower>(minion,1);
        Creature pet=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;
        Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature,phantom,peerPhantom,minion,pet});
        await NewPlay(card);Check(MAttacks.Single()is{Hits:4,Eye:true,Combo:0,Morale:2},"81 team eye and morale precede main4; new medium combo is absent during main");
        Check(new[]{Hero,peer.Creature,phantom,peerPhantom}.All(c=>c.GetPowerAmount<HbrComboMediumPower>()==3&&c.GetPowerAmount<HbrMoralePower>()==2)&&new[]{peer.Creature,phantom,peerPhantom}.All(c=>c.HasPower<MindsEyePower>()),"81 all heroes/main phantoms receive team grants; allies retain unused eye: "+string.Join(";",new[]{Hero,peer.Creature,phantom,peerPhantom}.Select(c=>$"eye={c.GetPowerAmount<MindsEyePower>()},combo={c.GetPowerAmount<HbrComboMediumPower>()},morale={c.GetPowerAmount<HbrMoralePower>()}")));
        Check(new[]{minion,pet}.All(c=>!c.HasPower<MindsEyePower>()&&!c.HasPower<HbrComboMediumPower>()&&!c.HasPower<HbrMoralePower>()),"81 ordinary disposable pets and phantom minions receive none of the team grants");
        await NewPlay(MCard());Check(MAttacks.Last().Hits==7&&Hero.GetPowerAmount<HbrComboMediumPower>()==2,"81 next complete attack gains exactly three hits and consumes one medium use");
        await NewPlay(MCard());await NewPlay(MCard());Check(MAttacks.TakeLast(2).All(a=>a.Hits==7)&&!Hero.HasPower<HbrComboMediumPower>(),"81 medium remains for three whole attacks then expires");
        MReset();Power<HbrMoralePower>(Hero,9);await NewPlay(MCard(6));Equal(Hero.GetPowerAmount<HbrMoralePower>(),10,"81 real native morale cap is ten");
        MReset();card=MCard(4);MFailAttack=true;try{await NewPlay(card);throw new Exception("Missing main-attack fault");}catch(InvalidOperationException e){Check(e.Message.Contains("controlled"),"81 controlled native attack failure remains visible");}
        Check(Hero.HasPower<MindsEyePower>()&&!Hero.HasPower<HbrComboMediumPower>(),"81 failed main attack never grants postattack combo");
        MReset(false);await NewPlay(MCard(2,3,4,6));Check(MAttacks.Count==0&&Enemy.Powers.Count==0&&Hero.Powers.All(p=>p is ExtraServingHookPower),"81 HBR-only entire body produces no legacy damage/new effects");
        await MReturnCases();
    }
    static decimal MPotency(PowerModel p)=>(decimal)AccessTools.PropertyGetter(typeof(HbrPercentDebuffPower),"EffectPercent").Invoke(p,null)!;
    static decimal MResolved(PowerModel power,decimal basis)=>(decimal)Call("HbrPercentDebuffHelper","ResolveBasis",power,basis,Hero,Enemy)!;
    static async Task MReturnCases()
    {
        Type mion=Mod.GetType("HBRGOSakiMod.Core.Models.Cards.AsTheYoungLadyCommandsHbrgoSaki",true)!;MRegister(mion);
        CardModel Yanagi(int[]upgrades)
        {var c=FCard(mion);foreach(int id in upgrades){Check((bool)Native(c,"PrepareUpgradeEffect",id)!,"81 return fixture Yanagi upgrade "+id);c.UpgradeInternal();c.FinalizeUpgradeInternal();}c.Pile!.RemoveInternal(c,true);Player.PlayerCombatState!.ExhaustPile.AddInternal(c);return c;}
        MReset();var yanagi=Yanagi(new[]{0,2,6});var source=MCard(5);await NewPlay(source);
        Check(yanagi.Pile==Player.PlayerCombatState!.Hand&&yanagi.CurrentUpgradeLevel==3&&((int[])yanagi.GetType().GetProperty("UpgradeEffectOrder")!.GetValue(yanagi)!).SequenceEqual(new[]{0,2,6}),"81 actual Mion physical Exhaust entity returns with exact existing upgrade order");
        var later=Yanagi(Array.Empty<int>());await NewPlay(MCard(5));Check(later.Pile==Player.PlayerCombatState.ExhaustPile,"81 second physical Maruyama cannot repeat one-owner one-battle Mion return");
        MReset();source=MCard(5);await NewPlay(source);yanagi=Yanagi(Array.Empty<int>());await NewPlay(source);Check(yanagi.Pile==Player.PlayerCombatState!.Hand,"81 an empty candidate set does not waste the once-battle return");
        MReset();var first=Yanagi(new[]{1});var second=Yanagi(new[]{3});MSelected=second;await NewPlay(MCard(5));Check(MChoices.Single().ToHashSet().SetEquals(new[]{first,second})&&second.Pile==Player.PlayerCombatState!.Hand&&first.Pile==Player.PlayerCombatState.ExhaustPile,"81 multiple Mion physical candidates use native grid and return only selected actual entity");
        MReset();yanagi=Yanagi(Array.Empty<int>());source=MCard(5);while(Player.PlayerCombatState!.Hand.Cards.Count<CardPile.MaxCardsInHand)FCard(typeof(FOtherCard));await NewPlay(source);Check(Player.PlayerCombatState!.Hand.Cards.Count==CardPile.MaxCardsInHand&&yanagi.Pile==Player.PlayerCombatState.DiscardPile,"81 native full-hand rule safely routes returned Mion instead of retaining in Exhaust");
        MReset();source=MCard(5);source.Pile!.RemoveInternal(source,true);Player.PlayerCombatState!.ExhaustPile.AddInternal(source);await NewPlay(MCard(5));Check(source.Pile==Player.PlayerCombatState.ExhaustPile,"81 Kanata Exhaust card is not an invented Mion candidate");
    }
    static Task MBasis<T>(decimal basis,int duration=1)where T:PowerModel
    {
        Type profileType=Helper("HbrPercentDebuffApplication").Assembly.GetType("HBRGOSakiMod.Core.Helpers.HbrEffectProfile",true)!;
        ConstructorInfo ctor=profileType.GetConstructors(F).Single();object profile=ctor.Invoke(new[]{Enum.Parse(ctor.GetParameters()[0].ParameterType,"Default"),Enum.Parse(ctor.GetParameters()[1].ParameterType,"EnemyTurnEnd"),(object)duration});
        return(Task)Invoke(Helper("HbrPercentDebuffApplication").GetMethods(F).Single(m=>m.Name=="ApplyBasis"&&m.IsGenericMethodDefinition).MakeGenericMethod(typeof(T)),null,new ThrowingPlayerChoiceContext(),Enemy,basis,Hero,null,profile,false)!;
    }
    static void MSaveCache(Type type)
    {
        Assembly native=typeof(SavedProperties).Assembly;
        if(native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache")is{} old)old.GetMethod("InjectTypeIntoCache",F)!.Invoke(null,new[]{type});
        else{Type cache=native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!.Invoke(null,new[]{type});cache.GetField("_initialized",F)!.SetValue(null,true);}
    }
    static async Task MDpDownCases()
    {
        MReset();await MBasis<HbrDpDefenseDownPower>(50,2);await MBasis<HbrDpDefenseDownPower>(50,1);await MBasis<HbrDefenseDownPower>(30,2);await MBasis<HbrDefenseDownPower>(30,1);
        var dp=Enemy.Powers.OfType<HbrDpDefenseDownPower>().ToArray();var normal=Enemy.Powers.OfType<HbrDefenseDownPower>().ToArray();Check(dp.Length==2&&normal.Length==2,"81 DPdown and ordinary defense down own independent two-slot budgets");
        var shortest=dp.Single(p=>p.Amount==1);await MBasis<HbrDpDefenseDownPower>(60,2);Check(!Enemy.Powers.Contains(shortest)&&Enemy.Powers.OfType<HbrDpDefenseDownPower>().Count()==2,"81 stronger DPdown replaces weakest shortest slot and resets duration");
        await MBasis<HbrDpDefenseDownPower>(1);Check(Enemy.Powers.OfType<HbrDpDefenseDownPower>().Count()==2&&Enemy.Powers.OfType<HbrDpDefenseDownPower>().All(p=>MPotency(p)>1m),"81 weak DPdown does not displace stronger active slots");
        foreach(var p in Enemy.Powers.OfType<HbrDpDefenseDownPower>().ToArray())await p.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Player,new[]{Hero});Check(Enemy.Powers.OfType<HbrDpDefenseDownPower>().All(p=>p.Amount==2),"81 DPdown ignores player-side end tick");
        foreach(var p in Enemy.Powers.OfType<HbrDpDefenseDownPower>().ToArray())await p.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,new[]{Enemy});Check(Enemy.Powers.OfType<HbrDpDefenseDownPower>().All(p=>p.Amount==1),"81 all independent DPdown instances tick only at enemy end");
        MReset();await MBasis<HbrDpDefenseDownPower>(50,1);var d=Enemy.GetPower<HbrDpDefenseDownPower>()!;decimal before=MPotency(d);
        await(Task)Call("HbrPercentDebuffApplication","DoubleExisting",new ThrowingPlayerChoiceContext(),d,before,1,Hero,null,true,false)!;
        Equal(MPotency(d),before*2,"81 shared Double exactly doubles DPdown frozen potency");Equal(d.Amount,2,"81 shared Double doubles DPdown duration too");
        MSaveCache(typeof(HbrDpDefenseDownPower));var options=new JsonSerializerOptions{IncludeFields=true};using var payload=JsonDocument.Parse(JsonSerializer.Serialize(new{Amount=d.Amount,Extensions=SavedProperties.From(d)},options));var saved=JsonSerializer.Deserialize<SavedProperties>(payload.RootElement.GetProperty("Extensions").GetRawText(),options)!;
        var restored=(HbrDpDefenseDownPower)ModelDb.Power<HbrDpDefenseDownPower>().MutableClone();restored.ApplyInternal(Enemy,payload.RootElement.GetProperty("Amount").GetInt32());saved.Fill(restored);Equal(restored.Amount,2,"81 native counter initialization plus SavedProperties JSON restores DPdown duration");Check(restored.HbrEffectData==d.HbrEffectData&&restored.HbrDefaultRemainingOverride==d.HbrDefaultRemainingOverride,"81 native save restores inherited frozen potency and authored profile");
        MReset();Power<ArtifactPower>(Enemy,1);await MBasis<HbrDpDefenseDownPower>(50);Check(!Enemy.HasPower<HbrDpDefenseDownPower>(),"81 real native Artifact hook rejects DPdown before slot insertion");
        // Fix only application snapshots to isolate the common arithmetic bucket.
        MReset();var card=MCard();var shell=MShell(100000);Native(shell,"SetDpAmount",100000m,false);
        decimal Neutral(){Set(RunState,"<Rng>k__BackingField",new RunRngSet(NonCriticalSeed));return Actual(Enemy,Hero,6m,ValueProp.Move,card);}
        decimal baseValue=Neutral();await MBasis<HbrDefenseDownPower>(30);Native(Enemy.GetPower<HbrDefenseDownPower>()!,"Configure",30m,1);await MBasis<HbrDpDefenseDownPower>(50);Native(Enemy.GetPower<HbrDpDefenseDownPower>()!,"Configure",50m,1);
        decimal boosted=Neutral();Equal(boosted,decimal.Floor(baseValue*1.8m),"81 actual shared damage accumulator adds normal30 and DP50 to1point8, not product1point95");
        Native(shell,"SetDpAmount",0m,false);Equal(Neutral(),decimal.Floor(baseValue*1.3m),"81 HP-stage actual arithmetic retains normal30 but removes DP50");
        Native(shell,"SetDpAmount",100000m,false);int start=FDamage.Count;await(Task)Call("HpOnlyDamageContext","Damage",new ThrowingPlayerChoiceContext(),Enemy,100m,Hero,card)!;
        Equal(shell.CurrentDp,100000m,"81 explicit canonical HP-only supplemental command bypasses existing DP resource");
        Check(FDamage.Count==start+1&&FDamage.Last().Lost>0,"81 HP-only supplemental still deals actual HP loss");
        Equal(Enemy.GetPower<HbrDpDefenseDownPower>()!.ModifyDamageMultiplicative(Enemy,1m,ValueProp.Unpowered,Hero,card),1,"81 unpowered supplement receives no DP defense down");
    }
}
