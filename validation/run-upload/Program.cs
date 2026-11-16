using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.RunUpload;

// Offline checks for the run upload (Core/RunUpload/Common). Nothing here opens
// a network connection: every request goes to FakeTransport.
//   dotnet run --project validation/run-upload/Checks.csproj [handoff folder]
var checks = new Checks();
string fixtures = Path.Combine(AppContext.BaseDirectory, "fixtures");
string handoff = args.Length > 0 ? args[0] : @"D:\stsmod\.ai-collab\handoffs\run-upload-20261108";
string work = Path.Combine(AppContext.BaseDirectory, "work");
if (Directory.Exists(work)) Directory.Delete(work, true);
Directory.CreateDirectory(work);

string schemaText = File.ReadAllText(Path.Combine(fixtures, "schema-v1.md"));
var example = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(fixtures, "example-run-v1.json")));
HashSet<string> schemaKeys = RunValidator.SchemaKeys(schemaText);

FixtureChecks.Run(checks, fixtures, handoff);
EnvelopeChecks.Run(checks);
NameChecks.Run(checks);
ExampleChecks.Run(checks, example, schemaKeys);
BuilderChecks.Run(checks, schemaKeys);
SettingsChecks.Run(checks, work);
IntegrityChecks.Run(checks);
SubmitChecks.Run(checks, work);
await SwitchChecks.Run(checks, work);
await RetryChecks.Run(checks, work);
QueueChecks.Run(checks, work);
ConsoleChecks.Run(checks);
RecordingChecks.Run(checks, work, schemaKeys);
OdRequestChecks.Run(checks);
SourceChecks.Run(checks);

try
{
    Directory.Delete(work, true);
}
catch (IOException)
{
}
return checks.Report();

sealed class Checks
{
    private readonly List<string> _failures = new();
    private readonly List<(string Name, int Passed)> _sections = new();
    private int _passed;
    private string _section = string.Empty;

    public void Section(string name)
    {
        _section = name;
        _sections.Add((name, 0));
        Console.WriteLine($"== {name}");
    }

    public void True(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            if (_sections.Count > 0) _sections[^1] = (_sections[^1].Name, _sections[^1].Passed + 1);
            return;
        }
        _failures.Add($"[{_section}] {name}");
        Console.WriteLine($"  FAILED: {name}");
    }

    public void Equal<T>(T expected, T actual, string name) =>
        True(EqualityComparer<T>.Default.Equals(expected, actual), $"{name} (expected {expected}, got {actual})");

    public void Empty(IReadOnlyCollection<string> problems, string name)
    {
        True(problems.Count == 0, name + (problems.Count == 0 ? string.Empty : ": " + string.Join("; ", problems.Take(8))));
    }

    public int Report()
    {
        Console.WriteLine("passed per section:");
        foreach ((string name, int passed) in _sections) Console.WriteLine($"  {name}: {passed}");
        if (_failures.Count == 0)
        {
            Console.WriteLine($"PASS {_passed} run-upload checks");
            return 0;
        }
        Console.WriteLine($"FAIL {_failures.Count} of {_passed + _failures.Count} run-upload checks");
        foreach (string failure in _failures) Console.WriteLine("  " + failure);
        return 1;
    }
}

sealed class TestClock
{
    public DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    public DateTimeOffset Get() => Now;
}

sealed class FakeTransport : IRunUploadTransport
{
    public readonly List<(Uri Endpoint, byte[] Body, string UserAgent)> Calls = new();
    public Func<int, RunUploadResponse> Respond = _ => new RunUploadResponse(201, null, false, null, null);
    public Func<int, CancellationToken, Task> BeforeRespond;

    public async Task<RunUploadResponse> PostAsync(Uri endpoint, byte[] gzipBody, string userAgent, CancellationToken token)
    {
        Calls.Add((endpoint, gzipBody, userAgent));
        if (BeforeRespond != null) await BeforeRespond(Calls.Count, token);
        return Respond(Calls.Count);
    }
}

static class Testing
{
    public static byte[] Envelope(string uploadId, DateTimeOffset at, string installId = "b2e4d6f8-1a3c-4e5f-8a7b-9c0d1e2f3a4b") =>
        RunUploadJson.ToUtf8(RunUploadEnvelope.Create(uploadId, installId, "1.3.0.12", "v0.111.0", at,
            new JsonObject { ["outcome"] = "win", ["floorReached"] = 3 }));

    public static RunUploadCoordinator Coordinator(string root, FakeTransport transport, TestClock clock, Action onCreate = null) =>
        new(root, () =>
        {
            onCreate?.Invoke();
            return transport;
        }, clock.Get, _ => { }, RunUploadConstants.EndpointUri, "HBRGOSakiMod/test");

    public static JsonObject Body(byte[] gzip)
    {
        using var input = new MemoryStream(gzip);
        using var stream = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return (JsonObject)JsonNode.Parse(output.ToArray());
    }
}

static class JsonCompare
{
    // Every value in expected is present in actual; actual may have extra
    // object keys (checked separately against the schema's field names).
    public static List<string> Subset(JsonNode expected, JsonNode actual, string path = "$", ISet<string> skip = null)
    {
        var diffs = new List<string>();
        Walk(expected, actual, path, diffs, skip ?? new HashSet<string>());
        return diffs;
    }

    private static void Walk(JsonNode expected, JsonNode actual, string path, List<string> diffs, ISet<string> skip)
    {
        if (skip.Contains(path)) return;
        switch (expected)
        {
            case null:
                if (actual != null && actual.GetValueKind() != JsonValueKind.Null) diffs.Add($"{path}: expected null, got {actual.ToJsonString()}");
                break;
            case JsonObject eo:
                if (actual is not JsonObject ao)
                {
                    diffs.Add($"{path}: expected an object, got {actual?.ToJsonString() ?? "nothing"}");
                    return;
                }
                foreach (KeyValuePair<string, JsonNode> pair in eo)
                {
                    if (!ao.ContainsKey(pair.Key))
                    {
                        if (pair.Value != null) diffs.Add($"{path}.{pair.Key}: missing");
                        continue;
                    }
                    Walk(pair.Value, ao[pair.Key], $"{path}.{pair.Key}", diffs, skip);
                }
                break;
            case JsonArray ea:
                if (actual is not JsonArray aa)
                {
                    diffs.Add($"{path}: expected an array");
                    return;
                }
                if (ea.Count != aa.Count)
                {
                    diffs.Add($"{path}: expected {ea.Count} items, got {aa.Count}");
                    return;
                }
                for (int i = 0; i < ea.Count; i++) Walk(ea[i], aa[i], $"{path}[{i}]", diffs, skip);
                break;
            case JsonValue ev:
                if (actual is not JsonValue av || !Same(ev, av))
                    diffs.Add($"{path}: expected {ev.ToJsonString()}, got {actual?.ToJsonString() ?? "nothing"}");
                break;
        }
    }

    private static bool Same(JsonValue a, JsonValue b)
    {
        JsonValueKind ka = a.GetValueKind(), kb = b.GetValueKind();
        if (ka == JsonValueKind.Number && kb == JsonValueKind.Number)
            return decimal.Parse(a.ToJsonString(), CultureInfo.InvariantCulture) ==
                   decimal.Parse(b.ToJsonString(), CultureInfo.InvariantCulture);
        return ka == kb && a.ToJsonString() == b.ToJsonString();
    }
}

// The rules of schema-v1.md that a record must follow, checked on the
// example and on everything the builder produces.
static class RunValidator
{
    private static readonly HashSet<string> MapObjects = new(StringComparer.Ordinal) { "creatures", "start", "end", "names", "vars" };
    private static readonly HashSet<string> EventTypes = Set("turn_start turn_end draw play roll potion damage block power move spawn death exhaust discard generate shuffle select");
    private static readonly HashSet<string> RoomTypes = Set("monster elite boss event shop treasure rest_site");
    private static readonly HashSet<string> PointTypes = Set("ancient monster elite boss unknown shop treasure rest_site");
    private static readonly HashSet<string> RunOutcomes = Set("win loss abandoned");
    private static readonly HashSet<string> CombatOutcomes = Set("win loss escape abandoned interrupted");
    private static readonly HashSet<string> Tiers = Set("none first second third critical");
    private static readonly HashSet<string> IntentTypes = Set("attack buff debuff debuff_strong defend escape heal hidden summon sleep stun status_card card_debuff death_blow unknown");
    private static readonly HashSet<string> Sides = Set("player enemy");
    private static readonly HashSet<string> ChoiceSources = Set("reward event shop other");
    private static readonly HashSet<string> RelicSources = Set("combat elite boss treasure shop event ancient other");
    private static readonly HashSet<string> EquipmentActions = Set("drop buy sell alchemy equip unequip plus upgrade discard");
    private static readonly HashSet<string> Kinds = Set("booster chip ring earring bracelet necklace charm");
    private static readonly HashSet<string> BoughtKinds = Set("card relic potion removal");
    private static readonly HashSet<string> CreatureKinds = Set("player ally summon enemy");
    private static readonly HashSet<string> Rarities = Set("basic common uncommon rare ancient event token status curse quest");
    private static readonly Regex Time = new("^\\d{4}-\\d\\d-\\d\\dT\\d\\d:\\d\\d:\\d\\dZ$");

    private static HashSet<string> Set(string words) => new(words.Split(' '), StringComparer.Ordinal);

    public static HashSet<string> SchemaKeys(string schema)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(schema, "`([A-Za-z][A-Za-z0-9]*)`")) keys.Add(match.Groups[1].Value);
        foreach (Match match in Regex.Matches(schema, "\"([A-Za-z][A-Za-z0-9]*)\"")) keys.Add(match.Groups[1].Value);
        return keys;
    }

    public static List<string> Validate(JsonObject envelope, HashSet<string> schemaKeys, bool finished = true)
    {
        var errors = new List<string>();
        Keys(envelope, "$", null, schemaKeys, errors);
        string[] envelopeKeys = ["schema", "uploadId", "installId", "modVersion", "gameVersion", "uploadedAt", "run"];
        if (envelope.Count != 7 || envelopeKeys.Any(key => !envelope.ContainsKey(key)))
            errors.Add("the envelope must have exactly its seven fields");
        if (Str(envelope["schema"]) != "hbrgo-run/1") errors.Add("schema");
        if (!RunUploadIds.IsValidUuid(Str(envelope["uploadId"]))) errors.Add("uploadId");
        if (!RunUploadIds.IsValidUuid(Str(envelope["installId"]))) errors.Add("installId");
        if (!Time.IsMatch(Str(envelope["uploadedAt"]) ?? string.Empty)) errors.Add("uploadedAt");
        if (envelope["run"] is not JsonObject run)
        {
            errors.Add("run is not an object");
            return errors;
        }
        foreach (string key in new[] { "recorder", "startedAt", "character", "ascension", "seed", "acts", "floorReached" })
            if (!run.ContainsKey(key)) errors.Add($"run.{key} missing");
        if (!Time.IsMatch(Str(run["startedAt"]) ?? string.Empty)) errors.Add("run.startedAt");
        if (run["integrity"] is JsonObject integrity)
        {
            if (integrity["modifiers"] is not JsonArray modifiers || modifiers.Any(node => Str(node) == null))
                errors.Add("run.integrity.modifiers must be an array of labels");
            foreach (string flag in new[] { "consoleUsed", "otherGameplayMods" })
                if (integrity[flag] is not JsonValue value || value.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
                    errors.Add($"run.integrity.{flag} must be a boolean");
        }
        else if (run.ContainsKey("integrity") && run["integrity"] != null)
        {
            errors.Add("run.integrity must be an object");
        }
        if (finished)
        {
            if (!RunOutcomes.Contains(Str(run["outcome"]) ?? string.Empty)) errors.Add("run.outcome");
            if (!Time.IsMatch(Str(run["endedAt"]) ?? string.Empty)) errors.Add("run.endedAt");
            if (run["final"] is not JsonObject) errors.Add("run.final");
        }
        int floorNumber = run["recorder"]?["fromFloor"]?.GetValue<int>() ?? 1;
        int lastFloor = 0;
        foreach (JsonObject act in (run["acts"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            var nodes = new HashSet<string>();
            if (act["map"] is JsonObject map)
            {
                foreach (JsonObject node in (map["nodes"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                {
                    nodes.Add(node["at"]?.ToJsonString());
                    if (!PointTypes.Contains(Str(node["type"]) ?? string.Empty)) errors.Add($"map node type {node["type"]?.ToJsonString()}");
                }
                foreach (JsonObject node in (map["nodes"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                    foreach (JsonNode next in node["next"] as JsonArray ?? new JsonArray())
                        if (!nodes.Contains(next?.ToJsonString())) errors.Add($"map edge to {next?.ToJsonString()} leaves the map");
            }
            var path = (act["path"] as JsonArray ?? new JsonArray()).Select(node => node?.ToJsonString()).ToList();
            var floors = (act["floors"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().ToList();
            var floorAts = floors.Where(floor => floor["at"] != null).Select(floor => floor["at"].ToJsonString()).ToList();
            if (!path.SequenceEqual(floorAts)) errors.Add($"act {act["index"]}: path and floors[].at differ");
            for (int i = 1; i < path.Count; i++)
            {
                JsonObject from = (act["map"]?["nodes"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(node => node["at"]?.ToJsonString() == path[i - 1]);
                if (from != null && !(from["next"] as JsonArray ?? new JsonArray()).Any(next => next?.ToJsonString() == path[i]))
                    errors.Add($"act {act["index"]}: path step {path[i - 1]} -> {path[i]} is not a map edge");
            }
            foreach (JsonObject floor in floors)
            {
                int number = floor["floor"]?.GetValue<int>() ?? -1;
                if (lastFloor != 0 && number != lastFloor + 1) errors.Add($"floor {number} follows {lastFloor}");
                if (lastFloor == 0 && number != floorNumber) errors.Add($"first floor {number}, fromFloor {floorNumber}");
                lastFloor = number;
                if (!PointTypes.Contains(Str(floor["pointType"]) ?? string.Empty)) errors.Add($"floor {number} pointType");
                foreach (JsonObject room in (floor["rooms"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                {
                    if (!RoomTypes.Contains(Str(room["type"]) ?? string.Empty)) errors.Add($"floor {number} room type {room["type"]?.ToJsonString()}");
                    if (room["combat"] is JsonObject combat) Combat(combat, number, errors);
                    foreach (JsonObject earlier in (room["earlierAttempts"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                        Combat(earlier, number, errors);
                }
                if (floor["choices"] is JsonObject choices) Choices(choices, number, errors);
                if (floor["hbr"]?["equipment"] is JsonArray actions)
                    foreach (JsonObject action in actions.OfType<JsonObject>())
                    {
                        if (!EquipmentActions.Contains(Str(action["action"]) ?? string.Empty)) errors.Add($"floor {number} equipment action");
                        if (action["item"]?["kind"] is { } kind && !Kinds.Contains(Str(kind) ?? string.Empty)) errors.Add($"floor {number} item kind");
                        if (action["slot"] is { } slot && !Kinds.Contains(Str(slot) ?? string.Empty)) errors.Add($"floor {number} slot");
                    }
            }
        }
        if (run["floorReached"]?.GetValue<int>() is int reached && lastFloor != 0 && reached != lastFloor)
            errors.Add($"floorReached {reached}, last floor {lastFloor}");
        return errors;
    }

    private static void Combat(JsonObject combat, int floor, List<string> errors)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonNode> pair in combat["creatures"] as JsonObject ?? new JsonObject())
        {
            keys.Add(pair.Key);
            if (!Regex.IsMatch(pair.Key, "^(p|a[1-3]|s\\d+|e\\d+)$")) errors.Add($"floor {floor} creature key {pair.Key}");
            if (!CreatureKinds.Contains(Str(pair.Value?["kind"]) ?? string.Empty)) errors.Add($"floor {floor} creature kind");
        }
        foreach (JsonObject round in (combat["rounds"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            foreach (KeyValuePair<string, JsonNode> state in round["start"] as JsonObject ?? new JsonObject())
            {
                if (!keys.Contains(state.Key)) errors.Add($"floor {floor} start state for unknown {state.Key}");
                foreach (JsonObject intent in (state.Value?["intents"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                    if (!IntentTypes.Contains(Str(intent["type"]) ?? string.Empty)) errors.Add($"floor {floor} intent type");
            }
            foreach (JsonObject e in (round["events"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                string type = Str(e["type"]);
                if (!EventTypes.Contains(type ?? string.Empty)) errors.Add($"floor {floor} event type {type}");
                if (type is "turn_start" or "turn_end" && !Sides.Contains(Str(e["side"]) ?? string.Empty)) errors.Add($"floor {floor} side");
                if (type == "roll" && !Tiers.Contains(Str(e["tier"]) ?? string.Empty)) errors.Add($"floor {floor} roll tier");
                foreach (string field in new[] { "by", "to", "from", "who", "target" })
                    if (e[field] is JsonValue value && Str(value) is { } key && !keys.Contains(key))
                        errors.Add($"floor {floor} {type}.{field} names unknown creature {key}");
                if (type == "damage")
                {
                    int amount = e["amount"].GetValue<int>(), blocked = e["blocked"].GetValue<int>(), hp = e["hpLoss"].GetValue<int>();
                    int dp = e["dpLoss"]?.GetValue<int>() ?? 0, overkill = e["overkill"]?.GetValue<int>() ?? 0;
                    if (amount != blocked + hp + dp + overkill) errors.Add($"floor {floor} damage parts do not add up");
                }
            }
        }
        if (combat["result"] is JsonObject result && !CombatOutcomes.Contains(Str(result["outcome"]) ?? string.Empty))
            errors.Add($"floor {floor} combat outcome");
    }

    private static void Choices(JsonObject choices, int floor, List<string> errors)
    {
        foreach (JsonObject choice in (choices["cardChoices"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            if (!ChoiceSources.Contains(Str(choice["source"]) ?? string.Empty)) errors.Add($"floor {floor} card choice source");
            foreach (string list in new[] { "offered", "picked" })
                foreach (JsonObject card in (choice[list] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                    if (card["rarity"] is { } rarity && !Rarities.Contains(Str(rarity) ?? string.Empty))
                        errors.Add($"floor {floor} card rarity {rarity.ToJsonString()}");
        }
        foreach (JsonObject relic in (choices["relicsGained"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            if (relic["source"] is { } source && !RelicSources.Contains(Str(source) ?? string.Empty)) errors.Add($"floor {floor} relic source");
        foreach (string name in new[] { "ancientChoices", "rest", "eventChoices", "cardChoices", "relicChoices", "potionChoices" })
            if (choices[name] != null && choices[name] is not JsonArray) errors.Add($"floor {floor} {name} must be an array");
        foreach (JsonObject bought in (choices["shop"]?["bought"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            if (!BoughtKinds.Contains(Str(bought["kind"]) ?? string.Empty)) errors.Add($"floor {floor} shop kind");
    }

    private static void Keys(JsonNode node, string path, string parentKey, HashSet<string> schemaKeys, List<string> errors)
    {
        switch (node)
        {
            case JsonObject obj:
                bool map = parentKey != null && MapObjects.Contains(parentKey);
                foreach (KeyValuePair<string, JsonNode> pair in obj)
                {
                    if (!map && !schemaKeys.Contains(pair.Key)) errors.Add($"{path}.{pair.Key} is not a field of schema-v1.md");
                    Keys(pair.Value, $"{path}.{pair.Key}", map ? null : pair.Key, schemaKeys, errors);
                }
                break;
            case JsonArray array:
                for (int i = 0; i < array.Count; i++) Keys(array[i], $"{path}[{i}]", parentKey, schemaKeys, errors);
                break;
        }
    }

    private static string Str(JsonNode node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}

static class FixtureChecks
{
    public static void Run(Checks c, string fixtures, string handoff)
    {
        c.Section("fixtures");
        foreach (string name in new[] { "schema-v1.md", "example-run-v1.json" })
        {
            string fixture = Path.Combine(fixtures, name);
            c.True(File.Exists(fixture), $"{name} is in fixtures/");
            string source = Path.Combine(handoff, name);
            if (!File.Exists(source))
            {
                Console.WriteLine($"  note: {source} not found; checking against the fixture copy only");
                continue;
            }
            c.True(File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(fixture)),
                $"fixtures/{name} matches the handoff copy (copy it again after editing the schema)");
        }
    }
}

static class EnvelopeChecks
{
    public static void Run(Checks c)
    {
        c.Section("envelope and serialization");
        var at = new DateTimeOffset(2026, 11, 8, 20, 58, 9, TimeSpan.FromHours(8));
        string uploadId = RunUploadIds.NewUuid();
        JsonObject envelope = RunUploadEnvelope.Create(uploadId, "b2e4d6f8-1a3c-4e5f-8a7b-9c0d1e2f3a4b", "1.3.0.12",
            "v0.111.0", at, new JsonObject { ["seed"] = "X" });
        c.Equal(7, envelope.Count, "the envelope has seven fields");
        c.True(envelope.Select(pair => pair.Key).SequenceEqual(new[] { "schema", "uploadId", "installId", "modVersion", "gameVersion", "uploadedAt", "run" }),
            "envelope fields in the fixed order");
        c.Equal("hbrgo-run/1", envelope["schema"]?.GetValue<string>(), "schema id");
        c.Equal("2026-11-08T12:58:09Z", envelope["uploadedAt"]?.GetValue<string>(), "uploadedAt is UTC with Z");
        c.True(RunUploadIds.IsValidUuid(uploadId), "new upload ids are valid");
        c.True(Guid.Parse(uploadId).ToByteArray()[7] >> 4 == 4, "new upload ids are version 4");
        c.True(uploadId == uploadId.ToLowerInvariant() && uploadId.Length == 36, "upload ids are lowercase with hyphens");
        c.True(!RunUploadIds.IsValidUuid("00000000-0000-0000-0000-000000000000"), "the all-zero uuid is refused");
        c.True(!RunUploadIds.IsValidUuid("B2E4D6F8-1A3C-4E5F-8A7B-9C0D1E2F3A4B"), "uppercase uuids are not produced");
        var ids = Enumerable.Range(0, 200).Select(_ => RunUploadIds.NewUuid()).ToHashSet();
        c.Equal(200, ids.Count, "upload ids do not repeat");

        c.Equal("1.3.0.12", RunUploadEnvelope.CleanModVersion("1.3.0.12"), "mod version kept");
        c.Equal("unknown", RunUploadEnvelope.CleanModVersion(""), "empty mod version");
        c.Equal("unknown", RunUploadEnvelope.CleanModVersion("1.3 beta"), "mod version with a space");
        c.Equal("unknown", RunUploadEnvelope.CleanModVersion("-1.3"), "mod version must start alphanumeric");
        c.Equal("unknown", RunUploadEnvelope.CleanModVersion(new string('1', 33)), "mod version over 32 characters");
        c.Equal("v0.111.0", RunUploadEnvelope.CleanGameVersion("v0.111.0"), "game version kept");
        c.Equal("v0.1", RunUploadEnvelope.CleanGameVersion("v0.1\u4e2d"), "non-ASCII stripped from the game version");
        c.Equal(64, RunUploadEnvelope.CleanGameVersion(new string('a', 80)).Length, "game version cut to 64");
        c.Equal("unknown", RunUploadEnvelope.CleanGameVersion(null), "missing game version");

        byte[] json = RunUploadJson.ToUtf8(envelope);
        byte[] restamped = RunUploadEnvelope.Restamp(json, at.AddMinutes(5));
        JsonObject again = RunUploadJson.ParseObject(restamped);
        c.Equal("2026-11-08T13:03:09Z", again["uploadedAt"]?.GetValue<string>(), "restamping moves uploadedAt");
        again["uploadedAt"] = envelope["uploadedAt"]!.GetValue<string>();
        c.True(JsonNode.DeepEquals(again, envelope), "restamping changes nothing else");
        c.True(Encoding.UTF8.GetString(RunUploadJson.ToUtf8(new JsonObject { ["text"] = "打击" })).Contains("打击"),
            "Chinese text is written as UTF-8, not escaped");

        byte[] gzip = RunUploadEnvelope.Gzip(json);
        c.True(gzip.Length > 10 && gzip[0] == 0x1f && gzip[1] == 0x8b, "gzip magic bytes");
        c.True(RunUploadEnvelope.Gunzip(gzip, 1 << 20).AsSpan().SequenceEqual(json), "gzip round trip");
        // A single member's trailer holds the size of the whole body.
        c.Equal((uint)json.Length, BitConverter.ToUInt32(gzip, gzip.Length - 4), "the body is a single gzip member");
        bool threw = false;
        try
        {
            RunUploadEnvelope.Gunzip(gzip, 10);
        }
        catch (InvalidDataException)
        {
            threw = true;
        }
        c.True(threw, "decompression stops at the size limit");
    }
}

static class NameChecks
{
    public static void Run(Checks c)
    {
        c.Section("enum spelling");
        (string, string)[] snake =
        [
            ("RestSite", "rest_site"), ("Monster", "monster"), ("DebuffStrong", "debuff_strong"), ("StatusCard", "status_card"),
            ("CardDebuff", "card_debuff"), ("DeathBlow", "death_blow"), ("Standard", "standard"), ("Critical", "critical"),
            ("Booster", "booster"), ("Hand", "hand"), ("Player", "player")
        ];
        foreach ((string input, string expected) in snake) c.Equal(expected, RunRecordNames.Snake(input), $"Snake({input})");
        (string, string)[] camel =
        [
            ("LeadershipCharm", "leadershipCharm"), ("MentalStrength", "mentalStrength"), ("Madness", "madness"),
            ("AttackRoll", "attackRoll"), ("BossDamageTaken", "bossDamageTaken"), ("Strength", "strength")
        ];
        foreach ((string input, string expected) in camel) c.Equal(expected, RunRecordNames.Camel(input), $"Camel({input})");
    }
}

static class ExampleChecks
{
    public static void Run(Checks c, JsonObject example, HashSet<string> schemaKeys)
    {
        c.Section("example and schema");
        c.Empty(RunValidator.Validate(example, schemaKeys), "the example follows the schema rules");

        JsonObject built = ExampleBuild.Envelope();
        // v1 records fewer feature groups than the example demonstrates.
        var skip = new HashSet<string> { "$.run.recorder.features" };
        c.Empty(JsonCompare.Subset(example, built, skip: skip), "the builder reproduces every field of the example");
        c.Empty(RunValidator.Validate(built, schemaKeys), "the builder's record follows the schema rules");
        var features = (built["run"]?["recorder"]?["features"] as JsonArray)?.Select(node => node?.GetValue<string>()).ToList();
        c.True(features != null && features.SequenceEqual(RunUploadConstants.RecordedFeatures), "recorder.features lists what v1 records");
        c.True(features!.All(feature => schemaKeys.Contains(feature) || feature == "names"), "every recorded feature is documented");

        string text = built.ToJsonString(RunUploadJson.Options);
        c.True(JsonNode.DeepEquals(JsonNode.Parse(text), built), "the record survives a JSON round trip");
        c.True(!Regex.IsMatch(text, "\\d{17}"), "no 17-digit number (a Steam ID would look like one)");
        c.True(!text.Contains(":\\\\", StringComparison.Ordinal) && !text.Contains("Users", StringComparison.Ordinal),
            "no file system path");
    }
}

static class BuilderChecks
{
    public static void Run(Checks c, HashSet<string> schemaKeys)
    {
        c.Section("builder behaviour");
        var combat = new CombatRecordBuilder(1);
        object me = new(), ally = new(), enemyA = new(), enemyB = new(), pet = new(), cardA = new(), cardB = new();
        c.Equal("p", combat.Player(me, "CHARACTER.A"), "local player is p");
        c.Equal("a2", combat.Ally(ally, 2, "CHARACTER.B"), "allies are keyed by seat");
        c.Equal("e0", combat.Enemy(enemyA, "MONSTER.A", 150m, null), "first enemy e0");
        c.Equal("e1", combat.Enemy(enemyB, "MONSTER.B", null, 3), "second enemy e1");
        c.Equal("e0", combat.Enemy(enemyA, "MONSTER.A", 150m, null), "the same enemy keeps its key");
        c.Equal("s0", combat.Summon(pet, "MONSTER.PET", "p"), "first summon s0");
        c.Equal(3, combat.Combat["creatures"]?["e1"]?["joinedRound"]?.GetValue<int>(), "joinedRound is kept");
        c.Equal(1, combat.Uid(cardA), "first card uid 1");
        c.Equal(2, combat.Uid(cardB), "second card uid 2");
        c.Equal(1, combat.Uid(cardA), "a card keeps its uid");

        // Events before the first snapshot open round 1, in order.
        combat.Power("e0", "p", null, "POWER.X", 1, 1);
        combat.CloseRound();
        combat.TurnStart("player");
        combat.EnsureRound(1, () => new JsonObject());
        var events = (JsonArray)combat.Combat["rounds"]![0]!["events"]!;
        c.Equal("power", events[0]?["type"]?.GetValue<string>(), "a combat-start effect opens round 1");
        c.Equal("turn_start", events[1]?["type"]?.GetValue<string>(), "then the turn marker");
        // An extra turn in the same round continues it.
        combat.TurnEnd("player");
        combat.CloseRound();
        combat.TurnStart("player");
        combat.EnsureRound(1, () => throw new InvalidOperationException("no new snapshot for the same round"));
        c.Equal(1, combat.RoundCount, "an extra turn does not open a new round");
        c.Equal(4, events.Count, "the extra turn's marker joins the same round");

        // Damage during an open play counts for that card; after it ends, not.
        JsonObject play = combat.Play("p", combat.Card(cardA, "CARD.A", 0), "e0", 1, 0, false, 0);
        combat.Damage("p", "e0", CombatRecordBuilder.Source("cardUid", 1), 100, 20, 30, 50, 0, false);
        combat.Damage("p", "e1", CombatRecordBuilder.Source("cardUid", 1), 40, 0, 10, 0, 30, true);
        combat.EndPlay(1);
        combat.Damage("p", "e0", CombatRecordBuilder.Source("cardUid", 1), 7, 0, 7, 0, 0, false);
        combat.Damage("e0", "p", CombatRecordBuilder.Source("move", "BITE"), 60, 10, 25, 25, 0, false);
        c.Equal("{\"total\":140,\"hp\":40,\"dp\":50,\"blocked\":20,\"kills\":1}", play["dealt"]?.ToJsonString(), "play.dealt sums its damage");
        combat.Finish("win", new JsonObject { ["hp"] = 1 });
        c.Equal("{\"total\":147,\"hp\":47,\"dp\":50,\"blocked\":20,\"kills\":1}", combat.Combat["result"]?["dealt"]?.ToJsonString(), "result.dealt counts only p");
        c.Equal("{\"total\":60,\"hp\":25,\"dp\":25,\"blocked\":10}", combat.Combat["result"]?["taken"]?.ToJsonString(), "result.taken counts damage to p");
        c.Equal(1, combat.Combat["result"]?["rounds"]?.GetValue<int>(), "result.rounds");

        // Truncation keeps the turn markers.
        var small = new CombatRecordBuilder(1, maxEvents: 3);
        small.Player(me, "CHARACTER.A");
        small.EnsureRound(1, () => new JsonObject());
        for (int i = 0; i < 5; i++) small.Draw("p", small.Card(new object(), "CARD.X", 0));
        small.TurnEnd("player");
        var smallEvents = (JsonArray)small.Combat["rounds"]![0]!["events"]!;
        c.True(small.IsTruncated, "a combat over the event limit is marked truncated");
        c.Equal(4, smallEvents.Count, "events stop at the limit, the turn marker still lands");

        // The format still allows partial records numbered from fromFloor; the
        // recorder never writes them (RecordingChecks, SourceChecks).
        var partial = new RunRecordBuilder(new RunRecordHeader
        {
            Character = "CHARACTER.A", Ascension = 1, Seed = "S", GameMode = "standard",
            StartedAt = DateTimeOffset.UnixEpoch, Locale = "zhs", Partial = true, FromFloor = 7, Rules = new JsonObject()
        });
        partial.EnsureAct(1, "ACT.B", null);
        JsonObject first = partial.BeginFloor((2, 5), "monster", firstActFloor: 4);
        JsonObject second = partial.BeginFloor((2, 6), "elite", firstActFloor: 99);
        c.Equal(7, first["floor"]?.GetValue<int>(), "first partial floor number");
        c.Equal(4, first["actFloor"]?.GetValue<int>(), "first partial floor in the act");
        c.Equal(8, second["floor"]?.GetValue<int>(), "next floor number");
        c.Equal(5, second["actFloor"]?.GetValue<int>(), "next floor in the act");
        c.True(partial.Run["recorder"]?["partial"]?.GetValue<bool>() == true, "recorder.partial");

        // Resume continues numbering and counts the session.
        string saved = partial.Run.ToJsonString(RunUploadJson.Options);
        RunRecordBuilder resumed = RunRecordBuilder.Resume((JsonObject)JsonNode.Parse(saved));
        c.True(resumed != null, "a saved record resumes");
        resumed!.AddSession();
        JsonObject third = resumed.BeginFloor((3, 7), "rest_site");
        c.Equal(9, third["floor"]?.GetValue<int>(), "numbering continues after resuming");
        c.Equal(2, resumed.Run["recorder"]?["sessions"]?.GetValue<int>(), "sessions counts the continued session");
        c.True(RunRecordBuilder.Resume(new JsonObject { ["x"] = 1 }) == null, "something else does not resume");

        resumed.AddRoom("monster", "ENCOUNTER.A", ["MONSTER.A"]);
        resumed.Note("记录器测试");
        resumed.Note("记录器测试");
        c.Equal(1, (resumed.Run["recorder"]?["notes"] as JsonArray)?.Count, "notes are not repeated");
        resumed.Finish("loss", new JsonObject { ["encounter"] = "ENCOUNTER.A" }, null, DateTimeOffset.UnixEpoch.AddHours(1), 3600,
            new JsonObject { ["floor"] = 9 }, new Dictionary<string, string> { ["MONSTER.A"] = "甲", ["ACT.B"] = "" });
        IReadOnlyCollection<string> ids = resumed.CollectIds();
        c.True(ids.Contains("CHARACTER.A") && ids.Contains("ENCOUNTER.A") && ids.Contains("MONSTER.A") && ids.Contains("ACT.B"),
            "CollectIds finds the game ids");
        c.True(!ids.Contains("standard") && !ids.Contains("zhs"), "CollectIds ignores other strings");
        c.Equal("{\"MONSTER.A\":\"甲\"}", resumed.Run["names"]?.ToJsonString(RunUploadJson.Options), "empty names are dropped");
        c.Equal("1970-01-01T01:00:00Z", resumed.Run["endedAt"]?.GetValue<string>(), "endedAt");
        c.Equal(9, resumed.Run["floorReached"]?.GetValue<int>(), "floorReached");

        string longText = new('字', 300);
        c.Equal(200, RunRecordBuilder.Text(longText).Length, "texts are cut to 200 characters");
        c.True(RunRecordBuilder.Option("K", longText)["text"]!.GetValue<string>().Length == 200, "option texts are cut");

        JsonObject envelope = RunUploadEnvelope.Create(RunUploadIds.NewUuid(), RunUploadIds.NewUuid(), "1.3.0.12", "v0.107.1",
            DateTimeOffset.UnixEpoch, resumed.Run);
        c.Empty(RunValidator.Validate(envelope, schemaKeys), "a resumed partial record follows the schema rules");
    }
}

static class SettingsChecks
{
    public static void Run(Checks c, string work)
    {
        c.Section("settings store");
        string root = Path.Combine(work, "settings");
        var store = new RunUploadSettingsStore(root);
        c.True(!store.IsEnabled, "off by default");
        c.Equal(string.Empty, store.InstallId, "no install id by default");
        string id = store.Enable(DateTimeOffset.UnixEpoch);
        c.True(RunUploadIds.IsValidUuid(id) && store.IsEnabled, "agreeing turns it on with an install id");
        var reread = new RunUploadSettingsStore(root);
        c.True(reread.IsEnabled && reread.InstallId == id, "the switch and the id persist");
        reread.Disable();
        c.True(!reread.IsEnabled && reread.InstallId == id, "turning off keeps the id for deletion requests");
        c.True(!new RunUploadSettingsStore(root).IsEnabled, "off persists");
        c.Equal(id, new RunUploadSettingsStore(root).Enable(DateTimeOffset.UnixEpoch), "turning on again keeps the same id");
        JsonObject file = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(root, "settings.json")));
        c.True(file.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal)
                .SequenceEqual(new[] { "ConsentVersion", "ConsentedAt", "Enabled", "InstallId", "PromptAnswer", "PromptAnsweredAt", "PromptVersion" }),
            "the settings file holds only the switch, the answer, consent and id");

        c.Section("first-launch prompt");
        string promptRoot = Path.Combine(work, "prompt");
        var prompt = new RunUploadSettingsStore(promptRoot);
        c.True(prompt.NeedsPrompt && !prompt.IsEnabled, "before the answer: asked, and off");
        c.Equal(string.Empty, prompt.AnswerPrompt(false, DateTimeOffset.UnixEpoch), "deny creates no id");
        c.True(!prompt.NeedsPrompt && !prompt.IsEnabled, "deny is remembered and stays off");
        var reread2 = new RunUploadSettingsStore(promptRoot);
        c.True(!reread2.NeedsPrompt && !reread2.IsEnabled && reread2.InstallId == string.Empty, "deny persists");
        string allowed = reread2.AnswerPrompt(true, DateTimeOffset.UnixEpoch);
        c.True(RunUploadIds.IsValidUuid(allowed) && reread2.IsEnabled && !reread2.NeedsPrompt, "allow turns it on with an id");
        c.True(new RunUploadSettingsStore(promptRoot).IsEnabled, "allow persists");
        File.WriteAllText(Path.Combine(promptRoot, "settings.json"),
            $"{{\"Enabled\":true,\"ConsentVersion\":{RunUploadConstants.ConsentVersion},\"PromptVersion\":{RunUploadConstants.ConsentVersion - 1},\"InstallId\":\"{allowed}\"}}");
        var older = new RunUploadSettingsStore(promptRoot);
        c.True(older.NeedsPrompt && !older.IsEnabled, "an answer to an older prompt version asks again and is off");
        File.WriteAllText(Path.Combine(promptRoot, "settings.json"),
            $"{{\"Enabled\":true,\"ConsentVersion\":{RunUploadConstants.ConsentVersion - 1},\"PromptVersion\":{RunUploadConstants.ConsentVersion - 1},\"InstallId\":\"{allowed}\"}}");
        older = new RunUploadSettingsStore(promptRoot);
        c.True(older.NeedsPrompt && !older.IsEnabled, "a consent from before the current version does not count");
        c.Equal(allowed, older.AnswerPrompt(true, DateTimeOffset.UnixEpoch), "answering again keeps the id");

        c.Section("new anonymous id");
        var fresh = new RunUploadSettingsStore(Path.Combine(work, "regenerate-none"));
        c.Equal(((string)null, (string)null), fresh.RegenerateInstallId(), "no id yet: nothing to regenerate");
        c.Equal(string.Empty, fresh.InstallId, "and none is created");
        var regen = new RunUploadSettingsStore(Path.Combine(work, "regenerate"));
        string first = regen.AnswerPrompt(true, DateTimeOffset.UnixEpoch);
        (string old, string next) = regen.RegenerateInstallId();
        c.True(old == first && RunUploadIds.IsValidUuid(next) && next != first, "a new id replaces the old one");
        c.Equal(next, new RunUploadSettingsStore(Path.Combine(work, "regenerate")).InstallId, "the new id persists");
        c.True(regen.IsEnabled, "regenerating keeps the switch on");
        regen.Disable();
        (string offOld, string offNext) = regen.RegenerateInstallId();
        c.True(offOld == next && offNext != next && !regen.IsEnabled, "it also works while off and keeps it off");
    }
}

static class IntegrityChecks
{
    public static void Run(Checks c)
    {
        c.Section("integrity and filter");
        RunModInfo own = new("HBRGOSakiMod", true), baseLib = new("BaseLib", false), ritsu = new("STS2-RitsuLib", false);
        RunIntegrity clean = RunIntegrity.FromMods([own, baseLib, ritsu, new RunModInfo("QuickSL", false),
            new RunModInfo("HBRGOSakiMod_SoundPack", false)]);
        c.True(!clean.HasModifiers && !clean.OtherGameplayMods && !clean.ConsoleUsed, "this mod, its libraries and non-gameplay mods are clean");
        RunIntegrity other = RunIntegrity.FromMods([own, baseLib, ritsu, new RunModInfo("TargetAnyone", true)]);
        c.True(other.OtherGameplayMods && !other.HasModifiers, "another gameplay mod is flagged");
        RunIntegrity modded = RunIntegrity.FromMods([own, new RunModInfo("Loadout", true), new RunModInfo("freeloadout", true),
            new RunModInfo("STS2_GOD_MOD", true)]);
        c.True(modded.Modifiers.SequenceEqual(new[] { "free_loadout", "god_mod", "loadout" }), "known modifiers, case-insensitive, sorted labels");
        c.True(!modded.OtherGameplayMods, "a modifier is not also counted as another gameplay mod");
        c.Equal("{\"modifiers\":[],\"consoleUsed\":false,\"otherGameplayMods\":true}", other.ToJson().ToJsonString(), "integrity JSON");
        c.Equal(5, RunUploadConstants.EarlyEndFloors, "early end: 5 floors (confirmed by the user, 2026-11-08)");

        JsonObject Run(string outcome, int floor, RunIntegrity integrity) => new()
        {
            ["outcome"] = outcome,
            ["floorReached"] = floor,
            ["integrity"] = integrity?.ToJson()
        };
        c.Equal((string)null, RunUploadFilter.RejectReason(Run("loss", 6, clean)), "loss on floor 6 is uploaded");
        c.Equal("early-end", RunUploadFilter.RejectReason(Run("loss", 5, clean)), "loss on floor 5 is not");
        c.Equal("early-end", RunUploadFilter.RejectReason(Run("abandoned", 1, clean)), "abandoned on floor 1 is not");
        c.Equal((string)null, RunUploadFilter.RejectReason(Run("abandoned", 6, clean)), "abandoned on floor 6 is uploaded");
        c.Equal((string)null, RunUploadFilter.RejectReason(Run("win", 3, clean)), "a win is never an early end");
        c.Equal("modifier", RunUploadFilter.RejectReason(Run("win", 52, modded)), "a modifier run is not uploaded");
        c.Equal("console", RunUploadFilter.RejectReason(Run("win", 52, new RunIntegrity(Array.Empty<string>(), true, false))),
            "a console run is not uploaded");
        c.Equal((string)null, RunUploadFilter.RejectReason(Run("loss", 30, other)), "another gameplay mod alone is uploaded");
        c.Equal((string)null, RunUploadFilter.RejectReason(Run("loss", 30, null)), "no integrity block: the filter has nothing to refuse");

        var builder = new RunRecordBuilder(new RunRecordHeader
        {
            Character = "CHARACTER.A", Ascension = 0, Seed = "S", GameMode = "standard", StartedAt = DateTimeOffset.UnixEpoch,
            Locale = "eng", Rules = new JsonObject(), Integrity = other
        });
        c.Equal("{\"modifiers\":[],\"consoleUsed\":false,\"otherGameplayMods\":true}", builder.Run["integrity"]?.ToJsonString(), "the header writes integrity");
        builder.ApplyIntegrity(modded, true);
        c.Equal("{\"modifiers\":[\"free_loadout\",\"god_mod\",\"loadout\"],\"consoleUsed\":true,\"otherGameplayMods\":true}",
            builder.Run["integrity"]?.ToJsonString(), "a later session's findings are merged, flags only turn on");
        builder.ApplyIntegrity(clean, false);
        c.Equal(true, builder.Run["integrity"]?["consoleUsed"]?.GetValue<bool>(), "a clean session does not clear them");
        c.Equal("{\"id\":\"CARD.A\",\"upgrade\":1,\"rarity\":\"rare\"}", RunRecordBuilder.CardRef("CARD.A", 1, null, "rare").ToJsonString(),
            "card choices carry the rarity");
    }
}

static class SubmitChecks
{
    public static void Run(Checks c, string work)
    {
        c.Section("finished runs");
        var clock = new TestClock();
        var transport = new FakeTransport();
        RunUploadCoordinator coordinator = Testing.Coordinator(Path.Combine(work, "submit"), transport, clock);
        Directory.CreateDirectory(coordinator.Root);

        JsonObject Finished(string outcome, int floor, RunIntegrity integrity) => new()
        {
            ["outcome"] = outcome,
            ["floorReached"] = floor,
            ["integrity"] = integrity.ToJson()
        };

        File.WriteAllText(coordinator.InProgressPath, "{\"key\":\"k\",\"run\":{}}");
        RunSubmitResult result = coordinator.SubmitFinishedRun(Finished("win", 40, RunIntegrity.Clean), "1.3.0.12", "v0.111.0");
        c.True(!result.Queued && coordinator.Queue.Count == 0, "before the prompt is answered nothing is queued");
        c.True(!File.Exists(coordinator.InProgressPath), "and the run in progress is not kept");

        coordinator.AnswerPrompt(false);
        result = coordinator.SubmitFinishedRun(Finished("win", 40, RunIntegrity.Clean), "1.3.0.12", "v0.111.0");
        c.True(!result.Queued && coordinator.Queue.Count == 0, "after deny nothing is queued");

        string installId = coordinator.AnswerPrompt(true);
        foreach ((string outcome, int floor, RunIntegrity integrity, string reason) in new[]
                 {
                     ("loss", 5, RunIntegrity.Clean, "early-end"),
                     ("abandoned", 2, RunIntegrity.Clean, "early-end"),
                     ("win", 50, new RunIntegrity(["loadout"], false, false), "modifier"),
                     ("loss", 30, new RunIntegrity(Array.Empty<string>(), true, false), "console")
                 })
        {
            File.WriteAllText(coordinator.InProgressPath, "{\"key\":\"k\",\"run\":{}}");
            result = coordinator.SubmitFinishedRun(Finished(outcome, floor, integrity), "1.3.0.12", "v0.111.0");
            c.True(!result.Queued && result.Reason == reason, $"{outcome} on floor {floor} ({reason}) is not uploaded");
            c.True(coordinator.Queue.Count == 0 && !File.Exists(coordinator.InProgressPath), $"{reason}: nothing is kept on this computer");
        }

        result = coordinator.SubmitFinishedRun(Finished("loss", 6, new RunIntegrity(Array.Empty<string>(), false, true)), "1.3.0.12", "v0.111.0");
        c.True(result.Queued && coordinator.Queue.Count == 1, "another gameplay mod alone: queued");
        JsonObject queued = RunUploadJson.ParseObject(coordinator.Queue.ReadEnvelope(result.UploadId));
        c.Equal(installId, queued["installId"]?.GetValue<string>(), "queued with the install id");
        c.Equal(true, queued["run"]?["integrity"]?["otherGameplayMods"]?.GetValue<bool>(), "and with its flag");

        c.Section("excluded runs");
        coordinator.MarkRunExcluded("seed|10", "console");
        c.Equal("console", coordinator.ExcludedReason("seed|10"), "the marker names the run and the reason");
        c.Equal((string)null, coordinator.ExcludedReason("seed|11"), "another run is not excluded");
        string marker = File.ReadAllText(coordinator.InProgressPath);
        c.True(!marker.Contains("\"run\"") && !marker.Contains("acts"), "the marker holds no run data");
        coordinator.ClearLocalData();
        c.Equal("console", coordinator.ExcludedReason("seed|10"), "clearing local data keeps the marker");
        coordinator.Disable();
        coordinator.MarkRunExcluded("seed|12", "modifier");
        c.Equal((string)null, coordinator.ExcludedReason("seed|12"), "nothing is written while off");
    }
}

static class SwitchChecks
{
    public static async Task Run(Checks c, string work)
    {
        c.Section("switch off: no request");
        var clock = new TestClock();
        var transport = new FakeTransport();
        int created = 0;
        string root = Path.Combine(work, "switch");
        RunUploadCoordinator coordinator = Testing.Coordinator(root, transport, clock, () => created++);
        c.True(!coordinator.IsEnabled, "a fresh install is off");
        RunUploadPassResult result = await coordinator.SendDueAsync();
        c.Equal("disabled", result.StopReason, "a pass while off stops at once");
        c.True(!coordinator.Enqueue(RunUploadIds.NewUuid(), Testing.Envelope(RunUploadIds.NewUuid(), clock.Now), out _),
            "a finished run is not queued while off");
        coordinator.SendInBackground();
        await Task.Delay(150);
        c.Equal(0, transport.Calls.Count, "no request while off");
        c.Equal(0, created, "no HTTP client is created while off");
        c.Equal(0, coordinator.Queue.Count, "nothing queued while off");
        c.True(!File.Exists(Path.Combine(root, "settings.json")), "nothing written while never turned on");
        c.True(coordinator.NeedsPrompt, "the prompt is still to be answered");

        // "拒绝", or closing the prompt (the dialog treats both as deny).
        coordinator.AnswerPrompt(false);
        c.True(!coordinator.NeedsPrompt && !coordinator.IsEnabled, "deny is remembered and stays off");
        result = await coordinator.SendDueAsync();
        c.True(!coordinator.Enqueue(RunUploadIds.NewUuid(), Testing.Envelope(RunUploadIds.NewUuid(), clock.Now), out _),
            "after deny a finished run is not queued");
        coordinator.SendInBackground();
        await Task.Delay(150);
        c.Equal(0, transport.Calls.Count, "after deny: no request");
        c.Equal(0, created, "after deny: no HTTP client");
        c.Equal(string.Empty, coordinator.InstallId, "after deny: no install id");

        string installId = coordinator.Enable();
        var queued = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            string uploadId = RunUploadIds.NewUuid();
            queued.Add(uploadId);
            c.True(coordinator.Enqueue(uploadId, Testing.Envelope(uploadId, clock.Now, installId), out _), $"run {i + 1} queued while on");
            clock.Now = clock.Now.AddSeconds(1);
        }
        coordinator.Disable();
        result = await coordinator.SendDueAsync();
        c.Equal(0, transport.Calls.Count, "turned off again: no request");
        c.Equal(0, created, "turned off again: still no HTTP client");
        c.Equal(3, coordinator.Queue.Count, "turning off keeps the unsent runs");
        c.Equal(installId, coordinator.InstallId, "turning off keeps the install id");

        string settings = Path.Combine(root, "settings.json");
        File.WriteAllText(settings, "{not json");
        c.True(!Testing.Coordinator(root, transport, clock).IsEnabled, "a damaged settings file means off");
        File.WriteAllText(settings, $"{{\"Enabled\":true,\"ConsentVersion\":0,\"InstallId\":\"{installId}\"}}");
        RunUploadCoordinator outdated = Testing.Coordinator(root, transport, clock, () => created++);
        c.True(!outdated.IsEnabled, "an older consent means off");
        await outdated.SendDueAsync();
        File.WriteAllText(settings, $"{{\"Enabled\":true,\"ConsentVersion\":{RunUploadConstants.ConsentVersion},\"InstallId\":\"nope\"}}");
        RunUploadCoordinator broken = Testing.Coordinator(root, transport, clock, () => created++);
        c.True(!broken.IsEnabled, "an invalid install id means off");
        await broken.SendDueAsync();
        c.Equal(0, transport.Calls.Count, "damaged or outdated consent: no request");
        c.Equal(0, created, "damaged or outdated consent: no HTTP client");

        // The sender itself also refuses when the switch reads off.
        var queue = new RunUploadQueue(Path.Combine(root, "queue"), clock.Get);
        var sender = new RunUploadSender(queue, transport, () => false, clock.Get, RunUploadConstants.EndpointUri, "ua", _ => { });
        result = await sender.SendDueAsync(CancellationToken.None);
        c.Equal(0, transport.Calls.Count, "the sender sends nothing while off");

        // Turning off during a pass: the request in flight finishes, no next one starts.
        string midRoot = Path.Combine(work, "switch-mid");
        var midTransport = new FakeTransport();
        RunUploadCoordinator mid = Testing.Coordinator(midRoot, midTransport, clock);
        string midInstall = mid.Enable();
        for (int i = 0; i < 3; i++)
        {
            string uploadId = RunUploadIds.NewUuid();
            mid.Enqueue(uploadId, Testing.Envelope(uploadId, clock.Now, midInstall), out _);
            clock.Now = clock.Now.AddSeconds(1);
        }
        midTransport.Respond = n =>
        {
            if (n == 1) mid.Disable();
            return new RunUploadResponse(201, null, false, null, null);
        };
        result = await mid.SendDueAsync();
        c.Equal(1, midTransport.Calls.Count, "turned off mid-pass: no further request");
        c.Equal(2, mid.Queue.Count, "turned off mid-pass: the rest stay queued");

        // Turning off cancels a request that is still waiting for the site.
        mid.Enable();
        midTransport.Respond = _ => new RunUploadResponse(201, null, false, null, null);
        midTransport.BeforeRespond = async (_, token) =>
        {
            mid.Disable();
            await Task.Delay(TimeSpan.FromSeconds(30), token);
        };
        result = await mid.SendDueAsync();
        c.Equal("cancelled", result.StopReason, "turning off cancels the request in flight");
        c.Equal(2, mid.Queue.Count, "a cancelled request leaves the run queued");
        c.True(mid.Queue.List().All(item => item.Attempts == 0), "a cancelled request is not counted as an attempt");

        // Clearing local data removes the queue and the run in progress.
        File.WriteAllText(mid.InProgressPath, "{}");
        int removed = mid.ClearLocalData();
        c.Equal(2, removed, "clearing reports the removed runs");
        c.Equal(0, mid.Queue.Count, "clearing empties the queue");
        c.True(!File.Exists(mid.InProgressPath), "clearing deletes the run in progress");
    }
}

static class RetryChecks
{
    public static async Task Run(Checks c, string work)
    {
        c.Section("retry");
        var clock = new TestClock();
        var transport = new FakeTransport();
        RunUploadCoordinator coordinator = Testing.Coordinator(Path.Combine(work, "retry"), transport, clock);
        string installId = coordinator.Enable();

        string first = Queue(coordinator, clock, installId);
        transport.Respond = _ => new RunUploadResponse(404, null, false, "not_found", null);
        RunUploadPassResult result = await coordinator.SendDueAsync();
        RunUploadQueueItem item = coordinator.Queue.List().Single();
        c.Equal(1, transport.Calls.Count, "404: one request");
        c.Equal(first, item.UploadId, "404: the run stays queued");
        c.Equal(1, item.Attempts, "404: counted as an attempt");
        c.Equal(clock.Now + TimeSpan.FromMinutes(1), item.NextAttemptAt, "404: first retry after one minute");
        c.Equal("http-404", result.StopReason, "404 ends the pass");
        c.True(item.LastError.Contains("404"), "the last error is kept");

        result = await coordinator.SendDueAsync();
        c.Equal(1, transport.Calls.Count, "not due yet: no request");
        c.Equal("nothing due", result.StopReason, "not due yet");

        clock.Now = clock.Now.AddSeconds(61);
        transport.Respond = _ => new RunUploadResponse(201, null, false, null, null);
        result = await coordinator.SendDueAsync();
        c.Equal(1, result.Delivered, "201 after a retry delivers");
        c.Equal(0, coordinator.Queue.Count, "a delivered run leaves the queue");
        JsonObject body = Testing.Body(transport.Calls[^1].Body);
        c.Equal(first, body["uploadId"]?.GetValue<string>(), "the retry reuses the upload id");
        c.Equal(RunUploadEnvelope.FormatTime(clock.Now), body["uploadedAt"]?.GetValue<string>(), "uploadedAt is the time of the attempt");
        c.Equal(installId, body["installId"]?.GetValue<string>(), "the install id is sent");
        c.Equal(RunUploadConstants.EndpointUri, transport.Calls[^1].Endpoint, "the request goes to the endpoint constant");
        c.Equal("https://runs.hbrgo.com/api/v1/runs", RunUploadConstants.EndpointUri.ToString(), "endpoint per endpoint.md");
        c.Equal("HBRGOSakiMod/test", transport.Calls[^1].UserAgent, "user agent");

        string second = Queue(coordinator, clock, installId);
        transport.Respond = _ => new RunUploadResponse(503, TimeSpan.FromSeconds(120), false, "storage_unavailable", null);
        await coordinator.SendDueAsync();
        c.Equal(clock.Now + TimeSpan.FromSeconds(120), coordinator.Queue.List().Single().NextAttemptAt, "503 waits Retry-After when longer");
        clock.Now = clock.Now.AddSeconds(121);
        transport.Respond = _ => new RunUploadResponse(429, TimeSpan.FromHours(1), false, "rate_limited", null);
        await coordinator.SendDueAsync();
        item = coordinator.Queue.List().Single();
        c.Equal(2, item.Attempts, "429: second attempt counted");
        c.Equal(clock.Now + TimeSpan.FromHours(1), item.NextAttemptAt, "429 waits Retry-After");
        clock.Now = clock.Now.AddHours(1).AddSeconds(1);
        transport.Respond = _ => new RunUploadResponse(0, null, false, null, "HttpRequestException: offline");
        result = await coordinator.SendDueAsync();
        item = coordinator.Queue.List().Single();
        c.Equal(3, item.Attempts, "offline: third attempt counted");
        c.Equal(clock.Now + TimeSpan.FromMinutes(4), item.NextAttemptAt, "offline: backoff doubles");
        c.True(item.LastError.Contains("offline"), "offline: the error is kept");
        c.Equal("unreachable", result.StopReason, "offline ends the pass");
        clock.Now = clock.Now.AddMinutes(5);
        transport.Respond = _ => new RunUploadResponse(200, null, true, null, null);
        result = await coordinator.SendDueAsync();
        c.True(result.Delivered == 1 && coordinator.Queue.Count == 0, "200 duplicate counts as delivered");
        c.Equal(second, Testing.Body(transport.Calls[^1].Body)["uploadId"]?.GetValue<string>(), "the same upload id throughout");

        foreach (int status in new[] { 400, 409, 413, 415, 422 })
        {
            Queue(coordinator, clock, installId);
            transport.Respond = _ => new RunUploadResponse(status, null, false, "x", null);
            result = await coordinator.SendDueAsync();
            c.True(result.Rejected == 1 && coordinator.Queue.Count == 0, $"{status}: dropped, never retried");
        }
        foreach (int status in new[] { 301, 401, 403, 404, 405, 408, 500, 502 })
        {
            Queue(coordinator, clock, installId);
            transport.Respond = _ => new RunUploadResponse(status, null, false, "x", null);
            result = await coordinator.SendDueAsync();
            c.True(result.Deferred == 1 && coordinator.Queue.Count == 1, $"{status}: kept for later");
            coordinator.Queue.Clear();
        }

        // Oldest first; a failure stops the pass so the rest are not hammered.
        string older = Queue(coordinator, clock, installId);
        string newer = Queue(coordinator, clock, installId);
        int before = transport.Calls.Count;
        transport.Respond = _ => new RunUploadResponse(502, null, false, null, null);
        await coordinator.SendDueAsync();
        c.Equal(before + 1, transport.Calls.Count, "a failure stops the pass after one request");
        c.Equal(older, Testing.Body(transport.Calls[^1].Body)["uploadId"]?.GetValue<string>(), "the oldest run goes first");
        clock.Now = clock.Now.AddMinutes(10);
        transport.Respond = _ => new RunUploadResponse(201, null, false, null, null);
        result = await coordinator.SendDueAsync();
        c.True(result.Delivered == 2 && coordinator.Queue.Count == 0, "both runs go once the site answers");
        c.True(transport.Calls.Skip(before + 1).Select(call => Testing.Body(call.Body)["uploadId"]?.GetValue<string>())
            .SequenceEqual(new[] { older, newer }), "in the order they were queued");

        c.Equal(TimeSpan.FromMinutes(1), RunUploadResponsePolicy.Backoff(1), "backoff 1");
        c.Equal(TimeSpan.FromMinutes(2), RunUploadResponsePolicy.Backoff(2), "backoff 2");
        c.Equal(TimeSpan.FromMinutes(256), RunUploadResponsePolicy.Backoff(9), "backoff 9");
        c.Equal(TimeSpan.FromHours(6), RunUploadResponsePolicy.Backoff(10), "backoff capped at six hours");
        c.Equal(TimeSpan.FromHours(6), RunUploadResponsePolicy.Backoff(40), "backoff stays capped");

        c.Equal((true, (string)null), HttpRunUploadTransport.ParseBody("{\"ok\":true,\"uploadId\":\"x\",\"duplicate\":true}"), "duplicate flag");
        c.Equal((false, (string)null), HttpRunUploadTransport.ParseBody("{\"ok\":true,\"duplicate\":false}"), "not a duplicate");
        c.Equal((false, "rate_limited"), HttpRunUploadTransport.ParseBody("{\"error\":{\"code\":\"rate_limited\",\"message\":\"m\"}}"), "error code");
        c.Equal((false, (string)null), HttpRunUploadTransport.ParseBody("<html>"), "a non-JSON answer");
    }

    private static string Queue(RunUploadCoordinator coordinator, TestClock clock, string installId)
    {
        string uploadId = RunUploadIds.NewUuid();
        coordinator.Enqueue(uploadId, Testing.Envelope(uploadId, clock.Now, installId), out _);
        clock.Now = clock.Now.AddSeconds(1);
        return uploadId;
    }
}

static class QueueChecks
{
    public static void Run(Checks c, string work)
    {
        c.Section("queue limits");
        var clock = new TestClock();
        var queue = new RunUploadQueue(Path.Combine(work, "queue-count"), clock.Get);
        var ids = new List<string>();
        for (int i = 0; i < RunUploadConstants.MaxQueuedRuns + 5; i++)
        {
            string id = RunUploadIds.NewUuid();
            ids.Add(id);
            queue.Enqueue(id, Testing.Envelope(id, clock.Now), out _);
            clock.Now = clock.Now.AddMinutes(1);
        }
        IReadOnlyList<RunUploadQueueItem> items = queue.List();
        c.Equal(RunUploadConstants.MaxQueuedRuns, items.Count, $"at most {RunUploadConstants.MaxQueuedRuns} runs");
        c.True(items.Select(item => item.UploadId).SequenceEqual(ids.Skip(5)), "the oldest runs are dropped first");
        c.Equal(50, RunUploadConstants.MaxQueuedRuns, "limit constant: 50 runs");
        c.Equal(TimeSpan.FromDays(30), RunUploadConstants.MaxQueueAge, "limit constant: 30 days");

        var aging = new RunUploadQueue(Path.Combine(work, "queue-age"), clock.Get);
        string old = RunUploadIds.NewUuid();
        aging.Enqueue(old, Testing.Envelope(old, clock.Now), out _);
        clock.Now = clock.Now.AddDays(30).AddSeconds(1);
        string fresh = RunUploadIds.NewUuid();
        aging.Enqueue(fresh, Testing.Envelope(fresh, clock.Now), out _);
        c.True(aging.List().Select(item => item.UploadId).SequenceEqual(new[] { fresh }), "runs older than 30 days are dropped");
        clock.Now = clock.Now.AddDays(31);
        c.Equal(1, aging.Prune(), "pruning on its own drops expired runs");
        c.Equal(0, aging.Count, "nothing left after pruning");

        var limits = new RunUploadQueue(Path.Combine(work, "queue-size"), clock.Get);
        c.True(!limits.Enqueue(RunUploadIds.NewUuid(), new byte[RunUploadConstants.MaxUncompressedBytes + 1], out string error) &&
               error.Contains(RunUploadConstants.MaxUncompressedBytes.ToString(CultureInfo.InvariantCulture)),
            "records above 16 MiB are refused");
        byte[] noise = new byte[6 * 1024 * 1024];
        new Random(7).NextBytes(noise);
        c.True(!limits.Enqueue(RunUploadIds.NewUuid(), noise, out error) && error.Contains("compressed"),
            "records above 4 MiB compressed are refused");
        c.True(!limits.Enqueue("not-a-uuid", Testing.Envelope(RunUploadIds.NewUuid(), clock.Now), out _), "an invalid upload id is refused");
        c.Equal(0, limits.Count, "refused records leave nothing behind");

        string kept = RunUploadIds.NewUuid();
        byte[] envelope = Testing.Envelope(kept, clock.Now);
        limits.Enqueue(kept, envelope, out _);
        c.True(limits.ReadEnvelope(kept).AsSpan().SequenceEqual(envelope), "a queued record reads back unchanged");
        c.True(File.ReadAllBytes(Path.Combine(limits.Directory, kept + ".json.gz"))[0] == 0x1f, "queued records are stored compressed");
        File.WriteAllText(Path.Combine(limits.Directory, RunUploadIds.NewUuid() + ".meta.json"), "{broken");
        c.Equal(1, limits.List().Count, "a damaged sidecar is ignored and removed");
        c.Equal(1, Directory.GetFiles(limits.Directory, "*.meta.json").Length, "the damaged sidecar is gone");
        c.Equal(1, limits.Clear(), "clear reports what it removed");
        c.Equal(0, Directory.GetFiles(limits.Directory).Length, "clear leaves the folder empty");
    }
}

// The example run, rebuilt through the builder with the same values.
static class ConsoleChecks
{
    // The game's own commands as DevConsole registers them (v0.107.1 and v0.111.0 alike).
    public static readonly string[] Harmless =
        ["achievement", "art", "bestiary", "dump", "getlogs", "help", "instant", "leaderboard", "log", "open", "sentry", "trailer"];

    // IsNetworked commands: they change the state the players share.
    public static readonly string[] Networked =
    [
        "act", "afflict", "ancient", "block", "card", "damage", "die", "draw", "enchant", "energy", "event", "fight", "godmode",
        "gold", "heal", "kill", "potion", "power", "relic", "remove_card", "room", "stars", "travel", "upgrade", "win"
    ];

    public static void Run(Checks c)
    {
        c.Section("console commands");
        c.True(RunUploadConstants.HarmlessConsoleCommands.OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(Harmless),
            "the harmless list is exactly the twelve commands read in the game's source");
        foreach (string name in Harmless) c.True(!RunUploadConsole.ChangesRun(name), $"{name} does not change the run");
        foreach (string name in new[] { "LOG", "Help", " getlogs ", "Instant" })
            c.True(!RunUploadConsole.ChangesRun(name), $"'{name}': names are compared without case");
        c.Equal(25, Networked.Length, "the game marks 25 commands as networked");
        foreach (string name in Networked) c.True(RunUploadConsole.ChangesRun(name), $"{name} changes the run");
        foreach (string name in new[] { "unlock", "cloud", "multiplayer" })
            c.True(RunUploadConsole.ChangesRun(name), $"{name} counts: not proven harmless");
        c.True(RunUploadConsole.ChangesRun("hbrgosakimod") && RunUploadConsole.ChangesRun("HBRGOSakiMod"), "this mod's own command counts");
        foreach (string name in new[] { "baselib", "ritsulib", "loadout", "spawn", "xyzzy" })
            c.True(RunUploadConsole.ChangesRun(name), $"{name} (another mod's or an unknown command) counts");
        c.True(RunUploadConsole.ChangesRun(null) && RunUploadConsole.ChangesRun("") && RunUploadConsole.ChangesRun("  "),
            "a missing name counts");
        c.True(RunUploadConsole.ChangesRun("logs") && RunUploadConsole.ChangesRun("log2") && RunUploadConsole.ChangesRun("helpme"),
            "only the exact names are harmless");
    }
}

// Plays runs the way RunRecorder drives RunRecordingGate (Core/RunUpload/RunRecorder.cs):
// one instance is one game session; a new one on the same folder is the game started again.
sealed class SessionSim
{
    public readonly RunUploadCoordinator Uploads;
    public readonly RunRecordingGate Gate;
    public RunRecordBuilder Record;
    private readonly RunIntegrity _integrity;
    private string _key;

    public SessionSim(string root, TestClock clock, RunIntegrity integrity = null)
    {
        Uploads = Testing.Coordinator(root, new FakeTransport(), clock);
        Directory.CreateDirectory(root);
        Gate = new RunRecordingGate(Uploads);
        _integrity = integrity ?? RunIntegrity.Clean;
    }

    public static string Key(string seed) => seed + "|0";

    // HandleRunStarted
    public bool Start(string seed)
    {
        _key = Key(seed);
        Record = null;
        if (!Gate.RunStarted(_key, _integrity)) return false;
        Record = new RunRecordBuilder(new RunRecordHeader
        {
            Character = "CHARACTER.A", Ascension = 0, Seed = seed, GameMode = "standard",
            StartedAt = DateTimeOffset.UnixEpoch, Locale = "zhs", Rules = new JsonObject(), Integrity = _integrity
        });
        Record.EnsureAct(0, "ACT.A", null);
        Persist();
        return true;
    }

    // HandleRunLoaded
    public bool Continue(string seed)
    {
        _key = Key(seed);
        (Record, _) = Gate.RunLoaded(_key, _integrity);
        Persist();
        return Record != null;
    }

    // HandleRoomEntered on new map points: nothing happens without a record.
    public void Floors(int count)
    {
        for (int i = 0; i < count && Record != null; i++)
        {
            Record.BeginFloor((0, Record.FloorCount + 1), "monster");
            Record.AddRoom("monster", "ENCOUNTER.A", ["MONSTER.A"]);
            Persist();
        }
    }

    // HandleCombatStarting in the current room, a few events, and the end of the battle or not.
    public JsonObject Battle(bool finish)
    {
        if (Record == null) return null;
        JsonObject room = Record.CurrentRoom;
        var combat = new CombatRecordBuilder(RunRecordBuilder.BeginCombatAttempt(room));
        combat.Player(new object(), "CHARACTER.A");
        combat.Enemy(new object(), "MONSTER.A", null, null);
        combat.EnsureRound(1, () => new JsonObject());
        combat.TurnStart("player");
        combat.Draw("p", combat.Card(new object(), "CARD.A", 0));
        room["combat"] = combat.Combat;
        if (finish) combat.Finish("win", new JsonObject { ["hp"] = 50 });
        Persist();
        return room;
    }

    // RunUploadService.Answer(true): the first-launch prompt or the settings toggle.
    public void TurnOn()
    {
        Uploads.AnswerPrompt(true);
        Gate.StopCurrentRun(RunRecordingGate.ReasonTurnedOn);
        Record = null;
    }

    // RunUploadService.Disable
    public void TurnOff()
    {
        Uploads.Disable();
        Gate.StopCurrentRun(RunRecordingGate.ReasonOff);
        Record = null;
        Uploads.DeleteInProgress();
    }

    // RunUploadService.ConfirmClearLocalData
    public void ClearLocalData()
    {
        Uploads.ClearLocalData();
        Gate.StopCurrentRun(RunRecordingGate.ReasonCleared);
        Record = null;
    }

    // RunRecordConsolePatch → RunRecorder.OnConsoleCommand
    public bool Console(string name, bool succeeded = true)
    {
        if (!Gate.ConsoleCommand(name, succeeded)) return false;
        Record = null;
        return true;
    }

    // A line typed into the console, here or by a teammate (the game runs a teammate's
    // networked command on every peer). DevConsole trims the line, splits it on spaces and
    // runs the command named by the first word; the console patch gets that word as cmdName.
    public bool Typed(string line, bool succeeded = true) => Console(CommandName(line), succeeded);

    public static string CommandName(string line) => line.Trim().Split(' ')[0];

    // HandleRunEnded. abandonedSeed: a saved run abandoned from the main menu without being loaded.
    public RunSubmitResult End(string outcome, string abandonedSeed = null)
    {
        Gate.RunEnded();
        string key = Gate.RunKey;
        if (Record == null)
        {
            if (abandonedSeed != null) key = Key(abandonedSeed);
            Record = RunRecordBuilder.Resume(Gate.SavedRecordOfEndedRun(key));
            if (Record == null) return null;
        }
        Record.Finish(outcome, null, null, DateTimeOffset.UnixEpoch.AddHours(1), 3600,
            new JsonObject { ["floor"] = Record.FloorCount }, new Dictionary<string, string>());
        Record.ApplyIntegrity(_integrity, Gate.ConsoleUsedIn(key));
        RunSubmitResult result = Uploads.SubmitFinishedRun(Record.Run, "1.3.0.12", "v0.111.0");
        Uploads.DeleteInProgress();
        Record = null;
        return result;
    }

    public JsonObject Queued(RunSubmitResult result) => RunUploadJson.ParseObject(Uploads.Queue.ReadEnvelope(result.UploadId));

    private void Persist()
    {
        if (Record != null) Uploads.SaveInProgress(_key, Record.Run, null);
    }
}

static class RecordingChecks
{
    public static void Run(Checks c, string work, HashSet<string> schemaKeys)
    {
        var clock = new TestClock();
        string Root(string name) => Path.Combine(work, "gate-" + name);

        c.Section("console during a run");
        var harmless = new SessionSim(Root("harmless"), clock);
        harmless.Uploads.Enable();
        c.True(!harmless.Console("gold"), "a command outside a run excludes nothing");
        c.True(harmless.Start("CON0"), "a run started while on is recorded");
        harmless.Floors(3);
        foreach (string name in ConsoleChecks.Harmless) c.True(!harmless.Console(name), $"{name} keeps the run");
        c.True(harmless.Record != null && harmless.Gate.SkipReason == null && !harmless.Gate.ConsoleUsed,
            "after every harmless command the run is still recorded");
        c.Equal((string)null, harmless.Uploads.ExcludedReason(SessionSim.Key("CON0")), "and has no exclusion marker");
        c.True(!harmless.Console("gold", succeeded: false), "a command that failed does not count");
        harmless.Floors(4);
        RunSubmitResult kept = harmless.End("loss");
        c.True(kept is { Queued: true }, "a run with only harmless or failed commands is uploaded");
        if (kept is { Queued: true })
            c.Equal(false, harmless.Queued(kept)["run"]?["integrity"]?["consoleUsed"]?.GetValue<bool>(), "with consoleUsed false");

        foreach (string name in new[] { "gold", "card", "relic", "unlock", "hbrgosakimod", "someothermodcmd", "WHATISTHIS" })
        {
            var run = new SessionSim(Root("console-" + name), clock);
            run.Uploads.Enable();
            run.Start("CON-" + name);
            run.Floors(3);
            c.True(run.Console(name), $"{name} excludes the run");
            c.True(run.Record == null && run.Gate.SkipReason == RunRecordingGate.ReasonConsole && run.Gate.ConsoleUsed,
                $"{name}: the record is dropped");
            c.Equal("console", run.Uploads.ExcludedReason(SessionSim.Key("CON-" + name)), $"{name}: a marker keeps it excluded");
            run.Floors(4);
            c.True(run.End("win") == null && run.Uploads.Queue.Count == 0, $"{name}: nothing is uploaded");
        }

        var before = new SessionSim(Root("console-restart"), clock);
        before.Uploads.Enable();
        before.Start("CON9");
        before.Floors(2);
        before.Console("heal");
        var after = new SessionSim(Root("console-restart"), clock);
        c.True(!after.Continue("CON9") && after.Gate.SkipReason == RunRecordingGate.ReasonConsole,
            "continued after a restart: still excluded");

        var modded = new SessionSim(Root("modifier"), clock, new RunIntegrity(["loadout"], false, false));
        modded.Uploads.Enable();
        c.True(!modded.Start("MOD1") && modded.Gate.SkipReason == RunRecordingGate.ReasonModifier &&
               modded.Uploads.ExcludedReason(SessionSim.Key("MOD1")) == "modifier", "a modifier mod: the run is never recorded");

        // The OD gauge no longer sends a console command (source section "OD gauge is not the
        // console"); typing that command, or any other, still counts.
        c.Section("typed console commands still count");
        c.Equal("hbrgosakimod", SessionSim.CommandName("  hbrgosakimod __activate_overclock 2 "),
            "the console patch gets the first word of the typed line");
        foreach ((string seed, string line) in new[]
                 {
                     ("TYPED-OD", "hbrgosakimod __activate_overclock 2"),
                     ("TYPED-OD-CASE", "  HBRGOSakiMod __activate_overclock 1"),
                     ("TYPED-ROLL", "hbrgosakimod watashikakamininanimashu true"),
                     ("TYPED-GOLD", "gold 999")
                 })
        {
            var run = new SessionSim(Root("typed-" + seed), clock);
            run.Uploads.Enable();
            run.Start(seed);
            run.Floors(2);
            c.True(run.Typed(line) && run.Record == null && run.Gate.ConsoleUsed &&
                   run.Gate.SkipReason == RunRecordingGate.ReasonConsole &&
                   run.Uploads.ExcludedReason(SessionSim.Key(seed)) == "console", $"typed '{line.Trim()}': the run is excluded");
            run.Floors(2);
            c.True(run.End("win") == null && run.Uploads.Queue.Count == 0, $"typed '{line.Trim()}': nothing is uploaded");
        }
        var typedFailed = new SessionSim(Root("typed-od-failed"), clock);
        typedFailed.Uploads.Enable();
        typedFailed.Start("TYPED-OD-FAILED");
        typedFailed.Floors(2);
        c.True(!typedFailed.Typed("hbrgosakimod __activate_overclock 3", succeeded: false) && typedFailed.Record != null &&
               !typedFailed.Gate.ConsoleUsed, "typed OD that could not open (the command failed) keeps the run, like any failed command");
        c.True(typedFailed.Typed("hbrgosakimod __activate_overclock 1") && typedFailed.Record == null,
            "typed again and it opens: the run is excluded");

        c.Section("turned on during a run");
        var on = new SessionSim(Root("turned-on"), clock);
        c.True(!on.Start("ON1") && on.Gate.SkipReason == RunRecordingGate.ReasonOff, "a run started while off is not recorded");
        on.Floors(2);
        on.TurnOn();
        c.True(on.Uploads.IsEnabled && on.Gate.SkipReason != null, "turned on during it: uploading is on, the run stays skipped");
        on.Floors(6);
        c.True(on.Record == null && !File.Exists(on.Uploads.InProgressPath), "nothing of it is recorded or saved");
        c.True(on.End("win") == null && on.Uploads.Queue.Count == 0, "that run is not uploaded");
        c.True(on.Start("ON2"), "the next new run is recorded");
        on.Floors(6);
        RunSubmitResult next = on.End("loss");
        c.True(next is { Queued: true } && on.Uploads.Queue.Count == 1, "and uploaded");
        if (next is { Queued: true })
        {
            JsonObject recorder = on.Queued(next)["run"]?["recorder"] as JsonObject;
            c.True(recorder?["partial"]?.GetValue<bool>() == false && recorder?["fromFloor"]?.GetValue<int>() == 1,
                "as a whole run: partial false, fromFloor 1");
        }

        var later = new SessionSim(Root("turned-on-saved"), clock);
        later.Start("ON3");
        later.Floors(2);
        later.TurnOn();
        later.Floors(1);
        var resumed = new SessionSim(Root("turned-on-saved"), clock);
        c.True(!resumed.Continue("ON3"), "turned on during a run, continued after a restart: still not recorded");
        resumed.Floors(5);
        c.True(resumed.End("win") == null && resumed.Uploads.Queue.Count == 0, "and not uploaded");

        var menu = new SessionSim(Root("turned-on-menu"), clock);
        menu.TurnOn();
        c.True(!menu.Gate.RunActive && menu.Gate.SkipReason == null, "turned on at the main menu: nothing is skipped");
        c.True(menu.Start("ON4"), "the run started next is recorded");

        var offOn = new SessionSim(Root("off-on"), clock);
        offOn.Uploads.Enable();
        offOn.Start("ON5");
        offOn.Floors(3);
        offOn.TurnOff();
        offOn.TurnOn();
        offOn.Floors(4);
        c.True(offOn.Record == null && offOn.Gate.SkipReason == RunRecordingGate.ReasonOff, "turned off and on again during a run: skipped");
        c.True(offOn.End("win") == null && offOn.Uploads.Queue.Count == 0, "and not uploaded");

        var cleared = new SessionSim(Root("cleared"), clock);
        cleared.Uploads.Enable();
        cleared.Start("ON6");
        cleared.Floors(3);
        cleared.ClearLocalData();
        cleared.Floors(4);
        c.True(cleared.Gate.SkipReason == RunRecordingGate.ReasonCleared && cleared.End("win") == null && cleared.Uploads.Queue.Count == 0,
            "local data cleared during a run: the rest is not recorded or uploaded");

        c.Section("continued without its record");
        var offRun = new SessionSim(Root("no-record"), clock);
        offRun.Start("NR1");
        offRun.Floors(3);
        // The game started again; the first-launch prompt is answered at the main menu, then the saved run continues.
        var allowed = new SessionSim(Root("no-record"), clock);
        allowed.TurnOn();
        c.True(!allowed.Continue("NR1") && allowed.Gate.SkipReason == RunRecordingGate.ReasonNoRecord,
            "a saved run without a record from its start is not recorded");
        allowed.Floors(5);
        c.True(!File.Exists(allowed.Uploads.InProgressPath), "nothing is saved for it");
        c.True(allowed.End("win") == null && allowed.Uploads.Queue.Count == 0, "it is not uploaded");
        c.True(allowed.Start("NR2"), "the next new run is recorded");
        allowed.Floors(6);
        c.True(allowed.End("loss") is { Queued: true }, "and uploaded");

        var other = new SessionSim(Root("other-record"), clock);
        other.Uploads.Enable();
        other.Start("NR3");
        other.Floors(2);
        var notMine = new SessionSim(Root("other-record"), clock);
        c.True(!notMine.Continue("NR4") && notMine.Gate.SkipReason == RunRecordingGate.ReasonNoRecord,
            "another run's saved record is not taken over");
        c.True(!File.Exists(notMine.Uploads.InProgressPath), "it is dropped");

        var saved = new SessionSim(Root("off-at-load"), clock);
        saved.Uploads.Enable();
        saved.Start("NR5");
        saved.Floors(2);
        var offLoad = new SessionSim(Root("off-at-load"), clock);
        offLoad.Uploads.Disable();
        c.True(File.Exists(offLoad.Uploads.InProgressPath), "(the record is still on disk)");
        c.True(!offLoad.Continue("NR5") && !File.Exists(offLoad.Uploads.InProgressPath),
            "continued while off: the saved record is deleted");
        offLoad.Floors(2);
        var onAgain = new SessionSim(Root("off-at-load"), clock);
        onAgain.Uploads.Enable();
        c.True(!onAgain.Continue("NR5") && onAgain.Gate.SkipReason == RunRecordingGate.ReasonNoRecord,
            "turned on again later: that run is still not recorded");

        c.Section("continued with its record");
        var first = new SessionSim(Root("with-record"), clock);
        first.Uploads.Enable();
        c.True(first.Start("WR1"), "recorded from its start");
        first.Floors(4);
        var second = new SessionSim(Root("with-record"), clock);
        c.True(second.Continue("WR1"), "continued after a restart with its own record");
        c.Equal(2, second.Record?.Run["recorder"]?["sessions"]?.GetValue<int>(), "sessions counts the continued session");
        second.Floors(3);
        RunSubmitResult whole = second.End("loss");
        c.True(whole is { Queued: true }, "uploaded");
        if (whole is { Queued: true })
        {
            JsonObject envelope = second.Queued(whole);
            c.Empty(RunValidator.Validate(envelope, schemaKeys), "follows the schema rules");
            JsonObject recorder = envelope["run"]?["recorder"] as JsonObject;
            c.True(recorder?["partial"]?.GetValue<bool>() == false && recorder?["fromFloor"]?.GetValue<int>() == 1,
                "a whole run: partial false, fromFloor 1");
            c.Equal(7, envelope["run"]?["floorReached"]?.GetValue<int>(), "floors 1 to 7, both sessions");
        }

        var plain = new SessionSim(Root("with-record-mod"), clock);
        plain.Uploads.Enable();
        plain.Start("WR2");
        plain.Floors(3);
        var withMod = new SessionSim(Root("with-record-mod"), clock, new RunIntegrity(Array.Empty<string>(), false, true));
        c.True(withMod.Continue("WR2"), "continued with another gameplay mod on");
        withMod.Floors(3);
        RunSubmitResult flagged = withMod.End("win");
        c.True(flagged is { Queued: true } && withMod.Queued(flagged)["run"]?["integrity"]?["otherGameplayMods"]?.GetValue<bool>() == true,
            "uploaded with otherGameplayMods");

        var abandon = new SessionSim(Root("abandoned"), clock);
        abandon.Uploads.Enable();
        abandon.Start("WR3");
        abandon.Floors(7);
        var menuSession = new SessionSim(Root("abandoned"), clock);
        RunSubmitResult abandoned = menuSession.End("abandoned", abandonedSeed: "WR3");
        c.True(abandoned is { Queued: true }, "a recorded run abandoned from the main menu is uploaded from its saved record");
        var skippedAbandon = new SessionSim(Root("abandoned-skipped"), clock);
        skippedAbandon.Start("WR4");
        skippedAbandon.Floors(7);
        skippedAbandon.TurnOn();
        c.True(skippedAbandon.End("abandoned", abandonedSeed: "WR4") == null && skippedAbandon.Uploads.Queue.Count == 0,
            "a skipped run abandoned from the main menu is not");

        c.Section("battle interrupted, then continued");
        var fight = new SessionSim(Root("interrupted"), clock);
        fight.Uploads.Enable();
        fight.Start("BI1");
        fight.Floors(5);
        JsonObject cut = fight.Battle(finish: false);
        c.Equal(1, cut?["combat"]?["attempt"]?.GetValue<int>(), "the first attempt");
        c.True(cut?["combat"]?["result"] == null, "the game is closed during the battle");
        var back = new SessionSim(Root("interrupted"), clock);
        c.True(back.Continue("BI1"), "continued with its record");
        JsonObject replayed = back.Battle(finish: true);
        c.Equal(2, replayed?["combat"]?["attempt"]?.GetValue<int>(), "the battle played again is attempt 2");
        JsonObject earlier = (replayed?["earlierAttempts"] as JsonArray)?.FirstOrDefault() as JsonObject;
        c.Equal("interrupted", earlier?["result"]?["outcome"]?.GetValue<string>(), "the cut-short battle is marked interrupted");
        c.Equal(1, earlier?["result"]?["rounds"]?.GetValue<int>(), "with the rounds it had");
        back.Floors(2);
        RunSubmitResult interrupted = back.End("win");
        c.True(interrupted is { Queued: true }, "a run with an interrupted battle is uploaded");
        if (interrupted is { Queued: true })
        {
            JsonObject envelope = back.Queued(interrupted);
            c.Empty(RunValidator.Validate(envelope, schemaKeys), "follows the schema rules");
            JsonObject recorder = envelope["run"]?["recorder"] as JsonObject;
            c.True(recorder?["partial"]?.GetValue<bool>() == false && recorder?["sessions"]?.GetValue<int>() == 2,
                "as a whole run in two sessions");
        }
    }
}

// The OD gauge's request (Core/Integration/HbrOverclockActivation.cs, linked): its payload and
// whose Overclock every peer opens. In co-op the party shares OD, so whoever presses opens the
// one the gauge shows (user, 2026-11-09: 「联机的od是共用的」「谁按都能开」).
static class OdRequestChecks
{
    private sealed record FakePlayer(ulong NetId, int Side, bool HoldsOverclock);

    private static FakePlayer Target(IEnumerable<FakePlayer> players, FakePlayer sender, ulong ownerNetId) =>
        HbrOverclockActivation.Target(players, sender, ownerNetId, static player => player.NetId,
            static (player, other) => player.Side == other.Side, static player => player.HoldsOverclock);

    public static void Run(Checks c)
    {
        c.Section("OD request: payload");
        foreach (ulong owner in new ulong[] { 1, 76561198012345678, ulong.MaxValue })
            for (int level = 1; level <= 3; level++)
            {
                byte[] bytes = HbrOverclockActivation.Serialize(new HbrOverclockActivation { Level = level, OwnerNetId = owner });
                HbrOverclockActivation back = HbrOverclockActivation.Deserialize(bytes);
                c.True(back.Level == level && back.OwnerNetId == owner,
                    $"level {level}, player {owner}: the round trip keeps both ({Encoding.UTF8.GetString(bytes)})");
            }
        var json = (JsonObject)JsonNode.Parse(HbrOverclockActivation.Serialize(new HbrOverclockActivation { Level = 2, OwnerNetId = 7 }));
        c.True(json.Count == 2 && json["Level"]?.GetValue<int>() == 2 && json["OwnerNetId"]?.GetValue<ulong>() == 7,
            "the payload holds exactly Level and OwnerNetId");
        c.True(Throws<ArgumentException>("null"), "a null payload is rejected");
        c.True(Throws<JsonException>("{oops"), "a malformed payload is rejected");
        c.True(Throws<JsonException>("{\"Level\":2,\"OwnerNetId\":-1}"), "a negative player id is rejected");
        c.Equal(0ul, HbrOverclockActivation.Deserialize("{\"Level\":2}"u8).OwnerNetId, "a payload without a player names id 0");

        c.Section("OD request: whose OD opens");
        var holder = new FakePlayer(11, 0, true);
        var teammate = new FakePlayer(22, 0, false);
        var secondHolder = new FakePlayer(33, 0, true);
        var otherSide = new FakePlayer(44, 1, true);
        FakePlayer[] party = [holder, teammate, secondHolder, otherSide];
        c.True(ReferenceEquals(Target(party, holder, 11), holder), "the OD holder presses: their own OD");
        c.True(ReferenceEquals(Target(party, teammate, 11), holder), "a teammate without OD presses: the OD the gauge shows");
        c.True(ReferenceEquals(Target(party, secondHolder, 11), holder),
            "both hold OD and the second presses: the first one's, which the gauge shows");
        var solo = new FakePlayer(1, 0, true);
        c.True(ReferenceEquals(Target([solo], solo, 1), solo), "single player: their own OD");
        c.True(Target(party, teammate, 99) == null, "the named player is not in the run: nobody's OD opens");
        c.True(Target(party, teammate, 44) == null, "the named player is on the other side: nobody's OD opens");
        c.True(Target(party, holder, 22) == null, "the named player holds no OD: nobody's OD opens");
        c.True(Target(party, null, 11) == null, "no sender: nobody's OD opens");
        c.True(Target(party, teammate, 0) == null, "a payload without a player: nobody's OD opens");
    }

    private static bool Throws<T>(string payload) where T : Exception
    {
        try
        {
            HbrOverclockActivation.Deserialize(Encoding.UTF8.GetBytes(payload));
            return false;
        }
        catch (T)
        {
            return true;
        }
    }
}

// The game-side files cannot be compiled here; these read them as text.
static class SourceChecks
{
    public static void Run(Checks c)
    {
        string repo = AppContext.BaseDirectory;
        while (repo != null && !File.Exists(Path.Combine(repo, "HBRGOSakiMod.csproj"))) repo = Path.GetDirectoryName(repo);
        c.Section("recorder wiring");
        if (repo == null)
        {
            c.True(false, "the mod's folder is found above the checks");
            return;
        }
        string Read(string path) => File.ReadAllText(Path.Combine(repo, path));
        string recorder = Read("Core/RunUpload/RunRecorder.cs");
        string patches = Read("Core/RunUpload/RunRecorderPatches.cs");
        string service = Read("Core/RunUpload/RunUploadService.cs");
        c.Equal(1, Regex.Matches(recorder, Regex.Escape("new RunRecordBuilder(")).Count, "a record is started only when a run starts");
        c.True(!recorder.Contains("Partial =") && !recorder.Contains("FromFloor ="), "the recorder never writes a partial header");
        foreach (string call in new[] { "Gate.RunStarted(", "Gate.RunLoaded(", "Gate.ConsoleCommand(", "Gate.SavedRecordOfEndedRun(",
                     "Gate.StopCurrentRun(", "Gate.RunEnded()", "RunRecordBuilder.BeginCombatAttempt(" })
            c.True(recorder.Contains(call), $"the recorder calls {call}");
        c.True(patches.Contains("Postfix(string cmdName, ") && patches.Contains("OnConsoleCommand(cmdName, __result.success)"),
            "the console patch passes the command name and the result");
        foreach (string reason in new[] { "ReasonTurnedOn", "ReasonOff", "ReasonCleared" })
            c.True(service.Contains($"RunRecorder.SkipCurrentRun(RunRecordingGate.{reason})"), $"the service skips the run in progress: {reason}");

        // The gauge used to send "hbrgosakimod __activate_overclock <level>" as a console action,
        // so every run with OD opened from it was excluded (user, 2026-11-09: 「bug修一下」).
        c.Section("OD gauge is not the console (read as text)");
        string ReadCode(string path) => File.Exists(Path.Combine(repo, path)) ? CSharpText.Code(Read(path)) : string.Empty;
        string gauge = ReadCode("Core/Nodes/Combat/NOverclockGauge.cs");
        string odActions = ReadCode("Core/Integration/HbrOverclockNetActions.cs");
        string initializer = ReadCode("HBRGOSakiModInitializer.cs");
        string modCommand = ReadCode("Core/DevConsole/FixedSoulRollConsoleCmd.cs");
        string relic = ReadCode("Core/Models/Relics/Overclock.cs");
        c.True(gauge.Contains("HbrOverclockNetActions.Request(_relic, level);"),
            "the gauge asks for OD through HbrOverclockNetActions, for the relic it shows");
        c.True(!gauge.Contains("ConsoleCmdGameAction") && !gauge.Contains("ActivateOverclockSubcommand") &&
               !gauge.Contains("\"hbrgosakimod"), "and builds no console command");
        c.True(odActions.Contains("var request = new HbrOverclockActivation { Level = level, OwnerNetId = relic.Owner.NetId };") &&
               odActions.Contains("RitsuLibManagedNetActions.Request(RunManager.Instance, descriptor, request)"),
            "the request is the local player's managed action and names the player whose OD the gauge shows");
        c.True(odActions.Contains("Descriptor(\"hbr_overclock_activate_combat\", GameActionType.CombatPlayPhaseOnly)") &&
               odActions.Contains("Descriptor(\"hbr_overclock_activate_any\", GameActionType.Any)") &&
               odActions.Contains("CombatManager.Instance.IsInProgress ? CombatDescriptor : AnyDescriptor"),
            "queued as the console action was: play phase only when asked in combat, any time outside");
        c.True(odActions.Contains("ModuleId, key, HbrOverclockActivation.Serialize, HbrOverclockActivation.Deserialize, Execute, type)"),
            "both keys carry HbrOverclockActivation with its own Serialize and Deserialize (tested in \"OD request\")");
        c.True(odActions.Contains("IEnumerable<Player> players = sender?.RunState?.Players ?? [];") &&
               Regex.IsMatch(odActions, @"HbrOverclockActivation\.Target\(players, sender, context\.Message\.OwnerNetId,\s*" +
                   @"static player => player\.NetId,\s*static \(player, other\) => player\.Creature\?\.Side == other\.Creature\?\.Side,\s*" +
                   @"static player => player\.GetRelic<Overclock>\(\) != null\);") &&
               odActions.Contains("owner?.GetRelic<Overclock>().TryActivate(context.Message.Level);") &&
               !odActions.Contains("context.Player.GetRelic<Overclock>()"),
            "every peer opens the OD of the player the request names, chosen by HbrOverclockActivation.Target, and TryActivate rechecks it");
        c.True(relic.Contains("(Owner?.RunState is { } run && LocalContext.GetMe(run.Players) is { } me &&") &&
               relic.Contains("CombatManager.Instance.IsPlayerReadyToEndTurn(me));") &&
               !relic.Contains("IsPlayerReadyToEndTurn(Owner)"),
            "pressing after ending the turn depends on the local player's ready vote, not the relic owner's");
        c.True(!Regex.IsMatch(odActions, "DevConsole|ConsoleCmd|ProcessNetCommand|ProcessCommand"), "the action never goes through the console");
        c.True(odActions.Contains("RitsuLibManagedNetActions.Register(CombatDescriptor)") &&
               odActions.Contains("RitsuLibManagedNetActions.Register(AnyDescriptor)") &&
               initializer.Contains("HbrOverclockNetActions.Initialize();"),
            "both actions are registered at startup, so every peer can run a teammate's request");
        // Nothing in the mod sends a console command itself, so whatever reaches the console
        // patch was typed by a player.
        List<string> sources = CSharpText.ProductionSources(repo);
        c.True(sources.Count > 1000 && sources.Contains(Path.Combine(repo, "Core", "Nodes", "Combat", "NOverclockGauge.cs")),
            $"the scan reads the mod's sources ({sources.Count} files, the gauge among them)");
        List<string> senders = sources
            .Where(path => Regex.IsMatch(CSharpText.Code(File.ReadAllText(path)), @"ConsoleCmdGameAction|\bProcessNetCommand\s*\(|\.\s*ProcessCommand\s*\("))
            .Select(path => Path.GetRelativePath(repo, path)).ToList();
        c.Empty(senders, "no mod code sends a console command (ConsoleCmdGameAction, ProcessNetCommand, ProcessCommand)");
        c.True(modCommand.Contains("public override string CmdName => \"hbrgosakimod\";") &&
               modCommand.Contains("args[0].Equals(ActivateOverclockSubcommand, StringComparison.OrdinalIgnoreCase)") &&
               modCommand.Contains("return ActivateOverclock(player, requestedLevel);"),
            "typed by hand, hbrgosakimod __activate_overclock still opens OD as the console command hbrgosakimod");

        c.Section("explanation texts");
        string text = Read("Core/RunUpload/RunUploadText.cs");
        const string cloudflare = "网站托管在 Cloudflare 上。和访问任何网站一样，Cloudflare 在网络层面传输数据时会短暂处理连接的 IP 地址，这一层不归 hbrgo.com 控制。";
        c.True(text.Contains(cloudflare), "the Chinese explanation has the Cloudflare sentence");
        c.True(text.IndexOf(cloudflare, StringComparison.Ordinal) > text.IndexOf("IP 不会被保存", StringComparison.Ordinal),
            "right after the IP line");
        c.True(text.Contains("The site is hosted on Cloudflare."), "so does the English one");
        c.True(text.Contains("用控制台改动过对局（例如加卡、加遗物、改金币和血量）的对局") && !text.Contains("用过开发者控制台"),
            "console: only commands that change the run");
        c.True(text.Contains("runs changed through the console") && !text.Contains("with developer console commands"), "in English too");
        c.True(text.Contains("打开时如果有一局正在进行（包括存档里还没打完的局），这一局不上传"), "a run going when it is turned on is not uploaded");
        c.True(text.Contains("if a run is in progress when you turn this on (including a saved run not yet finished), that run is not uploaded"),
            "in English too");
        foreach ((string file, string[] phrases, string old) in new[]
                 {
                     ("HBRGOSakiMod/localization/zhs/settings_ui.json",
                         new[] { "打开时如果有一局正在进行（包括存档里还没打完的局），这一局不上传，从下一局开始记录", "用控制台改动过对局（例如加卡、加遗物、改金币和血量）" },
                         "用过控制台"),
                     ("HBRGOSakiMod/localization/eng/settings_ui.json",
                         new[] { "that run is not uploaded; recording starts with the next run", "runs changed through the console" },
                         "or console commands")
                 })
        {
            JsonObject settings = RunUploadJson.ParseObject(File.ReadAllBytes(Path.Combine(repo, file)));
            string hover = settings?["HBRGOSAKIMOD-UPLOAD_RUN_DATA.hover.desc"]?.GetValue<string>() ?? string.Empty;
            c.True(phrases.All(hover.Contains) && !hover.Contains(old), $"{file}: the switch's hover says the same");
            string all = settings?.ToJsonString(RunUploadJson.Options) ?? string.Empty;
            c.True(!all.Contains("安装 ID") && !all.Contains("install ID"), $"{file}: it is called the anonymous ID everywhere");
        }
    }
}

// Reading the mod's C# as text for SourceChecks.
static class CSharpText
{
    private static readonly string[] SkippedFolders = ["validation", "bin", "obj", "output", "outputs", "artwork-work"];

    // The .cs files the mod build and its HextechCompat project compile (the csproj removes
    // validation, output, outputs and artwork-work; the SDK skips bin, obj and dot folders).
    public static List<string> ProductionSources(string repo)
    {
        var files = new List<string>();
        void Walk(string folder)
        {
            files.AddRange(Directory.EnumerateFiles(folder, "*.cs"));
            foreach (string sub in Directory.EnumerateDirectories(folder))
            {
                string name = Path.GetFileName(sub);
                if (!name.StartsWith('.') && !SkippedFolders.Contains(name, StringComparer.OrdinalIgnoreCase)) Walk(sub);
            }
        }
        Walk(repo);
        return files;
    }

    // The source without // and /* */ comments. String and char literals (regular, verbatim
    // and raw) are kept, so a "//" inside one is not taken for a comment.
    public static string Code(string source)
    {
        var code = new StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            char ch = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (ch == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
            }
            else if (ch == '/' && next == '*')
            {
                int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? source.Length : end + 2;
                code.Append(' ');
            }
            else if (ch is '"' or '\'')
            {
                int start = i;
                i = LiteralEnd(source, i);
                code.Append(source, start, i - start);
            }
            else
            {
                code.Append(ch);
                i++;
            }
        }
        return code.ToString();
    }

    // The index just after the literal whose opening quote is at start.
    private static int LiteralEnd(string s, int start)
    {
        if (s[start] == '"')
        {
            bool verbatim = start > 0 && (s[start - 1] == '@' || (s[start - 1] == '$' && start > 1 && s[start - 2] == '@'));
            int quotes = 0;
            while (start + quotes < s.Length && s[start + quotes] == '"') quotes++;
            if (quotes >= 3 && !verbatim)
            {
                int close = s.IndexOf(new string('"', quotes), start + quotes, StringComparison.Ordinal);
                return close < 0 ? s.Length : close + quotes;
            }
            int i = start + 1;
            while (i < s.Length)
            {
                if (verbatim && s[i] == '"' && i + 1 < s.Length && s[i + 1] == '"') i += 2;
                else if (!verbatim && s[i] == '\\') i += 2;
                else if (s[i] == '"') return i + 1;
                else if (!verbatim && s[i] == '\n') return i;
                else i++;
            }
            return s.Length;
        }
        int j = start + 1;
        while (j < s.Length && s[j] != '\'' && s[j] != '\n') j += s[j] == '\\' ? 2 : 1;
        return Math.Min(j + 1, s.Length);
    }
}

static class ExampleBuild
{
    private const string Strike = "CARD.STRIKE_HBRGO_SAKI";
    private const string Defend = "CARD.DEFEND_HBRGO_SAKI";
    private const string Sword = "CARD.MAGIC_SWORD_LUCKY_SEVEN_HBRGO_SAKI";
    private const string Swallow = "CARD.SWALLOW_REVERSAL_HBRGO_SAKI";
    private const string Gym = "EVENT.HBRGO_SAKI_MOD_EVENT_GYM_HBRGO_SAKI";
    private const string Saki = "CHARACTER.HBRGO_SAKI_31O";
    private const string Destruction = "POWER.DESTRUCTION_RATE_POWER";

    public static JsonObject Envelope()
    {
        var builder = new RunRecordBuilder(new RunRecordHeader
        {
            Character = Saki,
            Ascension = 10,
            Seed = "K3R7Q2M9X1AB",
            GameMode = "standard",
            Modifiers = Array.Empty<string>(),
            StartedAt = DateTimeOffset.Parse("2026-11-08T12:31:40Z", CultureInfo.InvariantCulture),
            Locale = "zhs",
            Multiplayer = null,
            Partial = false,
            FromFloor = 1,
            Rules = new JsonObject { ["rollTiers"] = new JsonObject { ["first"] = 50, ["second"] = 76, ["third"] = 91, ["max"] = 100 } }
        });

        (int, int, string, (int, int)[])[] nodes =
        [
            (3, 0, "ancient", [(1, 1), (3, 1), (5, 1)]), (1, 1, "monster", [(2, 2)]), (3, 1, "monster", [(2, 2), (4, 2)]),
            (5, 1, "monster", [(4, 2)]), (2, 2, "unknown", [(2, 3), (3, 3)]), (4, 2, "monster", [(3, 3)]),
            (2, 3, "shop", [(2, 4)]), (3, 3, "unknown", [(4, 4)]), (2, 4, "rest_site", [(2, 5)]), (4, 4, "treasure", [(4, 5)]),
            (2, 5, "monster", [(3, 6)]), (4, 5, "elite", [(3, 6)]), (3, 6, "rest_site", [(3, 7)]), (3, 7, "boss", [])
        ];
        builder.EnsureAct(0, "ACT.OVERGROWTH", RunRecordBuilder.Map(7, 7, (3, 0), (3, 7), null,
            nodes.Select(node => new RunMapNode(node.Item1, node.Item2, node.Item3,
                node.Item4.Select(next => (next.Item1, next.Item2)).ToList())).ToList()));

        // Floor 1: the ancient.
        builder.BeginFloor((3, 0), "ancient");
        builder.AddRoom("event", "EVENT.NEOW", null);
        builder.SetFloorSection("choices", new JsonObject
        {
            ["ancientChoices"] = new JsonArray(new JsonObject
            {
                ["offered"] = new JsonArray(RunRecordBuilder.Option("ARCANE_SCROLL", null), RunRecordBuilder.Option("NUTRITIOUS_OYSTER", null),
                    RunRecordBuilder.Option("SMALL_CAPSULE", null)),
                ["picked"] = RunRecordBuilder.Option("ARCANE_SCROLL", null)
            }),
            ["relicsGained"] = new JsonArray(new JsonObject { ["id"] = "RELIC.ARCANE_SCROLL", ["source"] = "ancient" })
        });
        builder.SetFloorSection("stats", RunRecordBuilder.Stats(3500, 3500, 7000, 7000, 99, 0, 0, 0, 0, 0, 0, 0, 0));

        // Floor 2: a two-round battle.
        builder.BeginFloor((3, 1), "monster");
        JsonObject room = builder.AddRoom("monster", "ENCOUNTER.CREST_HOPPER_WEAK", ["MONSTER.CREST_HOPPER"]);
        room["turns"] = 2;
        var combat = new CombatRecordBuilder(1);
        object me = new(), hopper = new();
        combat.Player(me, Saki);
        combat.Enemy(hopper, "MONSTER.CREST_HOPPER", 156m, null);
        combat.SetHbr(Hbr(1, 110, 110, 110, 110, 110, 110, RunRecordBuilder.Senses(5, 5, 5, 5, 5, 5), new JsonArray(), Bonus(0, 0), null));
        room["combat"] = combat.Combat;
        object[] cards = Enumerable.Range(0, 10).Select(_ => new object()).ToArray();
        string[] ids = [Strike, Defend, Strike, Sword, Defend, Strike, Strike, Defend, Strike, Defend];

        combat.CloseRound();
        combat.TurnStart("player");
        combat.EnsureRound(1, () => new JsonObject
        {
            ["p"] = RunRecordBuilder.CreatureState(3500, 3500, 0, 7000, 7000, 3, null, 0, RunRecordBuilder.Piles(0, 10, 0, 0), new JsonArray()),
            ["e0"] = RunRecordBuilder.CreatureState(420, 420, 0, 783, 783, powers: new JsonArray(RunRecordBuilder.Power(Destruction, 100)),
                intents: new JsonArray(RunRecordBuilder.Intent("attack", "ATTACK_MOVE", 420, 1, null)))
        });
        for (int i = 0; i < 5; i++) combat.Draw("p", combat.Card(cards[i], ids[i], 0));
        PlayAndRoll(combat, cards[0], Strike, "e0", "打击", 63, "first");
        combat.Damage("p", "e0", CombatRecordBuilder.Source("cardUid", 1), 207, 0, 0, 207, 0, false);
        combat.EndPlay(1);
        PlayAndRoll(combat, cards[3], Sword, "e0", "魔剑幸运七", 92, "third");
        combat.Damage("p", "e0", CombatRecordBuilder.Source("cardUid", 4), 360, 0, 0, 360, 0, false);
        combat.EndPlay(4);
        PlayAndRoll(combat, cards[1], Defend, null, "防御", 31, "none");
        combat.Block("p", CombatRecordBuilder.Source("cardUid", 2), 500);
        combat.EndPlay(2);
        combat.TurnEnd("player");
        combat.TurnStart("enemy");
        combat.Move("e0", "ATTACK_MOVE");
        combat.Damage("e0", "p", CombatRecordBuilder.Source("move", "ATTACK_MOVE"), 420, 420, 0, 0, 0, false);
        combat.TurnEnd("enemy");
        combat.EndRound(new JsonObject
        {
            ["p"] = RunRecordBuilder.EndState(3500, 80, 7000),
            ["e0"] = RunRecordBuilder.EndState(420, 0, 216)
        });

        combat.CloseRound();
        combat.TurnStart("player");
        combat.EnsureRound(2, () => new JsonObject
        {
            ["p"] = RunRecordBuilder.CreatureState(3500, 3500, 0, 7000, 7000, 3, null, 24, RunRecordBuilder.Piles(0, 5, 5, 0), new JsonArray()),
            ["e0"] = RunRecordBuilder.CreatureState(420, 420, 0, 216, 783, powers: new JsonArray(RunRecordBuilder.Power(Destruction, 100)),
                intents: new JsonArray(RunRecordBuilder.Intent("attack", "COMBO_MOVE", 60, 8, null)))
        });
        for (int i = 5; i < 10; i++) combat.Draw("p", combat.Card(cards[i], ids[i], 0));
        PlayAndRoll(combat, cards[5], Strike, "e0", "打击", 88, "second");
        combat.Damage("p", "e0", CombatRecordBuilder.Source("cardUid", 6), 216, 0, 0, 216, 0, false);
        combat.Power("e0", "p", CombatRecordBuilder.Source("cardUid", 6), Destruction, 30, 130);
        combat.EndPlay(6);
        PlayAndRoll(combat, cards[6], Strike, "e0", "打击", 100, "critical");
        combat.Damage("p", "e0", CombatRecordBuilder.Source("cardUid", 7), 351, 0, 351, 0, 0, false);
        combat.EndPlay(7);
        PlayAndRoll(combat, cards[8], Strike, "e0", "打击", 55, "first");
        combat.Damage("p", "e0", CombatRecordBuilder.Source("cardUid", 9), 269, 0, 69, 0, 200, true);
        combat.EndPlay(9);
        combat.Death("e0");
        combat.EndRound(new JsonObject
        {
            ["p"] = RunRecordBuilder.EndState(3500, 0, 7000),
            ["e0"] = RunRecordBuilder.EndState(0, 0, 0)
        });
        combat.Finish("win", Player());

        builder.SetFloorSection("choices", new JsonObject
        {
            ["cardChoices"] = new JsonArray(new JsonObject
            {
                ["source"] = "reward",
                ["offered"] = new JsonArray(RunRecordBuilder.CardRef("CARD.STRENGTHEN_HBRGO_SAKI", 0, null, "common"),
                    RunRecordBuilder.CardRef(Swallow, 0, null, "common"),
                    RunRecordBuilder.CardRef("CARD.PER_TURN_CHARGE_HBRGO_SAKI", 0, null, "uncommon")),
                ["picked"] = new JsonArray(RunRecordBuilder.CardRef(Swallow, 0, null, "common")),
                ["skipped"] = false
            }),
            ["potionChoices"] = new JsonArray(new JsonObject { ["offered"] = new JsonArray("POTION.WEAK_POTION"), ["picked"] = "POTION.WEAK_POTION" }),
            ["cardsGained"] = new JsonArray(RunRecordBuilder.CardRef(Swallow, 0)),
            ["potionsGained"] = new JsonArray("POTION.WEAK_POTION")
        });
        builder.SetFloorSection("hbr", new JsonObject
        {
            ["levelBefore"] = 1,
            ["levelAfter"] = 2,
            ["senseGained"] = 1,
            ["senseAllocations"] = new JsonArray(new JsonObject { ["madness"] = 1 })
        });
        builder.SetFloorSection("stats", RunRecordBuilder.Stats(3500, 3500, 7000, 7000, 116, 17, 0, 0, 0, 0, 0, 0, 0));

        // Floor 3: the gym event, a skipped card choice.
        builder.BeginFloor((2, 2), "unknown");
        builder.AddRoom("event", Gym, null);
        const string initial = "HBRGO_SAKI_MOD_EVENT_GYM_HBRGO_SAKI.pages.INITIAL.options.";
        const string taki = "HBRGO_SAKI_MOD_EVENT_GYM_HBRGO_SAKI.pages.TAKI.options.CHOICE.title";
        const string takiText = "获得8点领袖魅力，从3张卡牌中选择1张获得";
        builder.SetFloorSection("choices", new JsonObject
        {
            ["eventChoices"] = new JsonArray(
                new JsonObject
                {
                    ["event"] = Gym,
                    ["page"] = "INITIAL",
                    ["offered"] = new JsonArray(RunRecordBuilder.Option(initial + "CHOICE.title", "椎名立希"),
                        RunRecordBuilder.Option(initial + "CHOICE_KAREN.title", "朝仓可怜")),
                    ["picked"] = RunRecordBuilder.Option(initial + "CHOICE.title", "椎名立希")
                },
                new JsonObject
                {
                    ["event"] = Gym,
                    ["page"] = "TAKI",
                    ["offered"] = new JsonArray(RunRecordBuilder.Option(taki, takiText)),
                    ["picked"] = RunRecordBuilder.Option(taki, takiText)
                }),
            ["cardChoices"] = new JsonArray(new JsonObject
            {
                ["source"] = "event",
                ["offered"] = new JsonArray(RunRecordBuilder.CardRef("CARD.SLIDE_STEP_HBRGO_SAKI", 0, null, "common"),
                    RunRecordBuilder.CardRef("CARD.DRINK_TEA_HBRGO_SAKI", 0, null, "common"),
                    RunRecordBuilder.CardRef("CARD.QUICK_SWITCH_HBRGO_SAKI", 0, null, "common")),
                ["picked"] = new JsonArray(),
                ["skipped"] = true
            })
        });
        builder.SetFloorSection("hbr", new JsonObject
        {
            ["senseChanges"] = new JsonArray(new JsonObject { ["attribute"] = "leadershipCharm", ["delta"] = 8, ["source"] = Gym })
        });
        builder.SetFloorSection("stats", RunRecordBuilder.Stats(3500, 3500, 7000, 7000, 116, 0, 0, 0, 0, 0, 0, 0, 0));

        // Floor 4: the shop.
        builder.BeginFloor((2, 3), "shop");
        builder.AddRoom("shop", null, null);
        builder.SetFloorSection("choices", new JsonObject
        {
            ["shop"] = new JsonObject
            {
                ["offered"] = new JsonObject
                {
                    ["cards"] = new JsonArray(
                        new JsonObject { ["card"] = RunRecordBuilder.CardRef("CARD.SLIDE_STEP_HBRGO_SAKI", 0), ["price"] = 52 },
                        new JsonObject { ["card"] = RunRecordBuilder.CardRef("CARD.STRENGTHEN_HBRGO_SAKI", 0), ["price"] = 49 },
                        new JsonObject { ["card"] = RunRecordBuilder.CardRef("CARD.QUICK_SWITCH_HBRGO_SAKI", 0), ["price"] = 38, ["onSale"] = true }),
                    ["relics"] = new JsonArray(new JsonObject { ["id"] = "RELIC.MINIATURE_TENT", ["price"] = 162 }),
                    ["potions"] = new JsonArray(new JsonObject { ["id"] = "POTION.DP_POTION_HBRGO_SAKI", ["price"] = 53 }),
                    ["removalPrice"] = 75
                },
                ["bought"] = new JsonArray(new JsonObject { ["kind"] = "removal", ["card"] = RunRecordBuilder.CardRef(Strike, 0), ["price"] = 75 })
            },
            ["cardsRemoved"] = new JsonArray(RunRecordBuilder.CardRef(Strike, 0))
        });
        builder.SetFloorSection("hbr", new JsonObject
        {
            ["equipmentOffers"] = new JsonArray(
                new JsonObject { ["item"] = RunRecordBuilder.EquipmentItem(null, "SBooster_0101", "booster", 1, "第一阶段", [], []), ["price"] = 25 },
                new JsonObject { ["item"] = RunRecordBuilder.EquipmentItem(null, "SBooster_0102", "booster", 2, "高墙", [], []), ["price"] = 40 }),
            ["equipment"] = new JsonArray(
                new JsonObject { ["action"] = "buy", ["item"] = Booster(), ["price"] = 25 },
                new JsonObject { ["action"] = "equip", ["itemId"] = 1, ["slot"] = "booster", ["index"] = 0 })
        });
        builder.SetFloorSection("stats", RunRecordBuilder.Stats(3500, 3500, 7000, 7000, 16, 0, 100, 0, 0, 0, 0, 0, 0));

        // Floor 5: the rest site.
        builder.BeginFloor((2, 4), "rest_site");
        builder.AddRoom("rest_site", null, null);
        builder.SetFloorSection("choices", new JsonObject
        {
            ["rest"] = new JsonArray(new JsonObject { ["choice"] = "SMITH", ["card"] = RunRecordBuilder.CardRef(Sword, 1) }),
            ["cardsUpgraded"] = new JsonArray(RunRecordBuilder.CardRef(Sword, 1))
        });
        builder.SetFloorSection("stats", RunRecordBuilder.Stats(3500, 3500, 7000, 7000, 16, 0, 0, 0, 0, 0, 0, 0, 0));

        // Floor 6: abandoned during the first round.
        builder.BeginFloor((2, 5), "monster");
        room = builder.AddRoom("monster", "ENCOUNTER.SMALL_HOPPERS_WEAK", ["MONSTER.SMALL_HOPPER", "MONSTER.SMALL_HOPPER"]);
        room["turns"] = 1;
        combat = new CombatRecordBuilder(1);
        combat.Player(me, Saki);
        combat.Enemy(new object(), "MONSTER.SMALL_HOPPER", 180m, null);
        combat.Enemy(new object(), "MONSTER.SMALL_HOPPER", 180m, null);
        JsonObject equipped = new() { ["slot"] = "booster", ["index"] = 0, ["item"] = Booster() };
        combat.SetHbr(Hbr(2, 130, 144, 128, 128, 128, 128, RunRecordBuilder.Senses(6, 13, 5, 5, 5, 5), new JsonArray(equipped), Bonus(14, 0), null));
        room["combat"] = combat.Combat;
        cards = Enumerable.Range(0, 5).Select(_ => new object()).ToArray();
        combat.CloseRound();
        combat.TurnStart("player");
        JsonObject hopperState(int intentDamage, string move, int hits) => RunRecordBuilder.CreatureState(380, 380, 0, 500, 500,
            powers: new JsonArray(RunRecordBuilder.Power(Destruction, 100)),
            intents: new JsonArray(RunRecordBuilder.Intent("attack", move, intentDamage, hits, null)));
        combat.EnsureRound(1, () => new JsonObject
        {
            ["p"] = RunRecordBuilder.CreatureState(3500, 3500, 0, 7000, 7000, 3, null, 0, RunRecordBuilder.Piles(0, 10, 0, 0), new JsonArray()),
            ["e0"] = hopperState(300, "ATTACK_MOVE", 1),
            ["e1"] = hopperState(50, "COMBO_MOVE", 4)
        });
        string[] hand = [Swallow, Strike, Sword, Defend, Strike];
        for (int i = 0; i < 5; i++) combat.Draw("p", combat.Card(cards[i], hand[i], hand[i] == Sword ? 1 : 0));
        combat.Potion("p", "POTION.WEAK_POTION", "e1");
        combat.Power("e1", "p", CombatRecordBuilder.Source("potion", "POTION.WEAK_POTION"), "POWER.WEAK_POWER", 1, 1);
        combat.Play("p", combat.Card(cards[2], Sword, 1), "e0", 1, 0, false, 0);
        combat.Roll("p", 3, "魔剑幸运七", 77, 77, "基础骰", new JsonArray(), "second", false, 100, 777, new JsonArray());
        combat.Damage("p", "e0", CombatRecordBuilder.Source("cardUid", 3), 420, 0, 0, 420, 0, false);
        combat.Finish("abandoned", Player());
        builder.SetFloorSection("choices", new JsonObject { ["potionsUsed"] = new JsonArray("POTION.WEAK_POTION") });
        builder.SetFloorSection("stats", RunRecordBuilder.Stats(3500, 3500, 7000, 7000, 16, 0, 0, 0, 0, 0, 0, 0, 0));

        var deck = new JsonArray();
        foreach ((string id, int upgrade, int floor) in new[]
                 {
                     (Strike, 0, 1), (Strike, 0, 1), (Strike, 0, 1), (Strike, 0, 1), (Defend, 0, 1), (Defend, 0, 1),
                     (Defend, 0, 1), (Defend, 0, 1), (Sword, 1, 1), (Swallow, 0, 2)
                 })
        {
            JsonObject card = RunRecordBuilder.CardRef(id, upgrade);
            card["floorAdded"] = floor;
            deck.Add(card);
        }
        var relics = new JsonArray();
        foreach (string relic in new[]
                 {
                     "RELIC.ELECTRONIC_SOLDIER_MANUAL_31_O", "RELIC.SOUL_OF_ANCHOR", "RELIC.DEFLECTOR_PROTECTION", "RELIC.OVERCLOCK",
                     "RELIC.ARCANE_SCROLL"
                 })
            relics.Add(new JsonObject { ["id"] = relic, ["floorAdded"] = 1 });
        var final = new JsonObject
        {
            ["floor"] = 6,
            ["hp"] = 3500,
            ["maxHp"] = 3500,
            ["dp"] = 7000,
            ["maxDp"] = 7000,
            ["gold"] = 16,
            ["deck"] = deck,
            ["relics"] = relics,
            ["potions"] = new JsonArray(),
            ["hbr"] = Hbr(2, 130, 144, 128, 128, 128, 128, RunRecordBuilder.Senses(6, 13, 5, 5, 5, 5),
                new JsonArray(new JsonObject { ["slot"] = "booster", ["index"] = 0, ["item"] = Booster() }), Bonus(14, 0),
                new JsonArray(Booster()))
        };
        var names = new Dictionary<string, string>
        {
            [Saki] = "丰川祥子（31O）", [Strike] = "打击", [Defend] = "防御", [Sword] = "魔剑幸运七", [Swallow] = "燕反",
            ["CARD.PER_TURN_CHARGE_HBRGO_SAKI"] = "每回合充能", ["CARD.SLIDE_STEP_HBRGO_SAKI"] = "滑步",
            ["CARD.DRINK_TEA_HBRGO_SAKI"] = "大吉岭红茶", ["CARD.QUICK_SWITCH_HBRGO_SAKI"] = "快速切换", ["CARD.STRENGTHEN_HBRGO_SAKI"] = "强化",
            ["RELIC.ELECTRONIC_SOLDIER_MANUAL_31_O"] = "丰川祥子（31O）的电子军人手册", ["RELIC.SOUL_OF_ANCHOR"] = "出目历史",
            ["RELIC.DEFLECTOR_PROTECTION"] = "偏导护盾", ["RELIC.OVERCLOCK"] = "超频", ["POTION.DP_POTION_HBRGO_SAKI"] = "DP药水",
            [Gym] = "健身房", ["ENCOUNTER.CREST_HOPPER_WEAK"] = "CrestHopper", ["ENCOUNTER.SMALL_HOPPERS_WEAK"] = "两只杂鱼",
            ["MONSTER.CREST_HOPPER"] = "CrestHopper", ["MONSTER.SMALL_HOPPER"] = "SmallHopper", [Destruction] = "破坏率"
        };
        builder.Finish("abandoned", null, null, DateTimeOffset.Parse("2026-11-08T12:58:03Z", CultureInfo.InvariantCulture), 1497, final, names);

        return RunUploadEnvelope.Create("6f1c2a7e-4b8d-4c1f-9a3e-2d5b7c9e0f14", "b2e4d6f8-1a3c-4e5f-8a7b-9c0d1e2f3a4b", "1.3.0.12",
            "v0.111.0", DateTimeOffset.Parse("2026-11-08T12:58:09Z", CultureInfo.InvariantCulture), builder.Run);
    }

    private static void PlayAndRoll(CombatRecordBuilder combat, object card, string id, string target, string label, int roll, string tier)
    {
        combat.Play("p", combat.Card(card, id, 0), target, 1, 0, false, 0);
        combat.Roll("p", combat.Uid(card), label, roll, roll, "基础骰", new JsonArray(), tier, false, 100, 777, new JsonArray());
    }

    private static JsonObject Booster() => RunRecordBuilder.EquipmentItem(1, "SBooster_0101", "booster", 1, "第一阶段", [], []);

    private static JsonObject Player() => new() { ["hp"] = 3500, ["maxHp"] = 3500, ["dp"] = 7000, ["maxDp"] = 7000 };

    private static JsonObject Bonus(decimal sixStats, decimal dp) => new()
    {
        ["strength"] = sixStats, ["dexterity"] = sixStats, ["intelligence"] = sixStats, ["vitality"] = sixStats,
        ["spirit"] = sixStats, ["luck"] = sixStats, ["dp"] = dp, ["critRatePercent"] = 0
    };

    private static JsonObject Hbr(int level, decimal str, decimal dex, decimal intel, decimal vit, decimal spi, decimal luck,
        JsonObject senses, JsonArray equipped, JsonObject bonus, JsonArray inventory)
    {
        var equipment = new JsonObject { ["equipped"] = equipped, ["bonus"] = bonus, ["effects"] = new JsonObject() };
        if (inventory != null) equipment["inventory"] = inventory;
        return RunRecordBuilder.Hbr(level, RunRecordBuilder.SixStats(str, dex, intel, vit, spi, luck), senses, 0, 0m, equipment);
    }
}
