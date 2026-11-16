using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Map;

internal static partial class HbrThirtyOneDScoutingHelper
{
    private static readonly FieldInfo TreasurePlayers = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_playerCollection") ??
        AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_runState");
    private static readonly FieldInfo TreasureRng = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_rng");
    private static readonly FieldInfo TreasureBag = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_sharedGrabBag");
    private static readonly FieldInfo RelicDeques = AccessTools.Field(typeof(RelicGrabBag), "_deques");
    private static readonly FieldInfo RelicFallback = AccessTools.Field(typeof(RelicGrabBag), "_mpFallbackDequeue");
    private static readonly AsyncLocal<TreasurePickingScope> CurrentTreasurePicking = new();

    private static readonly bool NativeHasBlitzTreasure = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_roundsLeft") != null;
    internal static HbrScoutedRelic[] ReserveTreasure(RunState run, Rng rng)
    {
        List<HbrScoutedRelic> selected = new();
        void Reserve(ulong playerId, bool bonus)
        {
            RelicRarity rarity = RelicFactory.RollRarity(rng);
            RelicModel pulled = run.SharedRelicGrabBag.PullFromFront(rarity, run);
            RelicModel relic = pulled ?? RelicFactory.FallbackRelic;
            selected.Add(new HbrScoutedRelic
            {
                PlayerId = playerId, RelicId = relic.Id.Entry,
                Rarity = rarity, Reserved = pulled != null, IsBonus = bonus
            });
            if (pulled != null) AddPendingReservation(run, relic.Id.Entry);
        }
        foreach (Player player in run.Players)
            if (Hook.ShouldGenerateTreasure(run, player)) Reserve(player.NetId, false);
        // Beta's Blitz mode appends one singleplayer/two multiplayer choices.
        // Its original rounds/voting/award logic remains entirely native.
        if (NativeHasBlitzTreasure && run.GameMode.ToString() == "Blitz")
            for (int index = 0; index < (run.Players.Count > 1 ? 2 : 1); index++) Reserve(0, true);
        return selected.ToArray();
    }

    // Native serializable bags omit their multiplayer fallback deque. Capture
    // both collections so generation rollback also restores fallback draws and
    // removes any new rarity deque created by a native refresh.
    internal static Action SnapshotTreasureBag(RunState run)
    {
        var deques = (Dictionary<RelicRarity, List<RelicModel>>)RelicDeques.GetValue(run.SharedRelicGrabBag);
        var fallback = (List<RelicModel>)RelicFallback.GetValue(run.SharedRelicGrabBag);
        var saved = deques.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        RelicModel[] savedFallback = fallback.ToArray();
        return () =>
        {
            deques.Clear();
            foreach (var pair in saved) deques.Add(pair.Key, new List<RelicModel>(pair.Value));
            fallback.Clear(); fallback.AddRange(savedFallback);
        };
    }

    internal static void ReturnTreasure(RunState run, HbrScoutedPoint point)
    {
        if (run == null || point == null || point.TreasureOpened || point.Released) return;
        ReturnReservedRelics(run, point.Relics, Array.Empty<string>());
    }

    // A private treasure-choice offer returns only its discarded physical item;
    // it does not release or rewrite any scouting reservation.
    internal static void ReturnTreasureChoice(RunState run, RelicModel relic)
    {
        if (run == null || relic == null) return;
        ReturnReservedRelics(run, new[] { new HbrScoutedRelic
        { RelicId = relic.Id.Entry, Rarity = relic.Rarity, Reserved = true } }, Array.Empty<string>());
    }

    private static bool IsOwned(IRunState run, string id) => run.Players.Any(player =>
        player.Relics.Any(relic => relic.Id.Entry == id));

    private static void ReturnReservedRelics(RunState run, IEnumerable<HbrScoutedRelic> relics,
        IEnumerable<string> inCurrentChest)
    {
        var deques = (Dictionary<RelicRarity, List<RelicModel>>)RelicDeques.GetValue(run.SharedRelicGrabBag);
        var fallback = (List<RelicModel>)RelicFallback.GetValue(run.SharedRelicGrabBag);
        var current = new HashSet<string>(inCurrentChest, StringComparer.Ordinal);
        foreach (HbrScoutedRelic item in relics.Where(item => item.Reserved).DistinctBy(item => item.RelicId))
        {
            if (current.Contains(item.RelicId) || IsOwned(run, item.RelicId) ||
                deques.Values.Any(deque => deque.Any(relic => relic.Id.Entry == item.RelicId)) ||
                fallback.Any(relic => relic.Id.Entry == item.RelicId)) continue;
            RelicModel relic = ModelDb.GetByIdOrNull<RelicModel>(new ModelId("RELIC", item.RelicId));
            if (relic == null) continue;
            if (!deques.TryGetValue(relic.Rarity, out List<RelicModel> deque))
                deques.Add(relic.Rarity, deque = new());
            deque.Add(relic);
        }
    }

    internal static TreasurePickingScope BeginTreasurePicking(TreasureRoomRelicSynchronizer synchronizer)
    {
        var collection = (IPlayerCollection)TreasurePlayers.GetValue(synchronizer);
        RunState run = collection as RunState ?? collection.Players.FirstOrDefault()?.RunState as RunState;
        HbrScoutedPoint point = run?.CurrentMapPoint is { } mapPoint ? GetKnownPoint(run, mapPoint) : null;
        if (point is not { RoomType: RoomType.Treasure, TreasureOpened: false, Released: false, Invalidated: false }) return null;
        return new TreasurePickingScope(run, point, (RelicGrabBag)TreasureBag.GetValue(synchronizer), (Rng)TreasureRng.GetValue(synchronizer), synchronizer);
    }

    internal static void ObserveTreasureEligibility(Player player, bool generates)
    {
        if (CurrentTreasurePicking.Value is { } scope && ReferenceEquals(player?.RunState, scope.Run))
            scope.SelectPlayer(player, generates);
    }
    internal static bool TryScoutedTreasureRarity(Rng rng, out RelicRarity rarity)
    {
        TreasurePickingScope scope = CurrentTreasurePicking.Value;
        HbrScoutedRelic current = scope != null && ReferenceEquals(scope.Rng, rng) ? scope.SelectRarity() : null;
        rarity = current?.Rarity ?? default;
        return current != null;
    }
    internal static bool UsesScoutedTreasure(TreasureRoomRelicSynchronizer synchronizer) =>
        CurrentTreasurePicking.Value is { Selected: not null } scope && ReferenceEquals(scope.Synchronizer, synchronizer);
    internal static void ObserveTutorialTreasure(TreasureRoomRelicSynchronizer synchronizer, RelicModel relic)
    {
        if (relic != null && CurrentTreasurePicking.Value is { } scope && ReferenceEquals(scope.Synchronizer, synchronizer))
        { scope.UsedRelics.Add(relic.Id.Entry); scope.Selected = null; }
    }
    internal static bool TryTakeScoutedTreasure(RelicGrabBag bag, RelicRarity rarity, IRunState run, out RelicModel relic)
    {
        TreasurePickingScope scope = CurrentTreasurePicking.Value;
        relic = null;
        if (scope?.Selected is not { } selected || selected.Rarity != rarity ||
            !ReferenceEquals(scope.Bag, bag) || !ReferenceEquals(scope.Run, run)) return false;
        relic = ModelDb.GetByIdOrNull<RelicModel>(new ModelId("RELIC", selected.RelicId));
        if (relic == null) return false;
        scope.UsedRelics.Add(relic.Id.Entry);
        scope.Selected = null;
        return true;
    }

    internal sealed class TreasurePickingScope : IDisposable
    {
        internal readonly RunState Run;
        internal readonly RelicGrabBag Bag;
        internal readonly Rng Rng;
        internal readonly TreasureRoomRelicSynchronizer Synchronizer;
        internal readonly HbrScoutedPoint Point;
        private readonly TreasurePickingScope _previous;
        internal readonly HashSet<string> UsedRelics = new(StringComparer.Ordinal);
        internal HbrScoutedRelic Selected;
        private readonly HashSet<ulong> _observedPlayers = new();
        private readonly HbrScoutedRelic[] _bonus;
        private int _nextBonus;
        private bool _normalRarityPending;
        private bool _completed;
        private bool _disposed;
        internal TreasurePickingScope(RunState run, HbrScoutedPoint point, RelicGrabBag bag, Rng rng, TreasureRoomRelicSynchronizer synchronizer)
        {
            Run = run; Point = point; Bag = bag; Rng = rng; Synchronizer = synchronizer;
            _bonus = point.Relics.Where(item => item.IsBonus).ToArray();
            _previous = CurrentTreasurePicking.Value;
            CurrentTreasurePicking.Value = this;
        }
        internal void SelectPlayer(Player player, bool generates)
        {
            _observedPlayers.Add(player.NetId);
            _normalRarityPending = generates;
            Selected = generates ? Point.Relics.FirstOrDefault(item => !item.IsBonus && item.PlayerId == player.NetId) : null;
            ValidateSelected();
        }
        internal HbrScoutedRelic SelectRarity()
        {
            if (_normalRarityPending)
            { _normalRarityPending = false; return Selected; }
            if (_observedPlayers.Count < Run.Players.Count || _nextBonus >= _bonus.Length) return null;
            Selected = _bonus[_nextBonus++];
            ValidateSelected();
            return Selected;
        }
        private void ValidateSelected()
        {
            if (Selected == null) return;
            RelicModel relic = ModelDb.GetByIdOrNull<RelicModel>(new ModelId("RELIC", Selected.RelicId));
            // Entering with different eligibility/ownership follows native
            // generation rather than awarding a now-invalid frozen item.
            if (relic == null || IsOwned(Run, Selected.RelicId) || !relic.IsAllowed(Run)) Selected = null;
        }
        internal void Complete()
        {
            if (_completed) return;
            _completed = true;
            ReturnReservedRelics(Run, Point.Relics.Where(item => !UsedRelics.Contains(item.RelicId)), UsedRelics);
            MarkTreasureOpened(Run, Point);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(CurrentTreasurePicking.Value, this)) CurrentTreasurePicking.Value = _previous;
        }
    }
}
