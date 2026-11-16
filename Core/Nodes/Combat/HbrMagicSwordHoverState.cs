namespace HBRGOSakiMod.Core.Nodes.Combat;

// Godot keeps a Control focused after a mouse click. That focus is useful for
// navigation, but must not keep a mouse-owned tooltip alive after pointer exit.
internal sealed class HbrMagicSwordHoverState
{
    internal static readonly string[] NavigationActions =
        { "ui_left", "ui_right", "ui_up", "ui_down", "ui_focus_next", "ui_focus_prev", "ui_accept", "ui_select" };
    private bool _navigationInput = true;
    internal bool PointerInside { get; private set; }
    internal bool Focused { get; private set; }
    internal bool WantsTooltip => PointerInside || Focused && _navigationInput;

    internal void PointerEntered()
    {
        PointerInside = true;
        UsePointer();
    }

    internal void PointerExited()
    {
        PointerInside = false;
        UsePointer();
    }

    internal void SetFocused(bool focused) => Focused = focused;
    internal void UsePointer() => _navigationInput = false;
    internal bool NavigationInput(string action, bool focused)
    {
        if (System.Array.IndexOf(NavigationActions, action) < 0) return false;
        _navigationInput = true;
        Focused = focused;
        return true;
    }

    internal void Reset()
    {
        PointerInside = false;
        Focused = false;
        _navigationInput = false;
    }
}
