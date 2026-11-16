using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Which met characters the memory garden has already shown in colour, so the silhouette of a newly met one drains
// away once (ui-concepts/memory-garden-names-20261106). Purely cosmetic, so it lives on this computer in user://,
// keyed by run seed, player and character, and never in the run save: relic save data is part of the multiplayer
// checksum (NetFullCombatState), and a change made by one client's UI alone would read as a desync.
// A run saved before this existed has no entry yet, so the first garden visit plays it once for everyone met.
internal static class HbrMemoryGardenShownStore
{
    private const string FilePath = "user://hbrgo_memory_garden_shown.cfg";
    private const string Section = "shown";
    private const int KeptRuns = 40;

    // The ids in `met` the garden has not shown on this computer yet; they count as shown from now on.
    internal static IReadOnlySet<string> TakeNewlyShown(Player player, IReadOnlyCollection<string> met)
    {
        HashSet<string> fresh = new(StringComparer.Ordinal);
        if (player == null || met == null || met.Count == 0) return fresh;
        try
        {
            string key = Key(player);
            if (key == null) return fresh;
            ConfigFile config = new();
            config.Load(FilePath); // no file yet: start empty
            HashSet<string> shown = new(config.GetValue(Section, key, string.Empty).AsString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            foreach (string id in met)
                if (!shown.Contains(id)) fresh.Add(id);
            if (fresh.Count == 0) return fresh;
            shown.UnionWith(fresh);
            config.SetValue(Section, key, string.Join(",", shown.OrderBy(id => id, StringComparer.Ordinal)));
            string[] keys = config.GetSectionKeys(Section);
            for (int index = 0; index < keys.Length - KeptRuns; index++)
                if (keys[index] != key) config.EraseSectionKey(Section, keys[index]);
            Error error = config.Save(FilePath);
            if (error != Error.Ok) Log.Warn($"[HBRGOSakiMod][MemoryGarden] Could not save {FilePath}: {error}");
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod][MemoryGarden] Shown characters unavailable: {error.Message}");
        }
        return fresh;
    }

    private static string Key(Player player)
    {
        string seed = player.RunState?.Rng?.StringSeed;
        return string.IsNullOrEmpty(seed) ? null : $"{seed}|{player.NetId}|{player.Character?.Id.Entry}";
    }
}
