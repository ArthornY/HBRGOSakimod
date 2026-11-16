using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.RunUpload;

// The first-launch question about run uploading. It waits on the main menu
// until no other modal is open (the challenge announcement goes first), asks
// once, and asks again only when RunUploadConstants.ConsentVersion is raised.
public partial class NRunUploadFirstPrompt : Node
{
    private const string NodeName = "RunUploadFirstPrompt";
    private NMainMenu _menu;

    internal static void Install(NMainMenu menu)
    {
        if (menu == null || !RunUploadService.NeedsFirstPrompt || menu.GetNodeOrNull<Node>(NodeName) != null) return;
        menu.AddChild(new NRunUploadFirstPrompt { Name = NodeName, _menu = menu });
    }

    public override void _Process(double delta)
    {
        if (!RunUploadService.NeedsFirstPrompt || !GodotObject.IsInstanceValid(_menu) || _menu.IsQueuedForDeletion())
        {
            SetProcess(false);
            QueueFree();
            return;
        }

        NModalContainer container = NModalContainer.Instance;
        if (!_menu.IsVisibleInTree() || NGame.Instance?.Transition?.InTransition == true ||
            container == null || container.OpenModal != null ||
            !ActiveScreenContext.Instance.IsCurrent(_menu)) return;

        if (RunUploadService.ShowFirstPrompt(_menu.DefaultFocusedControl))
        {
            SetProcess(false);
            QueueFree();
        }
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class RunUploadFirstPromptPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NMainMenu __instance) => NRunUploadFirstPrompt.Install(__instance);
}
