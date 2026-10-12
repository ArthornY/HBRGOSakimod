using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Use the game's select action and controller focus rules for the title UI.
public partial class NHbrTitleButton : NButton
{
    internal Action Activated { get; set; }
    internal Action<bool> Highlighted { get; set; }

    public override void _Ready() => ConnectSignals();
    protected override void OnRelease() => Activated?.Invoke();
    protected override void OnFocus()
    {
        base.OnFocus();
        Highlighted?.Invoke(true);
    }
    protected override void OnUnfocus() => Highlighted?.Invoke(false);
    protected override void OnDisable()
    {
        base.OnDisable();
        Modulate = new Color(1, 1, 1, 0.4f);
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        Modulate = Colors.White;
    }
}
