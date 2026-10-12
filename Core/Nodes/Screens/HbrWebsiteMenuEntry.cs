using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Screens;

internal static class HbrWebsiteMenuEntry
{
    private const string NodeName = "HbrgoWebsiteButton";
    private const string WebsiteUrl = "https://hbrgo.com";

    internal static NMainMenuTextButton Find(NMainMenu menu) =>
        menu.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/" + NodeName);

    // Install before the menu's _Ready connects focus/reticle signals to all
    // text buttons. Copy only the label, never the multiplayer button's actions.
    internal static void Install(NMainMenu menu)
    {
        if (Find(menu) != null) return;
        Control list = menu.GetNode<Control>("MainMenuTextButtons");
        NMainMenuTextButton multiplayer = list.GetNode<NMainMenuTextButton>("MultiplayerButton");
        var button = new NMainMenuTextButton
        {
            Name = NodeName,
            CustomMinimumSize = multiplayer.CustomMinimumSize,
            SizeFlagsHorizontal = multiplayer.SizeFlagsHorizontal,
            Theme = multiplayer.Theme,
            FocusMode = Control.FocusModeEnum.All,
            MouseFilter = multiplayer.MouseFilter,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        var label = (MegaLabel)multiplayer.GetChild<MegaLabel>(0)
            .Duplicate((int)Node.DuplicateFlags.Scripts);
        button.AddChild(label);
        list.AddChild(button);
        list.MoveChild(button, multiplayer.GetIndex() + 1);
        label.Resized += () => label.PivotOffset = label.Size * 0.5f;
        RefreshLabel(menu);
        button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
        {
            if (button.IsEnabled && button.IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(menu))
                OS.ShellOpen(WebsiteUrl);
        }));
    }

    internal static void RefreshLabel(NMainMenu menu)
    {
        if (Find(menu)?.label is not { } label) return;
        label.Text = "HBRGO网站";
        label.ApplyLocaleFontSubstitution(FontType.Regular, ThemeConstants.Label.Font);
        label.PivotOffset = label.Size * 0.5f;
    }
}
