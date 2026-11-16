using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

public static class HbrMemoryGardenUpgradeStoryLocalStore
{
    private sealed class Warning { internal int Issued; }
    private static readonly ConditionalWeakTable<Player, Warning> MissingKeys = new();
    private static readonly FieldInfo StartTimeField = typeof(RunManager).GetField("_startTime", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly object FileGate = new();
    private static HbrMemoryGardenUpgradeStoryLocalFile _file;
    private static HbrMemoryGardenUpgradeStoryLocalFile File
    {
        get
        {
            lock (FileGate) return _file ??= new HbrMemoryGardenUpgradeStoryLocalFile(
                Path.Combine(OS.GetUserDataDir(), "mod_configs", "HBRGOSakiMod.memory_garden_upgrade_stories.json"),
                message => Log.Warn("[HBRGOSakiMod][UpgradeStory] " + message));
        }
    }

    public static bool HasStarted(string runKey, string perspective, string actor, int level) =>
        File.HasStarted(runKey, perspective, actor, level);

    public static bool MarkStarted(string runKey, string perspective, string actor, int level) =>
        HBRGOSakiConfig.MemoryGardenUpgradeStoryPlayback != HbrMemoryGardenStoryPlayback.Off &&
        File.MarkStarted(runKey, perspective, actor, level);

    public static bool HasSeen(string perspective, string actor, int level) => File.HasSeen(perspective, actor, level);

    public static bool MarkSeen(string perspective, string actor, int level) => File.MarkSeen(perspective, actor, level);

    public static bool TryGetRunKey(Player owner, out string runKey)
    {
        runKey = string.Empty;
        if (owner == null) return false;
        RunManager manager = RunManager.Instance;
        if (owner.RunState is not RunState run || !ReferenceEquals(manager.DebugOnlyGetState(), run) ||
            StartTimeField?.GetValue(manager) is not long startTime || startTime <= 0 ||
            string.IsNullOrEmpty(run.Rng?.StringSeed))
        {
            if (Interlocked.Exchange(ref MissingKeys.GetOrCreateValue(owner).Issued, 1) == 0)
                Log.Warn("[HBRGOSakiMod][UpgradeStory] Stable native run identity is unavailable; presentation skipped without writing a local receipt.");
            return false;
        }

        // Native save StartTime is restored unchanged on reload. Length-prefix
        // the seed so delimiters in an explicit custom seed cannot collide.
        string seed = run.Rng.StringSeed;
        runKey = string.Create(CultureInfo.InvariantCulture,
            $"v1:{startTime}:{seed.Length}:{seed}:{run.AscensionLevel}:{owner.NetId}");
        return true;
    }
}
