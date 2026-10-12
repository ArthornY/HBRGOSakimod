using System;
using System.Collections.Generic;
using Godot;

namespace HBRGOSakiMod.Core.Helpers;

// Presentation time is independent of combat speed, pause, and gameplay RNG.
internal sealed class HbrClickAnimationState
{
    internal const double Lifetime = 0.55;
    internal const int MaximumClicks = 24;
    internal readonly record struct Click(Vector2 Position, double StartedAt);
    private readonly List<Click> _clicks = [];
    private double _lastTime;
    private bool _hasTime;
    internal IReadOnlyList<Click> Clicks => _clicks;
    internal bool IsPressed { get; private set; }
    internal float PressAmount { get; private set; }
    internal bool IsAnimating => _clicks.Count > 0 || Math.Abs(PressAmount - (IsPressed ? 1f : 0f)) > 0.0001f;

    internal void Press(Vector2 position, double now)
    {
        Advance(now);
        IsPressed = true;
        PressAmount = Math.Max(PressAmount, 0.25f);
        if (_clicks.Count == MaximumClicks) _clicks.RemoveAt(0);
        _clicks.Add(new Click(position, now));
    }

    internal void Release(double now)
    {
        Advance(now);
        IsPressed = false;
    }

    internal void Advance(double now)
    {
        double delta = _hasTime ? Math.Max(0, now - _lastTime) : 0;
        _hasTime = true;
        _lastTime = now;
        float target = IsPressed ? 1f : 0f;
        float step = (float)(delta / (IsPressed ? 0.075 : 0.12));
        PressAmount = IsPressed ? Math.Min(target, PressAmount + step) : Math.Max(target, PressAmount - step);
        for (int i = _clicks.Count - 1; i >= 0; i--)
            if (now - _clicks[i].StartedAt >= Lifetime) _clicks.RemoveAt(i);
    }

    internal void Reset()
    {
        IsPressed = false;
        PressAmount = 0;
        _hasTime = false;
        _clicks.Clear();
    }
}
