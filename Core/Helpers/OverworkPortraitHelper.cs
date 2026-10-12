using System;
using Godot;
using HBRGOSakiMod.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Helpers;

internal static class OverworkPortraitHelper
{
    internal const string PortraitPath = "res://images/packed/card_portraits/hbrgo_saki/overwork_hbrgo_saki.png";
    private static Texture2D _sourceTexture;
    private static ImageTexture _texture;
    private static byte[] _originalPixels;
    private static NOverworkPortraitRenderer _renderer;
    private static bool _failed;
    private static bool _prewarmRequested;

    internal static void Prewarm()
    {
        if (_prewarmRequested) return;
        _prewarmRequested = true;
        // Invoked by initialization, then runs on the next main-thread idle turn
        // after scripts/resources have been registered. First card draw reuses it.
        Callable.From(PrewarmDeferred).CallDeferred();
    }

    private static void PrewarmDeferred()
    {
        if (Engine.GetMainLoop() is not SceneTree tree || !GodotObject.IsInstanceValid(tree.Root)) return;
        GetPortrait();
    }

    // Called only by the game's portrait UI on its main thread. A stable texture
    // survives individual NCard instances, so opening/closing cards cannot free a
    // viewport still used by another card. The source image is the initial value.
    internal static Texture2D GetPortrait()
    {
        if (_failed) return GodotObject.IsInstanceValid(_texture) ? _texture : _sourceTexture;
        try
        {
            if (!GodotObject.IsInstanceValid(_texture))
            {
                if (!ResourceLoader.Exists(PortraitPath))
                    throw new InvalidOperationException("The Overwork portrait resource is unavailable.");
                _sourceTexture = ResourceLoader.Load<Texture2D>(PortraitPath);
                using Image source = _sourceTexture.GetImage();
                if (source.IsCompressed()) source.Decompress();
                source.Convert(Image.Format.Rgba8);
                if (source.GetWidth() != OverworkScreenCompositor.PortraitWidth ||
                    source.GetHeight() != OverworkScreenCompositor.PortraitHeight)
                    throw new InvalidOperationException("The Overwork portrait must preserve the original 1448 x 1086 image.");
                _originalPixels = source.GetData();
                _texture = ImageTexture.CreateFromImage(source);
            }
            if (!_failed && !GodotObject.IsInstanceValid(_renderer) && Engine.GetMainLoop() is SceneTree tree)
            {
                _renderer = new NOverworkPortraitRenderer();
                _renderer.Initialize(_texture, _originalPixels);
                tree.Root.CallDeferred(Node.MethodName.AddChild, _renderer);
            }
            return _texture;
        }
        catch (Exception exception)
        {
            if (!_failed) GD.PushWarning($"Overwork portrait: {exception.Message}");
            _failed = true;
            return GodotObject.IsInstanceValid(_texture)
                ? _texture : _sourceTexture;
        }
    }
}
