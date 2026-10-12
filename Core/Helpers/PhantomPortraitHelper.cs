using System;
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomPortraitHelper
{
    internal const string FallbackPath = "res://images/packed/card_portraits/hbrgo_saki/phantom_white.tres";
    private sealed class CacheEntry
    {
        internal ImageTexture Texture { get; }
        internal PhantomPortraitRequestState Request { get; } = new();

        internal CacheEntry(ImageTexture texture) => Texture = texture;
    }

    private static readonly Dictionary<ModelId, CacheEntry> Cache = new();
    private static NPhantomPortraitRenderer _renderer;

    internal static Texture2D GetPortrait(ModelId monsterId)
    {
        if (!Cache.TryGetValue(monsterId, out CacheEntry entry))
        {
            using Image white = Image.CreateEmpty(PhantomPortraitCompositor.Width, PhantomPortraitCompositor.Height,
                false, Image.Format.Rgba8);
            white.Fill(Colors.White);
            entry = new CacheEntry(ImageTexture.CreateFromImage(white));
            // Eviction never disposes textures still displayed by a card.
            if (Cache.Count >= 128)
            {
                using var iterator = Cache.Keys.GetEnumerator();
                if (iterator.MoveNext()) Cache.Remove(iterator.Current);
            }
            Cache[monsterId] = entry;
        }

        if (!entry.Request.CanBegin(System.Environment.TickCount64)) return entry.Texture;
        // Early requests can precede model registration or the scene tree. Leave them
        // unrequested so the same texture can be populated once those dependencies exist.
        if (ModelDb.GetByIdOrNull<MonsterModel>(monsterId) is not { } monster) return entry.Texture;
        if (Engine.GetMainLoop() is not SceneTree tree) return entry.Texture;
        if (!entry.Request.TryBegin(System.Environment.TickCount64)) return entry.Texture;
        try
        {
            if (!GodotObject.IsInstanceValid(_renderer))
            {
                _renderer = new NPhantomPortraitRenderer();
                tree.Root.CallDeferred(Node.MethodName.AddChild, _renderer);
            }
            // Keep the entry, not a later lookup by id: an evicted request must only
            // complete its own texture, including references already held by cards.
            _renderer.Enqueue(monster, entry.Texture,
                (success, retryable) => entry.Request.Complete(success, System.Environment.TickCount64, retryable));
        }
        catch (Exception exception)
        {
            entry.Request.Complete(false, System.Environment.TickCount64);
            GD.PushWarning($"Phantom portrait {monsterId}: {exception.Message}");
        }
        return entry.Texture;
    }
}

internal enum PhantomPortraitRequestStatus
{
    Unrequested,
    Pending,
    Succeeded,
    Failed,
    Unavailable
}

// Managed request bookkeeping stays independent of Godot for deterministic checks.
internal sealed class PhantomPortraitRequestState
{
    internal const int MaximumAttempts = 3;
    internal const long RetryCooldownMilliseconds = 2000;
    private long _retryAt;

    internal PhantomPortraitRequestStatus Status { get; private set; }
    internal int Attempts { get; private set; }

    internal bool CanBegin(long now) =>
        Status is not (PhantomPortraitRequestStatus.Pending or PhantomPortraitRequestStatus.Succeeded
            or PhantomPortraitRequestStatus.Unavailable)
        && Attempts < MaximumAttempts
        && (Status != PhantomPortraitRequestStatus.Failed || now >= _retryAt);

    internal bool TryBegin(long now)
    {
        if (!CanBegin(now)) return false;
        Attempts++;
        Status = PhantomPortraitRequestStatus.Pending;
        return true;
    }

    internal void Complete(bool success, long now, bool retryable = true)
    {
        if (Status != PhantomPortraitRequestStatus.Pending) return;
        Status = success ? PhantomPortraitRequestStatus.Succeeded
            : retryable ? PhantomPortraitRequestStatus.Failed : PhantomPortraitRequestStatus.Unavailable;
        if (!success && retryable) _retryAt = now + RetryCooldownMilliseconds;
    }
}
