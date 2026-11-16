using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HBRGOSakiMod.Core.RunUpload;

internal sealed record RunRecordHeader
{
    public string Character { get; init; }
    public int Ascension { get; init; }
    public string Seed { get; init; }
    public string GameMode { get; init; }
    public IReadOnlyList<string> Modifiers { get; init; } = Array.Empty<string>();
    public DateTimeOffset StartedAt { get; init; }
    public string Locale { get; init; }
    public JsonObject Multiplayer { get; init; }
    // The format keeps these, but the recorder records a run from its start
    // or not at all (RunRecordingGate): it always writes false and 1.
    public bool Partial { get; init; }
    public int FromFloor { get; init; } = 1;
    public JsonObject Rules { get; init; }
    public RunIntegrity Integrity { get; init; } = RunIntegrity.Clean;
}

internal sealed record RunMapNode(int Col, int Row, string Type, IReadOnlyList<(int Col, int Row)> Next);

// Enum names to the record's spelling: RestSite → rest_site (values),
// LeadershipCharm → leadershipCharm (keys).
internal static class RunRecordNames
{
    internal static string Snake(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var builder = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c) && i > 0 && !char.IsUpper(name[i - 1])) builder.Append('_');
            builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    internal static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
}

// Builds the "run" object of schema-v1.md. It knows nothing about the game:
// the recorder turns game objects into ids, keys and numbers first. Field
// names live only here, so the offline checks can compare them with the
// schema and the example.
internal sealed class RunRecordBuilder
{
    private static readonly Regex ModelIdPattern = new("^[A-Z][A-Z0-9_]*\\.[A-Z0-9_]+$", RegexOptions.CultureInvariant);
    private const int MaxNotes = 20;

    private readonly JsonObject _run;
    private JsonObject _act;
    private JsonObject _floor;
    private int _floorCount;

    internal RunRecordBuilder(RunRecordHeader header)
    {
        _floorCount = Math.Max(0, header.FromFloor - 1);
        _run = new JsonObject
        {
            ["recorder"] = new JsonObject
            {
                ["version"] = RunUploadConstants.RecorderVersion,
                ["features"] = Strings(RunUploadConstants.RecordedFeatures),
                ["partial"] = header.Partial,
                ["fromFloor"] = Math.Max(1, header.FromFloor),
                ["sessions"] = 1,
                ["notes"] = new JsonArray()
            },
            ["rules"] = header.Rules?.DeepClone() ?? new JsonObject(),
            ["startedAt"] = RunUploadEnvelope.FormatTime(header.StartedAt),
            ["endedAt"] = null,
            ["playSeconds"] = 0,
            ["character"] = header.Character,
            ["ascension"] = header.Ascension,
            ["seed"] = header.Seed,
            ["gameMode"] = header.GameMode,
            ["modifiers"] = Strings(header.Modifiers),
            ["outcome"] = null,
            ["killedBy"] = null,
            ["floorReached"] = _floorCount,
            ["integrity"] = (header.Integrity ?? RunIntegrity.Clean).ToJson(),
            ["multiplayer"] = header.Multiplayer?.DeepClone(),
            ["locale"] = header.Locale,
            ["acts"] = new JsonArray()
        };
    }

    // Another gameplay mod was on during a later session of this run.
    internal void MarkOtherGameplayMods() => ApplyIntegrity(new RunIntegrity(Array.Empty<string>(), false, true), false);

    // Merges what a session saw into run.integrity; flags only ever turn on.
    internal void ApplyIntegrity(RunIntegrity session, bool consoleUsed)
    {
        if (_run["integrity"] is not JsonObject integrity) _run["integrity"] = integrity = RunIntegrity.Clean.ToJson();
        var modifiers = new SortedSet<string>(StringComparer.Ordinal);
        foreach (JsonNode node in integrity["modifiers"] as JsonArray ?? new JsonArray())
            if (node is JsonValue value && value.TryGetValue(out string label)) modifiers.Add(label);
        foreach (string label in session?.Modifiers ?? Array.Empty<string>()) modifiers.Add(label);
        integrity["modifiers"] = Strings(modifiers);
        if (consoleUsed || session?.ConsoleUsed == true) integrity["consoleUsed"] = true;
        if (session?.OtherGameplayMods == true) integrity["otherGameplayMods"] = true;
    }

    private RunRecordBuilder(JsonObject run)
    {
        _run = run;
        _act = Acts.LastOrDefault() as JsonObject;
        _floor = (_act?["floors"] as JsonArray)?.LastOrDefault() as JsonObject;
        _floorCount = _run["floorReached"]?.GetValue<int>() ?? 0;
    }

    // Continues a record saved before the game was closed.
    internal static RunRecordBuilder Resume(JsonObject run) =>
        run?["acts"] is JsonArray && run["recorder"] is JsonObject ? new RunRecordBuilder(run) : null;

    internal JsonObject Run => _run;
    internal JsonObject CurrentAct => _act;
    internal JsonObject CurrentFloor => _floor;
    internal int FloorCount => _floorCount;

    internal JsonObject CurrentRoom => (_floor?["rooms"] as JsonArray)?.LastOrDefault() as JsonObject;

    private JsonArray Acts => (JsonArray)_run["acts"];

    private JsonObject Recorder => (JsonObject)_run["recorder"];

    internal void AddSession() => Recorder["sessions"] = (Recorder["sessions"]?.GetValue<int>() ?? 1) + 1;

    internal void Note(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || Recorder["notes"] is not JsonArray notes) return;
        text = Text(text);
        if (notes.Count >= MaxNotes || notes.Any(node => node?.GetValue<string>() == text)) return;
        notes.Add(text);
    }

    // An act's map is captured when the act is entered; a repeated entry
    // (for example after continuing a saved run) reuses the existing act.
    internal JsonObject EnsureAct(int index, string actId, JsonObject map)
    {
        foreach (JsonNode node in Acts)
        {
            if (node is JsonObject act && act["index"]?.GetValue<int>() == index)
            {
                if (act["map"] == null && map != null) act["map"] = map;
                if (act["id"] == null && actId != null) act["id"] = actId;
                if (!ReferenceEquals(_act, act))
                {
                    _act = act;
                    _floor = (act["floors"] as JsonArray)?.LastOrDefault() as JsonObject;
                }
                return act;
            }
        }
        _act = new JsonObject
        {
            ["index"] = index,
            ["id"] = actId,
            ["map"] = map,
            ["path"] = new JsonArray(),
            ["floors"] = new JsonArray()
        };
        Acts.Add(_act);
        _floor = null;
        return _act;
    }

    internal static JsonObject Map(int cols, int rows, (int Col, int Row)? start, (int Col, int Row)? boss,
        (int Col, int Row)? secondBoss, IEnumerable<RunMapNode> nodes)
    {
        var list = new JsonArray();
        foreach (RunMapNode node in nodes ?? Enumerable.Empty<RunMapNode>())
        {
            var next = new JsonArray();
            foreach ((int col, int row) in node.Next ?? Array.Empty<(int, int)>()) next.Add(Coord(col, row));
            list.Add(new JsonObject { ["at"] = Coord(node.Col, node.Row), ["type"] = node.Type, ["next"] = next });
        }
        return new JsonObject
        {
            ["cols"] = cols,
            ["rows"] = rows,
            ["start"] = start is { } s ? Coord(s.Col, s.Row) : null,
            ["boss"] = boss is { } b ? Coord(b.Col, b.Row) : null,
            ["secondBoss"] = secondBoss is { } sb ? Coord(sb.Col, sb.Row) : null,
            ["nodes"] = list
        };
    }

    // A new map point. firstActFloor is used only for the first recorded
    // floor of an act, when recording began part way through it.
    internal JsonObject BeginFloor((int Col, int Row)? at, string pointType, int firstActFloor = 1)
    {
        if (_act == null) EnsureAct(0, null, null);
        var floors = (JsonArray)_act["floors"];
        int actFloor = floors.LastOrDefault() is JsonObject previous
            ? previous["actFloor"].GetValue<int>() + 1
            : Math.Max(1, firstActFloor);
        _floorCount++;
        _floor = new JsonObject
        {
            ["floor"] = _floorCount,
            ["actFloor"] = actFloor,
            ["at"] = at is { } coord ? Coord(coord.Col, coord.Row) : null,
            ["pointType"] = pointType,
            ["rooms"] = new JsonArray()
        };
        floors.Add(_floor);
        if (at is { } point) ((JsonArray)_act["path"]).Add(Coord(point.Col, point.Row));
        _run["floorReached"] = _floorCount;
        return _floor;
    }

    internal JsonObject AddRoom(string type, string model, IReadOnlyList<string> monsters)
    {
        if (_floor == null) BeginFloor(null, "unknown");
        var room = new JsonObject { ["type"] = type };
        if (!string.IsNullOrEmpty(model)) room["model"] = model;
        if (monsters is { Count: > 0 }) room["monsters"] = Strings(monsters);
        ((JsonArray)_floor["rooms"]).Add(room);
        return room;
    }

    // A battle starts in this room. One already there without a result (the
    // game was closed during it and the room is played again) moves to
    // earlierAttempts as "interrupted". The number of the new attempt.
    internal static int BeginCombatAttempt(JsonObject room)
    {
        if (room?["combat"] is not JsonObject previous) return 1;
        int attempt = (previous["attempt"]?.GetValue<int>() ?? 1) + 1;
        room.Remove("combat");
        if (previous["result"] == null)
            previous["result"] = new JsonObject
            {
                ["outcome"] = "interrupted",
                ["rounds"] = (previous["rounds"] as JsonArray)?.Count ?? 0
            };
        if (room["earlierAttempts"] is not JsonArray earlier) room["earlierAttempts"] = earlier = new JsonArray();
        earlier.Add(previous);
        return attempt;
    }

    // Choices, stats and hbr are written once the floor is left.
    internal void SetFloorSection(string name, JsonObject value)
    {
        if (_floor == null || value == null) return;
        if (value.Count == 0) _floor.Remove(name);
        else _floor[name] = value;
    }

    internal void Finish(string outcome, JsonObject killedBy, DateTimeOffset? startedAt, DateTimeOffset endedAt,
        long playSeconds, JsonObject final, IReadOnlyDictionary<string, string> names)
    {
        if (startedAt is { } start) _run["startedAt"] = RunUploadEnvelope.FormatTime(start);
        _run["endedAt"] = RunUploadEnvelope.FormatTime(endedAt);
        _run["playSeconds"] = Math.Max(0, playSeconds);
        _run["outcome"] = outcome;
        _run["killedBy"] = killedBy;
        _run["floorReached"] = _floorCount;
        _run["final"] = final;
        if (names is { Count: > 0 })
        {
            var table = new JsonObject();
            foreach (KeyValuePair<string, string> pair in names.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                if (!string.IsNullOrEmpty(pair.Value)) table[pair.Key] = Text(pair.Value);
            if (table.Count > 0) _run["names"] = table;
        }
    }

    // Every game id that appears anywhere in the record, for the names table.
    internal IReadOnlyCollection<string> CollectIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Collect(_run, ids);
        return ids;
    }

    private static void Collect(JsonNode node, HashSet<string> ids)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (KeyValuePair<string, JsonNode> pair in obj)
                    if (pair.Key != "names") Collect(pair.Value, ids);
                break;
            case JsonArray array:
                foreach (JsonNode item in array) Collect(item, ids);
                break;
            case JsonValue value when value.TryGetValue(out string text) && ModelIdPattern.IsMatch(text):
                ids.Add(text);
                break;
        }
    }

    internal static JsonArray Coord(int col, int row) => new(col, row);

    internal static JsonArray Strings(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (string value in values ?? Enumerable.Empty<string>())
            if (!string.IsNullOrEmpty(value)) array.Add(value);
        return array;
    }

    internal static JsonObject CardRef(string id, int upgrade, string enchantment = null, string rarity = null)
    {
        var card = new JsonObject { ["id"] = id, ["upgrade"] = upgrade };
        if (!string.IsNullOrEmpty(enchantment)) card["enchantment"] = enchantment;
        if (!string.IsNullOrEmpty(rarity)) card["rarity"] = rarity;
        return card;
    }

    internal static string Text(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = text.Trim();
        return text.Length <= RunUploadConstants.MaxTextLength ? text : text.Substring(0, RunUploadConstants.MaxTextLength);
    }

    internal static JsonObject Stats(int hp, int maxHp, int? dp, int? maxDp, int gold, int goldGained, int goldSpent,
        int goldLost, int goldStolen, int damageTaken, int hpHealed, int maxHpGained, int maxHpLost)
    {
        var stats = new JsonObject { ["hp"] = hp, ["maxHp"] = maxHp };
        if (dp.HasValue) stats["dp"] = dp.Value;
        if (maxDp.HasValue) stats["maxDp"] = maxDp.Value;
        stats["gold"] = gold;
        stats["goldGained"] = goldGained;
        stats["goldSpent"] = goldSpent;
        stats["goldLost"] = goldLost;
        stats["goldStolen"] = goldStolen;
        stats["damageTaken"] = damageTaken;
        stats["hpHealed"] = hpHealed;
        stats["maxHpGained"] = maxHpGained;
        stats["maxHpLost"] = maxHpLost;
        return stats;
    }

    internal static JsonObject SixStats(decimal strength, decimal dexterity, decimal intelligence, decimal vitality,
        decimal spirit, decimal luck) => new()
    {
        ["strength"] = strength,
        ["dexterity"] = dexterity,
        ["intelligence"] = intelligence,
        ["vitality"] = vitality,
        ["spirit"] = spirit,
        ["luck"] = luck
    };

    internal static JsonObject Senses(int madness, int leadershipCharm, int talent, int gentleness, int mentalStrength,
        int simplicity) => new()
    {
        ["madness"] = madness,
        ["leadershipCharm"] = leadershipCharm,
        ["talent"] = talent,
        ["gentleness"] = gentleness,
        ["mentalStrength"] = mentalStrength,
        ["simplicity"] = simplicity
    };

    internal static JsonObject Hbr(int battleLevel, JsonObject stats, JsonObject senses, int unspentSensePoints,
        decimal critRatePercent, JsonObject equipment) => new()
    {
        ["battleLevel"] = battleLevel,
        ["stats"] = stats,
        ["senses"] = senses,
        ["unspentSensePoints"] = unspentSensePoints,
        ["critRatePercent"] = critRatePercent,
        ["equipment"] = equipment
    };

    internal static JsonObject EquipmentItem(int? itemId, string template, string kind, int stars, string name,
        IEnumerable<int> rolls, IEnumerable<(string Stat, int Level)> plus)
    {
        var item = new JsonObject();
        if (itemId.HasValue) item["itemId"] = itemId.Value;
        item["template"] = template;
        item["kind"] = kind;
        item["stars"] = stars;
        item["name"] = Text(name);
        var rollArray = new JsonArray();
        foreach (int roll in rolls ?? Enumerable.Empty<int>()) rollArray.Add(roll);
        item["rolls"] = rollArray;
        var plusArray = new JsonArray();
        foreach ((string stat, int level) in plus ?? Enumerable.Empty<(string, int)>())
            plusArray.Add(new JsonObject { ["stat"] = stat, ["level"] = level });
        item["plus"] = plusArray;
        return item;
    }

    internal static JsonObject CreatureState(int hp, int maxHp, int block, int? dp = null, int? maxDp = null,
        int? energy = null, int? stars = null, int? od = null, JsonObject piles = null, JsonArray powers = null,
        JsonArray intents = null)
    {
        var state = new JsonObject { ["hp"] = hp, ["maxHp"] = maxHp, ["block"] = block };
        if (dp.HasValue) state["dp"] = dp.Value;
        if (maxDp.HasValue) state["maxDp"] = maxDp.Value;
        if (energy.HasValue) state["energy"] = energy.Value;
        if (stars is > 0) state["stars"] = stars.Value;
        if (od.HasValue) state["od"] = od.Value;
        if (piles != null) state["piles"] = piles;
        state["powers"] = powers ?? new JsonArray();
        if (intents != null) state["intents"] = intents;
        return state;
    }

    internal static JsonObject EndState(int hp, int block, int? dp)
    {
        var state = new JsonObject { ["hp"] = hp, ["block"] = block };
        if (dp.HasValue) state["dp"] = dp.Value;
        return state;
    }

    internal static JsonObject Piles(int hand, int draw, int discard, int exhaust) => new()
    {
        ["hand"] = hand,
        ["draw"] = draw,
        ["discard"] = discard,
        ["exhaust"] = exhaust
    };

    internal static JsonObject Power(string id, int amount) => new() { ["id"] = id, ["amount"] = amount };

    internal static JsonObject Intent(string type, string move, int? damage, int? hits, int? cards)
    {
        var intent = new JsonObject { ["type"] = type };
        if (!string.IsNullOrEmpty(move)) intent["move"] = move;
        if (damage.HasValue) intent["damage"] = damage.Value;
        if (hits.HasValue) intent["hits"] = hits.Value;
        if (cards.HasValue) intent["cards"] = cards.Value;
        return intent;
    }

    internal static JsonObject Option(string key, string text, bool locked = false, JsonObject vars = null)
    {
        var option = new JsonObject { ["key"] = key };
        if (!string.IsNullOrEmpty(text)) option["text"] = Text(text);
        if (locked) option["locked"] = true;
        if (vars is { Count: > 0 }) option["vars"] = vars;
        return option;
    }
}

internal sealed class RunDamageTotals
{
    internal int Total;
    internal int Hp;
    internal int Dp;
    internal int Blocked;
    internal int Kills;

    internal void Add(int amount, int hp, int dp, int blocked, bool killed)
    {
        Total += amount;
        Hp += hp;
        Dp += dp;
        Blocked += blocked;
        if (killed) Kills++;
    }

    internal JsonObject ToJson(bool withKills)
    {
        var json = new JsonObject { ["total"] = Total, ["hp"] = Hp, ["dp"] = Dp, ["blocked"] = Blocked };
        if (withKills) json["kills"] = Kills;
        return json;
    }
}

// One battle (schema-v1.md 7). Creature keys and card uids are assigned by
// object identity, so the same game object always maps to the same key.
internal sealed class CombatRecordBuilder
{
    private readonly JsonObject _combat;
    private readonly JsonObject _creatures = new();
    private readonly JsonArray _rounds = new();
    private readonly JsonArray _pending = new();
    private readonly Dictionary<object, string> _keys = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, int> _uids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, JsonObject> _openPlays = new();
    private readonly RunDamageTotals _dealt = new();
    private readonly RunDamageTotals _taken = new();
    private readonly int _maxEvents;
    private JsonObject _round;
    private JsonArray _events;
    private int _nextUid = 1;
    private int _enemies;
    private int _summons;
    private int _eventCount;

    internal CombatRecordBuilder(int attempt, int maxEvents = RunUploadConstants.MaxCombatEvents)
    {
        _maxEvents = Math.Max(1, maxEvents);
        _combat = new JsonObject
        {
            ["attempt"] = Math.Max(1, attempt),
            ["creatures"] = _creatures,
            ["rounds"] = _rounds
        };
    }

    internal JsonObject Combat => _combat;
    internal int RoundCount => _rounds.Count;
    internal int CurrentRound => _round?["round"]?.GetValue<int>() ?? 0;
    internal bool IsFinished => _combat["result"] != null;
    internal bool IsTruncated => _combat["truncated"]?.GetValue<bool>() == true;

    internal string Player(object identity, string characterId) =>
        Register(identity, "p", new JsonObject { ["kind"] = "player", ["id"] = characterId });

    internal string Ally(object identity, int seat, string characterId) =>
        Register(identity, "a" + seat, new JsonObject { ["kind"] = "ally", ["id"] = characterId });

    internal string Summon(object identity, string monsterId, string ownerKey)
    {
        string existing = KeyOf(identity);
        if (existing != null) return existing;
        var creature = new JsonObject { ["kind"] = "summon", ["id"] = monsterId };
        if (ownerKey != null) creature["owner"] = ownerKey;
        return Register(identity, "s" + _summons++, creature);
    }

    internal string Enemy(object identity, string monsterId, decimal? hbrParameter, int? joinedRound)
    {
        string existing = KeyOf(identity);
        if (existing != null) return existing;
        var creature = new JsonObject { ["kind"] = "enemy", ["id"] = monsterId };
        if (hbrParameter.HasValue) creature["hbrParameter"] = hbrParameter.Value;
        if (joinedRound.HasValue) creature["joinedRound"] = joinedRound.Value;
        return Register(identity, "e" + _enemies++, creature);
    }

    internal string KeyOf(object identity) =>
        identity != null && _keys.TryGetValue(identity, out string key) ? key : null;

    private string Register(object identity, string key, JsonObject creature)
    {
        string existing = KeyOf(identity);
        if (existing != null) return existing;
        if (identity != null) _keys[identity] = key;
        _creatures[key] = creature;
        return key;
    }

    internal int Uid(object card)
    {
        if (card == null) return 0;
        if (!_uids.TryGetValue(card, out int uid))
        {
            uid = _nextUid++;
            _uids[card] = uid;
        }
        return uid;
    }

    internal JsonObject Card(object card, string id, int upgrade) =>
        new() { ["uid"] = Uid(card), ["id"] = id, ["upgrade"] = upgrade };

    internal void SetHbr(JsonObject hbr)
    {
        if (hbr != null) _combat["hbr"] = hbr;
    }

    internal void BeginRound(int round, JsonObject start)
    {
        _events = new JsonArray();
        _round = new JsonObject { ["round"] = round, ["start"] = start ?? new JsonObject(), ["events"] = _events };
        _rounds.Add(_round);
        FlushPending();
    }

    // Opens the round with this number, or continues it (an extra turn in the
    // same round). Events held while no round was open come first.
    internal void EnsureRound(int round, Func<JsonObject> start)
    {
        if (_round != null && CurrentRound == round)
        {
            _events = (JsonArray)_round["events"];
            FlushPending();
            return;
        }
        BeginRound(round, start?.Invoke());
    }

    // Between a turn's start and the snapshot that opens the next round,
    // events wait so that they land at the beginning of that round.
    internal void CloseRound() => _events = null;

    internal bool RoundEnded => _round?["end"] != null;

    private void FlushPending()
    {
        // Effects that fire before the first turn (combat-start relics) open round 1.
        while (_pending.Count > 0)
        {
            JsonNode node = _pending[0];
            _pending.RemoveAt(0);
            _events.Add(node);
        }
    }

    internal void EndRound(JsonObject end)
    {
        if (_round != null && end is { Count: > 0 }) _round["end"] = end;
    }

    internal void TurnStart(string side) => Append(new JsonObject { ["type"] = "turn_start", ["side"] = side }, true);

    internal void TurnEnd(string side) => Append(new JsonObject { ["type"] = "turn_end", ["side"] = side }, true);

    internal void Draw(string by, JsonObject card) =>
        Append(new JsonObject { ["type"] = "draw", ["by"] = by, ["card"] = card });

    internal JsonObject Play(string by, JsonObject card, string target, int energy, int stars, bool auto, int repeat)
    {
        var play = new JsonObject
        {
            ["type"] = "play",
            ["by"] = by,
            ["card"] = card,
            ["target"] = target,
            ["energy"] = energy
        };
        if (stars > 0) play["stars"] = stars;
        if (auto) play["auto"] = true;
        if (repeat > 0) play["repeat"] = repeat;
        Append(play);
        int uid = card?["uid"]?.GetValue<int>() ?? 0;
        if (uid > 0) _openPlays[uid] = play;
        return play;
    }

    internal void EndPlay(int uid) => _openPlays.Remove(uid);

    internal void Roll(string by, int? cardUid, string label, int baseRoll, int finalRoll, string dice,
        JsonArray bonuses, string tier, bool forcedCritical, int critMin, int absCritMin, JsonArray discarded)
    {
        var roll = new JsonObject { ["type"] = "roll", ["by"] = by };
        if (cardUid is > 0) roll["cardUid"] = cardUid.Value;
        roll["label"] = RunRecordBuilder.Text(label);
        roll["base"] = baseRoll;
        roll["final"] = finalRoll;
        roll["dice"] = RunRecordBuilder.Text(dice);
        roll["bonuses"] = bonuses ?? new JsonArray();
        roll["tier"] = tier;
        if (forcedCritical) roll["forcedCritical"] = true;
        roll["critMin"] = critMin;
        roll["absCritMin"] = absCritMin;
        roll["discarded"] = discarded ?? new JsonArray();
        Append(roll);
    }

    internal static JsonObject RollBonus(int amount, string source, bool percent)
    {
        var bonus = new JsonObject { ["amount"] = amount, ["source"] = RunRecordBuilder.Text(source) };
        if (percent) bonus["percent"] = true;
        return bonus;
    }

    internal static JsonObject DiscardedRoll(int baseRoll, int finalRoll, string tier) =>
        new() { ["base"] = baseRoll, ["final"] = finalRoll, ["tier"] = tier };

    internal void Potion(string by, string potion, string target)
    {
        var use = new JsonObject { ["type"] = "potion", ["by"] = by, ["potion"] = potion };
        if (target != null) use["target"] = target;
        Append(use);
    }

    internal void Damage(string from, string to, JsonObject src, int amount, int blocked, int hpLoss, int dpLoss,
        int overkill, bool killed)
    {
        var damage = new JsonObject { ["type"] = "damage" };
        if (from != null) damage["from"] = from;
        damage["to"] = to;
        if (src != null) damage["src"] = src;
        damage["amount"] = amount;
        damage["blocked"] = blocked;
        damage["hpLoss"] = hpLoss;
        damage["dpLoss"] = dpLoss;
        if (overkill > 0) damage["overkill"] = overkill;
        if (killed) damage["killed"] = true;
        Append(damage);

        if (from == "p")
        {
            _dealt.Add(amount, hpLoss, dpLoss, blocked, killed);
            if (src?["cardUid"]?.GetValue<int>() is int uid && _openPlays.TryGetValue(uid, out JsonObject play))
            {
                JsonObject dealt = play["dealt"] as JsonObject ?? new JsonObject
                {
                    ["total"] = 0, ["hp"] = 0, ["dp"] = 0, ["blocked"] = 0, ["kills"] = 0
                };
                dealt["total"] = dealt["total"].GetValue<int>() + amount;
                dealt["hp"] = dealt["hp"].GetValue<int>() + hpLoss;
                dealt["dp"] = dealt["dp"].GetValue<int>() + dpLoss;
                dealt["blocked"] = dealt["blocked"].GetValue<int>() + blocked;
                dealt["kills"] = dealt["kills"].GetValue<int>() + (killed ? 1 : 0);
                play["dealt"] = dealt;
            }
        }
        if (to == "p") _taken.Add(amount, hpLoss, dpLoss, blocked, false);
    }

    internal void Block(string to, JsonObject src, int amount)
    {
        var block = new JsonObject { ["type"] = "block", ["to"] = to };
        if (src != null) block["src"] = src;
        block["amount"] = amount;
        Append(block);
    }

    internal void Power(string to, string from, JsonObject src, string power, int delta, int amount)
    {
        var change = new JsonObject { ["type"] = "power", ["to"] = to };
        if (from != null) change["from"] = from;
        if (src != null) change["src"] = src;
        change["power"] = power;
        change["delta"] = delta;
        change["amount"] = amount;
        Append(change);
    }

    internal void Move(string by, string move) =>
        Append(new JsonObject { ["type"] = "move", ["by"] = by, ["move"] = move });

    internal void Spawn(string who, string id) =>
        Append(new JsonObject { ["type"] = "spawn", ["who"] = who, ["id"] = id });

    internal void Death(string who) => Append(new JsonObject { ["type"] = "death", ["who"] = who });

    internal void CardEvent(string type, string by, JsonObject card) =>
        Append(new JsonObject { ["type"] = type, ["by"] = by, ["card"] = card });

    internal void Generate(string by, JsonObject card, string pile)
    {
        var generate = new JsonObject { ["type"] = "generate", ["by"] = by, ["card"] = card };
        if (pile != null) generate["pile"] = pile;
        Append(generate);
    }

    internal void Shuffle(string by, int count) =>
        Append(new JsonObject { ["type"] = "shuffle", ["by"] = by, ["count"] = count });

    internal static JsonObject Source(string kind, JsonNode value) => new() { [kind] = value };

    internal void Finish(string outcome, JsonObject player)
    {
        _openPlays.Clear();
        if (_rounds.Count == 0 && _pending.Count > 0) BeginRound(1, null);
        _combat["result"] = new JsonObject
        {
            ["outcome"] = outcome,
            ["rounds"] = _rounds.Count,
            ["player"] = player,
            ["dealt"] = _dealt.ToJson(true),
            ["taken"] = _taken.ToJson(false)
        };
    }

    private void Append(JsonObject node, bool always = false)
    {
        if (!always && _eventCount >= _maxEvents)
        {
            _combat["truncated"] = true;
            return;
        }
        if (!always) _eventCount++;
        (_events ?? _pending).Add(node);
    }
}
