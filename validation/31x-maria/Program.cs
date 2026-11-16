using System.Numerics;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class Maria91Standalone{public static void Run(string path)=>EffectChecks.X91Run(path,EffectChecks.MariaCases);}
static partial class EffectChecks
{
    static decimal MariaFaith(Creature? target=null)=>(decimal)HbrFaithResource.Get(target??Hero);
    static decimal MariaInspiration(Creature? target=null)=>(target??Hero).Powers.OfType<HbrAttackUpPower>().Sum(power=>(decimal)Native(power,"get_EffectPotency")!);
    static async Task<MariaRepentancePower> MariaApply(params int[] ids)
    {var card=NewGarden<RepentanceTimeHbrgoSaki>(ids);await X91Play(card);return Hero.GetPower<MariaRepentancePower>()!;}
    static async Task MariaBasicCases()
    {
        X91Reset();var dp=NewDp(1000);var card=NewGarden<RepentanceTimeHbrgoSaki>();decimal hp=Hero.CurrentHp;
        Check(card.Type==CardType.Power&&card.MaxUpgradeLevel==7&&card.EnergyCost.Canonical==1,"91 rare1energy Power has seven upgrades");
        await X91Play(card);Equal(dp.Dp,700m,"actual card sacrifice loses30percent current DP");Equal(Hero.CurrentHp,hp,"sacrifice never costsHP");Equal(MariaFaith(),2m,"sacrifice grants exact2faith");Equal(MariaInspiration(),6m,"playing immediately grants fixed6percent inspiration independent of INT");
        Equal(Hero.GetPower<MariaRepentancePower>()!.DisplayAmount,6,"ability icon displays applied inspiration percent");
        await X91Play(NewGarden<RepentanceTimeHbrgoSaki>());Equal(dp.Dp,490m,"second physical power sacrifices its own currentDP");Equal(MariaFaith(),4m,"second cast grants another2faith");
        Equal(Hero.Powers.OfType<MariaRepentancePower>().Count(),1,"same-owner persistent ability is not duplicated");Equal(Hero.Powers.OfType<HbrAttackUpPower>().Count(),1,"same-owner inspiration replaces its earlier turn grant");Equal(MariaInspiration(),12m,"second cast updates rather than stacks inspiration");
        HbrFaithResource.Gain(Hero,100);Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await Hook.AfterEnergyReset(Combat,Player);Equal(MariaInspiration(),30m,"real native energy-reset hook caps inspiration at10faith");
        await Hook.AfterEnergyReset(Combat,Player);Equal(Hero.Powers.OfType<HbrAttackUpPower>().Count(),1,"duplicate energy-reset notifications do not apply another inspiration");
        X91Reset();dp=NewDp(0);await MariaApply();Equal(dp.Dp,0,"zeroDP sacrifice pays nothing");Equal(MariaFaith(),2,"zeroDP still grants2faith");
        X91Reset();card=NewGarden<RepentanceTimeHbrgoSaki>(0);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),0,"Flash costs0");await X91Play(card);
        X91Reset();dp=NewDp(1000);card=NewGarden<RepentanceTimeHbrgoSaki>(1);decimal expected=Math.Min(dp.MaxDp,700+decimal.Floor(dp.MaxDp*.1m));await X91Play(card);Equal(dp.Dp,expected,"Punishment restores10percent real maximumDP to lowest actual pool");
        X91Reset();dp=NewDp(1000);card=NewGarden<RepentanceTimeHbrgoSaki>(2);await X91Play(card);Check(X91Hits.Count==2&&X91Hits.All(hit=>hit.Raw==6.3m&&hit.Knock==32m&&hit.Destruction==1.625m/3m),"Knock is105percent twice with献祭-after2faith snapshot and normalized1point625 destruction");
        Check(card.Type==CardType.Power,"Knock never changes the card into an attack play");
        Equal((decimal)Call("MariaRepentanceCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,0,"Knock boost closes outside its command");
        X91Reset();await PowerCmd.Apply<HbrAttackDownPower>(new ThrowingPlayerChoiceContext(),Hero,1m,Enemy,null);Power<WeakPower>(Hero,1);await MariaApply(3);Check(!Hero.HasPower<HbrAttackDownPower>()&&Hero.HasPower<WeakPower>(),"Requiem removes only new attack-down on play and preserves vanillaWeak");
        await PowerCmd.Apply<HbrAttackDownPower>(new ThrowingPlayerChoiceContext(),Hero,1m,Enemy,null);Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await Hook.AfterEnergyReset(Combat,Player);Check(Hero.HasPower<HbrAttackDownPower>(),"Requiem does not cleanse on later turns");
        X91Reset();await MariaApply(5);Equal(MariaFaith(),3,"DinnerIntroduction grants self additional1faith");Equal(MariaInspiration(),9,"immediate inspiration sees the final current faith");
        X91Reset();var teammate=SupportTeammate();Creature friend=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(friend,1);Set(RunState,"_players",new List<Player>{Player,teammate});Set(Combat,"_allies",new List<Creature>{Hero,teammate.Creature,friend});await MariaApply(5);
        Equal(MariaFaith(teammate.Creature),1,"DinnerIntroduction grants teammate exact1faith");Equal(MariaFaith(friend),1,"DinnerIntroduction grants owned main phantom exact1faith");Equal(MariaInspiration(teammate.Creature),9,"inspiration reaches living teammate");Equal(MariaInspiration(friend),9,"inspiration reaches living main phantom");
        X91Reset();card=NewGarden<RepentanceTimeHbrgoSaki>(0,1,2,3,4,5,6);X91SaveType(card.GetType());var options=new JsonSerializerOptions{IncludeFields=true};var saved=JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(card),options),options)!;var clone=(RepentanceTimeHbrgoSaki)card.MutableClone();saved.Fill(clone);Check(clone.UpgradeEffectOrder.SequenceEqual(card.UpgradeEffectOrder)&&!ReferenceEquals(clone.UpgradeEffectOrder,card.UpgradeEffectOrder),"all7upgrade choices survive real SavedProperties JSON Fill and independent clone");
    }
    static async Task MariaBlessingCases()
    {
        X91Reset();var dp=NewDp(500);await MariaApply(4);var source=NewGarden<RepentanceTimeHbrgoSaki>();
        for(int i=0;i<3;i++)await (Task)Call("SeiraDpRecoveryHelper","RestoreAmount",new ThrowingPlayerChoiceContext(),Hero,10m,false,source)!;
        Equal(MariaFaith(),4,"three actual card DP recoveries grant only2faith in an ordinary turn");
        using(MisatoExtra(false))
        {for(int i=0;i<3;i++)await (Task)Call("SeiraDpRecoveryHelper","RestoreAmount",new ThrowingPlayerChoiceContext(),Hero,10m,false,source)!;Equal(MariaFaith(),6,"personal extra turn owns another2blessing limit");}
        await (Task)Call("SeiraDpRecoveryHelper","RestoreAmount",new ThrowingPlayerChoiceContext(),Hero,10m,false,source)!;Equal(MariaFaith(),6,"returning ordinary turn retains its already-spent limit");
        using(MisatoExtra(true))
        {await (Task)Call("SeiraDpRecoveryHelper","RestoreAmount",new ThrowingPlayerChoiceContext(),Hero,10m,false,source)!;Equal(MariaFaith(),7,"OD extra turn has an independent blessing receipt");}
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);await (Task)Call("SeiraDpRecoveryHelper","RestoreAmount",new ThrowingPlayerChoiceContext(),Hero,10m,false,source)!;Equal(MariaFaith(),8,"next ordinary turn grants new blessing uses");
        decimal before=MariaFaith();Set(dp,"_dp",dp.MaxDp);await (Task)Call("SeiraDpRecoveryHelper","RestoreAmount",new ThrowingPlayerChoiceContext(),Hero,100m,false,source)!;Equal(MariaFaith(),before,"fullDP zero-gain restore earns nofaith");
        Set(dp,"_dp",0);Native(dp,"RestoreDpAmountWithLimit",10m,false);Equal(MariaFaith(),before,"plain relic resource restoration has no invented card/ability source");
        X91Reset();dp=NewDp(500);await MariaApply(4);await (Task)Call("SeiraDpRecoveryHelper","RestoreFromAbility",new ThrowingPlayerChoiceContext(),Hero,.01m,false)!;Equal(MariaFaith(),3,"explicit native ability recovery earns faith once");
        var peer=SupportTeammate();Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});
        var peerPower=(MariaRepentancePower)ModelDb.Power<MariaRepentancePower>().ToMutable();Native(peerPower,"set_UpgradeMask",1<<4);peerPower.ApplyInternal(peer.Creature,1);Call("MariaRepentanceCombatHelper","OnDpRestored",peer.Creature,10m,source,false);Equal(MariaFaith(peer.Creature),1,"cloned canonical power blessing counters are isolated per owner");
    }
    static bool MariaVfxNode(ref Godot.Node __result){__result=null!;return false;}
    static void MariaInstallNativeHp(Harmony h)
    {
        void Patch(MethodBase target,string method)=>h.Patch(target,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(method,F)!));
        Patch(typeof(Creature).GetMethod("GetVfxContainer",F)!,nameof(MariaVfxNode));
        Type numbers=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Vfx.NDamageNumVfx",true)!;
        foreach(var create in numbers.GetMethods(F).Where(method=>method.Name=="Create"))Patch(create,nameof(Skip));
        Type vignette=typeof(Creature).Assembly.GetTypes().Single(type=>type.Name=="PlayerHurtVignetteHelper");Patch(vignette.GetMethod("Play",F)!,nameof(Skip));
        foreach(MethodInfo kill in typeof(CreatureCmd).GetMethods(F).Where(method=>method.Name=="Kill"&&method.ReturnType==typeof(Task)))Patch(kill,nameof(VisualTask));
        Check(h.CreateClassProcessor(Mod.GetTypes().Single(type=>type.Name=="HbrHealthCostCallsitePatch")).Patch().Count>0,"installs exact native health-cost caller provenance patch");
        Check(h.CreateClassProcessor(Mod.GetTypes().Single(type=>type.Name=="HbrNativeProfiledDamagePatch")).Patch().Count>0,"installs existing native damage-profile cost adapter alongside new callers");
    }
    static async Task MariaLifesaveCases(Harmony h)
    {
        MariaInstallNativeHp(h);
        X91Reset();await MariaApply(6);Set(Hero,"_currentHp",100);X91NativeDamage=true;await X91Command(Enemy,1000m,ValueProp.Unpowered,null,Hero);Equal(Hero.CurrentHp,1,"real canonical Damage native HP write is guarded to1HP");
        await X91Command(Enemy,1000m,ValueProp.Unpowered,null,Hero);Equal(Hero.CurrentHp,0,"second lethal damage is not protected");
        X91Reset();var spent=await MariaApply(6);Set(Hero,"_currentHp",100);X91NativeDamage=true;await X91Command(Enemy,1000m,ValueProp.Unpowered,null,Hero);
        await PowerCmd.Remove(spent);var replacement=await MariaApply(6);Check(replacement.LifesaveUsed,"replaying after actual dispel carries spent party rescue into replacement power");
        X91SaveType(typeof(MariaRepentancePower));var saveOptions=new JsonSerializerOptions{IncludeFields=true};var savedPower=JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(replacement),saveOptions),saveOptions)!;
        var loaded=(MariaRepentancePower)ModelDb.Power<MariaRepentancePower>().ToMutable();savedPower.Fill(loaded);replacement.RemoveInternal();loaded.ApplyInternal(Hero,1);
        object lifesaves=Helper("MariaRepentanceCombatHelper").GetField("Lifesaves",F)!.GetValue(null)!;lifesaves.GetType().GetMethod("Remove")!.Invoke(lifesaves,[Combat]);
        Set(Hero,"_currentHp",100);await X91Command(Enemy,1000m,ValueProp.Unpowered,null,Hero);Check(loaded.LifesaveUsed&&Hero.CurrentHp==0,"real SavedProperties reload after dispel/recast cannot grant a second party rescue");
        X91Reset();spent=await MariaApply(6);Set(Hero,"_currentHp",100);X91NativeDamage=true;await X91Command(Enemy,1000m,ValueProp.Unpowered,null,Hero);await PowerCmd.Remove(spent);
        Check(!Hero.HasPower<MariaRepentancePower>()&&Player.GetRelic<ThirtyOneXCollection>()==null,"temporary Maria remains valid with all powers removed and without awarding a31X collection relic");
        var manual=Player.GetRelic<ElectronicSoldierManual>()!;X91SaveType(typeof(ElectronicSoldierManual));savedPower=JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(manual),saveOptions),saveOptions)!;
        var savedManual=Blank<ElectronicSoldierManual>();Set(savedManual,"<IsMutable>k__BackingField",true);Set(savedManual,"_owner",Player);savedPower.Fill(savedManual);Set(Player,"_relics",new List<RelicModel>{savedManual});
        lifesaves=Helper("MariaRepentanceCombatHelper").GetField("Lifesaves",F)!.GetValue(null)!;lifesaves.GetType().GetMethod("Remove")!.Invoke(lifesaves,[Combat]);
        Check((bool)Native(savedManual,"HasUsedMariaLifesave",Combat)!,"native manual SavedProperties JSON reload preserves the party receipt while no Maria power exists");
        Set(Hero,"_currentHp",100);replacement=await MariaApply(6);Check(replacement.LifesaveUsed,"recast after loading a power-free save rehydrates the spent receipt from existing manual");
        await X91Command(Enemy,1000m,ValueProp.Unpowered,null,Hero);Equal(Hero.CurrentHp,0,"all powers dispelled before save cannot reset the same battle's rescue");
        await PowerCmd.Remove(replacement);Set(Hero,"_currentHp",100);await savedManual.AfterCombatEnd(null!);
        Check(!(bool)Native(savedManual,"HasUsedMariaLifesave",Combat)!,"actual manual AfterCombatEnd clears the saved once-per-battle receipt");
        replacement=await MariaApply(6);await X91Command(Enemy,1000m,ValueProp.Unpowered,null,Hero);Equal(Hero.CurrentHp,1,"a new battle after native end has a fresh single party rescue");
        X91Reset();await MariaApply(6);var peer=SupportTeammate();Set(peer.Creature,"_currentHp",100);Set(peer.Creature,"_maxHp",100);Set(RunState,"_players",new List<Player>{Player,peer});Set(Combat,"_allies",new List<Creature>{Hero,peer.Creature});
        var other=(MariaRepentancePower)ModelDb.Power<MariaRepentancePower>().ToMutable();Native(other,"set_UpgradeMask",1<<6);other.ApplyInternal(peer.Creature,1);Set(Hero,"_currentHp",100);X91NativeDamage=true;
        await X91Command(Enemy,1000m,ValueProp.Unpowered,null,Hero);await X91Command(Enemy,1000m,ValueProp.Unpowered,null,peer.Creature);Equal(Hero.CurrentHp,1,"first allied lethal damage claims the whole-party rescue");Equal(peer.Creature.CurrentHp,0,"second Maria holder does not add another party rescue");
        X91Reset();var power=await MariaApply(6);Set(Hero,"_currentHp",100);Set(Hero,"_block",100000);X91NativeDamage=true;await X91Command(Enemy,10m,ValueProp.Move,null,Hero);Check(!power.LifesaveUsed&&Hero.CurrentHp==100,"fully blocked real command never consumes lifesave");
        Set(Hero,"_block",0);await X91Command(Enemy,1000m,ValueProp.Unpowered,null,Hero);Equal(Hero.CurrentHp,1,"later genuinely lethal command still rescues after a full block");
        X91Reset();power=await MariaApply(6);Set(Hero,"_currentHp",100);X91NativeDamage=true;await X91Play(FormCard<Bloodletting>());Check(Hero.CurrentHp==0&&!power.LifesaveUsed,"actual native Bloodletting health cost bypasses rescue without consuming it");
        X91Reset();power=await MariaApply(6);Hero.SetCurrentHpInternal(0m);Check(!power.LifesaveUsed,"structuralHP setter never claims lifesave");
        X91Reset();power=await MariaApply(6);Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());
        var reign=(MomentOfReign)ModelDb.Relic<MomentOfReign>().ToMutable();Set(reign,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(reign).ToList());
        Creature phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});
        Set(phantom,"_currentHp",100);Set(phantom,"_maxHp",100);X91NativeDamage=true;await X91Command(Enemy,1000m,ValueProp.Move,null,Hero);Equal(phantom.CurrentHp,1,"real native redirection to30A primary phantom receives the same party rescue");Equal(Hero.CurrentHp,5000,"redirected rescue does not subtract ownerHP");
        X91Reset();power=await MariaApply(6);phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});
        Call("LargeCreatureHealthHelper","SetMaxHp",phantom,5000000000m);Call("LargeCreatureHealthHelper","SetCurrentHp",phantom,5000000000m);X91NativeDamage=true;await X91Command(Enemy,decimal.MaxValue,ValueProp.Unpowered,null,phantom);Check(!power.LifesaveUsed&&(decimal)Call("LargeCreatureHealthHelper","GetCurrentHp",phantom)!>0,"native single-hit cap prevents false lethal reservation on authoritative large phantomHP");
        X91Reset();await MariaApply(6);Set(Hero,"_maxHp",1000);Set(Hero,"_currentHp",100);X91NativeDamage=true;
        await (Task)Call("MonakaFriendlyFireHelper","DamageTeam",new ThrowingPlayerChoiceContext(),NewGarden<RepentanceTimeHbrgoSaki>())!;Equal(Hero.CurrentHp,1,"actual Monaka friendly-fire damage with sourceOwner equal target is eligible for rescue");
        X91Reset();await MariaApply(6);Set(Hero,"_currentHp",100);X91NativeDamage=true;
        var galvanic=Power<GalvanicPower>(Enemy,10000);var played=FormCard<Inflame>();var affliction=(AfflictionModel)ModelDb.Affliction<Galvanized>().ToMutable();affliction.Amount=10000;affliction.Card=played;Set(played,"<Affliction>k__BackingField",affliction);
        await (Task)Native(galvanic,"AfterCardPlayed",new ThrowingPlayerChoiceContext(),Play(played))!;Equal(Hero.CurrentHp,1,"actual native Galvanic self-card-triggered enemy ability damage remains eligible for rescue");
        X91Reset();await MariaApply(6);Set(Hero,"_currentHp",1000);X91NativeDamage=true;Power<GalvanicPower>(Enemy,10000);var blood=FormCard<Bloodletting>();affliction=(AfflictionModel)ModelDb.Affliction<Galvanized>().ToMutable();affliction.Amount=10000;affliction.Card=blood;Set(blood,"<Affliction>k__BackingField",affliction);
        await X91Play(blood);Equal(Hero.CurrentHp,1,"native Bloodletting cost scope closes before same-card Galvanic damage claims lifesave");
    }
    public static async Task MariaCases(Harmony h){await MariaBasicCases();await MariaBlessingCases();await MariaLifesaveCases(h);}
}
