using Godot;
using HBRGOSakiMod.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrClickEffectHelper
{
    private static bool _installQueued;

    internal static void EnsureInstalled()
    {
        if (_installQueued) return;
        _installQueued = true;
        // The initializer can run while NGame is still adding its scene children.
        Callable.From(InstallDeferred).CallDeferred();
    }

    private static void InstallDeferred()
    {
        _installQueued = false;
        NGame game = NGame.Instance;
        if (!GodotObject.IsInstanceValid(game) || !game.IsInsideTree() || game.IsQueuedForDeletion()) return;
        if (game.GetNodeOrNull<CanvasLayer>(NHbrClickEffectOverlay.LayerName) != null) return;

        // Append after the game's UI children so _Input observes clicks before GUI handling.
        game.AddChild(new NHbrClickEffectOverlay
        {
            Name = NHbrClickEffectOverlay.LayerName,
            Layer = 120,
            ProcessMode = Node.ProcessModeEnum.Always
        });
    }
}
