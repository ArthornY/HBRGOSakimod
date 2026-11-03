using System;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// A monotonic wall-clock timeline keeps interrupted fades continuous, including
// a new selection arriving between rendered frames or after the hold expires.
internal sealed class SakiSelectionPortraitFade
{
    private const double FadeDuration = 0.5;
    private const double AfterPlayHold = 1.0;
    private float _startOpacity;
    private double _fadeInAt;
    private double _fadeOutAt;
    private bool _selected;

    internal float OpacityAt(double now)
    {
        double fadeInElapsed = Math.Max(0, Math.Min(now, _fadeOutAt) - _fadeInAt);
        double opacity = Math.Clamp(_startOpacity + fadeInElapsed / FadeDuration, 0, 1);
        double fadeOutElapsed = Math.Max(0, now - _fadeOutAt);
        return (float)Math.Clamp(opacity - fadeOutElapsed / FadeDuration, 0, 1);
    }

    // No card is held and the last fade has finished, so a frame can be skipped.
    internal bool IsIdle(double now) => !_selected && OpacityAt(now) <= 0f;

    internal void Select(double now)
    {
        if (_selected) return;
        _startOpacity = OpacityAt(now);
        _fadeInAt = now;
        _fadeOutAt = double.PositiveInfinity;
        _selected = true;
        // Reversing at alpha takes (1-alpha)*0.5 seconds to become opaque;
        // it never restarts at zero or repeats a complete half-second fade.
    }

    internal void Release(double now, bool played)
    {
        if (!_selected) return;
        _startOpacity = OpacityAt(now);
        _fadeInAt = now;
        _fadeOutAt = now + (played ? AfterPlayHold : 0);
        _selected = false;
    }

    internal void Reset()
    {
        _startOpacity = 0;
        _fadeInAt = 0;
        _fadeOutAt = 0;
        _selected = false;
    }
}
