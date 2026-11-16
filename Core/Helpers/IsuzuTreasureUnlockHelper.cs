using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Map;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

// Only the treasure-room award callsite enters Obtain. Normal rewards, shops
// and relic commands are never intercepted or re-entered through a prefix.
internal static class IsuzuTreasureUnlockHelper
{
    private sealed class Chest
    {
        internal readonly HashSet<Player> Used=new(ReferenceEqualityComparer.Instance);
        internal Task Tail=Task.CompletedTask;
        internal bool Checked,Enabled;
        internal readonly Dictionary<string,AwardReceipt> Fallbacks=new(StringComparer.Ordinal);
    }
    private sealed class AwardReceipt
    {
        internal readonly TaskCompletionSource<RelicModel> Completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly HashSet<RelicGrabBag> PeerBags=new(ReferenceEqualityComparer.Instance);
        internal Chest Chest;
        internal string OriginalId;
        internal RelicModel Selected;
        internal bool Failed;
        internal Task<RelicModel> Task=>Completion.Task;
    }
    private sealed class PendingItems
    {
        internal readonly Dictionary<string,int> Counts=new(StringComparer.Ordinal);
    }
    private static readonly ConditionalWeakTable<TreasureRoomRelicSynchronizer,Chest> Chests=new();
    private static readonly ConditionalWeakTable<RunState,PendingItems> Pending=new();
    private static readonly ConditionalWeakTable<RelicModel,AwardReceipt> Receipts=new();
    private static readonly FieldInfo RngField=AccessTools.Field(typeof(TreasureRoomRelicSynchronizer),"_rng");
    internal static bool HasUpgrade(Player player)=>player?.Deck?.Cards.Any(card=>ReferenceEquals(card.Owner,player)&&
        !card.HasBeenRemovedFromState&&card is OpenTheLockHbrgoSaki isuzu&&isuzu.HasUpgradeEffect(OpenTheLockHbrgoSaki.TreasureUnlock))==true;
    internal static void Begin(TreasureRoomRelicSynchronizer synchronizer)
    { if(synchronizer!=null)Chests.Remove(synchronizer); }
    internal static bool HasPending(RunState run)
    {
        if(run==null||!Pending.TryGetValue(run,out PendingItems state))return false;
        lock(state.Counts)return state.Counts.Count>0;
    }
    internal static bool IsPending(RunState run,string id)
    {
        if(run==null||!Pending.TryGetValue(run,out PendingItems state))return false;
        lock(state.Counts)return state.Counts.ContainsKey(id);
    }
    private static void Hold(RunState run,string id)
    {
        PendingItems state=Pending.GetOrCreateValue(run);
        lock(state.Counts){state.Counts.TryGetValue(id,out int count);state.Counts[id]=count+1;}
    }
    private static void Release(RunState run,string id)
    {
        if(!Pending.TryGetValue(run,out PendingItems state))return;
        lock(state.Counts)
        { if(!state.Counts.TryGetValue(id,out int count))return;if(count<=1)state.Counts.Remove(id);else state.Counts[id]=count-1; }
    }
    internal static Task<RelicModel> Obtain(RelicModel original,Player player,int index=-1)
    {
        if(Receipts.TryGetValue(original,out AwardReceipt receipt))
        { receipt.Chest.Fallbacks[receipt.OriginalId]=receipt;return receipt.Task; }
        TreasureRoomRelicSynchronizer sync=RunManager.Instance.TreasureRoomRelicSynchronizer;
        if(sync==null||player?.RunState is not RunState run)return RelicCmd.Obtain(original,player,index);
        Chest chest=Chests.GetOrCreateValue(sync);
        if(!chest.Checked){chest.Checked=true;chest.Enabled=run.Players.Any(HasUpgrade);}
        if(!chest.Enabled)return RelicCmd.Obtain(original,player,index);
        bool offer=HasUpgrade(player)&&chest.Used.Add(player);
        receipt=new() {Chest=chest,OriginalId=original.Id.Entry};
        Task previous=chest.Tail;chest.Tail=receipt.Task;
        Receipts.Add(original,receipt);
        chest.Fallbacks[receipt.OriginalId]=receipt;
        _=FinishQueued(previous,receipt,original,player,index,run,(Rng)RngField.GetValue(sync),offer);
        return receipt.Task;
    }
    private static async Task FinishQueued(Task previous,AwardReceipt receipt,
        RelicModel original,Player player,int index,RunState run,Rng rng,bool offer)
    {
        try
        {
            // Every client processes the original award order, regardless of
            // which human/private choice finishes first. A failed older award
            // must not discard another player's already assigned item.
            try{await previous;}catch{}
            RelicModel result=offer&&HasUpgrade(player)?await ChooseAndObtain(original,player,index,run,rng):await RelicCmd.Obtain(original,player,index);
            RelicGrabBag[] peers;
            lock(receipt.PeerBags)
            { receipt.Selected=result;peers=receipt.PeerBags.ToArray();receipt.PeerBags.Clear(); }
            foreach(RelicGrabBag bag in peers)bag.MoveToFallback(result);
            receipt.Completion.TrySetResult(result);
        }
        catch(Exception error)
        {
            lock(receipt.PeerBags){receipt.Failed=true;receipt.PeerBags.Clear();}
            receipt.Completion.TrySetException(error);
        }
    }
    // Native award callbacks emit the canonical original immediately after the
    // mutable Obtain call. Defer only that peer-bag update until its synchronized
    // private choice and real Obtain succeed. Unchosen stock keeps its old tier.
    internal static void MoveToFallback(RelicGrabBag bag,RelicModel original)
    {
        TreasureRoomRelicSynchronizer sync=RunManager.Instance.TreasureRoomRelicSynchronizer;
        if(sync==null||!Chests.TryGetValue(sync,out Chest chest)||
            !chest.Fallbacks.TryGetValue(original.Id.Entry,out AwardReceipt receipt))
        {bag.MoveToFallback(original);return;}
        RelicModel selected;
        lock(receipt.PeerBags)
        {
            if(receipt.Failed)return;
            selected=receipt.Selected;
            if(selected==null){receipt.PeerBags.Add(bag);return;}
        }
        bag.MoveToFallback(selected);
    }
    // Testable business entry, still using the actual native selector and bag.
    internal static async Task<RelicModel> ChooseAndObtain(RelicModel original,Player player,int index,RunState run,Rng rng)
    {
        RelicModel extra=null,chosen=original;
        bool originalHeld=true,extraHeld=false;
        Hold(run,original.Id.Entry);
        try
        {
            extra=run.SharedRelicGrabBag.PullFromFront(RelicFactory.RollRarity(rng),
                relic=>relic.Id!=original.Id&&!IsPending(run,relic.Id.Entry),run);
            if(extra!=null)
            {
                Hold(run,extra.Id.Entry);extraHeld=true;
                try
                {
                    RelicModel selected=await RelicSelectCmd.FromChooseARelicScreen(player,new[] {original,extra});
                    if(HasUpgrade(player)&&ReferenceEquals(selected,extra))chosen=extra.ToMutable();
                }
                catch
                {
                    // An exceptional UI teardown may not have synchronized a
                    // choice yet. Do not grant a local-only fallback while peers
                    // still wait; release both physical offers and keep the
                    // original command failure visible.
                    HbrThirtyOneDScoutingHelper.ReturnTreasureChoice(run,extra);
                    HbrThirtyOneDScoutingHelper.ReturnTreasureChoice(run,original);
                    throw;
                }
                RelicModel discarded=ReferenceEquals(chosen,original)?extra:original;
                HbrThirtyOneDScoutingHelper.ReturnTreasureChoice(run,discarded);
                if(ReferenceEquals(discarded,original)){Release(run,original.Id.Entry);originalHeld=false;}
                else{Release(run,extra.Id.Entry);extraHeld=false;}
            }
            return await RelicCmd.Obtain(chosen,player,index);
        }
        finally
        {
            if(extraHeld)Release(run,extra.Id.Entry);
            if(originalHeld)Release(run,original.Id.Entry);
        }
    }
    internal static void Animate(NRelicInventory inventory,RelicModel original,Vector2? position=null,Vector2? scale=null)
    {
        if(!Receipts.TryGetValue(original,out AwardReceipt receipt))
        {inventory.AnimateRelic(original,position,scale);return;}
        TaskHelper.RunSafely(AnimateChosen(inventory,receipt.Task,position,scale));
    }
    private static async Task AnimateChosen(NRelicInventory inventory,Task<RelicModel> task,Vector2? position,Vector2? scale)
    {
        RelicModel selected=await task;
        if(GodotObject.IsInstanceValid(inventory))inventory.AnimateRelic(selected,position,scale);
    }
}
