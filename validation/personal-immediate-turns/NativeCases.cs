using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

static partial class EffectChecks
{
    static readonly List<string> PersonalNativeEvents = [];
    static bool PersonalThrowDraw;
    static int PersonalSerializedCommands;
    static Type PersonalCoordinator => Helper("PersonalImmediateTurnCoordinator");
    static object PersonalPlayerSchedule => PersonalCoordinator.GetField("Schedule", F)!.GetValue(null)!;
    static object? PersonalCall(string name, params object?[] args) =>
        Invoke(PersonalCoordinator.GetMethod(name, F)!, null, args);
    static T PersonalOwned<T>() where T : CardModel, new()
    {
        T card = ModelDb.Contains(typeof(T))
            ? (T)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(T))).MutableClone()
            : Owned<T>();
        Set(card, "_owner", Player); return card;
    }
    static bool PersonalFalse(ref bool __result) { __result = false; return false; }
    static bool PersonalDraw(Player player, decimal count, ref Task<IEnumerable<CardModel>> __result)
    {
        if (PersonalThrowDraw) throw new InvalidOperationException("controlled-personal-draw-failure");
        PersonalDrawObserver?.Invoke(player);
        // Isolate animation/selection/network completion only. Pile movement is
        // real; the deck itself was cloned by native PopulateCombatState.
        var cards = player.PlayerCombatState!.DrawPile.Cards.Take((int)count).ToArray();
        foreach (CardModel card in cards)
        {
            player.PlayerCombatState!.DrawPile.RemoveInternal(card);
            player.PlayerCombatState!.Hand.AddInternal(card);
        }
        PersonalNativeEvents.Add("draw:" + player.NetId + ":" + cards.Length);
        __result = Task.FromResult<IEnumerable<CardModel>>(cards); return false;
    }

    static Player PersonalWorld()
    {
        PersonalCall("Clear"); Reset(); As31O(true); CombatInProgressWithTurn();
        Set(Combat, "<CurrentSide>k__BackingField", CombatSide.Player);
        Set(Combat, "_allCards", new List<CardModel>());
        Set(Player, "<NetId>k__BackingField", 101UL);
        Set(Player, "_relics", new List<RelicModel>());
        void State(Player player)
        {
            var state = Blank<PlayerCombatState>();
            Set(state, "_player", player); Set(state, "_pets", new List<Creature>());
            Set(state, "<TurnNumber>k__BackingField", 4);
            Set(state, "_phase", PlayerTurnPhase.Play);
            Set(state, "_energy", player == Player ? 7 : 5);
            foreach (var row in new[] { ("Hand", PileType.Hand), ("DrawPile", PileType.Draw),
                ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust), ("PlayPile", PileType.Play) })
                Set(state, "<" + row.Item1 + ">k__BackingField", new CardPile(row.Item2));
            Set(player, "<PlayerCombatState>k__BackingField", state);
            Set(player, "<Deck>k__BackingField", new CardPile(PileType.Deck));
            Set(player, "_runPiles", null);
        }
        State(Player);
        var peer = Blank<Player>(); Set(peer, "_runState", RunState); Set(peer, "<NetId>k__BackingField", 202UL);
        Set(peer, "<Character>k__BackingField", Blank<HbrgoSaki31O>());
        Set(peer, "_relics", new List<RelicModel>()); State(peer);
        Creature actor = (Creature)H("Actor", null, peer, null)!; Set(peer, "<Creature>k__BackingField", actor);
        Set(RunState, "_players", new List<Player> { Player, peer });
        Set(Combat, "_allies", new List<Creature> { Hero, actor });
        var queues = new ActionQueueSet(new[] { Player, peer });
        Set(RunManager.Instance, "<ActionQueueSet>k__BackingField", queues);
        Set(RunManager.Instance, "<ActionExecutor>k__BackingField", Blank<ActionExecutor>());
#if HBR_BETA_NATIVE
        object turn = typeof(CombatManager).GetField("_turnState", F)!.GetValue(CombatManager.Instance)!;
        Set(turn, "<PlayersThatCannotUnEndTurn>k__BackingField", new HashSet<Player>());
        Type signalType = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.EndTurnSignal", true)!;
        Set(turn, "<EndTurnSignalSource>k__BackingField", Activator.CreateInstance(typeof(TaskCompletionSource<>).MakeGenericType(signalType)));
#else
        Set(CombatManager.Instance, "_playersReadyToEndTurn", new HashSet<Player>());
        Set(CombatManager.Instance, "_playersTakingExtraTurn", new List<Player>());
        Set(CombatManager.Instance, "_inPlayerTurnSetup", true);
        Set(CombatManager.Instance, "_deferredEndTurnTransition", null);
#endif
        PersonalNativeEvents.Clear(); return peer;
    }

    static bool PersonalEndSignalCompleted()
    {
#if HBR_BETA_NATIVE
        object turn = typeof(CombatManager).GetField("_turnState", F)!.GetValue(CombatManager.Instance)!;
        object source = turn.GetType().GetProperty("EndTurnSignalSource", F)!.GetValue(turn)!;
        return ((Task)source.GetType().GetProperty("Task")!.GetValue(source)!).IsCompleted;
#else
        return typeof(CombatManager).GetField("_deferredEndTurnTransition", F)!.GetValue(CombatManager.Instance) != null;
#endif
    }

    static object PersonalTicketObject() => PersonalPlayerSchedule.GetType().GetProperty("Next", F)!.GetValue(PersonalPlayerSchedule)!;
    static (long Id, int Ordinal) PersonalIdentity(object ticket) =>
        ((long)ticket.GetType().GetProperty("RequestId", F)!.GetValue(ticket)!,
         (int)ticket.GetType().GetProperty("Ordinal", F)!.GetValue(ticket)!);
    static Task PersonalNetExecute(bool begin, Player owner, long requestId, int ordinal, string room = "")
    {
        Type net = Mod.GetType("HBRGOSakiMod.Core.Integration.HbrPersonalTurnNetActions", true)!;
        Type messageType = net.GetNestedType("Message", F)!;
        object message = Activator.CreateInstance(messageType, requestId, ordinal, room)!;
        object descriptor = net.GetField(begin ? "BeginDescriptor" : "FinishDescriptor", F)!.GetValue(null)!;
        return (Task)Invoke(typeof(EffectChecks).GetMethod(nameof(PersonalSerializedExecute), F)!.MakeGenericMethod(messageType),
            null, descriptor, message, owner)!;
    }
    static Task PersonalSerializedExecute<T>(object rawDescriptor, object rawMessage, Player owner)
    {
        var descriptor = (RitsuLibManagedNetActionDescriptor<T>)rawDescriptor;
        byte[] payload = descriptor.Serialize((T)rawMessage);
        T received = descriptor.Deserialize(payload);
        PersonalSerializedCommands++;
        return descriptor.Execute(new RitsuLibManagedNetActionContext<T>(received, owner, null!, Blank<GameActionPlayerChoiceContext>()));
    }

    static async Task PersonalNativeTurnCases()
    {
        var harmony = new Harmony(ProductionId + ".personal-native");
        MethodInfo falseLeaf = typeof(EffectChecks).GetMethod(nameof(PersonalFalse), F)!;
        var installed = new List<MethodBase>();
        void PatchLeaf(MethodInfo target, MethodInfo leaf)
        { harmony.Patch(target, prefix: new HarmonyMethod(leaf)); installed.Add(target); }
        try
        {
            // Explicit host boundaries: no running Godot/transport lifecycle.
            // Ready, UndoReady, EndTurn, net Execute, Begin/Finish and snapshots
            // remain genuine production/native implementations.
            PatchLeaf(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), falseLeaf);
            PatchLeaf(AccessTools.PropertyGetter(typeof(RunManager), "IsSingleplayerOrFakeMultiplayer"), falseLeaf);
            foreach (MethodInfo method in typeof(LocalContext).GetMethods(F).Where(m => m.Name == "IsMe" && m.ReturnType == typeof(bool)))
                PatchLeaf(method, falseLeaf);
            foreach (MethodInfo method in typeof(Cmd).GetMethods(F).Where(m => (m.Name is "Wait" or "CustomScaledWait") && m.ReturnType == typeof(Task)))
                PatchLeaf(method, typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!);
            PatchLeaf(typeof(CardPileCmd).GetMethods(F).Single(m => m.Name == "Draw" && m.GetParameters().Length == 4),
                typeof(EffectChecks).GetMethod(nameof(PersonalDraw), F)!);
            PatchLeaf(Helper("OverclockAllyActionHelper").GetMethod("PerformEntryActions", F)!, typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!);
            foreach (string name in new[] { "PersonalImmediateTurnReadyPatch", "PersonalImmediateTurnEndGatePatch", "OverclockTurnEndSignalPatch" })
            {
                Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
                harmony.CreateClassProcessor(patch).Patch();
                installed.Add(AccessTools.Method(typeof(CombatManager), name == "PersonalImmediateTurnEndGatePatch"
                    ? "AfterAllPlayersReadyToEndTurn" : "SetReadyToEndTurn"));
            }
            await PersonalNativeReadyAndRestore(false);
            await PersonalNativeReadyAndRestore(true);
            await PersonalNativeReadyAndRestore(true, forcedReady: true);
            await PersonalForcedReadyWithoutPeerCase();
            await PersonalPendingVotesCase();
            await PersonalMultipleOwnersCase();
            await PersonalSourceTailCases();
            await PersonalDeckAndReplicationCases();
            await PersonalNativeFailureCleanup();
            await PersonalRestoreFailureCase();
            await PersonalDurationCases();
            await PersonalOverclockChargeCases();
            await PersonalOverclockHandoffCase();
            await PersonalInsertedStartCases();
            PersonalDescriptionCases();
            Check(PersonalSerializedCommands > 20, "personal native cases replay actual descriptor-serialized payloads through the registered production Execute delegate");
        }
        finally
        {
            PersonalCall("Clear");
            foreach (MethodBase target in installed.Distinct()) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
        }
    }

    static async Task PersonalNativeReadyAndRestore(bool savedReady, bool forcedReady = false)
    {
        Player peer = PersonalWorld();
        var original = PersonalOwned<StrikeIronclad>();
        Player.PlayerCombatState!.Hand.AddInternal(original);
        ((List<CardModel>)typeof(CombatState).GetField("_allCards", F)!.GetValue(Combat)!).Add(original);
        CardPile peerHand = peer.PlayerCombatState!.Hand;
        if (savedReady) CombatManager.Instance.SetReadyToEndTurn(Player, !forcedReady);
        Check(!PersonalEndSignalCompleted(), "one of two native ready votes does not end the ordinary turn");
        PersonalCall("Request", Player, 1);
        object ticket = PersonalTicketObject(); var identity = PersonalIdentity(ticket);
        Check(PersonalCoordinator.GetField("_current", F)!.GetValue(null) == null && original.Pile == Player.PlayerCombatState!.Hand,
            "request admission leaves the causing card's ordinary hand attached until recorded begin executes");
        await PersonalNetExecute(true, Player, identity.Id, identity.Ordinal, "wrong-room");
        Check(PersonalCoordinator.GetField("_current", F)!.GetValue(null) == null,
            "recorded begin rejects a stale room before changing any piles");
        await PersonalNetExecute(true, Player, identity.Id, identity.Ordinal);
        Check((bool)PersonalCall("IsActiveFor", Player)! && !(bool)PersonalCall("IsActiveFor", peer)!,
            "real managed net Execute starts only the ticket owner's personal session");
        Type ui = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.PersonalImmediateTurnUiParticipantPatch", true)!;
        MethodInfo contains = ui.GetMethod("ContainsParticipant", F)!;
        Check((bool)Invoke(contains, null, Array.Empty<Player>(), Player)! &&
            !(bool)Invoke(contains, null, Array.Empty<Player>(), peer)! &&
            (bool)Invoke(contains, null, new[] { peer }, peer)!,
            "shared native UI membership reader adds only the personal owner and preserves existing native extra-turn participants");
        Check(original.Pile == null && Player.PlayerCombatState!.Energy == 2 && peer.PlayerCombatState!.Energy == 5 && ReferenceEquals(peerHand, peer.PlayerCombatState!.Hand),
            "real entry detaches the owner's ordinary cards and sets two energy while preserving the teammate's hand and energy");
        Check(!CombatManager.Instance.IsPlayerReadyToEndTurn(Player), "personal entry removes only the owner's saved native ready vote");
        // A normal teammate play makes persistent progress while owner is away.
        peer.PlayerCombatState!.Energy = 3;
        var peerCard = PersonalOwned<StrikeIronclad>(); Set(peerCard, "<IsMutable>k__BackingField", true); Set(peerCard, "_owner", peer);
        peerHand.AddInternal(peerCard);
        CombatManager.Instance.SetReadyToEndTurn(peer, !forcedReady);
        Check(CombatManager.Instance.IsPlayerReadyToEndTurn(peer), "teammate's real SetReady records a normal vote during owner's personal turn");
        PlayerCmd.EndTurn(Player, true);
        Check(!CombatManager.Instance.IsPlayerReadyToEndTurn(Player) &&
            (bool)PersonalCoordinator.GetField("_current", F)!.GetValue(null)!.GetType().GetField("EndRequested", F)!.GetValue(PersonalCoordinator.GetField("_current", F)!.GetValue(null))!,
            "real PlayerCmd.EndTurn reaches native prefix and requests private finish without adding an ordinary owner vote");
        Check(!PersonalEndSignalCompleted(), "private owner end never completes beta's one-shot ordinary signal or stable ordinary transition");
        await PersonalNetExecute(false, peer, identity.Id, identity.Ordinal);
        Check((bool)PersonalCall("IsActiveFor", Player)!, "a teammate cannot finish the owner's synchronized ticket");
        await PersonalNetExecute(false, Player, identity.Id, identity.Ordinal);
        Check(!(bool)PersonalCall("IsActiveFor", Player)! && !(bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)!,
            "real net finish runs owner end boundaries and clears its completed personal ticket");
        Check(original.Pile == Player.PlayerCombatState!.Hand && Player.PlayerCombatState!.Energy == 7,
            "real suspended-hand restoration restores original owner card identity, pile and energy");
        Check(ReferenceEquals(peer.PlayerCombatState!.Hand, peerHand) && peerHand.Cards.Contains(peerCard) && peer.PlayerCombatState!.Energy == 3 && CombatManager.Instance.IsPlayerReadyToEndTurn(peer),
            "owner restoration preserves teammate cards, spent energy and the newly recorded ready vote");
        Check(CombatManager.Instance.IsPlayerReadyToEndTurn(Player) == savedReady,
            "owner restoration recovers exactly its own prior ready vote");
#if HBR_BETA_NATIVE
        if (forcedReady)
            Check(!(bool)Invoke(typeof(CombatManager).GetMethod("CanUnEndTurn", F)!, CombatManager.Instance, Player)! &&
                !(bool)Invoke(typeof(CombatManager).GetMethod("CanUnEndTurn", F)!, CombatManager.Instance, peer)!,
                "forced owner-ready constraints are suspended for personal play then restored, while teammate's newer forced-ready constraint is preserved");
#endif
        if (savedReady)
            Check(PersonalEndSignalCompleted(), "both restored native votes release the ordinary signal only after the personal session is gone");
        else
        {
            Check(!PersonalEndSignalCompleted(), "a still-playing owner does not inherit the teammate's ready vote");
            CombatManager.Instance.SetReadyToEndTurn(Player, true);
            Check(PersonalEndSignalCompleted(), "a later genuine ordinary owner vote still completes the unconsumed native turn-end signal");
        }
        await PersonalNetExecute(false, Player, identity.Id, identity.Ordinal);
        Check(original.Pile == Player.PlayerCombatState!.Hand, "a repeated recorded finish is idempotent and cannot discard restored originals");
    }

}
