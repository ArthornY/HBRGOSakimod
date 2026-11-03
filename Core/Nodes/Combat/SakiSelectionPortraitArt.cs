using System.Collections.Generic;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// The portraits are large lossless textures. They are requested on a loader
// thread when combat binds the player and kept for the session, so the first
// card selection of a combat no longer decodes them on the main thread.
internal static class SakiSelectionPortraitArt
{
    private static readonly Dictionary<string, Texture2D> Loaded = new();
    private static readonly HashSet<string> Requested = new();

    internal static void Preload(string path)
    {
        if (Loaded.ContainsKey(path) || Requested.Contains(path)) return;
        if (ResourceLoader.LoadThreadedRequest(path) == Error.Ok) Requested.Add(path);
    }

    // Immediate once the threaded load has finished; otherwise this waits for
    // it, or loads directly when nothing was requested or the request failed.
    internal static Texture2D Get(string path)
    {
        if (Loaded.TryGetValue(path, out Texture2D cached) && GodotObject.IsInstanceValid(cached)) return cached;
        Texture2D texture = Requested.Remove(path) ? ResourceLoader.LoadThreadedGet(path) as Texture2D : null;
        texture ??= ResourceLoader.Load<Texture2D>(path);
        if (texture != null) Loaded[path] = texture;
        return texture;
    }
}
