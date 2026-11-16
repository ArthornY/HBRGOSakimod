using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Unlocks;

public sealed class ScoutedCommonA : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }
public sealed class ScoutedCommonB : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }
public sealed class ScoutedUncommonA : RelicModel { public override RelicRarity Rarity => RelicRarity.Uncommon; }
public sealed class ScoutedUncommonB : RelicModel { public override RelicRarity Rarity => RelicRarity.Uncommon; }
public sealed class ScoutedRareA : RelicModel { public override RelicRarity Rarity => RelicRarity.Rare; }
public sealed class ScoutedRareB : RelicModel { public override RelicRarity Rarity => RelicRarity.Rare; }

static partial class EffectChecks
{
    static readonly Type[] ScoutTreasureModels = [typeof(ScoutedCommonA), typeof(ScoutedCommonB),
        typeof(ScoutedUncommonA), typeof(ScoutedUncommonB), typeof(ScoutedRareA), typeof(ScoutedRareB)];
    static Type ScoutTreasureHelper => Mod.GetType("HBRGOSakiMod.Core.Map.HbrThirtyOneDScoutingHelper", true)!;
    static object? TreasureCall(string name, params object?[] args) => Invoke(ScoutTreasureHelper.GetMethods(F).Single(method =>
        method.Name == name && method.GetParameters().Length == args.Length), null, args);
    static object? TreasureGet(object owner, string name) => owner.GetType().GetProperty(name, F)!.GetValue(owner);
    static readonly HashSet<ulong> ScoutSkipPlayers = [];
    static bool ScoutEligibility(IRunState runState, Player player, ref bool __result)
    { __result = !ScoutSkipPlayers.Contains(player.NetId); return false; }
    static bool ScoutNotFake(ref bool __result) { __result = false; return false; }
    static bool ScoutNetId(ref ulong? __result) { __result = Player.NetId; return false; }
    static bool ScoutUiSkip() => false;
    static int ScoutLedgerRevision;
    static ThirtyOneDCollection ScoutTreasureReset(bool peer = false)
    {
        GardenReset(); ScoutSkipPlayers.Clear();
        Set(RunState, "<GameMode>k__BackingField", Enum.ToObject(typeof(RunState).GetProperty("GameMode")!.PropertyType, 0));
        var marker = (ThirtyOneDCollection)ModelDb.Relic<ThirtyOneDCollection>().ToMutable(); Set(marker, "_owner", Player);
        Set(Player, "_relics", Player.Relics.Concat(new[] { marker }).ToList());
        if (peer) Set(RunState, "_players", new List<Player> { Player, SupportTeammate() });
        var unlock = new UnlockState(Array.Empty<string>(), Array.Empty<ModelId>(), 10);
        foreach (Player player in RunState.Players)
            foreach (RelicModel relic in player.Relics)
                Set(relic, "<Id>k__BackingField", ModelDb.GetId(relic.GetType()));
        Set(RunState, "<UnlockState>k__BackingField", unlock);
        foreach (Player player in RunState.Players) Set(player, "<UnlockState>k__BackingField", unlock);
        var map = new MockSinglePointActMap(); map.MockCurrentMapPointType(MapPointType.Treasure); RunState.Map = map;
        Set(RunState, "_visitedMapCoords", new List<MapCoord> { new(0, 0) });
        Set(RunState, "_mapPointHistory", new List<List<MapPointHistoryEntry>>());
        var bag = new RelicGrabBag(true);
        bag.Populate(ScoutTreasureModels.Select(type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type))), new Rng(9384));
        Set(RunState, "<SharedRelicGrabBag>k__BackingField", bag);
        ScoutClearPending();
        return marker;
    }
    static void ScoutClearPending()
    {
        object table = ScoutTreasureHelper.GetField("Caches", F)!.GetValue(null)!;
        object?[] args = [RunState, null];
        if ((bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, args)!)
            ((HashSet<string>)args[1]!.GetType().GetField("Pending", F)!.GetValue(args[1])!).Clear();
    }
    static object ScoutTreasurePoint(object[] reservations)
    {
        string json = JsonSerializer.Serialize(new { ActIndex = RunState.CurrentActIndex, Column = 0, Row = 0,
            OriginalType = MapPointType.Treasure, RoomType = RoomType.Treasure, Relics = reservations });
        return JsonSerializer.Deserialize(json, Mod.GetType("HBRGOSakiMod.Core.Map.HbrScoutedPoint", true)!)!;
    }
    static void ScoutSaveTreasure(ThirtyOneDCollection marker, object point)
    {
        string json = JsonSerializer.Serialize(new { Schema = 1, Revision = ++ScoutLedgerRevision,
            Acts = new[] { new { ActIndex = RunState.CurrentActIndex, Viewers = RunState.Players.Select(player => player.NetId).ToArray(), Points = new[] { point } } } });
        Native(marker, "SetScoutingData", json); ScoutClearPending();
    }
    static object[] ScoutReserve(Rng rng) => ((System.Collections.IEnumerable)TreasureCall("ReserveTreasure", RunState, rng)!).Cast<object>().ToArray();
    static string ScoutRelicId(object reservation) => (string)TreasureGet(reservation, "RelicId")!;
    static RelicModel ScoutRelic(object reservation) => ModelDb.GetById<RelicModel>(new ModelId("RELIC", ScoutRelicId(reservation)));
    static bool ScoutContains(RelicGrabBag bag, object reservation) => bag.Contains(ScoutRelic(reservation));
    static IReadOnlySet<string> ScoutReserved() => (IReadOnlySet<string>)TreasureCall("GetReservedRelicIds", RunState)!;
    static TreasureRoomRelicSynchronizer ScoutSynchronizer(Rng rng) => new(RunState, Player.NetId, null!, RunState.SharedRelicGrabBag, rng);
    static void ScoutInstallTreasurePatches()
    {
        var harmony = new Harmony("hbr.31d-scouting.treasure-fixture");
        foreach (Type type in ScoutTreasureModels.Append(typeof(ThirtyOneDCollection)).Append(typeof(Gorget)).Append(typeof(Circlet)))
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        foreach (string patch in new[] { "ThirtyOneDScoutingTreasureBeginPatch", "ThirtyOneDScoutingTreasureEligibilityPatch",
            "ThirtyOneDScoutingTreasureRarityPatch", "ThirtyOneDScoutingTreasureTutorialPatch", "ThirtyOneDScoutingTreasurePullPatch",
            "ThirtyOneDScoutingReservedRelicFilterPatch" })
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + patch, true)!).Patch().Count > 0,
                "56 treasure installs real production " + patch);
        harmony.Patch(typeof(Hook).GetMethod(nameof(Hook.ShouldGenerateTreasure))!,
            prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ScoutEligibility), F)!) { priority = Priority.First + 10 });
        harmony.Patch(typeof(RunManager).GetProperty("IsSingleplayerOrFakeMultiplayer", F)!.GetMethod!,
            prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ScoutNotFake), F)!));
        Type local = typeof(RunState).Assembly.GetType("MegaCrit.Sts2.Core.Context.LocalContext", true)!;
        harmony.Patch(local.GetProperty("NetId", F)!.GetMethod!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ScoutNetId), F)!));
        harmony.Patch(Mod.GetType("HBRGOSakiMod.Core.Patches.ThirtyOneDScoutingMapUi", true)!.GetMethod("Refresh", F)!,
            prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ScoutUiSkip), F)!));
    }
    static void ScoutReserveAndReturnCases()
    {
        var marker = ScoutTreasureReset(peer: true); var named = new Rng(52641);
        object[] rows = ScoutReserve(named); object point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
        Check(rows.Length == 2 && rows.Select(row => (ulong)TreasureGet(row, "PlayerId")!).SequenceEqual(RunState.Players.Select(player => player.NetId)),
            "56 reserves one native-eligible slot in actual player order");
        Check(RngCount(named) == 2 && rows.All(row => (bool)TreasureGet(row, "Reserved")!), "56 named RNG rolls rarity once per player and real shared bag supplies both items");
        Check(rows.Select(ScoutRelicId).Distinct().Count() == 2 && rows.All(row => !ScoutContains(RunState.SharedRelicGrabBag, row)),
            "56 selected relic IDs are physically removed from shared bag exactly once");
        Check(rows.All(row => ScoutReserved().Contains(ScoutRelicId(row))), "56 saved ledger exposes every unopened reserved ID");
        var playerBag = new RelicGrabBag(true);
        playerBag.Populate(ScoutTreasureModels.Select(type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type))), new Rng(183));
        var draws = new List<RelicModel>();
        foreach (var row in rows)
        {
            RelicModel original = ScoutRelic(row);
            RelicModel? got = playerBag.PullFromFront(original.Rarity, RunState);
            if (got != null) draws.Add(got);
            got = playerBag.PullFromBack(original.Rarity, _ => true, RunState);
            if (got != null) draws.Add(got);
        }
        Check(draws.All(relic => !ScoutReserved().Contains(relic.Id.Entry)), "56 front/back draws and refreshed player bags cannot emit reserved IDs");
        var onlyReserved = new RelicGrabBag(true); onlyReserved.Populate(rows.Select(ScoutRelic), new Rng(13));
        Check(onlyReserved.PullFromFront(ScoutRelic(rows[0]).Rarity, RunState) == null &&
            onlyReserved.PullFromBack(ScoutRelic(rows[0]).Rarity, _ => true, RunState) == null,
            "56 a refreshed bag containing only reserved IDs yields no duplicate item");
        Check(playerBag.PullFromFront(ScoutRelic(rows[0]).Rarity, relic => relic.Id.Entry == ScoutRelicId(rows[0]), RunState) == null,
            "56 reservation exclusion composes with rather than replaces the caller's existing filter");
        TreasureCall("ReturnTreasure", RunState, point); TreasureCall("ReturnTreasure", RunState, point);
        Check(rows.All(row => ScoutContains(RunState.SharedRelicGrabBag, row)), "56 skipped unopened chest returns unowned reserved items idempotently");
        var deques = (Dictionary<RelicRarity, List<RelicModel>>)typeof(RelicGrabBag).GetField("_deques", F)!.GetValue(RunState.SharedRelicGrabBag)!;
        Check(rows.All(row => deques.Values.Sum(list => list.Count(relic => relic.Id.Entry == ScoutRelicId(row))) == 1), "56 repeat return never duplicates a deque entry");

        marker = ScoutTreasureReset(); rows = ScoutReserve(new Rng(918)); point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
        var owned = (RelicModel)ScoutRelic(rows[0]).ToMutable(); Set(owned, "_owner", Player);
        Set(Player, "_relics", Player.Relics.Append(owned).ToList()); TreasureCall("ReturnTreasure", RunState, point);
        Check(!ScoutContains(RunState.SharedRelicGrabBag, rows[0]), "56 an already-owned reserved ID is never returned to the shared bag");
        marker = ScoutTreasureReset(peer: true); ScoutSkipPlayers.Add(Player.NetId); named = new Rng(142);
        rows = ScoutReserve(named);
        Check(rows.Length == 1 && (ulong)TreasureGet(rows[0], "PlayerId")! == RunState.Players[1].NetId && RngCount(named) == 1,
            "56 original ShouldGenerateTreasure rejection reserves no item and consumes no rarity RNG for that player");
    }
    static void ScoutNativePickingCases()
    {
        ScoutTreasureReset(); var ordinaryRng = new Rng(1581); var ordinary = ScoutSynchronizer(ordinaryRng);
        ordinary.BeginRelicPicking();
        Check(ordinary.CurrentRelics!.Count == 1 && RngCount(ordinaryRng) == 1,
            "56 a chest without a saved scouting entry retains the ordinary native rarity draw");
        ScoutTreasureReset(peer: true); var awardControlRng = new Rng(18491); var awardControl = ScoutSynchronizer(awardControlRng);
        awardControl.BeginRelicPicking(); int beforeNativeAward = RngCount(awardControlRng);
        awardControl.OnPicked(Player, 0); awardControl.OnPicked(RunState.Players[1], 1);
        int nativeAwardCalls = RngCount(awardControlRng) - beforeNativeAward;
        Console.WriteLine("native ordinary uncontested award RNG calls=" + nativeAwardCalls);
        var marker = ScoutTreasureReset(peer: true); object[] rows = ScoutReserve(new Rng(5431)); object point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
        var nativeRng = new Rng(18491); var sync = ScoutSynchronizer(nativeRng); int votesChanged = 0; List<RelicPickingResult>? awarded = null;
        sync.VotesChanged += () => votesChanged++;
#if HBR_BETA_NATIVE
        sync.RelicsAwarded += (list, _) => awarded = list;
#else
        sync.RelicsAwarded += list => awarded = list;
#endif
        sync.BeginRelicPicking();
        Check(sync.CurrentRelics!.Select(relic => relic.Id.Entry).SequenceEqual(rows.Select(ScoutRelicId)), "56 real native BeginRelicPicking displays the pre-reserved IDs");
        Check(RngCount(nativeRng) == 0 && votesChanged == 1, "56 scouted native Begin preserves vote event and does not draw the live treasure RNG");
        Check((bool)TreasureGet(TreasureCall("GetKnownPoint", RunState, RunState.CurrentMapPoint)!, "TreasureOpened")!, "56 successful native Begin records the actual opened chest");
        Check(sync.GetPlayerVote(Player).voteReceived == false && sync.GetPlayerVote(RunState.Players[1]).voteReceived == false,
            "56 native vote receipts are initialized for both actual players");
        sync.OnPicked(Player, 0); Check(awarded == null && sync.CurrentRelics != null, "56 first multiplayer vote does not prematurely award or close the chest");
        sync.OnPicked(RunState.Players[1], 1);
        Check(awarded?.Count == 2 && awarded.All(result => result.player != null) && sync.CurrentRelics == null,
            "56 normal native multiplayer voting awards each distinct choice and closes picking");
        Check(RngCount(nativeRng) == nativeAwardCalls, "56 uncontested scouted awards exactly preserve native award RNG calls=" + nativeAwardCalls);

        marker = ScoutTreasureReset(peer: true); rows = ScoutReserve(new Rng(5431)); point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
        ScoutSkipPlayers.Add(Player.NetId); nativeRng = new Rng(481); sync = ScoutSynchronizer(nativeRng); sync.BeginRelicPicking();
        Check(sync.CurrentRelics!.Count == 1 && sync.CurrentRelics[0].Id.Entry == ScoutRelicId(rows[1]),
            "56 changed first-player eligibility matches the remaining slot by real NetId rather than queue position");
        Check(ScoutContains(RunState.SharedRelicGrabBag, rows[0]) && !ScoutContains(RunState.SharedRelicGrabBag, rows[1]),
            "56 entering without one original recipient returns only that unused reservation");

        marker = ScoutTreasureReset(); rows = ScoutReserve(new Rng(871)); point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
        var owned = (RelicModel)ScoutRelic(rows[0]).ToMutable(); Set(owned, "_owner", Player); Set(Player, "_relics", Player.Relics.Append(owned).ToList());
        nativeRng = new Rng(448); sync = ScoutSynchronizer(nativeRng); sync.BeginRelicPicking();
        Check(RngCount(nativeRng) == 1 && sync.CurrentRelics!.Single().Id.Entry != ScoutRelicId(rows[0]),
            "56 a reserved relic acquired before entry follows native reroll and cannot duplicate the owned item");
    }
    static void ScoutPassedRowReleaseCase()
    {
        var marker = ScoutTreasureReset(); object[] rows = ScoutReserve(new Rng(952)); object point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
        Set(Player, "<Character>k__BackingField", Blank<HBRGOSakiMod.Core.Models.Characters.HbrgoSaki31O>());
        Set(RunState, "_visitedMapCoords", new List<MapCoord> { new(0, 2) });
        TreasureCall("EnsureScouted", RunState, RunState.Map, null);
        Check(rows.All(row => ScoutContains(RunState.SharedRelicGrabBag, row)) && ScoutReserved().Count == 0,
            "56 actual EnsureScouted ReleasePassed returns an unopened treasure behind the current row");
        Check(TreasureCall("GetKnownPoint", RunState, RunState.CurrentMapPoint) == null,
            "56 a released native map point cannot be displayed as a still-frozen treasure");
        string bag = JsonSerializer.Serialize(RunState.SharedRelicGrabBag.ToSerializable());
        TreasureCall("EnsureScouted", RunState, RunState.Map, null);
        Check(JsonSerializer.Serialize(RunState.SharedRelicGrabBag.ToSerializable()) == bag,
            "56 repeated real passed-row release does not return the same reserved item twice");
    }
    static void ScoutScopeIsolationCases()
    {
        var marker = ScoutTreasureReset(); object[] rows = ScoutReserve(new Rng(913)); object point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
        var nativeRng = new Rng(193); var sync = ScoutSynchronizer(nativeRng);
        var scope = (IDisposable)TreasureCall("BeginTreasurePicking", sync)!;
        try
        {
            TreasureCall("ObserveTreasureEligibility", Player, true);
            var otherRng = new Rng(391);
            MegaCrit.Sts2.Core.Factories.RelicFactory.RollRarity(otherRng);
            Check(RngCount(otherRng) == 1, "56 an unrelated rarity stream inside the active picking scope remains native");
            var actual = MegaCrit.Sts2.Core.Factories.RelicFactory.RollRarity(nativeRng);
            Check(actual == (RelicRarity)TreasureGet(rows[0], "Rarity")! && RngCount(nativeRng) == 0,
                "56 only the synchronizer's exact RNG reference uses its frozen rarity");
            RelicModel? got = RunState.SharedRelicGrabBag.PullFromFront(actual, RunState);
            Check(got?.Id.Entry == ScoutRelicId(rows[0]), "56 the same run/bag/rarity consumes precisely its selected reserved item");
            Native(scope, "Complete");
        }
        finally { scope.Dispose(); }
        var after = new Rng(715); MegaCrit.Sts2.Core.Factories.RelicFactory.RollRarity(after);
        Check(RngCount(after) == 1, "56 disposing a picking scope clears its selection override");
    }
    static void ScoutTutorialAndRollbackCases()
    {
        var marker = ScoutTreasureReset(); object[] rows = ScoutReserve(new Rng(761)); object point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
        Set(RunState.UnlockState, "<NumberOfRuns>k__BackingField", 0);
#if HBR_BETA_NATIVE
        var tutorialPlayerBag = new RelicGrabBag(); tutorialPlayerBag.Populate(new[] { ModelDb.Relic<Gorget>() }, new Rng(781));
        Set(Player, "<RelicGrabBag>k__BackingField", tutorialPlayerBag);
#endif
        var history = new MapPointHistoryEntry(MapPointType.Treasure, RunState) { Rooms = new List<MapPointRoomHistoryEntry> { new() { RoomType = RoomType.Treasure } } };
        Set(RunState, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new() { history } });
        var rng = new Rng(719); var sync = ScoutSynchronizer(rng); sync.BeginRelicPicking();
        Check(sync.CurrentRelics!.Single() is Gorget && ScoutContains(RunState.SharedRelicGrabBag, rows[0]),
            "56 actual first-run first-chest tutorial keeps native Gorget and returns its displaced reservation");
        Check(RngCount(rng) == 0 && !ScoutReserved().Contains(ScoutRelicId(rows[0])), "56 tutorial exception consumes no live rarity roll and releases the saved reservation");

        marker = ScoutTreasureReset();
        var bag = RunState.SharedRelicGrabBag;
        var fallback = (List<RelicModel>)typeof(RelicGrabBag).GetField("_mpFallbackDequeue", F)!.GetValue(bag)!;
        fallback.Add(ModelDb.Relic<ScoutedRareA>());
        string before = JsonSerializer.Serialize(bag.ToSerializable()); string[] fallbackBefore = fallback.Select(relic => relic.Id.Entry).ToArray();
        var undo = (Action)TreasureCall("SnapshotTreasureBag", RunState)!;
        bag.PullFromFront(RelicRarity.Common, RunState); fallback.Clear();
        var deques = (Dictionary<RelicRarity, List<RelicModel>>)typeof(RelicGrabBag).GetField("_deques", F)!.GetValue(bag)!;
        deques.Add(RelicRarity.Shop, new List<RelicModel> { ModelDb.Relic<ScoutedCommonA>() });
        undo();
        Check(JsonSerializer.Serialize(bag.ToSerializable()) == before && fallback.Select(relic => relic.Id.Entry).SequenceEqual(fallbackBefore),
            "56 reservation rollback exactly restores deques, removes new rarity keys and restores the omitted multiplayer fallback queue");
    }
#if HBR_BETA_NATIVE
    static void ScoutBlitzTreasureCases()
    {
        foreach (bool peer in new[] { false, true })
        {
            var marker = ScoutTreasureReset(peer);
            Set(RunState, "<GameMode>k__BackingField", Enum.Parse(typeof(RunState).GetProperty("GameMode")!.PropertyType, "Blitz"));
            var named = new Rng(161); object[] rows = ScoutReserve(named); object point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
            int normal = peer ? 2 : 1, bonus = peer ? 2 : 1;
            Check(rows.Length == normal + bonus && rows.Count(row => (bool)TreasureGet(row, "IsBonus")!) == bonus && RngCount(named) == normal + bonus,
                "56 beta Blitz " + normal + " players reserves every normal and native bonus choice with named rarity RNG");
            var rng = new Rng(951); var sync = ScoutSynchronizer(rng); var awards = new List<(List<RelicPickingResult> Results, bool Last)>();
            sync.RelicsAwarded += (list, last) => awards.Add((list, last));
            sync.BeginRelicPicking();
            Check(sync.CurrentRelics!.Select(relic => relic.Id.Entry).SequenceEqual(rows.Select(ScoutRelicId)) && RngCount(rng) == 0,
                "56 beta Blitz native Begin uses all frozen bonus slots without live rarity rolls");
            if (!peer)
            {
                sync.OnPicked(Player, 0);
                Check(awards.Count == 1 && !awards[0].Last && sync.CurrentRelics!.Count == 1,
                    "56 beta Blitz singleplayer retains native two-round picking after its first award");
                sync.OnPicked(Player, 0);
                Check(awards.Count == 2 && awards[1].Last && sync.CurrentRelics == null &&
                    awards.SelectMany(round => round.Results).Select(result => result.relic.Id.Entry).Distinct().Count() == 2,
                    "56 beta Blitz second native round awards the remaining frozen item and closes normally");
                Check(RngCount(rng) == 2, "56 beta Blitz preserves one native shuffle per award round without rerolling the frozen candidate list");
            }
            else
            {
                sync.OnPicked(Player, 0); sync.OnPicked(RunState.Players[1], 1);
                Check(awards.Count == 1 && awards[0].Last && awards[0].Results.Count(result => result.player != null) == 2 && sync.CurrentRelics == null,
                    "56 beta Blitz multiplayer preserves native one-round votes/awards with its two extra choices");
                Check(RngCount(rng) == 1,
                    "56 beta Blitz keeps the native one shuffle of two unclaimed bonus items, without rerolling rarity");
            }
        }
        {
            var marker = ScoutTreasureReset();
            Set(RunState, "<GameMode>k__BackingField", Enum.Parse(typeof(RunState).GetProperty("GameMode")!.PropertyType, "Blitz"));
            object[] rows = ScoutReserve(new Rng(841)); object point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
            object normal = rows.Single(row => !(bool)TreasureGet(row, "IsBonus")!);
            var owned = (RelicModel)ScoutRelic(normal).ToMutable(); Set(owned, "_owner", Player); Set(Player, "_relics", Player.Relics.Append(owned).ToList());
            var rng = new Rng(841); var sync = ScoutSynchronizer(rng); sync.BeginRelicPicking();
            Check(sync.CurrentRelics!.Count == 2 && sync.CurrentRelics[1].Id.Entry == ScoutRelicId(rows.Single(row => (bool)TreasureGet(row, "IsBonus")!)) && RngCount(rng) == 1,
                "56 beta Blitz invalid last normal slot rerolls natively before consuming the separate frozen bonus slot");
        }
        {
            var marker = ScoutTreasureReset();
            Set(RunState, "<GameMode>k__BackingField", Enum.Parse(typeof(RunState).GetProperty("GameMode")!.PropertyType, "Blitz"));
            object[] rows = ScoutReserve(new Rng(481)); object point = ScoutTreasurePoint(rows); ScoutSaveTreasure(marker, point);
            ScoutSkipPlayers.Add(Player.NetId); var rng = new Rng(159); var sync = ScoutSynchronizer(rng); sync.BeginRelicPicking();
            Check(sync.CurrentRelics!.Single().Id.Entry == ScoutRelicId(rows.Single(row => (bool)TreasureGet(row, "IsBonus")!)) && RngCount(rng) == 0,
                "56 beta Blitz skipped final normal recipient still opens its native frozen bonus choice without slot shifting");
        }
    }
#endif
    static Task ScoutingTreasureCases()
    {
        ScoutInstallTreasurePatches(); ScoutReserveAndReturnCases(); ScoutNativePickingCases(); ScoutPassedRowReleaseCase(); ScoutScopeIsolationCases(); ScoutTutorialAndRollbackCases();
#if HBR_BETA_NATIVE
        ScoutBlitzTreasureCases();
#endif
        return Task.CompletedTask;
    }
}
