using System.Reflection;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
static partial class EffectChecks
{
    static (FollowBehindMeHbrgoSaki Origin,FollowBehindMeHbrgoSaki Copy) KPhysical(int[] upgrades,PileType pile=PileType.Hand)
    {
        var origin=KCard(upgrades);MisatoPermanent(origin);
        var list=(List<CardModel>)typeof(MegaCrit.Sts2.Core.Runs.RunState).GetField("_allCards",F)!.GetValue(RunState)!;if(!list.Contains(origin))list.Add(origin);
        var card=(FollowBehindMeHbrgoSaki)GMirror(origin,pile==PileType.Hand);
        if(card.Pile!.Type!=pile){card.Pile.RemoveInternal(card,true);pile.GetPile(Player).AddInternal(card);}
        return(origin,card);
    }
    static object KSuspend()
    {
        object?[] args={Player,null};Check((bool)Call("OverclockSuspendedHand","TrySuspend",args)!,"85 native shared snapshot accepts actual ordinary piles");return args[1]!;
    }
    static CardModel[] KPrepare(object snapshot)=>(CardModel[])Native(snapshot,"PrepareOverclockDeck")!;
    static async Task KSnapshotCases()
    {
        foreach(bool od in new[]{false,true})
        {
            KReset();var lead=KPhysical(new[]{4});var keep=KPhysical(Array.Empty<int>(),PileType.Draw);keep.Copy.EnergyCost.SetThisTurn(0);lead.Copy.EnergyCost.SetThisTurn(0);
            using(MisatoExtra(od))
            {
                object snapshot=KSuspend();var fresh=KPrepare(snapshot);var extraLeader=fresh.OfType<FollowBehindMeHbrgoSaki>().Single(c=>c.DeckVersion==lead.Origin);var extraKeep=fresh.Single(c=>c.DeckVersion==keep.Origin);
                await CardPileCmd.Add(extraLeader,PileType.Hand);await CardPileCmd.Add(extraKeep,PileType.Hand);var unrelated=FCard(typeof(KurosawaAttackProbe));await ChasePlay(extraLeader);await KExtraFlush(od);
                Check(extraKeep.Pile==Player.PlayerCombatState!.Hand&&unrelated.Pile==Player.PlayerCombatState.DiscardPile,"85 actual inserted flush keeps only marked garden mirror od="+od);
                int completions=0;Player.PlayerCombatState.Hand.CardAddFinished+=()=>completions++;
                Check((bool)Native(snapshot,"TryRestore")!,"85 actual shared snapshot restores with narrow carry od="+od);
                Check(keep.Copy.Pile==Player.PlayerCombatState.Hand&&lead.Copy.Pile==Player.PlayerCombatState.Hand,"85 retained mirror transfers its suspended original to resumed hand od="+od);
                Check(fresh.All(c=>c.HasBeenRemovedFromState)&&unrelated.HasBeenRemovedFromState&&Player.PlayerCombatState.AllCards.Count()==2,"85 disposable pool still removed with no mirror duplication od="+od);
                Equal(FCost(keep.Copy),0,"85 ordinary card's paused local cost survives carry mapping od="+od);Equal(Player.PlayerCombatState.Energy,50,"85 original ordinary energy restored after extra payment od="+od);
                Equal(completions,1,"85 mapped carried card completes native add exactly once without renderer od="+od);
                Check(!(bool)Native(snapshot,"TryRestore")!,"85 duplicate restore cannot carry a second time od="+od);
            }
        }
        foreach(bool od in new[]{false,true})
        {
            KReset();var dog=KPhysical(new[]{6},PileType.Draw);
            using(MisatoExtra(od))
            {
                object snapshot=KSuspend();var mirror=(FollowBehindMeHbrgoSaki)KPrepare(snapshot).Single();await CardPileCmd.Add(mirror,PileType.Hand);await ChasePlay(mirror);await KExtraFlush(od);
                Check(mirror.Pile==Player.PlayerCombatState!.Hand,"85 actual inserted discard-after-flush returns dog mirror od="+od);
                Check((bool)Native(snapshot,"TryRestore")!&&dog.Copy.Pile==Player.PlayerCombatState.Hand&&mirror.HasBeenRemovedFromState,"85 dog hand return carries original rather than dead mirror od="+od);
            }
            Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);Set(Player.PlayerCombatState,"_energy",50);await ChasePlay(dog.Copy);await KNativeFlush();Check(dog.Copy.Pile==Player.PlayerCombatState.DiscardPile,"85 dog once-battle receipt is shared with original after inserted mirror use od="+od);
        }
        KReset();var leader=KPhysical(new[]{4});
        using(MisatoExtra(true))
        {
            object snapshot=KSuspend();var mirror=(FollowBehindMeHbrgoSaki)KPrepare(snapshot).Single();await CardPileCmd.Add(mirror,PileType.Hand);await ChasePlay(mirror);
            var temporary=KCard();temporary.EnergyCost.SetThisTurn(0);await KExtraFlush(true);
            Check((bool)Native(snapshot,"TryRestore")!&&temporary.Pile==Player.PlayerCombatState!.Hand&&!temporary.HasBeenRemovedFromState,"85 marked lawful no-origin temporary entity itself survives pool disposal");
            Equal(FCost(temporary),2,"85 carried temporary card expires inserted local cost using native cleanup");Check(!temporary.ShouldRetainThisTurn,"85 inserted-only temporary retain does not leak into ordinary turn");
        }
        KReset();leader=KPhysical(new[]{4});var keeper=KPhysical(Array.Empty<int>(),PileType.Draw);
        for(int i=1;i<CardPile.MaxCardsInHand;i++)FCard(typeof(FOtherCard));
        var savedHand=Player.PlayerCombatState!.Hand.Cards.ToArray();
        using(MisatoExtra(true))
        {
            object snapshot=KSuspend();var fresh=KPrepare(snapshot);var source=fresh.OfType<FollowBehindMeHbrgoSaki>().Single(c=>c.DeckVersion==leader.Origin);var keep=fresh.Single(c=>c.DeckVersion==keeper.Origin);
            await CardPileCmd.Add(source,PileType.Hand);await CardPileCmd.Add(keep,PileType.Hand);await ChasePlay(source);await KExtraFlush(true);int discarded=0;Player.PlayerCombatState.DiscardPile.CardAddFinished+=()=>discarded++;
            Check((bool)Native(snapshot,"TryRestore")!&&Player.PlayerCombatState.Hand.Cards.SequenceEqual(savedHand),"85 full restored ordinary hand preserves its exact order before incoming carry");
            Check(keeper.Copy.Pile==Player.PlayerCombatState.DiscardPile&&Player.PlayerCombatState.Hand.Cards.Count==CardPile.MaxCardsInHand,"85 full resumed hand routes carried original to Discard rather than overflow");Equal(discarded,1,"85 full-hand fallback add finishes once");
        }
        KReset();leader=KPhysical(new[]{4});keeper=KPhysical(Array.Empty<int>(),PileType.Draw);
        using(MisatoExtra(true))
        {
            object snapshot=KSuspend();var fresh=KPrepare(snapshot);var source=fresh.OfType<FollowBehindMeHbrgoSaki>().Single(c=>c.DeckVersion==leader.Origin);var keep=fresh.Single(c=>c.DeckVersion==keeper.Origin);
            await CardPileCmd.Add(source,PileType.Hand);await CardPileCmd.Add(keep,PileType.Hand);await ChasePlay(source);await KExtraFlush(true);await CardPileCmd.Add(keep,PileType.Exhaust);
            Check((bool)Native(snapshot,"TryRestore")!&&keeper.Copy.Pile==Player.PlayerCombatState.DrawPile&&keep.HasBeenRemovedFromState,"85 a marked mirror later exhausted cannot carry or take its ordinary original");
        }
        KReset();leader=KPhysical(new[]{4});keeper=KPhysical(Array.Empty<int>(),PileType.Draw);
        using(MisatoExtra(true))
        {
            object snapshot=KSuspend();var fresh=KPrepare(snapshot);var source=fresh.OfType<FollowBehindMeHbrgoSaki>().Single(c=>c.DeckVersion==leader.Origin);var keep=fresh.Single(c=>c.DeckVersion==keeper.Origin);
            await CardPileCmd.Add(source,PileType.Hand);await CardPileCmd.Add(keep,PileType.Hand);await ChasePlay(source);await KExtraFlush(true);Set(Hero,"_currentHp",0);
            Check(!(bool)Native(snapshot,"TryRestore")!&&leader.Copy.HasBeenRemovedFromState&&keeper.Copy.HasBeenRemovedFromState&&keep.HasBeenRemovedFromState,"85 dead owner abandons carried and suspended cards instead of resurrecting them");
            Set(Hero,"_currentHp",5000);Check(!(bool)Native(snapshot,"TryRestore")!&&Player.PlayerCombatState!.Hand.IsEmpty,"85 later revival cannot restore an abandoned snapshot");
        }
        KReset();leader=KPhysical(new[]{4});keeper=KPhysical(Array.Empty<int>(),PileType.Draw);
        using(MisatoExtra(true))
        {
            object snapshot=KSuspend();var fresh=KPrepare(snapshot);var source=fresh.OfType<FollowBehindMeHbrgoSaki>().Single(c=>c.DeckVersion==leader.Origin);var keep=fresh.Single(c=>c.DeckVersion==keeper.Origin);
            await CardPileCmd.Add(source,PileType.Hand);await CardPileCmd.Add(keep,PileType.Hand);await ChasePlay(source);await KExtraFlush(true);Native(snapshot,"Abandon");
            Check(leader.Copy.HasBeenRemovedFromState&&keeper.Copy.HasBeenRemovedFromState&&keep.HasBeenRemovedFromState&&!(bool)Native(snapshot,"TryRestore")!,"85 explicit live abandonment is terminal and does not preserve narrow carry");
        }
        KReset();var plain=KPhysical(Array.Empty<int>());
        using(MisatoExtra(true))
        {
            object snapshot=KSuspend();var fresh=KPrepare(snapshot);var mirror=fresh.Single();await CardPileCmd.Add(mirror,PileType.Hand);mirror.GiveSingleTurnRetain();await KExtraFlush(true);
            Check(mirror.Pile==Player.PlayerCombatState!.DiscardPile,"85 unrelated native Retain does not broaden preexisting OD flush policy");
            Check((bool)Native(snapshot,"TryRestore")!&&plain.Copy.Pile==Player.PlayerCombatState.Hand&&mirror.HasBeenRemovedFromState,"85 default unmarked pool restores unchanged original and disposes mirror");
        }
    }
}
