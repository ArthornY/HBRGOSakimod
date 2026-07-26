using System;
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NOverclockExtraTurnOverlay : CanvasLayer
{
    private const float CutInDurationSeconds = 0.6f;
    private const float CutInEnterEndSeconds = 0.2f;
    private const float CutInHoldEndSeconds = 0.43f;
    private const float RushStartSeconds = 0.52f;
    private const float RushDurationSeconds = 0.58f;
    private const float EntryDurationSeconds = RushStartSeconds + RushDurationSeconds;
    private const float ExitDurationSeconds = 0.28f;
    private const float PersistentTintStrength = 0.2f;
    private const int StreakCount = 42;
    private const string CutInImagePath =
        "res://images/packed/character_select/sakikowin.png";

    private const string ScreenEffectShader = """
        shader_type canvas_item;

        uniform sampler2D screen_texture
            : hint_screen_texture, repeat_disable, filter_linear_mipmap;
        uniform float blur_strength : hint_range(0.0, 0.08) = 0.0;
        uniform float zoom_strength : hint_range(0.0, 0.08) = 0.0;
        uniform float tint_strength : hint_range(0.0, 1.0) = 0.0;
        uniform float flash_strength : hint_range(0.0, 1.0) = 0.0;

        void fragment() {
            vec2 center = vec2(0.5);
            vec2 direction = SCREEN_UV - center;
            vec2 base_uv = center + direction * (1.0 - zoom_strength);
            vec4 sampled = vec4(0.0);

            sampled += textureLod(
                screen_texture,
                clamp(base_uv - direction * blur_strength * 1.00, vec2(0.0), vec2(1.0)),
                blur_strength * 12.0);
            sampled += textureLod(
                screen_texture,
                clamp(base_uv - direction * blur_strength * 0.66, vec2(0.0), vec2(1.0)),
                blur_strength * 9.0);
            sampled += textureLod(
                screen_texture,
                clamp(base_uv - direction * blur_strength * 0.33, vec2(0.0), vec2(1.0)),
                blur_strength * 6.0);
            sampled += texture(screen_texture, base_uv);
            sampled += textureLod(
                screen_texture,
                clamp(base_uv + direction * blur_strength * 0.28, vec2(0.0), vec2(1.0)),
                blur_strength * 5.0);
            sampled /= 5.0;

            vec3 od_tint = sampled.rgb * vec3(1.08, 0.72, 1.16)
                + vec3(0.055, 0.0, 0.075);
            sampled.rgb = mix(sampled.rgb, od_tint, tint_strength);
            sampled.rgb += vec3(1.0, 0.42, 0.92) * flash_strength;
            COLOR = vec4(sampled.rgb, 1.0);
        }
        """;

    private const string CutInShader = """
        shader_type canvas_item;

        uniform float whiten : hint_range(0.0, 1.0) = 0.0;

        void fragment() {
            vec4 source = texture(TEXTURE, UV);
            COLOR = vec4(mix(source.rgb, vec3(1.0), whiten), source.a);
        }
        """;

    private sealed class StreakVisual
    {
        public required ColorRect Node { get; init; }

        public required float VerticalRatio { get; init; }

        public required float Speed { get; init; }
    }

    private readonly List<StreakVisual> _streaks = [];
    private readonly Line2D[] _cutInTrails = new Line2D[4];

    private Overclock _relic;
    private Player _viewedPlayer;
    private Control _overlay;
    private Control _streakLayer;
    private Control _cutInLayer;
    private TextureRect _cutInPortrait;
    private Polygon2D _speechBubbleShape;
    private Label _speechBubbleLabel;
    private ColorRect _inputBlocker;
    private ShaderMaterial _screenMaterial;
    private ShaderMaterial _cutInMaterial;
    private float _entryElapsed;
    private float _exitRemaining;
    private bool _effectVisible;
    private bool _wasInSpecialExtraTurn;

    public override void _Ready()
    {
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;
        BuildUi();
        GetViewport().SizeChanged += LayoutEffect;
        LayoutEffect();
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= LayoutEffect;
        BindRelic(null);
    }

    public override void _Process(double delta)
    {
        bool isInSpecialExtraTurn =
            _relic?.IsSpecialExtraTurnFor(_viewedPlayer) == true;
        if (isInSpecialExtraTurn && !_wasInSpecialExtraTurn)
        {
            BeginEffect();
        }
        else if (!isInSpecialExtraTurn && _wasInSpecialExtraTurn)
        {
            BeginExit();
        }

        _wasInSpecialExtraTurn = isInSpecialExtraTurn;
        if (!_effectVisible)
        {
            return;
        }

        float deltaSeconds = (float)delta;
        if (isInSpecialExtraTurn)
        {
            UpdateEntry(deltaSeconds);
        }
        else
        {
            UpdateExit(deltaSeconds);
        }

        UpdateStreaks(deltaSeconds);
    }

    public void BindRelic(Overclock relic, Player viewedPlayer = null)
    {
        if (ReferenceEquals(_relic, relic) &&
            ReferenceEquals(_viewedPlayer, viewedPlayer))
        {
            return;
        }

        _relic = relic;
        _viewedPlayer = viewedPlayer;
        _wasInSpecialExtraTurn = false;
        HideEffect();
    }

    private void BuildUi()
    {
        _overlay = new Control
        {
            Name = "OverclockExtraTurnEffect",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        SetFullRect(_overlay);
        AddChild(_overlay);

        Shader screenShader = new()
        {
            Code = ScreenEffectShader
        };
        _screenMaterial = new ShaderMaterial
        {
            Shader = screenShader
        };

        ColorRect screenEffect = new()
        {
            Name = "ScreenEffect",
            Color = Colors.White,
            Material = _screenMaterial,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        SetFullRect(screenEffect);
        _overlay.AddChild(screenEffect);

        _streakLayer = new Control
        {
            Name = "OdLightStreaks",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true
        };
        SetFullRect(_streakLayer);
        _overlay.AddChild(_streakLayer);
        BuildStreaks();
        BuildCutIn();

        _inputBlocker = new ColorRect
        {
            Name = "EntryInputBlocker",
            Color = Colors.Transparent,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        SetFullRect(_inputBlocker);
        _overlay.AddChild(_inputBlocker);
    }

    private void BuildStreaks()
    {
        RandomNumberGenerator random = new()
        {
            Seed = 0x4842524f44
        };

        for (int index = 0; index < StreakCount; index++)
        {
            float brightness = random.RandfRange(0.65f, 1f);
            ColorRect streak = new()
            {
                Name = $"OdStreak{index:00}",
                Size = new Vector2(
                    random.RandfRange(7f, 34f),
                    random.RandfRange(1f, 3.2f)),
                Color = new Color(
                    1f,
                    random.RandfRange(0.42f, 0.78f) * brightness,
                    random.RandfRange(0.86f, 1f),
                    random.RandfRange(0.28f, 0.78f)),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _streakLayer.AddChild(streak);
            _streaks.Add(new StreakVisual
            {
                Node = streak,
                VerticalRatio = random.Randf(),
                Speed = random.RandfRange(95f, 285f)
            });
        }
    }

    private void BuildCutIn()
    {
        _cutInLayer = new Control
        {
            Name = "OdCutIn",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        SetFullRect(_cutInLayer);
        _overlay.AddChild(_cutInLayer);

        for (int index = 0; index < _cutInTrails.Length; index++)
        {
            Line2D trail = new()
            {
                Name = $"CutInTrail{index}",
                Width = 3f,
                DefaultColor = Colors.White,
                Antialiased = true,
                Visible = false
            };
            _cutInLayer.AddChild(trail);
            _cutInTrails[index] = trail;
        }

        Shader portraitShader = new()
        {
            Code = CutInShader
        };
        _cutInMaterial = new ShaderMaterial
        {
            Shader = portraitShader
        };

        _cutInPortrait = new TextureRect
        {
            Name = "SakikoCutIn",
            Texture = ResourceLoader.Load<Texture2D>(CutInImagePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = _cutInMaterial,
            Visible = false
        };
        _cutInLayer.AddChild(_cutInPortrait);
        BuildSpeechBubble();
    }

    private void BuildSpeechBubble()
    {
        _speechBubbleShape = new Polygon2D
        {
            Name = "CutInSpeechBubble",
            Color = new Color("FFFDF8FF"),
            Visible = false
        };
        _cutInPortrait.AddChild(_speechBubbleShape);

        _speechBubbleLabel = new Label
        {
            Name = "CutInSpeech",
            Text = "今天我是来\n宣布一件事的",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _speechBubbleLabel.AddThemeColorOverride("font_color", Colors.Black);
        _cutInPortrait.AddChild(_speechBubbleLabel);
    }

    private void BeginEffect()
    {
        _entryElapsed = 0f;
        _exitRemaining = 0f;
        _effectVisible = true;
        _overlay.Visible = true;
        _overlay.Modulate = Colors.White;
        _inputBlocker.Visible = true;
        _streakLayer.Modulate = new Color(1f, 1f, 1f, 0f);
        ResetStreakPositions();
        UpdateCutIn(0f);
        ApplyScreenEffect(
            blurStrength: 0f,
            zoomStrength: 0f,
            tintStrength: 0f,
            flashStrength: 0f);
    }

    private void BeginExit()
    {
        if (!_effectVisible)
        {
            return;
        }

        _exitRemaining = ExitDurationSeconds;
        _inputBlocker.Visible = false;
    }

    private void UpdateEntry(float delta)
    {
        _entryElapsed = Math.Min(EntryDurationSeconds, _entryElapsed + delta);
        float progress = Mathf.Clamp(_entryElapsed / EntryDurationSeconds, 0f, 1f);
        float rushProgress = Mathf.Clamp(
            (_entryElapsed - RushStartSeconds) / RushDurationSeconds,
            0f,
            1f);
        float rushPulse = Mathf.Sin(rushProgress * Mathf.Pi);
        float tintProgress = EaseOutCubic(rushProgress);

        UpdateCutIn(_entryElapsed);

        ApplyScreenEffect(
            blurStrength: rushPulse * 0.055f,
            zoomStrength: rushPulse * 0.038f,
            tintStrength: PersistentTintStrength * tintProgress,
            flashStrength: rushPulse * 0.11f);

        float streakAlpha = Mathf.Clamp(
            (_entryElapsed - RushStartSeconds) / 0.18f,
            0f,
            1f);
        _streakLayer.Modulate = new Color(1f, 1f, 1f, streakAlpha);
        if (progress >= 1f)
        {
            _inputBlocker.Visible = false;
        }
    }

    private void UpdateCutIn(float elapsed)
    {
        if (elapsed >= CutInDurationSeconds)
        {
            HideCutIn();
            return;
        }

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        float portraitSize = Math.Min(viewportSize.Y * 1.16f, viewportSize.X * 0.68f);
        Vector2 startCenter = new(viewportSize.X * 0.84f, viewportSize.Y * 0.34f);
        Vector2 holdCenter = new(viewportSize.X * 0.51f, viewportSize.Y * 0.5f);
        Vector2 exitCenter = new(viewportSize.X * 0.78f, viewportSize.Y * 0.76f);

        Vector2 center;
        Vector2 trailVector;
        float scale;
        float rotation;
        float alpha;
        float whiten;
        float trailAlpha;

        if (elapsed < CutInEnterEndSeconds)
        {
            float rawProgress = Mathf.Clamp(elapsed / CutInEnterEndSeconds, 0f, 1f);
            float eased = EaseOutCubic(rawProgress);
            center = startCenter.Lerp(holdCenter, eased);
            scale = Mathf.Lerp(0.34f, 1.08f, eased);
            rotation = Mathf.Lerp(-0.045f, 0f, eased);
            alpha = Mathf.Clamp(elapsed / 0.045f, 0f, 1f);
            whiten = Mathf.Lerp(1f, 0.12f, eased);
            trailAlpha = (1f - rawProgress) * 0.9f;
            trailVector = (startCenter - holdCenter).Normalized()
                * viewportSize.X * 0.12f * (1f - rawProgress);
        }
        else if (elapsed < CutInHoldEndSeconds)
        {
            float rawProgress = Mathf.Clamp(
                (elapsed - CutInEnterEndSeconds) /
                (CutInHoldEndSeconds - CutInEnterEndSeconds),
                0f,
                1f);
            float eased = EaseOutCubic(rawProgress);
            center = holdCenter;
            scale = Mathf.Lerp(1.08f, 1f, eased);
            rotation = 0f;
            alpha = 1f;
            whiten = Mathf.Lerp(0.12f, 0f, Mathf.Clamp(rawProgress / 0.35f, 0f, 1f));
            trailAlpha = 0f;
            trailVector = Vector2.Zero;
        }
        else
        {
            float rawProgress = Mathf.Clamp(
                (elapsed - CutInHoldEndSeconds) /
                (CutInDurationSeconds - CutInHoldEndSeconds),
                0f,
                1f);
            float eased = rawProgress * rawProgress;
            center = holdCenter.Lerp(exitCenter, eased);
            scale = Mathf.Lerp(1f, 0.3f, eased);
            rotation = Mathf.Lerp(0f, 0.075f, eased);
            alpha = 1f - Mathf.Clamp((rawProgress - 0.58f) / 0.42f, 0f, 1f);
            whiten = Mathf.Lerp(0f, 0.6f, eased);
            trailAlpha = Mathf.Sin(rawProgress * Mathf.Pi) * 0.9f;
            trailVector = (holdCenter - exitCenter).Normalized()
                * viewportSize.X * 0.14f * rawProgress;
        }

        _cutInLayer.Visible = true;
        _cutInPortrait.Visible = true;
        _cutInPortrait.Size = Vector2.One * portraitSize;
        _cutInPortrait.PivotOffset = _cutInPortrait.Size / 2f;
        _cutInPortrait.Position = center - _cutInPortrait.Size / 2f;
        _cutInPortrait.Scale = Vector2.One * scale;
        _cutInPortrait.Rotation = rotation;
        _cutInPortrait.Modulate = new Color(1f, 1f, 1f, alpha);
        _cutInMaterial.SetShaderParameter("whiten", whiten);
        LayoutSpeechBubble(portraitSize, elapsed, alpha);
        UpdateCutInTrails(
            center,
            portraitSize,
            scale,
            rotation,
            trailVector,
            trailAlpha * alpha);
    }

    private void UpdateCutInTrails(
        Vector2 center,
        float portraitSize,
        float scale,
        float rotation,
        Vector2 trailVector,
        float alpha)
    {
        Vector2[] cornerOffsets =
        [
            new Vector2(-0.35f, -0.5f),
            new Vector2(0.2f, -0.5f),
            new Vector2(0.4f, 0.5f),
            new Vector2(-0.13f, 0.5f)
        ];

        for (int index = 0; index < _cutInTrails.Length; index++)
        {
            Line2D trail = _cutInTrails[index];
            if (alpha <= 0.01f || trailVector.IsZeroApprox())
            {
                trail.Visible = false;
                continue;
            }

            Vector2 corner = center +
                (cornerOffsets[index] * portraitSize * scale).Rotated(rotation);
            trail.ClearPoints();
            trail.AddPoint(corner);
            trail.AddPoint(corner + trailVector);
            trail.Width = Math.Max(2f, GetViewport().GetVisibleRect().Size.Y * 0.003f);
            trail.DefaultColor = new Color(1f, 0.94f, 1f, alpha);
            trail.Visible = true;
        }
    }

    private void LayoutSpeechBubble(float portraitSize, float elapsed, float portraitAlpha)
    {
        float bubbleAlpha = Mathf.Clamp((elapsed - 0.12f) / 0.08f, 0f, 1f)
            * portraitAlpha;
        bool isVisible = bubbleAlpha > 0.01f;
        _speechBubbleShape.Visible = isVisible;
        _speechBubbleLabel.Visible = isVisible;
        if (!isVisible)
        {
            return;
        }

        float width = portraitSize * 0.58f;
        float height = portraitSize * 0.22f;
        Vector2 origin = new(portraitSize * 0.2f, portraitSize * 0.1f);
        Vector2[] normalizedPoints =
        [
            new Vector2(0f, 0.18f),
            new Vector2(0.07f, 0.13f),
            new Vector2(0.09f, 0.02f),
            new Vector2(0.2f, 0.09f),
            new Vector2(0.31f, 0.03f),
            new Vector2(0.43f, 0.08f),
            new Vector2(0.55f, 0f),
            new Vector2(0.66f, 0.08f),
            new Vector2(0.78f, 0.03f),
            new Vector2(0.9f, 0.1f),
            new Vector2(0.98f, 0.07f),
            new Vector2(0.96f, 0.22f),
            new Vector2(1f, 0.34f),
            new Vector2(0.96f, 0.47f),
            new Vector2(1f, 0.62f),
            new Vector2(0.92f, 0.68f),
            new Vector2(0.96f, 0.82f),
            new Vector2(0.82f, 0.78f),
            new Vector2(0.73f, 0.9f),
            new Vector2(0.59f, 0.84f),
            new Vector2(0.53f, 1.14f),
            new Vector2(0.47f, 0.87f),
            new Vector2(0.34f, 0.94f),
            new Vector2(0.23f, 0.84f),
            new Vector2(0.1f, 0.9f),
            new Vector2(0.08f, 0.75f),
            new Vector2(0f, 0.68f),
            new Vector2(0.04f, 0.52f),
            new Vector2(0f, 0.4f),
            new Vector2(0.05f, 0.29f)
        ];
        Vector2[] points = new Vector2[normalizedPoints.Length];
        for (int index = 0; index < normalizedPoints.Length; index++)
        {
            points[index] = origin + new Vector2(
                normalizedPoints[index].X * width,
                normalizedPoints[index].Y * height);
        }

        _speechBubbleShape.Polygon = points;
        _speechBubbleShape.Color = new Color(1f, 0.992f, 0.97f, bubbleAlpha);
        _speechBubbleLabel.Position = origin + new Vector2(width * 0.09f, height * 0.13f);
        _speechBubbleLabel.Size = new Vector2(width * 0.82f, height * 0.64f);
        _speechBubbleLabel.AddThemeFontSizeOverride(
            "font_size",
            Math.Max(22, (int)(portraitSize * 0.048f)));
        _speechBubbleLabel.Modulate = new Color(1f, 1f, 1f, bubbleAlpha);
    }

    private void HideCutIn()
    {
        if (_cutInLayer == null)
        {
            return;
        }

        _cutInLayer.Visible = false;
        _cutInPortrait.Visible = false;
        _speechBubbleShape.Visible = false;
        _speechBubbleLabel.Visible = false;
        foreach (Line2D trail in _cutInTrails)
        {
            if (trail != null)
            {
                trail.Visible = false;
            }
        }
    }

    private void UpdateExit(float delta)
    {
        _exitRemaining = Math.Max(0f, _exitRemaining - delta);
        float alpha = Mathf.Clamp(_exitRemaining / ExitDurationSeconds, 0f, 1f);
        _overlay.Modulate = new Color(1f, 1f, 1f, alpha);
        if (_exitRemaining <= 0f)
        {
            HideEffect();
        }
    }

    private void UpdateStreaks(float delta)
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        float entryProgress = Mathf.Clamp(_entryElapsed / EntryDurationSeconds, 0f, 1f);
        float speedMultiplier = Mathf.Lerp(3.8f, 1f, entryProgress);
        foreach (StreakVisual streak in _streaks)
        {
            Vector2 position = streak.Node.Position;
            position.X += streak.Speed * speedMultiplier * delta;
            if (position.X > viewportSize.X + streak.Node.Size.X)
            {
                position.X = -streak.Node.Size.X;
            }

            streak.Node.Position = position;
        }
    }

    private void ResetStreakPositions()
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        RandomNumberGenerator random = new()
        {
            Seed = 0x53414b494f44
        };
        foreach (StreakVisual streak in _streaks)
        {
            streak.Node.Position = new Vector2(
                random.RandfRange(-streak.Node.Size.X, viewportSize.X),
                viewportSize.Y * streak.VerticalRatio);
        }
    }

    private void LayoutEffect()
    {
        if (_streakLayer == null)
        {
            return;
        }

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        foreach (StreakVisual streak in _streaks)
        {
            Vector2 position = streak.Node.Position;
            position.X = Mathf.Clamp(
                position.X,
                -streak.Node.Size.X,
                viewportSize.X);
            position.Y = viewportSize.Y * streak.VerticalRatio;
            streak.Node.Position = position;
        }
    }

    private void ApplyScreenEffect(
        float blurStrength,
        float zoomStrength,
        float tintStrength,
        float flashStrength)
    {
        _screenMaterial.SetShaderParameter("blur_strength", blurStrength);
        _screenMaterial.SetShaderParameter("zoom_strength", zoomStrength);
        _screenMaterial.SetShaderParameter("tint_strength", tintStrength);
        _screenMaterial.SetShaderParameter("flash_strength", flashStrength);
    }

    private void HideEffect()
    {
        _entryElapsed = 0f;
        _exitRemaining = 0f;
        _effectVisible = false;
        if (_overlay == null)
        {
            return;
        }

        _overlay.Visible = false;
        _overlay.Modulate = Colors.White;
        _inputBlocker.Visible = false;
        HideCutIn();
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp(value, 0f, 1f);
        return 1f - inverse * inverse * inverse;
    }

    private static void SetFullRect(Control control)
    {
        control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
    }
}
