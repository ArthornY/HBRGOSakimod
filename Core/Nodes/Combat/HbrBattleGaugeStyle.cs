using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// BattleAtlas gradients with the original 16px frame geometry. The frame and
// mask are drawn analytically so their edges stay sharp at the screen's actual
// resolution. Keep resources shared: damage callbacks must not decode art.
internal static class HbrBattleGaugeStyle
{
    private const string Directory = "res://images/ui/hbr_battle/";
    private static Texture2D _base;
    private static Texture2D _frame;
    private static Texture2D _hp;
    private static Texture2D _dp;
    private static Texture2D _damage;
    private static Texture2D _frontBase;
    private static AtlasTexture _frontReadoutBase;
    private static Texture2D _enemyBase;
    private static Shader _fillShader;
    private static Shader _shapeShader;
    private static Shader _maskShader;
    private static FontFile _numbers;
    private static FontFile _destructionNumbers;

    internal static Texture2D Base => _base ??= Load("GaugeHPDPBaseBattle");
    internal static Texture2D Frame => _frame ??= Load("GaugeHPDPFlameBattle");
    internal static Texture2D Hp => _hp ??= Load("GaugeHPBattleLarge");
    internal static Texture2D Dp => _dp ??= Load("GaugeDPBattleLarge");
    internal static Texture2D Damage => _damage ??= Load("GaugeDamageBattle");
    internal static Texture2D FrontBase => _frontBase ??= Load("GaugeBaseFront");
    // Preserve the player's numeric readout backing above the DP frame;
    // the original lower region also darkens the now-transparent gauges.
    internal static Texture2D FrontReadoutBase => _frontReadoutBase ??= new AtlasTexture
    {
        Atlas = FrontBase,
        Region = new Rect2(0f, 0f, 236f, 31f + NDeflectorProtectionDpBar.PlayerVerticalOffset)
    };
    internal static Texture2D EnemyBase => _enemyBase ??= Load("GaugeBaseEnemy");
    internal static readonly Color BackgroundColor = new("2D2D2D");
    // Use the opposing gauge's midpoint tint for readable numeric contrast.
    internal static readonly Color HpNumberColor = new("68BCD8");
    internal static readonly Color MonsterDpNumberColor = new("D45BD4");

    internal static Font NumberFont
    {
        get
        {
            if (_numbers != null) return _numbers;
            _numbers = ResourceLoader.Load<FontFile>(Directory + "font/hbr_ui_numbers.fnt");
            _numbers.FixedSizeScaleMode = TextServer.FixedSizeScaleMode.Enabled;
            _numbers.ModulateColorGlyphs = true;
            _numbers.Fallbacks = new Godot.Collections.Array<Font>
            {
                ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres")
            };
            return _numbers;
        }
    }

    internal static Font DestructionNumberFont
    {
        get
        {
            if (GodotObject.IsInstanceValid(_destructionNumbers)) return _destructionNumbers;
            // Render the readout at its actual text size. The former 23px
            // bitmap carried a baked red glow and blurred when enlarged to 40px.
            _destructionNumbers = ResourceLoader.Load<FontFile>("res://fonts/kreon_bold.ttf");
            return _destructionNumbers;
        }
    }

    internal static ShaderMaterial CreateFillMaterial() => new()
    {
        Shader = _fillShader ??= ResourceLoader.Load<Shader>(Directory + "gauge_fill.gdshader")
    };

    internal static void StyleBackground(NinePatchRect node)
    {
        StyleShape(node, false);
        // Keep the native node, tween bindings and hit-test visibility while
        // leaving unfilled HP/DP transparent. Native Modulate updates remain safe.
        node.SelfModulate = Colors.Transparent;
    }

    internal static void StyleMask(NinePatchRect node, float visualWidthPadding = 0f)
    {
        SetTexture(node, Base);
        node.ClipChildren = CanvasItem.ClipChildrenMode.Only;
        // A ClipChildren material replaces Godot's backbuffer compositor.
        // It must read the children, not draw the white mask over their fill.
        var material = new ShaderMaterial
        {
            Shader = _maskShader ??= ResourceLoader.Load<Shader>(Directory + "gauge_mask.gdshader")
        };
        node.Material = material;
        BindShapeGeometry(node, material, visualWidthPadding);
    }

    internal static void StyleFrame(NinePatchRect node) => StyleShape(node, true);

    private static void StyleShape(NinePatchRect node, bool frame)
    {
        // Keep native NinePatchRect nodes (including their tween references)
        // while using linear UVs across the whole frame, without another inset.
        SetTexture(node, frame ? Frame : Base);
        var material = new ShaderMaterial
        {
            Shader = _shapeShader ??= ResourceLoader.Load<Shader>(Directory + "gauge_shape.gdshader")
        };
        material.SetShaderParameter("draw_frame", frame);
        node.Material = material;
        BindShapeGeometry(node, material, 0f);
    }

    private static void BindShapeGeometry(NinePatchRect node, ShaderMaterial material, float visualWidthPadding)
    {
        void UpdateGeometry() => material.SetShaderParameter("gauge_size", new Vector2(
            Mathf.Max(1f, node.Size.X + visualWidthPadding), Mathf.Max(1f, node.Size.Y)));
        // HP preserves its native W-10 logical width, then visually stretches
        // that container to W. Supply the visual width for matching end caps.
        node.Resized += UpdateGeometry;
        UpdateGeometry();
    }

    internal static void SetTexture(NinePatchRect node, Texture2D texture,
        int left = 0, int right = 0)
    {
        node.Texture = texture;
        node.PatchMarginLeft = left;
        node.PatchMarginRight = right;
        node.PatchMarginTop = 0;
        node.PatchMarginBottom = 0;
        node.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        node.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    internal static NinePatchRect AddFrame(Control parent, string name)
    {
        var frame = new NinePatchRect { Name = name };
        StyleFrame(frame);
        parent.AddChild(frame);
        FillRect(frame);
        return frame;
    }

    internal static void StyleNumber(MegaLabel label, int fontSize = 18, Color? fontColor = null)
    {
        label.AutoSizeEnabled = true;
        label.MinFontSize = 10;
        label.MaxFontSize = fontSize;
        label.AddThemeFontOverride(ThemeConstants.Label.Font, NumberFont);
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, fontSize);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, fontColor ?? Colors.White);
        label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, new Color("141923"));
        label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, Colors.Transparent);
        // The original TMP underlay is baked into the converted glyph atlas;
        // bitmap fonts must not depend on Godot generating a dynamic outline.
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 0);
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 0);
        label.AddThemeConstantOverride("shadow_outline_size", 0);
    }

    internal static void FillRect(Control control, float left = 0, float top = 0,
        float right = 0, float bottom = 0)
    {
        control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }

    private static Texture2D Load(string name) => ResourceLoader.Load<Texture2D>(Directory + name + ".png");
}
