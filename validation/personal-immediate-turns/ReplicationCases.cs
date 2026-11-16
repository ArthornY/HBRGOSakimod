using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;

static partial class EffectChecks
{
    static Player? PersonalTailPeer;
    static bool PersonalTailObserved;
    static void PersonalRemoveTail(PowerModel power)
    {
        if (power is not (BoostUpExtraTurnPower or ShadowDanceExtraTurnPower)) return;
        Check((bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)!,
            "source power publishes pending personal turns before its actual native removal tail");
        Check(PersonalCoordinator.GetField("_current", F)!.GetValue(null) == null,
            "source power removal cannot begin a private hand before the source command finishes");
        CombatManager.Instance.SetReadyToEndTurn(Player, true);
        CombatManager.Instance.SetReadyToEndTurn(PersonalTailPeer!, true);
        PersonalTailObserved = true;
    }
    static async Task PersonalSourceTailCases()
    {
        var harmony = new Harmony(ProductionId + ".personal-source-tail");
        MethodInfo remove = typeof(PowerCmd).GetMethod(nameof(PowerCmd.Remove), F, null, new[] { typeof(PowerModel) }, null)!;
        harmony.Patch(remove, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(PersonalRemoveTail), F)!));
        try
        {
            foreach (bool boost in new[] { true, false })
            {
                PersonalTailPeer = PersonalWorld(); PersonalTailObserved = false;
                PowerModel power = boost ? Power<BoostUpExtraTurnPower>(Hero, 2) : Power<ShadowDanceExtraTurnPower>(Hero, 2);
                await power.AfterApplied(Hero, null!);
                Check(PersonalTailObserved && !Hero.Powers.Contains(power),
                    power.GetType().Name + " performs real native source removal after publishing its two-turn batch");
#if HBR_BETA_NATIVE
                Check(!PersonalEndSignalCompleted(), "source removal's genuine beta ready calls cannot consume the ordinary signal while pending exists");
#else
                var deferred = (Func<Task>)typeof(CombatManager).GetField("_deferredEndTurnTransition", F)!.GetValue(CombatManager.Instance)!;
                Set(CombatManager.Instance, "_deferredEndTurnTransition", null); await deferred();
                Check(!PersonalEndSignalCompleted(), "source removal's genuine stable ready callback waits behind the pending batch");
#endif
                var first = PersonalIdentity(PersonalTicketObject());
                await PersonalNetExecute(true, Player, first.Id, first.Ordinal);
                await PersonalNetExecute(false, Player, first.Id, first.Ordinal);
#if !HBR_BETA_NATIVE
                var betweenTurns = (Func<Task>?)typeof(CombatManager).GetField("_deferredEndTurnTransition", F)!.GetValue(CombatManager.Instance);
                Set(CombatManager.Instance, "_deferredEndTurnTransition", null);
                if (betweenTurns != null) await betweenTurns();
#endif
                Check(!PersonalEndSignalCompleted(), "ordinary turn stays deferred between consecutive turns of the same source batch");
                var second = PersonalIdentity(PersonalTicketObject());
                Check(second.Id == first.Id && second.Ordinal == first.Ordinal + 1,
                    "second source stack becomes the next ordinal of the same recorded batch");
                await PersonalNetExecute(true, Player, second.Id, second.Ordinal);
                await PersonalNetExecute(false, Player, second.Id, second.Ordinal);
                Check(PersonalEndSignalCompleted(), "ordinary native turn-end signal is released after the entire source batch restores");
            }
        }
        finally { harmony.Unpatch(remove, typeof(EffectChecks).GetMethod(nameof(PersonalRemoveTail), F)!); PersonalTailPeer = null; }
    }

    static CardModel PersonalCombatCard(CardModel card, CardPile pile, CardModel? origin = null)
    {
        if (origin != null) card.DeckVersion = origin;
        pile.AddInternal(card); ((List<CardModel>)typeof(CombatState).GetField("_allCards", F)!.GetValue(Combat)!).Add(card);
        return card;
    }
    static async Task PersonalMultipleOwnersCase()
    {
        Player peer = PersonalWorld();
        CardModel ownerCard = PersonalCombatCard(PersonalOwned<StrikeIronclad>(), Player.PlayerCombatState!.Hand);
        CardModel peerCard = PersonalCombatCard(PeerStrike(peer), peer.PlayerCombatState!.Hand);
        PersonalCall("Request", Player, 1); PersonalCall("Request", peer, 1);
        var first = PersonalIdentity(PersonalTicketObject());
        await PersonalNetExecute(true, Player, first.Id, first.Ordinal);
        await PersonalNetExecute(false, Player, first.Id, first.Ordinal);
        Check(ownerCard.Pile == Player.PlayerCombatState!.Hand && peerCard.Pile == peer.PlayerCombatState!.Hand &&
            !(bool)PersonalCall("IsActiveFor", peer)! && (bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)!,
            "finishing the first player's recorded session restores both ordinary hands while retaining the queued teammate ticket");
        var next = PersonalIdentity(PersonalTicketObject());
        await PersonalNetExecute(true, peer, next.Id, next.Ordinal);
        Check((bool)PersonalCall("IsActiveFor", peer)! && !(bool)PersonalCall("IsActiveFor", Player)! &&
            peerCard.Pile == null && ownerCard.Pile == Player.PlayerCombatState!.Hand &&
            peer.PlayerCombatState!.Energy == 2 && Player.PlayerCombatState!.Energy == 7,
            "the queued teammate's own serialized begin borrows only its own resources after the first owner's restoration");
        PlayerCmd.EndTurn(peer, true);
        Check(!CombatManager.Instance.IsPlayerReadyToEndTurn(peer), "private end interception follows the current teammate owner rather than the original requester");
        await PersonalNetExecute(false, peer, next.Id, next.Ordinal);
        Check(peerCard.Pile == peer.PlayerCombatState!.Hand && peer.PlayerCombatState!.Energy == 5 &&
            ownerCard.Pile == Player.PlayerCombatState!.Hand && Player.PlayerCombatState!.Energy == 7 &&
            !(bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)!,
            "both independently owned recorded personal turns restore their original card identities/energy and clear the FIFO schedule");
    }
    static async Task PersonalDeckAndReplicationCases()
    {
        foreach (Type type in new[] { typeof(StrikeIronclad), typeof(DefendIronclad) })
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<CardModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        async Task<string[]> World()
        {
            Player peer = PersonalWorld();
            // Simulate two independent processes with their own initial request
            // sequence, then run identical deserialized production net actions.
            Set(PersonalPlayerSchedule, "_sequence", 0L);
            var upgraded = PersonalOwned<StrikeIronclad>(); upgraded.UpgradeInternal(); upgraded.FinalizeUpgradeInternal();
            var defend = PersonalOwned<DefendIronclad>(); var exhausted = PersonalOwned<StrikeIronclad>();
            Player.Deck.AddInternal(upgraded); Player.Deck.AddInternal(defend); Player.Deck.AddInternal(exhausted);
            var ordinaryHand = PersonalCombatCard(PersonalOwned<StrikeIronclad>(), Player.PlayerCombatState!.Hand, upgraded);
            var ordinaryExhaust = PersonalCombatCard(PersonalOwned<StrikeIronclad>(), Player.PlayerCombatState!.ExhaustPile, exhausted);
            var snapshots = new List<string>();
            void Snapshot(string stage) => snapshots.Add(JsonSerializer.Serialize(new
            {
                stage, ownerEnergy = Player.PlayerCombatState!.Energy, peerEnergy = peer.PlayerCombatState!.Energy,
                ownerReady = CombatManager.Instance.IsPlayerReadyToEndTurn(Player), peerReady = CombatManager.Instance.IsPlayerReadyToEndTurn(peer),
                active = (bool)PersonalCall("IsActiveFor", Player)!, busy = (bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)!,
                ownerCards = Player.PlayerCombatState!.AllPiles.Select(pile => new { pile.Type, cards = pile.Cards.Select(card => new { kind = card.GetType().Name, card.IsUpgraded, origin = card.DeckVersion?.GetType().Name }).ToArray() }).ToArray(),
                peerCards = peer.PlayerCombatState!.AllPiles.Select(pile => new { pile.Type, count = pile.Cards.Count }).ToArray()
            }));
            PersonalCall("Request", Player, 1); var ticket = PersonalIdentity(PersonalTicketObject()); Snapshot("request");
            await PersonalNetExecute(true, Player, ticket.Id, ticket.Ordinal); Snapshot("begin");
            CardModel[] temporary = Player.PlayerCombatState!.AllCards.ToArray();
            Check(temporary.Length == 2 && temporary.All(card => !ReferenceEquals(card, ordinaryHand) && !ReferenceEquals(card, upgraded) && !ReferenceEquals(card.DeckVersion, exhausted)),
                "real native PopulateCombatState clones the available permanent deck and excludes the exhausted permanent origin");
            Check(temporary.Single(card => ReferenceEquals(card.DeckVersion, upgraded)).IsUpgraded && upgraded.IsUpgraded && Player.Deck.Cards.Count == 3,
                "personal deck preserves native upgrade state and permanent deck membership without sharing card instances");
            peer.PlayerCombatState!.Energy -= 2;
            PersonalCombatCard(PeerStrike(peer), peer.PlayerCombatState!.DiscardPile); Snapshot("peer-play");
            CombatManager.Instance.SetReadyToEndTurn(peer, true); Snapshot("peer-ready");
            PlayerCmd.EndTurn(Player, true); Snapshot("owner-private-end");
            await PersonalNetExecute(false, Player, ticket.Id, ticket.Ordinal); Snapshot("finish");
            Check(ordinaryHand.Pile == Player.PlayerCombatState!.Hand && ordinaryExhaust.Pile == Player.PlayerCombatState!.ExhaustPile && temporary.All(card => card.HasBeenRemovedFromState),
                "personal finish disposes every temporary clone and restores each original combat card to its exact ordinary pile: " +
                JsonSerializer.Serialize(new { hand = ordinaryHand.Pile?.Type, exhausted = ordinaryExhaust.Pile?.Type,
                    originalHandRemoved = ordinaryHand.HasBeenRemovedFromState, originalExhaustRemoved = ordinaryExhaust.HasBeenRemovedFromState,
                    clones = temporary.Select(card => new { pile = card.Pile?.Type, card.HasBeenRemovedFromState }).ToArray() }));
            return snapshots.ToArray();
        }
        string[] first = await World(), second = await World();
        Check(first.SequenceEqual(second),
            "two independent native session states replay the same begin/peer-play/peer-ready/end commands into identical piles, energy and ready flags at every boundary");
    }
    static CardModel PeerStrike(Player peer)
    {
        var card = PersonalOwned<StrikeIronclad>(); Set(card, "<IsMutable>k__BackingField", true); Set(card, "_owner", peer);
        Set(card, "<Id>k__BackingField", ModelDb.GetId(typeof(StrikeIronclad))); return card;
    }

    static readonly Exception PersonalRestoreError = new InvalidOperationException("controlled-personal-restore-failure");
    static bool PersonalThrowRestore() => throw PersonalRestoreError;
    static async Task PersonalRestoreFailureCase()
    {
        Player peer = PersonalWorld(); CombatManager.Instance.SetReadyToEndTurn(Player, true);
        var ticket = await PersonalBeginOwner();
        CombatManager.Instance.SetReadyToEndTurn(peer, true);
        object session = PersonalCoordinator.GetField("_current", F)!.GetValue(null)!;
        object hand = session.GetType().GetField("Hand", F)!.GetValue(session)!;
        var harmony = new Harmony(ProductionId + ".personal-restore-failure");
        MethodInfo restore = Helper("OverclockSuspendedHand").GetMethod("TryRestore", F)!;
        MethodInfo throwing = typeof(EffectChecks).GetMethod(nameof(PersonalThrowRestore), F)!;
        harmony.Patch(restore, prefix: new HarmonyMethod(throwing));
        try
        {
            try
            {
                await PersonalNetExecute(false, Player, ticket.Id, ticket.Ordinal);
                Check(false, "controlled restoration failure must propagate");
            }
            catch (InvalidOperationException error) when (ReferenceEquals(error, PersonalRestoreError))
            { Check(true, "failed pile restoration rethrows the exact original exception instance after independent cleanup"); }
            Check(Player.PlayerCombatState!.Phase == PlayerTurnPhase.Play && CombatManager.Instance.IsPlayerReadyToEndTurn(Player) && CombatManager.Instance.IsPlayerReadyToEndTurn(peer),
                "failed hand restoration still restores owner phase and saved ready without rolling back teammate's new vote");
            Check(!(bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)! && PersonalCoordinator.GetField("_current", F)!.GetValue(null) == null,
                "failed hand restoration always clears the active schedule/session and releases ordinary end gates");
            object queue = Invoke(typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSet).GetMethod("GetQueue", F)!,
                RunManager.Instance.ActionQueueSet, Player.NetId)!;
            Check(!(bool)queue.GetType().GetField("isPaused", F)!.GetValue(queue)! && !(bool)queue.GetType().GetField("isCancellingPlayerDrivenCombatActions", F)!.GetValue(queue)!,
                "failed hand restoration independently restores owner queue pause and cancellation flags");
        }
        finally
        {
            harmony.Unpatch(restore, throwing);
            Invoke(restore, hand);
        }
    }
}
