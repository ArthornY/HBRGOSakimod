using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The ± buttons on the radar vertices: HBR btnPlus* for plus, matching minus art. One step on press;
// holding repeats with SixSensePointRepeat until the step fails, the button is released or disabled.
public partial class NManualPointButton : TextureButton
{
    private readonly SixSensePointRepeat _repeat = new();
    private bool _holding;

    internal Func<bool> Attempt { get; set; }

    public override void _Ready()
    {
        IgnoreTextureSize = true;
        StretchMode = StretchModeEnum.Scale;
        TextureFilter = HbrManualPhoneArt.MipmapFilter;
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ButtonDown += () =>
        {
            _repeat.Reset();
            _holding = !Disabled && Attempt?.Invoke() == true;
        };
        ButtonUp += Stop;
        MouseExited += Stop;
        FocusExited += Stop;
    }

    internal void Stop() => _holding = false;

    public override void _Process(double delta)
    {
        if (!_holding) return;
        if (!IsVisibleInTree() || Disabled || !IsPressed() || !GetWindow().HasFocus())
        {
            Stop();
            return;
        }
        int count = _repeat.Advance(delta);
        for (int i = 0; i < count && _holding; i++)
            if (Attempt?.Invoke() != true) Stop();
    }
}
