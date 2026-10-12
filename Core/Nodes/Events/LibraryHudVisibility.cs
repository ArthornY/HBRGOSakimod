using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Nodes.Events;

/// <summary>
/// Temporarily removes the run HUD while the library reader owns the screen.
/// Card rewards, maps, pause menus and the rest of GlobalUi remain independent.
/// </summary>
internal sealed class LibraryHudVisibility : IDisposable
{
    private readonly List<HudState> _savedStates = new();
    private bool _active;
    private bool _disposed;

    public void SetActive(bool active)
    {
        if (_disposed || _active == active)
        {
            return;
        }

        if (!active)
        {
            Restore();
            return;
        }

        NGlobalUi globalUi = NRun.Instance?.GlobalUi;
        if (!IsUsable(globalUi))
        {
            return;
        }

        _active = true;
        Hide(globalUi.TopBar);
        Hide(globalUi.RelicInventory);
        Hide(globalUi.MultiplayerPlayerContainer);
    }

    /// <summary>
    /// The hidden top bar cannot receive its normal pause hotkey. Use the same
    /// pause-menu entry point from the active reader's input handler instead.
    /// </summary>
    public static bool OpenPause(IRunState runState)
    {
        NGlobalUi globalUi = NRun.Instance?.GlobalUi;
        if (runState == null || !IsUsable(globalUi) || !IsUsable(globalUi.SubmenuStack))
        {
            return false;
        }

        NPauseMenu menu = (NPauseMenu)globalUi.SubmenuStack.ShowScreen(CapstoneSubmenuType.PauseMenu);
        menu.Initialize(runState);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Restore();
    }

    private void Hide(Control control)
    {
        if (!IsUsable(control))
        {
            return;
        }

        _savedStates.Add(new HudState(control, control.Visible, control.FocusBehaviorRecursive));
        control.FocusBehaviorRecursive = Control.FocusBehaviorRecursiveEnum.Disabled;
        control.Visible = false;
    }

    private void Restore()
    {
        _active = false;
        foreach (HudState state in _savedStates)
        {
            if (!IsUsable(state.Control))
            {
                continue;
            }

            state.Control.Visible = state.Visible;
            state.Control.FocusBehaviorRecursive = state.FocusBehavior;
        }
        _savedStates.Clear();
    }

    private static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();

    private readonly record struct HudState(
        Control Control,
        bool Visible,
        Control.FocusBehaviorRecursiveEnum FocusBehavior);
}
