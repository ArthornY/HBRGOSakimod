using System.Reflection;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    static bool Bond50Leaf;
    static (DemonKingAndFamiliarBond Bond, WorldIsMyLaboratoryHbrgoSaki Yama, WorldConquestHbrgoSaki Bungo) Bond50Reset()
    {
        MachinaReset(0);var bond=BondRelic<DemonKingAndFamiliarBond>();
        var yama=BondPermanent<WorldIsMyLaboratoryHbrgoSaki>();var bungo=BondPermanent<WorldConquestHbrgoSaki>();
        return(bond,yama,bungo);
    }
    static decimal Bond50Bonus(CardModel card,Creature? target=null,Creature? actor=null,ValueProp props=ValueProp.Move)=>(decimal)Call("DemonKingAndFamiliarBondHelper","GetDamageAmplificationPercent",card,actor??Hero,target??Enemy,props)!;
    static Task Bond50Played(DemonKingAndFamiliarBond bond,CardModel card)=>bond.AfterCardPlayedLate(new ThrowingPlayerChoiceContext(),Play(card));
    static async Task<HighDimensionalShellPower> Bond50Shell(int amount)
    {await PowerCmd.Apply<HighDimensionalShellPower>(new ThrowingPlayerChoiceContext(),Enemy,amount,Enemy,null,true);return Enemy.GetPower<HighDimensionalShellPower>()!;}
    static async Task Bond50Cases()
    {
        var s=Bond50Reset();Player.Deck.RemoveInternal(s.Bungo);
        Check(!BondActive(s.Bond)&&s.Bond.DisplayAmount==1,"50: only Yamawaki leaves the visible bond marker inactive");
        s.Bungo=BondPermanent<WorldConquestHbrgoSaki>();Check(BondActive(s.Bond)&&s.Bond.DisplayAmount==2,"50: both physical partner card types in the same owner's permanent deck activate the bond");
        BondPermanent<WorldConquestHbrgoSaki>();Equal(s.Bond.DisplayAmount,2,"50: copied permanent Bungo cards never create additional partner kinds");
        Equal(Bond50Bonus(s.Bungo),0m,"50: Bungo played before Yamawaki has no follower bonus");
        await Bond50Played(s.Bond,s.Bungo);Equal(Bond50Bonus(s.Yama),0m,"50: playing Bungo alone never infers a DP-break receipt from a generic after-play hook");
        decimal baseline=Preview(Enemy,Hero,6m,ValueProp.Move,s.Bungo);
        await Bond50Played(s.Bond,s.Yama);
        Equal(Bond50Bonus(s.Bungo),50m,"50: a completed Yamawaki play enables the follower bonus");
        Equal(Preview(Enemy,Hero,6m,ValueProp.Move,s.Bungo),decimal.Floor(baseline*1.5m),"50: the real ordinary damage preview includes follower in the standard damage-amplification bucket");
        await Command(Hero,6m,ValueProp.Move,s.Bungo);Equal(Hits[^1].Final,decimal.Floor(baseline*1.5m),"50: the real native damage command receives the follower bonus");
        await Bond50Played(s.Bond,s.Bungo);await Command(Hero,6m,ValueProp.Move,s.Bungo);
        Equal(Hits[^1].Final,decimal.Floor(baseline*1.5m),"50: every later Bungo play keeps the bonus without consuming a one-shot charge");
        await PowerCmd.Apply<HotaruDamageAmplificationPower>(new ThrowingPlayerChoiceContext(),Hero,50m,Hero,null,true);
        Equal(Preview(Enemy,Hero,6m,ValueProp.Move,s.Bungo),decimal.Floor(baseline*2m),"50: follower50 adds to another damage-amplification50 rather than multiplying into225 percent");
        Equal((decimal)Call("DemonKingAndFamiliarBondHelper","GetDamageAmplificationPercent",ModelDb.GetById<CardModel>(s.Bungo.Id),null,Enemy,ValueProp.Move)!,0m,"50: an unowned canonical card and missing dealer safely receive no bonus");
        Equal(Bond50Bonus(s.Bungo,props:ValueProp.Unpowered),0m,"50: an unpowered reaction never receives the card attack bonus");
        Equal(Bond50Bonus(s.Bungo,Hero),0m,"50: the partner bonus never targets the owner's own side");
        var peer=MachinaPeer(0);Equal(Bond50Bonus(s.Bungo,actor:peer.Creature),0m,"50: a teammate acting with another owner's card attribution cannot borrow follower history");
        Player.Deck.RemoveInternal(s.Yama);Equal(Bond50Bonus(s.Bungo),0m,"50: losing the permanent Yamawaki partner immediately deactivates both bonuses");

        foreach(string extra in new[]{"OverclockImmediateTurnCoordinator","PersonalImmediateTurnCoordinator"})
        {
            s=Bond50Reset();await Bond50Played(s.Bond,s.Yama);
            Type coordinator=Helper(extra);FieldInfo field=coordinator.GetField("_current",F)!;object? previous=field.GetValue(null);
            FieldInfo? combatField=coordinator.GetField("_combat",F);object? oldCombat=combatField?.GetValue(null);
            object session=RuntimeHelpers.GetUninitializedObject(coordinator.GetNestedType("Session",F)!);Set(session,"Combat",Combat);
            if(extra.StartsWith("Overclock"))Set(session,"Players",new[]{Player});else Set(session,"Owner",Player);
            try
            {
                combatField?.SetValue(null,Combat);field.SetValue(null,session);
                Check(Call("OverclockImmediateTurnCoordinator","GetTurnToken",Player)!=null,"50: actual "+extra+" exposes its own inserted-turn token");
                await s.Bond.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);
                Equal(Bond50Bonus(s.Bungo),0m,"50: entering "+extra+" starts a separate partner ledger");
                await Bond50Played(s.Bond,s.Yama);Equal(Bond50Bonus(s.Bungo),50m,"50: Yamawaki enables follower only within this "+extra+" token");
            }
            finally{field.SetValue(null,previous);combatField?.SetValue(null,oldCombat);}
            Equal(Bond50Bonus(s.Bungo),50m,"50: returning from "+extra+" restores the ordinary turn's earlier follower receipt");
            await s.Bond.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(),Player);Equal(Bond50Bonus(s.Bungo),0m,"50: a genuinely new ordinary turn clears the prior ordinary partner history");
        }
        s=Bond50Reset();await Bond50Played(s.Bond,s.Yama);Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Enemy);
        await s.Bond.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,new[]{Enemy},Combat);
        Equal(Bond50Bonus(s.Bungo),0m,"50: the enemy-side turn cannot inherit the player's follower receipt");
        await Bond50Played(s.Bond,s.Yama);Equal(Bond50Bonus(s.Bungo),50m,"50: a true enemy-side auto-play can establish its separate same-side-turn receipt");
        await s.Bond.BeforeCombatStart();Equal(Bond50Bonus(s.Bungo),0m,"50: a new combat clears all ordinary, enemy and weak-token ledgers");

        foreach(bool bungo in new[]{false,true})
        {
            s=Bond50Reset();var shell=await Bond50Shell(1);Bond50Leaf=true;
            await Command(Hero,6m,ValueProp.Move,bungo?s.Bungo:s.Yama);
            Check(Enemy.HasPower<BreakPower>()&&!Enemy.Powers.Contains(shell),"50: actual DP absorption and AfterDamageReceived apply Break and remove the depleted shell");
            Equal(Bond50Bonus(s.Yama),bungo?50m:0m,"50: only an actual attributed Bungo DP break opens this exact enemy for Yamawaki");
            Creature other=(Creature)H("Actor",Blank<HBRGOSakiMod.Core.Models.Monsters.SmallHopper>(),null,null)!;
            Equal(Bond50Bonus(s.Yama,other),0m,"50: an unrelated enemy never inherits the first target's exact DP-break receipt");
            if(bungo)
            {
                await PowerCmd.Apply<HotaruDamageAmplificationPower>(new ThrowingPlayerChoiceContext(),Hero,50m,Hero,null,true);
                decimal withBoth=Preview(Enemy,Hero,6m,ValueProp.Move,s.Yama);
                Player.Deck.RemoveInternal(s.Bungo);decimal withoutBond=Preview(Enemy,Hero,6m,ValueProp.Move,s.Yama);
                // Remove the other amplification for a base unaffected by the
                // target's separate Break/destruction and Yamawaki-HP multipliers.
                await PowerCmd.Remove(Hero.GetPower<HotaruDamageAmplificationPower>()!);
                decimal baseYama=Preview(Enemy,Hero,6m,ValueProp.Move,s.Yama);
                Equal(withBoth,decimal.Floor(baseYama*2m),"50: exact-target familiar opening also enters the standard additive amplification bucket");
                Equal(withoutBond,decimal.Floor(baseYama*1.5m),"50: removing the partner stops the opened-target bonus immediately");
            }
        }
        s=Bond50Reset();Power<BreakPower>(Enemy,3);await Bond50Played(s.Bond,s.Bungo);
        Equal(Bond50Bonus(s.Yama),0m,"50: a pre-existing Break plus Bungo play is insufficient without a real DP-break event");
        var refill=await Bond50Shell(1);Bond50Leaf=true;await Command(Hero,6m,ValueProp.Move,s.Bungo);
        Equal(Bond50Bonus(s.Yama),0m,"50: depleting a newly granted shell on an already-broken enemy is not entering Break again");
        s=Bond50Reset();var huge=await Bond50Shell(100000);Bond50Leaf=true;await Command(Hero,6m,ValueProp.Move,s.Bungo);
        Check(huge.CurrentDp>0&&!Enemy.HasPower<BreakPower>()&&Bond50Bonus(s.Yama)==0m,"50: a Bungo hit that damages but fails to break DP grants no opening");
        s=Bond50Reset();var immediate=await Bond50Shell(1);
        await(Task)Invoke(typeof(HighDimensionalShellPower).GetMethod("BreakImmediately",F)!,immediate,new ThrowingPlayerChoiceContext(),0)!;
        Equal(Bond50Bonus(s.Yama),0m,"50: source-less immediate shell removal cannot counterfeit a Bungo damage receipt");
    }
}
