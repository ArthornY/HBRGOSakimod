using System;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Magic Sword Lucky Seven's finisher on screen (design v3, section 6). Full screen plays 暗场 → 召剑 → 斩击 →
// 收刀 in about two seconds and any click or key skips it; simplified only draws the slashes and the total
// over the targets. HbrLuckySevenFinisher feeds it the card's real hits and total and waits on its clock;
// nothing here reads or changes gameplay.
public partial class NHbrLuckySevenFinisher : CanvasLayer
{
    // Above the combat interface and the OD extra-turn cut-in (100), below the upload dialog (120).
    internal const int LayerIndex = 101;
    private const string DamageFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const int SwordMaskSize = 512;

    private static ImageTexture _maskedSword;

    private HbrFinisherPresentation _mode = HbrFinisherPresentation.Simplified;
    private float _speed = 1f;
    private float _t;
    private bool _hitsStarted;
    private float _hitsStartedAt;
    private bool _finishing;
    private float _finishAt;
    private float _dimAtFinish;
    private bool _skipped;
    private float _skippedAt;
    private bool _released;
    private float _totalShownAt = -1f;
    private bool _landed;
    private bool _flashSounded;
    private Texture2D _portrait;
    private Vector2 _size = new(1920f, 1080f);

    private Control _root;
    private ColorRect _dim;
    private NHbrLuckySevenRift _riftDark;
    private NHbrLuckySevenRift _riftGlow;
    private TextureRect _sakiGlow;
    private TextureRect _saki;
    private Control _swordPivot;
    private TextureRect _sword;
    private TextureRect _swordVeins;
    private Control _titleBand;
    private Control _slashes;
    private ColorRect _flash;
    private Control _totalBox;

    internal bool IntroDone => _mode != HbrFinisherPresentation.Fullscreen || _skipped || _t >= HbrLuckySevenTimeline.SummonEnd;

    internal bool FinishDone => _mode != HbrFinisherPresentation.Fullscreen || _skipped ||
        _finishing && _t - _finishAt >= HbrLuckySevenTimeline.FinishLength;

    internal float Speed => _speed;

    private bool FullScreen => _mode == HbrFinisherPresentation.Fullscreen && !_skipped;

    // portrait: the texture 31O is drawn with right now; the silhouette borrows it instead of loading another.
    internal static NHbrLuckySevenFinisher Create(HbrFinisherPresentation mode, float speed, Texture2D portrait) => new()
    {
        Name = "HbrLuckySevenFinisher",
        Layer = LayerIndex,
        _mode = mode,
        _speed = speed,
        _portrait = portrait
    };

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _root = new Control
        {
            Name = "Root",
            // The full-screen show holds the pointer, so a click skips it instead of reaching the hand.
            MouseFilter = _mode == HbrFinisherPresentation.Fullscreen ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore
        };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        if (_mode == HbrFinisherPresentation.Fullscreen) BuildFullScreen();
        _slashes = FullRect("Slashes");
        if (_mode == HbrFinisherPresentation.Fullscreen)
        {
            _flash = new ColorRect { Name = "Flash", Color = new Color(1f, 1f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.AddChild(_flash);
            _flash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        Layout();
    }

    private void BuildFullScreen()
    {
        _dim = new ColorRect { Name = "Dim", Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(_dim);
        _dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _riftDark = AddFull(new NHbrLuckySevenRift(false) { Name = "RiftDark" });
        _riftGlow = AddFull(new NHbrLuckySevenRift(true) { Name = "RiftGlow" });

        Texture2D portrait = _portrait ?? ResourceLoader.Load<Texture2D>(HbrMagicSwordArt.SakiPortrait);
        // A violet glow just larger than the silhouette makes a rim around it.
        _sakiGlow = Picture("SakiGlow", portrait, additive: true, new Color(0.62f, 0.32f, 1f, 0f));
        _saki = Picture("Saki", portrait, additive: false, new Color(0.06f, 0.035f, 0.09f, 0f));
        _sakiGlow.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _saki.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

        // The sword turns about its own centre: a pivot control carries the position, the pictures sit centred on it.
        _swordPivot = new Control { Name = "SwordPivot", MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(_swordPivot);
        Texture2D sword = MaskedSword(ResourceLoader.Load<Texture2D>(HbrMagicSwordArt.SwordIcon));
        _sword = Picture("Sword", sword, additive: true, new Color(1f, 1f, 1f, 0f), _swordPivot);
        _swordVeins = Picture("SwordVeins", sword, additive: true, new Color(0.63f, 0.36f, 1f, 0f), _swordPivot);
        _titleBand = BuildTitle();
    }

    private Control BuildTitle()
    {
        Control band = new() { Name = "Title", MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
        _root.AddChild(band);
        // Crimson band fading out to the left, a violet rule under it.
        GradientTexture2D fade = new()
        {
            Width = 256,
            Height = 4,
            FillFrom = new Vector2(0f, 0f),
            FillTo = new Vector2(1f, 0f),
            Gradient = new Gradient
            {
                Offsets = new[] { 0f, 0.35f, 1f },
                Colors = new[]
                {
                    new Color(HbrLuckySevenTimeline.CrimsonHex) with { A = 0f },
                    new Color(HbrLuckySevenTimeline.CrimsonHex) with { A = 0.78f },
                    new Color(0.35f, 0.02f, 0.08f, 0.92f)
                }
            }
        };
        band.AddChild(new TextureRect
        {
            Name = "Band",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = fade,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        band.AddChild(new ColorRect { Name = "Rule", Color = new Color(HbrLuckySevenTimeline.VioletHex), MouseFilter = Control.MouseFilterEnum.Ignore });
        Label title = TextLabel("Text", HbrLuckySevenTimeline.Title, HbrLuckySevenTimeline.TitleSize, LocaleFont(bold: true));
        title.AddThemeColorOverride("font_outline_color", new Color(0.3f, 0f, 0.06f));
        title.AddThemeConstantOverride("outline_size", 12);
        band.AddChild(title);
        return band;
    }

    private void Layout()
    {
        Viewport viewport = GetViewport();
        if (viewport != null) _size = viewport.GetVisibleRect().Size;
        float h = _size.Y, w = _size.X;
        float scale = h / 1080f;
        if (_riftDark != null)
        {
            foreach (NHbrLuckySevenRift rift in new[] { _riftDark, _riftGlow })
            {
                rift.RiftCentre = new Vector2(w * HbrLuckySevenTimeline.RiftX, h * HbrLuckySevenTimeline.RiftY);
                rift.GroundPoint = new Vector2(w * HbrLuckySevenTimeline.GroundX, h * HbrLuckySevenTimeline.GroundY);
                rift.Scale1080 = scale;
            }
        }

        if (_saki != null)
        {
            float sakiHeight = h * HbrLuckySevenTimeline.SakiHeight;
            Vector2 sakiSize = new(sakiHeight * 0.62f, sakiHeight);
            Vector2 sakiPosition = new(w * HbrLuckySevenTimeline.SakiLeft, h * HbrLuckySevenTimeline.SakiTop);
            _saki.Position = sakiPosition;
            _saki.Size = sakiSize;
            _sakiGlow.Size = sakiSize * 1.035f;
            _sakiGlow.Position = sakiPosition - sakiSize * 0.0175f;
            float swordSize = h * HbrLuckySevenTimeline.SwordSize;
            foreach (TextureRect picture in new[] { _sword, _swordVeins })
            {
                picture.Size = new Vector2(swordSize, swordSize);
                picture.Position = -picture.Size / 2f;
            }
        }

        if (_titleBand != null)
        {
            float top = h * HbrLuckySevenTimeline.TitleTop, height = h * HbrLuckySevenTimeline.TitleHeight;
            float left = w * HbrLuckySevenTimeline.TitleLeft;
            _titleBand.Size = new Vector2(w - left, height);
            _titleBand.Position = new Vector2(left, top);
            ((Control)_titleBand.GetNode("Band")).Size = _titleBand.Size;
            Control rule = (Control)_titleBand.GetNode("Rule");
            rule.Position = new Vector2(0f, height - 4f * scale);
            rule.Size = new Vector2(w - left, 3f * scale);
            Control text = (Control)_titleBand.GetNode("Text");
            text.Position = Vector2.Zero;
            text.Size = new Vector2(w - left - 40f * scale, height);
            ((Label)text).HorizontalAlignment = HorizontalAlignment.Right;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta * _speed;
        Layout();
        if (_mode == HbrFinisherPresentation.Fullscreen) UpdateFullScreen();
        UpdateTotal();
        if (_released && Finished()) QueueFree();
    }

    private bool Finished() =>
        // The total, if any, plays out; without one the layer leaves as soon as its show is over.
        (_totalShownAt < 0f || _t - _totalShownAt >= HbrLuckySevenTimeline.TotalLife) &&
        (_mode != HbrFinisherPresentation.Fullscreen || _skipped && _t - _skippedAt >= HbrLuckySevenTimeline.SkipFade ||
         _finishing && _t - _finishAt >= HbrLuckySevenTimeline.FinishLength || !_finishing && !_hitsStarted) &&
        _slashes.GetChildCount() == 0;

    private void UpdateFullScreen()
    {
        float t = _t;
        // Skipping fades everything of the summon away; slashes and the total still follow the real hits.
        float layerAlpha = _skipped ? 1f - HbrLuckySevenTimeline.Ramp(t, _skippedAt, _skippedAt + HbrLuckySevenTimeline.SkipFade) : 1f;

        float darkness = HbrLuckySevenTimeline.SummonDarkness(t);
        if (_hitsStarted)
            darkness = Mathf.Lerp(darkness, HbrLuckySevenTimeline.SlashDim,
                HbrLuckySevenTimeline.Ramp(t, _hitsStartedAt, _hitsStartedAt + HbrLuckySevenTimeline.SlashDimBlend));
        if (_finishing) darkness = HbrLuckySevenTimeline.FinishDarkness(t - _finishAt, _dimAtFinish);
        else _dimAtFinish = darkness;
        _dim.Color = new Color(0f, 0f, 0f, darkness * layerAlpha);

        float summonFade = layerAlpha * (_finishing ? 1f - HbrLuckySevenTimeline.Ramp(t - _finishAt, 0f, HbrLuckySevenTimeline.FinishLength * 0.6f) : 1f);
        float open = HbrLuckySevenTimeline.RiftOpen(t);
        // The rift closes once the sword is through.
        float riftOpen = open * (1f - HbrLuckySevenTimeline.Ramp(t, HbrLuckySevenTimeline.DrawStart, HbrLuckySevenTimeline.SummonEnd));
        foreach (NHbrLuckySevenRift rift in new[] { _riftDark, _riftGlow })
        {
            rift.Open = riftOpen;
            rift.Clock = t;
            rift.Fade = summonFade;
            rift.Shock = _landed ? t - HbrLuckySevenTimeline.SwordLand : -1f;
            rift.QueueRedraw();
        }

        float saki = HbrLuckySevenTimeline.SakiAlpha(t) * summonFade;
        float slide = (1f - HbrLuckySevenTimeline.SakiAlpha(t)) * -60f * (_size.Y / 1080f);
        _saki.Modulate = _saki.Modulate with { A = saki };
        _sakiGlow.Modulate = _sakiGlow.Modulate with { A = saki * (0.55f + 0.35f * HbrLuckySevenTimeline.Veins(t)) };
        _saki.Position = _saki.Position with { X = _size.X * HbrLuckySevenTimeline.SakiLeft + slide };
        _sakiGlow.Position = _sakiGlow.Position with { X = _saki.Position.X - _saki.Size.X * 0.0175f };

        UpdateSword(t, summonFade);

        float title = HbrLuckySevenTimeline.TitleSlide(t);
        _titleBand.Modulate = new Color(1f, 1f, 1f, Math.Min(1f, title * 1.4f) * summonFade);
        _titleBand.Position = _titleBand.Position with { X = _size.X * HbrLuckySevenTimeline.TitleLeft + (1f - title) * _size.X * 0.55f };

        if (_finishing && !_skipped)
        {
            float flash = HbrLuckySevenTimeline.Flash(t - _finishAt);
            _flash.Color = new Color(1f, 1f, 1f, flash);
            if (!_flashSounded)
            {
                _flashSounded = true;
                Sound(HbrMagicSwordArt.ImpactSfx);
                Shake(ShakeStrength.Medium, 90f);
            }
        }
        else if (_flash != null)
        {
            _flash.Color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private void UpdateSword(float t, float fade)
    {
        float h = _size.Y, w = _size.X;
        Vector2 sky = new(w * HbrLuckySevenTimeline.RiftX, h * HbrLuckySevenTimeline.RiftY);
        Vector2 ground = new(w * HbrLuckySevenTimeline.GroundX, h * HbrLuckySevenTimeline.GroundY);
        Vector2 held = new(w * HbrLuckySevenTimeline.HeldX, h * HbrLuckySevenTimeline.HeldY);
        float fall = HbrLuckySevenTimeline.SwordFall(t);
        float draw = HbrLuckySevenTimeline.SwordDraw(t);
        bool falling = t >= HbrLuckySevenTimeline.SwordFallStart;
        if (!_landed && t >= HbrLuckySevenTimeline.SwordLand && !_skipped)
        {
            _landed = true;
            Sound(HbrMagicSwordArt.ImpactSfx);
            Shake(ShakeStrength.Medium, 90f);
        }

        // The icon's blade points up and right (SwordTipAngle); tip straight down while falling and stuck, with the
        // centre lifted so the tip stands in the ground, then raised into Saki's hand at the icon's own angle.
        Vector2 stuck = ground - new Vector2(0f, h * HbrLuckySevenTimeline.SwordSize * HbrLuckySevenTimeline.SwordLandLift);
        Vector2 position = draw > 0f ? stuck.Lerp(held, draw) : sky.Lerp(stuck, fall);
        float rotation = Mathf.DegToRad(Mathf.Lerp(HbrLuckySevenTimeline.FallRotation, 0f, draw));
        _swordPivot.Position = position;
        _swordPivot.Rotation = rotation;
        float alpha = falling ? fade : 0f;
        _sword.Modulate = _sword.Modulate with { A = alpha };
        _swordVeins.Modulate = _swordVeins.Modulate with { A = alpha * HbrLuckySevenTimeline.Veins(t) * 0.9f };
    }

    // One slash over a hit target, in this layer's (screen) coordinates.
    internal void Slash(Vector2 screenPosition, int slash, bool big)
    {
        if (!_hitsStarted) BeginHits();
        _slashes.AddChild(NHbrLuckySevenSlash.Create(screenPosition, slash, big, _speed));
        if (!FullScreen) return;
        Sound(big ? HbrMagicSwordArt.BigSlashSfx : HbrMagicSwordArt.SlashSfx);
        Shake(big ? ShakeStrength.Medium : ShakeStrength.Weak, HbrLuckySevenTimeline.SlashAngle(slash, big) + 90f);
    }

    internal void BeginHits()
    {
        if (_hitsStarted) return;
        _hitsStarted = true;
        _hitsStartedAt = _t;
    }

    // 收刀: the white flash and the combined total. anchor: where the hits landed, for the simplified total.
    internal void Finish(decimal total, Vector2? anchor, bool showTotal)
    {
        if (_finishing) return;
        BeginHits();
        _finishing = true;
        _finishAt = _t;
        if (!showTotal) return;
        BuildTotal(total);
        Vector2 at = FullScreen || anchor == null
            ? new Vector2(_size.X * HbrLuckySevenTimeline.TotalX, _size.Y * HbrLuckySevenTimeline.TotalY)
            : anchor.Value - new Vector2(0f, HbrLuckySevenTimeline.SimplifiedTotalRise * _size.Y / 1080f);
        _totalBox.Position = at - _totalBox.Size / 2f;
        _totalShownAt = _t;
    }

    // The caller is done with the show; the layer finishes what is on screen and frees itself.
    internal void Release()
    {
        if (_released) return;
        _released = true;
        if (!_finishing && _mode == HbrFinisherPresentation.Fullscreen) Skip();
    }

    private void Skip()
    {
        if (_skipped || _mode != HbrFinisherPresentation.Fullscreen) return;
        _skipped = true;
        _skippedAt = _t;
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (_mode != HbrFinisherPresentation.Fullscreen || _skipped || _released || FinishDone) return;
        bool press = inputEvent switch
        {
            InputEventMouseButton { Pressed: true } => true,
            InputEventKey { Pressed: true, Echo: false } => true,
            InputEventJoypadButton { Pressed: true } => true,
            _ => false
        };
        if (!press) return;
        Skip();
        GetViewport()?.SetInputAsHandled();
    }

    private void BuildTotal(decimal total)
    {
        float width = HbrLuckySevenTimeline.TotalBoxWidth, height = HbrLuckySevenTimeline.TotalBoxHeight;
        _totalBox = new Control { Name = "Total", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(width, height) };
        _root.AddChild(_totalBox);
        Label caption = TextLabel("Caption", HbrLuckySevenTimeline.TotalCaption, HbrLuckySevenTimeline.CaptionSize, LocaleFont(bold: true));
        caption.Position = Vector2.Zero;
        caption.Size = new Vector2(width, 44f);
        caption.AddThemeColorOverride("font_color", new Color("FFD27A"));
        caption.AddThemeColorOverride("font_outline_color", new Color(0.18f, 0.04f, 0.08f));
        caption.AddThemeConstantOverride("outline_size", 8);
        _totalBox.AddChild(caption);
        Label number = TextLabel("Number", CardDamageNumberFormatter.Format(total), HbrLuckySevenTimeline.TotalSize, DamageFont());
        number.Position = new Vector2(0f, 40f);
        number.Size = new Vector2(width, height - 40f);
        number.AddThemeColorOverride("font_outline_color", new Color(HbrLuckySevenTimeline.CrimsonHex).Darkened(0.35f));
        number.AddThemeConstantOverride("outline_size", 16);
        _totalBox.AddChild(number);
        _totalBox.PivotOffset = _totalBox.Size / 2f;
    }

    private void UpdateTotal()
    {
        if (_totalBox == null || _totalShownAt < 0f) return;
        float v = _t - _totalShownAt;
        _totalBox.Scale = Vector2.One * HbrLuckySevenTimeline.TotalScale(v) * (_size.Y / 1080f);
        _totalBox.Modulate = new Color(1f, 1f, 1f, HbrLuckySevenTimeline.TotalAlpha(v));
    }

    private T AddFull<T>(T control) where T : Control
    {
        _root.AddChild(control);
        control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return control;
    }

    private Control FullRect(string name)
    {
        Control control = new() { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
        return AddFull(control);
    }

    // ExpandMode before Texture and Size (a Size set under KeepSize is held at the texture's own size).
    private TextureRect Picture(string name, Texture2D texture, bool additive, Color modulate, Control parent = null)
    {
        TextureRect picture = new()
        {
            Name = name,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = texture,
            Modulate = modulate,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        if (additive) picture.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        (parent ?? _root).AddChild(picture);
        return picture;
    }

    private static Label TextLabel(string name, string text, int size, Font font)
    {
        Label label = new()
        {
            Name = name,
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        if (font != null) label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", Colors.White);
        return label;
    }

    private static Font LocaleFont(bool bold)
    {
        try
        {
            string language = LocManager.Instance?.Language;
            if (language == null) return null;
            return FontManager.GetSubstituteFont(language, bold ? FontType.Bold : FontType.Regular)
                ?? FontManager.GetSubstituteFont(language, FontType.Regular);
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Lucky Seven font unavailable: {error.Message}");
            return null;
        }
    }

    private static Font DamageFont() => ResourceLoader.Load<Font>(DamageFontPath) ?? LocaleFont(bold: true);

    // The sword icon with everything outside a soft band along its blade made transparent
    // (HbrLuckySevenTimeline.SwordMask), built once: the additive sword then adds the blade and its own glow, not
    // the painted square around it.
    private static Texture2D MaskedSword(Texture2D source)
    {
        if (GodotObject.IsInstanceValid(_maskedSword)) return _maskedSword;
        try
        {
            Image image = source?.GetImage();
            if (image == null) return source;
            if (image.IsCompressed()) image.Decompress();
            image.Convert(Image.Format.Rgba8);
            image.Resize(SwordMaskSize, SwordMaskSize, Image.Interpolation.Bilinear);
            byte[] data = image.GetData();
            for (int y = 0; y < SwordMaskSize; y++)
            for (int x = 0; x < SwordMaskSize; x++)
            {
                int alpha = (y * SwordMaskSize + x) * 4 + 3;
                float keep = HbrLuckySevenTimeline.SwordMask((x + 0.5f) / SwordMaskSize, (y + 0.5f) / SwordMaskSize);
                data[alpha] = (byte)Math.Clamp((int)Math.Round(data[alpha] * keep), 0, 255);
            }
            using Image masked = Image.CreateFromData(SwordMaskSize, SwordMaskSize, false, Image.Format.Rgba8, data);
            _maskedSword = ImageTexture.CreateFromImage(masked);
            return _maskedSword;
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Lucky Seven sword mask failed: {error.Message}");
            return source;
        }
    }

    private static void Sound(string path)
    {
        try { SfxCmd.Play(path); }
        catch (Exception error) { Log.Warn($"[HBRGOSakiMod] Lucky Seven sound {path} failed: {error.Message}"); }
    }

    // An explicit angle: without one the game draws it from a random number generator.
    private static void Shake(ShakeStrength strength, float angle)
    {
        try { NGame.Instance?.ScreenShake(strength, ShakeDuration.Short, angle); }
        catch (Exception error) { Log.Warn($"[HBRGOSakiMod] Lucky Seven shake failed: {error.Message}"); }
    }
}
