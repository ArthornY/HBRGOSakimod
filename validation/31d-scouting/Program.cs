using System.Reflection;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.Events;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;

public class ScoutingEventFixture : EventModel
{
    public static readonly HashSet<Type> Disabled = [];
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => [];
    public override bool IsAllowed(IRunState runState) => !Disabled.Contains(GetType());
}
public sealed class ScoutingEventBFixture : ScoutingEventFixture { }
public sealed class ScoutingEventCFixture : ScoutingEventFixture { }
public sealed class ScoutingMapFixture : ActMap
{
    protected override MapPoint?[,] Grid { get; }
    public override MapPoint BossMapPoint { get; } = new(0, 20) { PointType = MapPointType.Boss };
    public override MapPoint StartingMapPoint { get; } = new(0, -1) { PointType = MapPointType.Ancient };
    public ScoutingMapFixture(params (int Col, int Row, MapPointType Type)[] points)
    {
        Grid = new MapPoint[8, 20];
        foreach (var point in points) Grid[point.Col, point.Row] = new MapPoint(point.Col, point.Row) { PointType = point.Type };
    }
}

static class ThirtyOneDScoutingStandalone { public static void Run(string path) => EffectChecks.RunScouting(path); }
static partial class EffectChecks
{
    static readonly Type[] ScoutEvents = [typeof(ScoutingEventFixture), typeof(ScoutingEventBFixture), typeof(ScoutingEventCFixture)];
    static HbrStoryEvent[] ScoutingMemories = [];
    static readonly Queue<float> ScoutingFloats = [];
    static bool ScoutingMemoryLeaf(ref IEnumerable<HbrStoryEvent> __result) { __result = ScoutingMemories; return false; }
    static bool ScoutingFloatLeaf(ref float __result)
    { if (ScoutingFloats.Count == 0) return true; __result = ScoutingFloats.Dequeue(); return false; }
    static void ScoutInitializeMemories()
    {
        Type store = Mod.GetType("HBRGOSakiMod.Core.Story.HbrStoryDefinitionStore", true)!;
        var definitions = (ConcurrentDictionary<string, HbrStoryDefinition>)store.GetField("Definitions", F)!.GetValue(null)!;
        var stories = new List<HbrStoryEvent>();
        foreach (Type type in new[] { typeof(Story_CAFE_HBRGO_SAKI), typeof(Story_GYM_HBRGO_SAKI), typeof(Story_CINEMA_HBRGO_SAKI) })
        {
            var shell = (HbrStoryEvent)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            string path = shell.DefinitionPath;
            definitions[path] = HbrStoryDefinition.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, path[6..])), path);
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            var canonical = ModelDb.GetById<HbrStoryEvent>(ModelDb.GetId(type));
            ModelId customId = new("EVENT", definitions[path].Id);
            Set(canonical, "<Id>k__BackingField", customId);
            // The managed host omits BaseLib's startup CustomID registration.
            // Register this real generated story under its authored ID as well.
            ((Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", F)!.GetValue(null)!)[customId] = canonical;
            stories.Add(canonical);
        }
        ScoutingMemories = stories.ToArray();
    }
    static ThirtyOneDCollection ScoutReset(int kinds, string seed = "SCOUT-REPLAY", bool firstRun = false)
    {
        var marker = DReset(kinds); ScoutingEventFixture.Disabled.Clear(); ScoutingFloats.Clear();
        Set(Player, "<NetId>k__BackingField", 1UL);
        Set(RunState, "<Rng>k__BackingField", new RunRngSet(seed));
        Set(RunState, "<Odds>k__BackingField", new RunOddsSet(RunState.Rng.UnknownMapPoint));
        var unlock = new UnlockState(Array.Empty<string>(), Array.Empty<ModelId>(), firstRun ? 0 : 10);
        Set(RunState, "<UnlockState>k__BackingField", unlock);
        Set(Player, "<UnlockState>k__BackingField", unlock);
        Set(RunState, "_visitedMapCoords", new List<MapCoord>());
        Set(RunState, "_mapPointHistory", new List<List<MapPointHistoryEntry>>());
        Set(RunState, "_visitedEventIds", new HashSet<ModelId> { new("EVENT", "JOINING_CEREMONY_HBRGO_SAKI") });
        Set(RunState, "<SharedRelicGrabBag>k__BackingField", new RelicGrabBag(true));
        Native(Player.GetRelic<ElectronicSoldierManual>()!, "SetMemoryFragmentMapData", "v1");
        var act = Blank<Overgrowth>(); Set(act, "<IsMutable>k__BackingField", true); Set(act, "<Id>k__BackingField", ModelDb.GetId<Overgrowth>());
        var rooms = new RoomSet(); rooms.events.AddRange(ScoutEvents.Select(type => ModelDb.GetById<EventModel>(ModelDb.GetId(type))));
        Set(act, "_rooms", rooms); Set(RunState, "<Acts>k__BackingField", new List<ActModel> { act, act, act });
        return marker;
    }
    static void ScoutForceEvents()
    {
        foreach (RoomType type in new[] { RoomType.Monster, RoomType.Elite, RoomType.Treasure, RoomType.Shop })
        { RunState.Odds.UnknownMapPoint.SetBaseOdds(type, 0); ScoutOdds(RunState.Odds.UnknownMapPoint)[type] = 0; }
    }
    static Dictionary<RoomType, float> ScoutOdds(UnknownMapPointOdds odds) => (Dictionary<RoomType, float>)typeof(UnknownMapPointOdds).GetField("_nonEventOdds", F)!.GetValue(odds)!;
    static Dictionary<RoomType, float> ScoutBasis(UnknownMapPointOdds odds) => (Dictionary<RoomType, float>)typeof(UnknownMapPointOdds).GetField("_baseOdds", F)!.GetValue(odds)!;
    static string ScoutOddsSnapshot(UnknownMapPointOdds odds) => JsonSerializer.Serialize(new { Current = ScoutOdds(odds), Basis = ScoutBasis(odds) });
    static void ScoutAt(MapPoint point) => Set(RunState, "_visitedMapCoords", new List<MapCoord> { point.coord });
    static object? ScoutKnown(MapPoint point) => TreasureCall("GetKnownPoint", RunState, point);
    static void ScoutEnsure(ActMap map) { RunState.Map = map; TreasureCall("EnsureScouted", RunState, map, null); }
    static string ScoutEventId(object point) => (string)TreasureGet(point, "EventId")!;
    static RoomType ScoutRoom(object point) => (RoomType)TreasureGet(point, "RoomType")!;
    static bool ScoutTryRoom(MapPoint point, RoomType[] blacklist, out RoomType room)
    {
        ScoutAt(point); object?[] args = [RunState, point.PointType, blacklist, RoomType.Unassigned];
        bool accepted = (bool)TreasureCall("TryRoomType", args)!; room = (RoomType)args[3]!; return accepted;
    }
    static bool ScoutTryEvent(MapPoint point, out EventModel? selected)
    {
        ScoutAt(point); object?[] args = [RunState, null]; bool accepted = (bool)TreasureCall("TryEvent", args)!;
        selected = (EventModel?)args[1]; return accepted;
    }
    static void ScoutingOddsParityCases()
    {
        ScoutReset(4);
        foreach (bool first in new[] { false, true })
        foreach (RoomType[] blacklist in new[] { Array.Empty<RoomType>(), new[] { RoomType.Shop }, new[] { RoomType.Monster, RoomType.Treasure }, new[] { RoomType.Monster, RoomType.Elite, RoomType.Treasure, RoomType.Shop } })
        foreach (int seed in Enumerable.Range(1, 30))
        {
            Set(RunState.UnlockState, "<NumberOfRuns>k__BackingField", first ? 0 : 10);
            Rng nativeRng = new((uint)seed), previewRng = new((uint)seed);
            var native = new UnknownMapPointOdds(nativeRng); native.MonsterOdds = .21f; native.EliteOdds = -.5f; native.ShopOdds = .17f; native.TreasureOdds = .09f;
            native.SetBaseOdds(RoomType.Monster, .13f); native.SetBaseOdds(RoomType.Elite, -.7f);
            var odds = new Dictionary<RoomType, float>(ScoutOdds(native)); var basis = new Dictionary<RoomType, float>(ScoutBasis(native));
            for (int seen = 0; seen < 7; seen++)
            {
                Set(RunState, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { Enumerable.Range(0, seen).Select(_ => new MapPointHistoryEntry { MapPointType = MapPointType.Unknown }).ToList() });
                RoomType actual = native.Roll(blacklist, RunState);
                RoomType preview = (RoomType)TreasureCall("RollPreviewRoomType", RunState, odds, basis, blacklist, previewRng, seen)!;
                Check(actual == preview && ScoutOdds(native).SequenceEqual(odds) && RngCount(nativeRng) == RngCount(previewRng),
                    $"native unknown roll parity first={first} seed={seed} seen={seen} blacklist={string.Join(',', blacklist)}");
            }
        }
    }
    static void ScoutingGenerationCases()
    {
        var marker = ScoutReset(4); ScoutingMemories = []; ScoutForceEvents();
        var map = new ScoutingMapFixture((1, 0, MapPointType.Unknown), (0, 0, MapPointType.Unknown), (0, 1, MapPointType.Unknown),
            (0, 2, MapPointType.Monster), (0, 3, MapPointType.RestSite), (0, 4, MapPointType.Shop));
        string rng = JsonSerializer.Serialize(RunState.Rng.ToSerializable()), odds = ScoutOddsSnapshot(RunState.Odds.UnknownMapPoint);
        string queue = JsonSerializer.Serialize(RunState.Act.ToSave().SerializableRooms); int visited = RunState.VisitedEventIds.Count;
        ScoutEnsure(map);
        MapPoint[] points = map.GetAllMapPoints().Where(point => point.PointType == MapPointType.Unknown).OrderBy(point => point.coord.row).ThenBy(point => point.coord.col).ToArray();
        Check(points.Select(ScoutKnown).All(point => point != null), "actual EnsureScouted persists every eligible unknown node");
        Check(points.Select(point => ScoutEventId(ScoutKnown(point)!)).SequenceEqual(ScoutEvents.Select(type => ModelDb.GetId(type).Entry)),
            "virtual ordinary queue advances bottom-up and then left-to-right without visiting events");
        Check(map.GetAllMapPoints().Where(point => point.PointType != MapPointType.Unknown).All(point => ScoutKnown(point) == null),
            "ordinary battle, shop and rest nodes are not scouted or changed");
        Check(JsonSerializer.Serialize(RunState.Rng.ToSerializable()) == rng && ScoutOddsSnapshot(RunState.Odds.UnknownMapPoint) == odds &&
            JsonSerializer.Serialize(RunState.Act.ToSave().SerializableRooms) == queue && RunState.VisitedEventIds.Count == visited,
            "real scouting generation preserves all native RNG streams, live odds, event queue and visited set");
        foreach (MapPoint point in points)
        {
            string predicted = ScoutEventId(ScoutKnown(point)!);
            Check(ScoutTryRoom(point, [], out RoomType type) && type == RoomType.Event, "authoritative room entry returns the saved room type without reroll");
            Check(ScoutTryEvent(point, out EventModel? selected) && selected?.Id.Entry == predicted && RunState.VisitedEventIds.Contains(selected.Id),
                "actual event entry returns exactly the marked event and only then records its visit");
        }
        Check(JsonSerializer.Serialize(RunState.Rng.ToSerializable()) == rng, "entering valid saved events never advances a native random stream");
        string data = marker.ScoutingData;
        var replay = ScoutReset(4); ScoutingMemories = []; ScoutForceEvents(); ScoutEnsure(map);
        Check(replay.ScoutingData == data, "same seed, map and peer identity independently generate byte-identical scouting ledgers");
        var peer = SupportTeammate(); Set(peer, "<NetId>k__BackingField", 2UL); Set(RunState, "_players", new List<Player> { Player, peer });
        Check((bool)TreasureCall("CanView", Player)! && !(bool)TreasureCall("CanView", peer)!, "shared scout content remains hidden from an ineligible local teammate");
    }
    static void ScoutingLateAndInvalidCases()
    {
        var marker = ScoutReset(3); ScoutingMemories = []; ScoutForceEvents();
        var map = new ScoutingMapFixture((0, 0, MapPointType.Unknown), (0, 2, MapPointType.Unknown), (0, 4, MapPointType.Unknown));
        ScoutAt(map.GetPoint(0, 0)!); ScoutEnsure(map); Check(ScoutKnown(map.GetPoint(0, 2)!) == null, "three members cannot scout the map");
        DMember(HBRGOSakiMod.Core.Models.Capabilities.ThirtyOneDMember.MurofushiRisa); ScoutEnsure(map);
        Check(ScoutKnown(map.GetPoint(0, 0)!) == null && ScoutKnown(map.GetPoint(0, 2)!) != null && ScoutKnown(map.GetPoint(0, 4)!) != null,
            "late fourth member scouts only current-act unvisited future nodes");
        var fourth = Player.Deck.Cards.OfType<ThirtyOneDMemberFixture>().Single(card => (int)card.ThirtyOneDMember == 3); Player.Deck.RemoveInternal(fourth); ScoutEnsure(map);
        Check((bool)TreasureCall("CanView", Player)! && ScoutKnown(map.GetPoint(0, 4)!) != null, "dropping to three keeps the scouting knowledge already gained this act");
        map.GetPoint(0, 2)!.PointType = MapPointType.Monster;
        Check(!ScoutTryRoom(map.GetPoint(0, 2)!, [], out _) && ScoutKnown(map.GetPoint(0, 2)!) == null, "a changed real node invalidates its old forecast and returns to native selection");
        Set(RunState, "_currentActIndex", 1); Set(RunState, "_visitedMapCoords", new List<MapCoord>()); ScoutEnsure(map);
        Check(!(bool)TreasureCall("CanView", Player)! && ScoutKnown(map.GetPoint(0, 4)!) == null, "three remaining members do not scout or retain visibility in a new act");
        marker = ScoutReset(4); ScoutingMemories = []; ScoutForceEvents();
        map = new ScoutingMapFixture((0, 0, MapPointType.Unknown)); ScoutEnsure(map); object known = ScoutKnown(map.GetPoint(0, 0)!)!;
        ScoutingEventFixture.Disabled.Add(ScoutEvents.Single(type => ModelDb.GetId(type).Entry == ScoutEventId(known)));
        Check(!ScoutTryEvent(map.GetPoint(0, 0)!, out _) && ScoutKnown(map.GetPoint(0, 0)!) == null,
            "a forecast event whose live eligibility changed is invalidated and left to the native fallback");
        marker = ScoutReset(4); ScoutingMemories = []; ScoutForceEvents(); ScoutEnsure(map);
        Check(!ScoutTryRoom(map.GetPoint(0, 0)!, [RoomType.Event], out _), "a new live unknown-room blacklist overrides an old forecast");
    }
    static void ScoutingShopNeighborCases()
    {
        foreach (int mode in new[] { 0, 1, 2 })
        {
            ScoutReset(4); ScoutingMemories = []; ScoutForceEvents();
            RunState.Odds.UnknownMapPoint.ShopOdds = 1f; RunState.Odds.UnknownMapPoint.SetBaseOdds(RoomType.Shop, 1f);
            var map = mode == 0 ? new ScoutingMapFixture((0, 0, MapPointType.Unknown), (0, 1, MapPointType.Shop))
                : mode == 1 ? new ScoutingMapFixture((0, 0, MapPointType.Shop), (0, 1, MapPointType.Unknown))
                : new ScoutingMapFixture((0, 0, MapPointType.Unknown), (0, 1, MapPointType.Unknown));
            map.GetPoint(0, 0)!.AddChildPoint(map.GetPoint(0, 1)!); ScoutEnsure(map);
            if (mode == 0) Check(ScoutRoom(ScoutKnown(map.GetPoint(0, 0)!)!) == RoomType.Event,
                "scouting excludes shop when every next node is a shop");
            else if (mode == 1) Check(ScoutRoom(ScoutKnown(map.GetPoint(0, 1)!)!) == RoomType.Event,
                "scouting excludes shop when any actual parent node is a shop");
            else Check(ScoutRoom(ScoutKnown(map.GetPoint(0, 0)!)!) == RoomType.Shop && ScoutRoom(ScoutKnown(map.GetPoint(0, 1)!)!) == RoomType.Event,
                "a shop forecast itself participates in the next node's virtual no-consecutive-shop rule");
        }
    }
    static void ScoutingMemoryCases()
    {
        var marker = ScoutReset(4); ScoutInitializeMemories(); HbrStoryEvent[] memories = ScoutingMemories;
        Type pool = Mod.GetType("HBRGOSakiMod.Core.Story.HbrMemoryFragmentPool", true)!;
        ScoutingFloats.Enqueue(.2f);
        var first = (HbrStoryEvent)Invoke(pool.GetMethod("ChooseForScouting", F)!, null, RunState, true, Array.Empty<string>(), new Rng(1), false)!;
        ScoutingFloats.Enqueue(.2f);
        var second = (HbrStoryEvent)Invoke(pool.GetMethod("ChooseForScouting", F)!, null, RunState, true, new[] { first.Definition.Id }, new Rng(1), false)!;
        Check(first == memories[0] && second == memories[1], "real memory selector halves and redistributes an already previewed story's weight");
        var map = new ScoutingMapFixture((0, 0, MapPointType.Unknown), (0, 1, MapPointType.Unknown));
        Native(Player.GetRelic<ElectronicSoldierManual>()!, "SetMemoryFragmentMapData", "v1;0,m,0,0;0,m,0,1");
        string rng = JsonSerializer.Serialize(RunState.Rng.ToSerializable()); int visited = RunState.VisitedEventIds.Count;
        ScoutingFloats.Enqueue(.2f); ScoutingFloats.Enqueue(.2f); ScoutEnsure(map);
        Check(ScoutEventId(ScoutKnown(map.GetPoint(0, 0)!)!) == memories[0].Id.Entry && ScoutEventId(ScoutKnown(map.GetPoint(0, 1)!)!) == memories[1].Id.Entry,
            "actual dedicated-memory generation feeds its earlier virtual pick into the next real weighted choice");
        Check(JsonSerializer.Serialize(RunState.Rng.ToSerializable()) == rng && RunState.VisitedEventIds.Count == visited &&
            string.IsNullOrEmpty(Player.GetRelic<ElectronicSoldierManual>()!.MemoryGardenMetCharacters),
            "preselected memories consume no native RNG and do not mark a story or character encountered");
        marker = ScoutReset(4); ScoutingMemories = memories; ScoutForceEvents();
        map = new ScoutingMapFixture((0, 0, MapPointType.Unknown), (0, 1, MapPointType.Unknown));
        foreach (float roll in new[] { .9f, 0f, .2f, .9f, 0f, .2f }) ScoutingFloats.Enqueue(roll);
        ScoutEnsure(map);
        Check(ScoutEventId(ScoutKnown(map.GetPoint(0, 0)!)!) == memories[0].Id.Entry && ScoutEventId(ScoutKnown(map.GetPoint(0, 1)!)!) == memories[1].Id.Entry &&
            map.GetAllMapPoints().All(point => (bool)TreasureGet(ScoutKnown(point)!, "IsMemory")!),
            "ordinary unknown nodes passing the30percent gate share the same virtual memory repeat weights");
        ScoutingFloats.Enqueue(.5f);
        Check(Invoke(pool.GetMethod("ChooseForScouting", F)!, null, RunState, false, Array.Empty<string>(), new Rng(1), false) == null,
            "ordinary unknown nodes retain the native mod30percent memory gate rather than becoming guaranteed fragments");
        marker = ScoutReset(4); ScoutInitializeMemories(); ScoutForceEvents(); Set(RunState, "_visitedEventIds", new HashSet<ModelId>());
        map = new ScoutingMapFixture((0, 0, MapPointType.Unknown), (0, 2, MapPointType.Unknown));
        Native(Player.GetRelic<ElectronicSoldierManual>()!, "SetMemoryFragmentMapData", "v1;0,g,0,0;0,m,0,2");
        ScoutEnsure(map);
        Check(ScoutKnown(map.GetPoint(0, 0)!) == null && ScoutKnown(map.GetPoint(0, 2)!) is { } future && (bool)TreasureGet(future, "IsMemory")! && ScoutEventId(future) != "",
            "preceremony scouting predicts the future dedicated memory after the mandatory gateway without scouting the gateway");
        Check(!RunState.VisitedEventIds.Any(id => id.Entry == "JOINING_CEREMONY_HBRGO_SAKI") && string.IsNullOrEmpty(Player.GetRelic<ElectronicSoldierManual>()!.MemoryGardenMetCharacters),
            "virtual future-ceremony eligibility neither records JOINING nor encounters a character early");
    }
    static void ScoutingSaveCases()
    {
        var marker = ScoutReset(4); ScoutingMemories = []; ScoutForceEvents();
        var map = new ScoutingMapFixture((0, 0, MapPointType.Unknown), (0, 1, MapPointType.Unknown)); ScoutEnsure(map);
        string[] before = map.GetAllMapPoints().Select(point => JsonSerializer.Serialize(ScoutKnown(point))).ToArray();
        Assembly native = typeof(SavedProperties).Assembly;
        if (native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is { } legacy)
            legacy.GetMethod("InjectTypeIntoCache", F)!.Invoke(null, [typeof(ThirtyOneDCollection)]);
        else
        {
            Type cache = native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
            cache.GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [typeof(ThirtyOneDCollection)]);
            cache.GetField("_initialized", F)!.SetValue(null, true);
        }
        var options = new JsonSerializerOptions { IncludeFields = true };
        string save = JsonSerializer.Serialize(SavedProperties.From(marker), options);
        var restored = (ThirtyOneDCollection)ModelDb.Relic<ThirtyOneDCollection>().MutableClone(); Set(restored, "_owner", Player);
        JsonSerializer.Deserialize<SavedProperties>(save, options)!.Fill(restored);
        Set(Player, "_relics", Player.Relics.Select(relic => ReferenceEquals(relic, marker) ? restored : relic).ToList());
        object table = ScoutTreasureHelper.GetField("Caches", F)!.GetValue(null)!;
        table.GetType().GetMethod("Remove")!.Invoke(table, [RunState]);
        Check(restored.ScoutingData == marker.ScoutingData && map.GetAllMapPoints().Select(point => JsonSerializer.Serialize(ScoutKnown(point))).SequenceEqual(before),
            "actual native SavedProperties JSON reload reconstructs identical known points without the old in-memory cache");
        Check((bool)TreasureCall("CanView", Player)!, "the saved per-act viewer qualification survives a native relic save reload");
    }
    static async Task ScoutingCases()
    {
        // UnlockState.all eagerly enumerates the unrelated encounter catalog.
        // This fixture exercises event/relic selection, not encyclopedia setup.
        typeof(ModelDb).GetField("_allEncounters", F)!.SetValue(null, Array.Empty<EncounterModel>());
        Set(State<ElectronicSoldierManual>("Manual"), "<Id>k__BackingField", ModelDb.GetId<ElectronicSoldierManual>());
        foreach (Type type in new[] { typeof(ThirtyOneDCollection), typeof(ThirtyOneDMemberFixture) }.Concat(ScoutEvents))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var harmony = new Harmony(ProductionId + ".31d-scouting");
        void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            Patch(Mod.GetType("HBRGOSakiMod.Core.Patches.ThirtyOneDScoutingMapUi", true)!.GetMethod("Refresh", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            Patch(Mod.GetType("HBRGOSakiMod.Core.Story.HbrMemoryFragmentPool", true)!.GetMethod("GetMemories", F)!, nameof(ScoutingMemoryLeaf));
            Patch(typeof(Rng).GetMethod("NextFloat", [typeof(float)])!, nameof(ScoutingFloatLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            if (Environment.GetEnvironmentVariable("HBR_SCOUT_CASE_FILTER") != "treasure")
            { ScoutingOddsParityCases(); ScoutingGenerationCases(); ScoutingLateAndInvalidCases(); ScoutingShopNeighborCases(); ScoutingMemoryCases(); ScoutingSaveCases(); }
            await ScoutingTreasureCases();
        }
        finally { harmony.UnpatchAll(harmony.Id); CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunScouting(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); ScoutingCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, caseSelection = Environment.GetEnvironmentVariable("HBR_SCOUT_CASE_FILTER") ?? "all", checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real deterministic scouting service, native unknown-room odds/event queue and treasure reserve/open paths. UI refresh and presentation/wait leaves are isolated; no Godot process or rendered map/input session." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " scouting checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
