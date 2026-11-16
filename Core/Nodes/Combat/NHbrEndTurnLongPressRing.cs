using Godot;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// The long-press progress of the HBR end turn disc, drawn as a ring. The native
// NEndTurnLongPressBar still times the press and ends the turn; this only mirrors
// its width (progress) and opacity, read every frame from the hidden bar.
public partial class NHbrEndTurnLongPressRing : Control
{
    private const int Points = 96;
    private static readonly Color Track = new(HbrBattleHudLayout.RingTrackColor);
    private static readonly Color Glow = new(HbrBattleHudLayout.RingGlowColor);

    private Control _bar;
    private float _progress = -1f;
    private float _alpha = -1f;

    internal void Bind(Control nativeBar)
    {
        _bar = nativeBar;
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(_bar)) return;
        float progress = HbrBattleHudLayout.LongPressProgress(_bar.Size.X);
        float alpha = _bar.Modulate.A;
        if (Mathf.IsEqualApprox(progress, _progress) && Mathf.IsEqualApprox(alpha, _alpha)) return;
        _progress = progress;
        _alpha = alpha;
        Modulate = new Color(1f, 1f, 1f, alpha);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_alpha <= 0f) return;
        Vector2 centre = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) / 2f;
        float width = radius * (HbrBattleHudLayout.RingOuter - HbrBattleHudLayout.RingInner);
        float middle = radius * (HbrBattleHudLayout.RingOuter + HbrBattleHudLayout.RingInner) / 2f;
        const float start = -Mathf.Pi / 2f;
        DrawArc(centre, middle, 0f, Mathf.Tau, Points, Track, width, true);
        if (_progress <= 0f) return;
        float end = start + Mathf.Tau * _progress;
        int points = Mathf.Max(4, Mathf.CeilToInt(Points * _progress));
        // drop-shadow(0 0 4px #ff6fd8): a wider, faint pink arc under the white one.
        DrawArc(centre, middle, start, end, points, new Color(Glow, 0.35f), width + 8f, true);
        DrawArc(centre, middle, start, end, points, Colors.White, width, true);
    }
}
