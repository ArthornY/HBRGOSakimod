using System;
using Godot;
using HBRGOSakiMod.Core.Config;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Wait for a usable main menu instead of replacing a native startup modal.
// In particular, Clear raises screen updates before hiding its backstop, so
// opening the announcement from that event would hide the new backstop.
public partial class NHbrChallengeAnnouncementPrompt : Node
{
    private const string NodeName = "HbrChallengeAnnouncementPrompt";
    private NMainMenu _menu;

    internal static void Install(NMainMenu menu)
    {
        if (menu == null || HBRGOSakiConfig.SeenDamageChallengeAnnouncement ||
            menu.GetNodeOrNull<Node>(NodeName) != null) return;

        menu.AddChild(new NHbrChallengeAnnouncementPrompt { Name = NodeName, _menu = menu });
    }

    public override void _Process(double delta)
    {
        if (HBRGOSakiConfig.SeenDamageChallengeAnnouncement ||
            !GodotObject.IsInstanceValid(_menu) || _menu.IsQueuedForDeletion())
        {
            SetProcess(false);
            QueueFree();
            return;
        }

        NModalContainer container = NModalContainer.Instance;
        if (!HBRGOSakiConfig.CanSaveAnnouncementAcknowledgment ||
            !_menu.IsVisibleInTree() || NGame.Instance?.Transition?.InTransition == true ||
            container == null || container.OpenModal != null ||
            !ActiveScreenContext.Instance.IsCurrent(_menu)) return;

        var screen = NHbrChallengeAnnouncement.Create(
            HBRGOSakiConfig.AcknowledgeDamageChallengeAnnouncement,
            _menu.DefaultFocusedControl);
        container.Add(screen, showBackstop: true);
        if (ReferenceEquals(container.OpenModal, screen))
        {
            SetProcess(false);
            QueueFree();
        }
        else screen.QueueFree();
    }
}
