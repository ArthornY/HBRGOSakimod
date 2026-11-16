using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

static partial class EffectChecks
{
    sealed record PersonalTicket(long RequestId, int Ordinal, string Owner);

    static object PersonalSchedule() => Activator.CreateInstance(
        Mod.GetType("HBRGOSakiMod.Core.Helpers.PersonalTurnSchedule`1", true)!.MakeGenericType(typeof(string)), true)!;
    static object? PersonalScheduleCall(object schedule, string method, params object?[] args) =>
        Invoke(schedule.GetType().GetMethod(method, F)!, schedule, args);
    static bool PersonalScheduleFlag(object schedule, string property) =>
        (bool)schedule.GetType().GetProperty(property, F)!.GetValue(schedule)!;
    static PersonalTicket? PersonalScheduleTicket(object schedule, string property = "Next")
    {
        object? ticket = schedule.GetType().GetProperty(property, F)!.GetValue(schedule);
        if (ticket == null) return null;
        T Read<T>(string name) => (T)ticket.GetType().GetProperty(name, F)!.GetValue(ticket)!;
        return new PersonalTicket(Read<long>("RequestId"), Read<int>("Ordinal"), Read<string>("Owner"));
    }
    static string PersonalScheduleSnapshot(object schedule) => JsonSerializer.Serialize(new
    {
        Next = PersonalScheduleTicket(schedule), Active = PersonalScheduleTicket(schedule, "Active"),
        Pending = PersonalScheduleFlag(schedule, "HasPending"), Busy = PersonalScheduleFlag(schedule, "IsBusy")
    });

    static void PersonalScheduleCases()
    {
        object first = PersonalSchedule(), second = PersonalSchedule();
        void Same(string step) => Check(PersonalScheduleSnapshot(first) == PersonalScheduleSnapshot(second),
            "two independent schedule states agree after " + step);
        void Both(string method, params object?[] args)
        {
            object? left = PersonalScheduleCall(first, method, args), right = PersonalScheduleCall(second, method, args);
            Check(Equals(left, right), "replicated " + method + " result agrees"); Same(method);
        }
        Same("initialization");
        Both("Request", "owner", 2); Both("Request", "peer", 1); Both("Request", "owner", 1);
        var owners = new List<string>();
        PersonalTicket? previous = null;
        while (PersonalScheduleTicket(first) is { } next)
        {
            string before = PersonalScheduleSnapshot(first);
            Check(!(bool)PersonalScheduleCall(first, "TryBegin", next.RequestId, next.Ordinal, "wrong-owner")!,
                "a peer cannot claim someone else's private-turn ticket");
            Check(PersonalScheduleSnapshot(first) == before, "rejected owner mismatch does not mutate schedule state");
            Both("TryBegin", next.RequestId, next.Ordinal, next.Owner);
            owners.Add(next.Owner);
            Check(PersonalScheduleTicket(first, "Active") == next, "the synchronized begin activates its exact ticket");
            Check(!(bool)PersonalScheduleCall(first, "TryBegin", next.RequestId, next.Ordinal, next.Owner)!,
                "a duplicate begin command cannot consume an additional queued turn");
            Same("duplicate begin rejection");
            // Teammate normal plays/readies are deliberately not scheduler commands.
            Same("peer plays an ordinary card"); Same("peer votes ready during the private turn");
            Check(!(bool)PersonalScheduleCall(first, "Complete", next.RequestId, next.Ordinal + 1)!,
                "a mismatched finish command cannot close the active ticket");
            Same("mismatched finish rejection");
            Both("Complete", next.RequestId, next.Ordinal);
            Check(!(bool)PersonalScheduleCall(first, "Complete", next.RequestId, next.Ordinal)!,
                "a duplicate finish command is harmless");
            Same("duplicate finish rejection");
            if (previous?.RequestId == next.RequestId)
                Check(next.Ordinal == previous.Ordinal + 1, "batched turns increment their ordinal without changing request identity");
            previous = next;
        }
        Check(owners.SequenceEqual(new[] { "owner", "owner", "peer", "owner" }),
            "multiple players' requested private turns remain in synchronized FIFO order");
        Check(!PersonalScheduleFlag(first, "IsBusy") && !PersonalScheduleFlag(first, "HasPending"),
            "a completed personal-turn schedule no longer blocks the ordinary turn");
        Both("Request", "owner", int.MaxValue);
        PersonalTicket stale = PersonalScheduleTicket(first)!;
        Both("TryBegin", stale.RequestId, stale.Ordinal, stale.Owner);
        Both("Clear");
        Check(PersonalScheduleTicket(first) == null && PersonalScheduleTicket(first, "Active") == null,
            "combat reset clears active and pending personal turns, even for a very large batch");
        Both("Request", "owner", 1);
        Check(PersonalScheduleTicket(first)!.RequestId > stale.RequestId,
            "reset never reuses request identities while stale synchronized commands can still arrive");
        Check(!(bool)PersonalScheduleCall(first, "TryBegin", stale.RequestId, stale.Ordinal, stale.Owner)!,
            "a stale pre-reset begin command cannot start a newly requested turn");
        Same("stale reset command rejection");
    }

    static void PersonalQueueCases()
    {
        var owner = Blank<Player>(); var peer = Blank<Player>();
        Set(owner, "<NetId>k__BackingField", 101UL); Set(peer, "<NetId>k__BackingField", 202UL);
        var queues = new ActionQueueSet(new[] { owner, peer });
        MethodInfo get = typeof(ActionQueueSet).GetMethod("GetQueue", F)!;
        object ownQueue = Invoke(get, queues, owner.NetId)!, peerQueue = Invoke(get, queues, peer.NetId)!;
        var own = (List<GameAction>)ownQueue.GetType().GetField("actions", F)!.GetValue(ownQueue)!;
        var other = (List<GameAction>)peerQueue.GetType().GetField("actions", F)!.GetValue(peerQueue)!;
        FieldInfo paused = ownQueue.GetType().GetField("isPaused", F)!;
        FieldInfo cancelling = ownQueue.GetType().GetField("isCancellingPlayerDrivenCombatActions", F)!;
        paused.SetValue(ownQueue, true); cancelling.SetValue(ownQueue, false);
        paused.SetValue(peerQueue, false); cancelling.SetValue(peerQueue, false);
        MagicSwordQueueProbe Probe(ulong id, uint actionId, GameActionType kind, GameActionState state)
        {
            var result = new MagicSwordQueueProbe(id, kind);
            Set(result, "<State>k__BackingField", state); Set(result, "<Id>k__BackingField", actionId);
            return result;
        }
        var pending = Probe(101UL, 1, GameActionType.CombatPlayPhaseOnly, GameActionState.WaitingForExecution);
        var running = Probe(101UL, 2, GameActionType.CombatPlayPhaseOnly, GameActionState.Executing);
        var choice = Probe(101UL, 3, GameActionType.CombatPlayPhaseOnly, GameActionState.GatheringPlayerChoice);
        var lifecycle = Probe(101UL, 4, GameActionType.Any, GameActionState.WaitingForExecution);
        var noncombat = Probe(101UL, 5, GameActionType.NonCombat, GameActionState.WaitingForExecution);
        var peerCard = Probe(202UL, 101, GameActionType.CombatPlayPhaseOnly, GameActionState.WaitingForExecution);
        var peerNext = Probe(202UL, 102, GameActionType.Combat, GameActionState.WaitingForExecution);
        own.AddRange(new GameAction[] { pending, running, choice, lifecycle, noncombat });
        other.AddRange(new GameAction[] { peerCard, peerNext });
        object guard = Helper("PersonalTurnQueueGuard").GetConstructors(F).Single(constructor => constructor.GetParameters().Length == 2)
            .Invoke(new object[] { queues, owner.NetId });
        void Guard(string method) => Invoke(guard.GetType().GetMethod(method, F)!, guard);
        var harmony = new Harmony(ProductionId + ".personal-queue-native-cancel-leaf");
        MethodInfo cancel = typeof(GameAction).GetMethod(nameof(GameAction.Cancel), F)!;
        MethodInfo cancelLeaf = typeof(EffectChecks).GetMethod(nameof(MagicSwordCancelLeaf), F)!;
        try
        {
            MagicSwordCancelledProbes.Clear();
            harmony.Patch(cancel, prefix: new HarmonyMethod(cancelLeaf));
            Guard("Lock");
            Check(pending.State == GameActionState.Canceled && MagicSwordCancelledProbes.SequenceEqual(new[] { pending }),
                "the real personal queue guard cancels only the owner's waiting player-driven combat action");
            Check(own.SequenceEqual(new GameAction[] { running, choice, lifecycle, noncombat }),
                "the owner guard keeps executing/choice cleanup, Any lifecycle commands and noncombat actions in order");
            Check(other.SequenceEqual(new GameAction[] { peerCard, peerNext }) && peerCard.Id == 101 && peerNext.Id == 102 &&
                peerCard.State == GameActionState.WaitingForExecution && !(bool)paused.GetValue(peerQueue)! && !(bool)cancelling.GetValue(peerQueue)!,
                "the teammate's queue, action IDs, ordering and execution flags are untouched while owner is locked");
            Guard("AllowPlay");
            Check(!(bool)paused.GetValue(ownQueue)! && !(bool)cancelling.GetValue(ownQueue)!,
                "the owner guard enables only its owner's private-turn play queue");
            other.Remove(peerCard);
            var peerLater = Probe(202UL, 103, GameActionType.CombatPlayPhaseOnly, GameActionState.WaitingForExecution);
            other.Add(peerLater); cancelling.SetValue(peerQueue, true);
            Guard("Restore");
            Check((bool)paused.GetValue(ownQueue)! && !(bool)cancelling.GetValue(ownQueue)!,
                "owner restoration restores its own original pause/cancellation flags");
            Check(other.SequenceEqual(new GameAction[] { peerNext, peerLater }) && peerNext.Id == 102 && peerLater.Id == 103 &&
                !(bool)paused.GetValue(peerQueue)! && (bool)cancelling.GetValue(peerQueue)!,
                "owner restoration does not roll back teammate queue progress or newer teammate flags");
        }
        finally { harmony.Unpatch(cancel, cancelLeaf); MagicSwordCancelledProbes.Clear(); }
    }

    static void RunPersonalImmediateTurnChecks(string reportPath)
    {
        bool ok = false; string? error = null;
        try
        {
            Install(); Localization();
            PersonalScheduleCases(); PersonalQueueCases();
            PersonalNativeTurnCases().GetAwaiter().GetResult();
            ok = true;
        }
        catch (Exception exception) { error = exception.ToString(); }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error, cases = Cases,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Player).Assembly.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            serializedCommands = PersonalSerializedCommands,
            boundary = "Managed native host; real native Ready/UndoReady/PlayerCmd.EndTurn, native deck clone/shuffle, production Begin/Finish/Restore, and descriptor Serialize/Deserialize/Execute. Explicit isolation: idle IsOverOrEnding, native multiplayer-mode/LocalMe lifecycle gates, Cmd visual waits, draw animation/completion leaf with real pile mutations, ally-entry action leaf and GameAction.Cancel engine completion. Stable keeps native enemy-transition callback deferred in player setup; beta exercises the real one-shot TaskCompletionSource. Pure scheduler/owner queue tests are supplemental. No game process, Godot rendering, full transport, actual ally-entry execution, package or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " personal-turn checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
