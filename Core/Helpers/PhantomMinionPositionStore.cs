using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomMinionPositionStore
{
    private static readonly Lazy<PhantomMinionPositionFile> File = new(() => new(
        Path.Combine(OS.GetUserDataDir(), "mod_configs", "HBRGOSakiMod.phantom_minion_positions.json"),
        message => Log.Warn($"Phantom minion positions: {message}")));

    // The ordinal is zero-based within a monster model, independent of run and owner IDs.
    internal static bool TryGet(string monsterId, int ordinal, out Vector2 position) =>
        File.Value.TryGet(monsterId, ordinal, out position);

    internal static bool Save(string monsterId, int ordinal, Vector2 position) =>
        File.Value.Save(monsterId, ordinal, position);

    internal static int ResolveOrdinal(string battleKey, string monsterId, uint combatId,
        IEnumerable<int> occupied) => File.Value.ResolveOrdinal(battleKey, monsterId, combatId, occupied);
}

// Path injection keeps persistence independent of Godot and of the user's real settings in checks.
internal sealed class PhantomMinionPositionFile
{
    private readonly string _path;
    private readonly Action<string> _logWarning;
    private readonly object _gate = new();
    private readonly SortedDictionary<string, SortedDictionary<int, Vector2>> _positions =
        new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, SortedDictionary<uint, int>> _bindings =
        new(StringComparer.Ordinal);
    private string _battleKey;
    private bool _loaded;
    private bool _dirty;

    internal PhantomMinionPositionFile(string path, Action<string> logWarning = null)
    {
        _path = path;
        _logWarning = logWarning;
    }

    internal bool TryGet(string monsterId, int ordinal, out Vector2 position)
    {
        position = default;
        if (!ValidKey(monsterId, ordinal)) return false;
        lock (_gate)
        {
            Load();
            return _positions.TryGetValue(monsterId, out var copies) &&
                copies.TryGetValue(ordinal, out position);
        }
    }

    internal bool Save(string monsterId, int ordinal, Vector2 position)
    {
        if (!ValidKey(monsterId, ordinal) || !Finite(position)) return false;
        lock (_gate)
        {
            Load();
            if (!_positions.TryGetValue(monsterId, out var copies))
                _positions.Add(monsterId, copies = new());
            if (!copies.TryGetValue(ordinal, out Vector2 previous) || previous != position)
            {
                copies[ordinal] = position;
                _dirty = true;
            }
            return !_dirty || Write();
        }
    }

    internal int ResolveOrdinal(string battleKey, string monsterId, uint combatId,
        IEnumerable<int> occupied)
    {
        HashSet<int> used = new(occupied ?? Array.Empty<int>());
        int firstFree = 0;
        while (used.Contains(firstFree)) firstFree++;
        if (string.IsNullOrWhiteSpace(battleKey) || string.IsNullOrWhiteSpace(monsterId))
            return firstFree;

        lock (_gate)
        {
            Load();
            if (!string.Equals(_battleKey, battleKey, StringComparison.Ordinal))
            {
                _battleKey = battleKey;
                _bindings.Clear();
                _dirty = true;
            }
            if (!_bindings.TryGetValue(monsterId, out var copies))
                _bindings.Add(monsterId, copies = new());
            if (!copies.TryGetValue(combatId, out int ordinal) || used.Contains(ordinal))
            {
                ordinal = firstFree;
                copies[combatId] = ordinal;
                _dirty = true;
            }
            // Persist assignments too: a surviving second copy must keep slot 1
            // after reloading a battle where the first copy has already died.
            if (_dirty) Write();
            return ordinal;
        }
    }

    private void Load()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (!System.IO.File.Exists(_path)) return;
            using FileStream stream = System.IO.File.OpenRead(_path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1 ||
                !root.TryGetProperty("positions", out var positions) || positions.ValueKind != JsonValueKind.Object)
                throw new JsonException("Invalid position file format.");

            foreach (JsonProperty monster in positions.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(monster.Name) || monster.Value.ValueKind != JsonValueKind.Object)
                    continue;
                foreach (JsonProperty copy in monster.Value.EnumerateObject())
                {
                    if (!int.TryParse(copy.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal) ||
                        ordinal < 0 || copy.Value.ValueKind != JsonValueKind.Array || copy.Value.GetArrayLength() != 2)
                        continue;
                    JsonElement x = copy.Value[0], y = copy.Value[1];
                    if (x.ValueKind != JsonValueKind.Number || y.ValueKind != JsonValueKind.Number ||
                        !x.TryGetSingle(out float px) || !y.TryGetSingle(out float py) ||
                        !float.IsFinite(px) || !float.IsFinite(py)) continue;
                    if (!_positions.TryGetValue(monster.Name, out var copies))
                        _positions.Add(monster.Name, copies = new());
                    copies[ordinal] = new Vector2(px, py);
                }
            }

            // Version 1 files written before battle bindings remain valid.
            if (root.TryGetProperty("battle", out var battle) && battle.ValueKind == JsonValueKind.Object &&
                battle.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(key.GetString()) &&
                battle.TryGetProperty("ordinals", out var bindings) && bindings.ValueKind == JsonValueKind.Object)
            {
                _battleKey = key.GetString();
                foreach (JsonProperty monster in bindings.EnumerateObject())
                {
                    if (string.IsNullOrWhiteSpace(monster.Name) || monster.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    foreach (JsonProperty copy in monster.Value.EnumerateObject())
                    {
                        if (!uint.TryParse(copy.Name, NumberStyles.None, CultureInfo.InvariantCulture, out uint combatId) ||
                            copy.Value.ValueKind != JsonValueKind.Number ||
                            !copy.Value.TryGetInt32(out int ordinal) || ordinal < 0) continue;
                        if (!_bindings.TryGetValue(monster.Name, out var copies))
                            _bindings.Add(monster.Name, copies = new());
                        copies[combatId] = ordinal;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logWarning?.Invoke($"Could not load saved layout: {exception.Message}");
        }
    }

    private bool Write()
    {
        string temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var positions = new SortedDictionary<string, SortedDictionary<int, float[]>>(StringComparer.Ordinal);
            foreach (var monster in _positions)
            {
                var copies = new SortedDictionary<int, float[]>();
                foreach (var copy in monster.Value)
                    copies.Add(copy.Key, new[] { copy.Value.X, copy.Value.Y });
                positions.Add(monster.Key, copies);
            }
            var battle = _battleKey == null ? null : new { key = _battleKey, ordinals = _bindings };
            using (FileStream stream = System.IO.File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, new { version = 1, positions, battle });
                stream.Flush(flushToDisk: true);
            }
            // Same-directory replacement matches BaseLib's configuration-save convention.
            System.IO.File.Move(temporaryPath, _path, overwrite: true);
            _dirty = false;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep the in-memory drag result; another save can retry without losing the previous file.
            _logWarning?.Invoke($"Could not save layout: {exception.Message}");
            return false;
        }
        finally
        {
            try { System.IO.File.Delete(temporaryPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static bool ValidKey(string monsterId, int ordinal) =>
        !string.IsNullOrWhiteSpace(monsterId) && ordinal >= 0;

    private static bool Finite(Vector2 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y);
}
