using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Patches;

namespace HBRGOSakiMod.Core.Nodes.CommonUi;

public partial class NHbrClickEffectOverlay : CanvasLayer
{
    internal const string LayerName = "HbrGlobalClickEffects";
    private readonly HbrClickAnimationState _animation = new();
    private ClickSurface _surface;
    private static double Now => Time.GetTicksUsec() / 1_000_000.0;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _surface = new ClickSurface { Name = "ClickVisuals", Animation = _animation };
        AddChild(_surface);
        SetProcess(false);
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;
        if (mouse.Pressed)
            _animation.Press(_surface.GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position, Now);
        else
            _animation.Release(Now);
        RefreshCursorFrame();
        _surface.QueueRedraw();
        SetProcess(_animation.IsAnimating);
        // Observe only: never consume the click or add a GUI hit-test surface.
    }

    public override void _Process(double delta)
    {
        _animation.Advance(Now);
        RefreshCursorFrame();
        _surface.QueueRedraw();
        SetProcess(_animation.IsAnimating);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut || what == NotificationApplicationFocusOut)
            ResetAnimation();
    }

    public override void _ExitTree() => ResetAnimation();

    private void ResetAnimation()
    {
        _animation.Reset();
        RefreshCursorFrame();
        if (GodotObject.IsInstanceValid(_surface)) _surface.QueueRedraw();
        SetProcess(false);
    }

    private void RefreshCursorFrame()
    {
        if (HbrCursorHelper.SetPressAmount(_animation.PressAmount))
            HbrCursorInstallPatch.ApplyToExistingManager();
    }

    private sealed partial class ClickSurface : Node2D
    {
        internal HbrClickAnimationState Animation { get; init; }

        public override void _Draw()
        {
            double now = Now;
            foreach (HbrClickAnimationState.Click click in Animation.Clicks)
            {
                float age = (float)Math.Max(0, now - click.StartedAt);
                float progress = Mathf.Clamp(age / (float)HbrClickAnimationState.Lifetime, 0f, 1f);
                if (progress >= 1f) continue;

                Vector2 center = click.Position;
                float travel = 1f - MathF.Pow(1f - progress, 3f);
                float fade = MathF.Pow(1f - progress, 1.6f);
                DrawRing(center, 7f + 48f * travel, fade);
                if (age >= 0.055f)
                {
                    float echo = Mathf.Clamp((age - 0.055f) / 0.38f, 0f, 1f);
                    DrawRing(center, 5f + 36f * (1f - (1f - echo) * (1f - echo)),
                        0.45f * (1f - echo));
                }

                float flash = Math.Max(0, 1f - age / 0.12f);
                if (flash > 0)
                    DrawCircle(center, 4f + 8f * (1f - flash),
                        new Color(1f, 0.86f, 0.98f, flash * 0.35f), true, -1f, true);

                for (int i = 0; i < 6; i++)
                {
                    float angle = Mathf.Tau * i / 6f - Mathf.Pi / 2f;
                    Vector2 position = center + Vector2.FromAngle(angle) * (10f + 42f * travel);
                    float size = (i % 2 == 0 ? 3.2f : 2.2f) * fade;
                    // At the end of the fade, adding a sub-pixel size to screen
                    // coordinates can collapse polygon vertices to the same float.
                    if (size < 0.05f) continue;
                    Vector2[] diamond =
                    [
                        position + new Vector2(0, -size * 1.5f),
                        position + new Vector2(size, 0),
                        position + new Vector2(0, size * 1.5f),
                        position + new Vector2(-size, 0)
                    ];
                    // A diamond is already a quad; no polygon triangulation is needed.
                    DrawPrimitive(diamond, [new Color(1f, 0.78f, 0.96f, fade * 0.9f)],
                        Array.Empty<Vector2>());
                }
            }
        }

        private void DrawRing(Vector2 center, float radius, float alpha)
        {
            if (alpha <= 0) return;
            DrawArc(center, radius, 0f, Mathf.Tau, 64, new Color(1f, 0.34f, 0.8f, alpha * 0.10f), 8f, true);
            DrawArc(center, radius, 0f, Mathf.Tau, 64, new Color(1f, 0.52f, 0.88f, alpha * 0.38f), 3.4f, true);
            DrawArc(center, radius, 0f, Mathf.Tau, 64, new Color(1f, 0.91f, 0.99f, alpha * 0.95f), 1.2f, true);
        }
    }
}
