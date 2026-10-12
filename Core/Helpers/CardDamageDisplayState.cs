using System;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed class CardDamageDisplayState
{
    internal const double HoldSeconds = 3d;
    internal const double FadeSeconds = 0.5d;
    private object _entry;
    private bool _timerRunning;
    private double _elapsed;

    internal bool Visible { get; private set; }
    internal float Alpha { get; private set; }

    // Binding a combat HUD must not replay a previously completed card.
    internal void BindBaseline(object entry)
    {
        _entry = entry;
        _timerRunning = false;
        _elapsed = 0d;
        Visible = false;
        Alpha = 0f;
    }

    internal void Observe(object entry, bool isComplete)
    {
        if (entry == null)
        {
            BindBaseline(null);
            return;
        }
        if (!ReferenceEquals(entry, _entry))
        {
            _entry = entry;
            _timerRunning = false;
            _elapsed = 0d;
            Visible = true;
            Alpha = 1f;
        }
        // Structural refreshes never extend a timer started by a damage receipt.
        // A card that deals no damage still gets its hold after completion.
        if (Visible && isComplete && !_timerRunning)
        {
            _timerRunning = true;
            _elapsed = 0d;
        }
    }

    internal void UpdateDamage(object entry)
    {
        if (entry == null)
        {
            BindBaseline(null);
            return;
        }
        _entry = entry;
        _timerRunning = true;
        _elapsed = 0d;
        Visible = true;
        Alpha = 1f;
    }

    internal void Tick(double delta)
    {
        if (!Visible || !_timerRunning || double.IsNaN(delta) || delta <= 0d) return;
        _elapsed += delta;
        if (_elapsed <= HoldSeconds) return;
        if (_elapsed >= HoldSeconds + FadeSeconds)
        {
            Visible = false;
            Alpha = 0f;
            return;
        }
        Alpha = (float)(1d - (_elapsed - HoldSeconds) / FadeSeconds);
    }
}
