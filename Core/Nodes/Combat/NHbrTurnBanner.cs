using Godot;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// The turn banner as HBR's UIDuelTurnChange at 0.55: DuelModalBG beside a HalftoneBase band that fades
// out to the right, vsFaceBaseLinePlayer lines above and below, then the title and the turn line. It is a
// child of the native NPlayerTurnBanner / NEnemyTurnBanner, which keep their fade, sound and lifetime;
// only their labels are hidden. Enemy turns use the red recolour, extra turns the violet one.
public partial class NHbrTurnBanner : Control
{
    internal const string NodeName = "HbrTurnBanner";
    private static readonly Color Shade = new(0f, 0f, 0f, HbrBattleHudLayout.BannerShade);
    private static readonly Color NumberColor = new(1f, 0.94f, 0.98f, 0.92f);
    private static readonly Color TextOutline = new(0.07f, 0.03f, 0.09f, 0.9f);

    private HbrTurnBannerKind _kind;
    private int _number;
    private Control _clip;
    private Control _band;
    private Control _top;
    private Control _bottom;
    private Label _title;
    private Control _line;

    internal static void Attach(Control nativeBanner, HbrTurnBannerKind kind, int number)
    {
        if (!GodotObject.IsInstanceValid(nativeBanner) || nativeBanner.GetNodeOrNull(NodeName) != null) return;
        foreach (Node child in nativeBanner.GetChildren())
        {
            if (child is Label label) label.Visible = false;
        }
        NHbrTurnBanner banner = new() { Name = NodeName, _kind = kind, _number = number };
        nativeBanner.AddChild(banner);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Vector2 viewport = GetViewportRect().Size;
        float width = viewport.X;
        float centre = width / 2f;

        Control root = new()
        {
            Name = "Banner",
            Position = new Vector2(0f, HbrBattleHudLayout.BannerTop(viewport.Y)),
            Size = new Vector2(width, HbrBattleHudLayout.BannerHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(root);

        // The band wipes open from the centre: a clipping window widens while its content stays put.
        _clip = new Control
        {
            Name = "BandClip",
            Position = new Vector2(centre, HbrBattleHudLayout.BandTop),
            Size = new Vector2(0f, HbrBattleHudLayout.BandHeight),
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddChild(_clip);
        _band = new Control
        {
            Name = "Band",
            Position = new Vector2(-centre, 0f),
            Size = new Vector2(width, HbrBattleHudLayout.BandHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _clip.AddChild(_band);
        TextureRect duel = Sprite("Duel", HbrBattleHudLayout.BannerTexture("DuelModalBG", _kind), centre, HbrBattleHudLayout.BannerDuel);
        duel.Material = DuelFade();
        _band.AddChild(duel);
        _band.AddChild(Sprite("Halftone", "HalftoneBase", centre, HbrBattleHudLayout.BannerHalftone, Shade));
        float fadeLeft = centre + HbrBattleHudLayout.BannerFadeLeft;
        _band.AddChild(new TextureRect
        {
            Name = "Fade",
            Position = new Vector2(fadeLeft, 0f),
            Size = new Vector2(Mathf.Max(0f, width - fadeLeft), HbrBattleHudLayout.BandHeight),
            Texture = FadeTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        });

        _top = Line("LineTop", centre, HbrBattleHudLayout.BannerLineTopY);
        root.AddChild(_top);
        _bottom = Line("LineBottom", centre, HbrBattleHudLayout.BannerLineBottomY);
        root.AddChild(_bottom);

        _title = HbrBattleHudArt.Label("Title", HbrBattleHudLayout.BannerTitle(_kind), HbrBattleHudLayout.BannerTitleSize,
            Colors.White, 16);
        _title.Position = new Vector2(0f, HbrBattleHudLayout.BannerTitleTop);
        _title.Size = new Vector2(width, HbrBattleHudLayout.BannerTitleHeight);
        _title.PivotOffset = _title.Size / 2f;
        HbrBattleHudArt.Outline(_title, TextOutline, 6);
        HbrBattleHudArt.Glow(_title, TitleGlow(_kind), 14);
        root.AddChild(_title);

        _line = TurnLine(width);
        root.AddChild(_line);

        Play();
    }

    private void Play()
    {
        Tween tween = CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(SetWipe), 0f, 1f, HbrBattleHudLayout.BannerWipe)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        foreach (Control edge in new[] { _top, _bottom })
        {
            edge.Scale = new Vector2(0f, 1f);
            tween.TweenProperty(edge, "scale:x", 1f, HbrBattleHudLayout.BannerLines)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        }

        _title.Modulate = new Color(1f, 1f, 1f, 0f);
        _line.Modulate = new Color(1f, 1f, 1f, 0f);
        if (_kind == HbrTurnBannerKind.Enemy)
        {
            // The title lands from 1.6x, holds, then turns red while the native banner fades out.
            _title.Scale = Vector2.One * HbrBattleHudLayout.EnemyTitleFromScale;
            tween.TweenProperty(_title, "scale", Vector2.One, HbrBattleHudLayout.EnemyTitleIn)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
            tween.TweenProperty(_title, "modulate:a", 1f, HbrBattleHudLayout.EnemyTitleIn)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
            tween.TweenProperty(_line, "modulate:a", 1f, HbrBattleHudLayout.EnemyTitleIn)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
            tween.TweenProperty(_title, "theme_override_colors/font_color", new Color(HbrBattleHudLayout.EnemyTitleColor),
                    HbrBattleHudLayout.EnemyFadeOut)
                .SetDelay(HbrBattleHudLayout.EnemyTitleIn + HbrBattleHudLayout.EnemyHold)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
            return;
        }

        // Player and extra turns: the title slides in from the right, the turn line drops into place.
        Vector2 title = _title.Position;
        _title.Position = title + new Vector2(HbrBattleHudLayout.PlayerTitleShift, 0f);
        tween.TweenProperty(_title, "position", title, HbrBattleHudLayout.PlayerTitleSlide)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        tween.TweenProperty(_title, "modulate:a", 1f, HbrBattleHudLayout.PlayerTitleSlide)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        Vector2 line = _line.Position;
        _line.Position = line + new Vector2(0f, HbrBattleHudLayout.PlayerNumberShift);
        tween.TweenProperty(_line, "position", line, HbrBattleHudLayout.PlayerNumberSlide)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        tween.TweenProperty(_line, "modulate:a", 1f, HbrBattleHudLayout.PlayerNumberSlide)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
    }

    private void SetWipe(float progress)
    {
        if (!GodotObject.IsInstanceValid(_clip)) return;
        float width = _band.Size.X;
        float left = width / 2f * (1f - progress);
        _clip.Position = new Vector2(left, _clip.Position.Y);
        _clip.Size = new Vector2(width * progress, _clip.Size.Y);
        _band.Position = new Vector2(-left, 0f);
    }

    // The duel block fades in from its left edge instead of starting on a near-hard line, matching the
    // halftone's fade on its right. One material serves every banner.
    private static ShaderMaterial _duelFade;

    private static ShaderMaterial DuelFade()
    {
        if (_duelFade != null && GodotObject.IsInstanceValid(_duelFade)) return _duelFade;
        Shader shader = new()
        {
            Code = "shader_type canvas_item;\nuniform float fade_end = 0.6;\n" +
                   "void fragment() {\n    COLOR.a *= smoothstep(0.0, fade_end, UV.x);\n}\n"
        };
        _duelFade = new ShaderMaterial { Shader = shader };
        _duelFade.SetShaderParameter("fade_end", HbrBattleHudLayout.BannerDuelFadeEnd);
        return _duelFade;
    }

    private static TextureRect Sprite(string name, string texture, float centre, HbrHudRect rect, Color? tint = null) =>
        HbrBattleHudArt.Sprite(name, texture, rect with { X = centre + rect.X }, tint);

    private Control Line(string name, float centre, float top)
    {
        float width = HbrBattleHudLayout.BannerLineWidth;
        float height = HbrBattleHudLayout.BannerLineHeight;
        float end = HbrBattleHudLayout.BannerLineEndWidth;
        Control line = new()
        {
            Name = name,
            Position = new Vector2(centre + HbrBattleHudLayout.BannerLineLeft, top),
            Size = new Vector2(width, height),
            PivotOffset = new Vector2(width / 2f, height / 2f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        line.AddChild(new NinePatchRect
        {
            Name = "Middle",
            Size = new Vector2(width, height),
            Texture = HbrBattleHudArt.Texture(HbrBattleHudLayout.BannerTexture("vsFaceBaseLinePlayer01", _kind)),
            PatchMarginLeft = HbrBattleHudLayout.BannerLineCap,
            PatchMarginRight = HbrBattleHudLayout.BannerLineCap,
            TextureFilter = HbrBattleHudArt.MipmapFilter,
            MouseFilter = MouseFilterEnum.Ignore
        });
        string fade = HbrBattleHudLayout.BannerTexture("vsFaceBaseLinePlayer02", _kind);
        line.AddChild(HbrBattleHudArt.Sprite("Left", fade, new HbrHudRect(-end, 0f, end, height)));
        TextureRect right = HbrBattleHudArt.Sprite("Right", fade, new HbrHudRect(width, 0f, end, height));
        right.FlipH = true;
        line.AddChild(right);
        return line;
    }

    // "TURN n" / "EXTRA n" (22px word, 30px number, 4px apart) or "ENEMY TURN", centred as one line.
    private Control TurnLine(float width)
    {
        Control line = new()
        {
            Name = "TurnLine",
            Position = new Vector2(0f, HbrBattleHudLayout.BannerNumberTop),
            Size = new Vector2(width, 44f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        string word = _kind switch
        {
            HbrTurnBannerKind.Enemy => HbrBattleHudLayout.EnemyTurnSub,
            HbrTurnBannerKind.Extra => HbrBattleHudLayout.ExtraWord,
            _ => HbrBattleHudLayout.TurnWord
        };
        Label wordLabel = HbrBattleHudArt.Label("Word", word, HbrBattleHudLayout.BannerNumberWordSize, NumberColor, 9);
        HbrBattleHudArt.Outline(wordLabel, TextOutline, 6);
        line.AddChild(wordLabel);
        Font font = HbrBattleHudArt.Font;
        float baseline = 32f;
        float wordWidth = wordLabel.GetThemeFont("font")
            .GetStringSize(word, HorizontalAlignment.Left, -1, HbrBattleHudLayout.BannerNumberWordSize).X;
        if (_kind == HbrTurnBannerKind.Enemy)
        {
            NHbrTurnIndicator.PlaceOnBaseline(wordLabel, font, HbrBattleHudLayout.BannerNumberWordSize,
                (width - wordWidth) / 2f, baseline);
            return line;
        }
        Label number = HbrBattleHudArt.Label("Number", _number.ToString(), HbrBattleHudLayout.BannerNumberSize, NumberColor, 9);
        HbrBattleHudArt.Outline(number, TextOutline, 6);
        line.AddChild(number);
        float numberWidth = number.GetThemeFont("font")
            .GetStringSize(number.Text, HorizontalAlignment.Left, -1, HbrBattleHudLayout.BannerNumberSize).X;
        float left = (width - wordWidth - 4f - numberWidth) / 2f;
        NHbrTurnIndicator.PlaceOnBaseline(wordLabel, font, HbrBattleHudLayout.BannerNumberWordSize, left, baseline);
        NHbrTurnIndicator.PlaceOnBaseline(number, font, HbrBattleHudLayout.BannerNumberSize, left + wordWidth + 4f, baseline);
        return line;
    }

    private static Color TitleGlow(HbrTurnBannerKind kind) => kind switch
    {
        HbrTurnBannerKind.Enemy => new Color(1f, 0.306f, 0.408f, 0.3f),
        HbrTurnBannerKind.Extra => new Color(0.631f, 0.49f, 1f, 0.34f),
        _ => new Color(1f, 0.435f, 0.847f, 0.28f)
    };

    // linear-gradient(90deg, rgba(0,0,0,.72), transparent 60%) from the halftone's right edge.
    private static GradientTexture2D FadeTexture()
    {
        Gradient gradient = new()
        {
            Offsets = new[] { 0f, HbrBattleHudLayout.BannerFadeStop, 1f },
            Colors = new[] { Shade, new Color(Shade, 0f), new Color(Shade, 0f) }
        };
        return new GradientTexture2D
        {
            Gradient = gradient,
            Width = 128,
            Height = 4,
            FillFrom = new Vector2(0f, 0f),
            FillTo = new Vector2(1f, 0f)
        };
    }
}
