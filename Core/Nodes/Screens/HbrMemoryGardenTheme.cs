using System;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Shared look of the memory garden screens (ui-concepts/memory-garden-20261104): ink text on a white space,
// frosted-glass panels, the HBR ButtonFirst buttons, and round buttons on a navy disc so they read on white.
// Positions follow the preview's 1920x1080 rig, centred in the screen.
internal static class HbrMemoryGardenTheme
{
    internal static readonly Vector2 Rig = new(1920, 1080);
    internal static readonly Color Ink = new("1d2340");
    internal static readonly Color Ink2 = new("5b6283");
    internal static readonly Color Ink3 = new("858ba6");
    internal static readonly Color PinkInk = new("d63d8f");
    internal static readonly Color Gold = new("b8862a");
    internal static readonly Color Mint = new("23946f");
    internal static readonly Color Pink = new("ff5fb4");
    internal static readonly Color Glass = new(1, 1, 1, .76f);
    internal static readonly Color Rim = new(126 / 255f, 146 / 255f, 196 / 255f, .42f);
    internal static readonly Color Navy = new(29 / 255f, 35 / 255f, 64 / 255f, .88f);
    private const string Sprites = "res://images/ui/hbr_phantom_roster/";

    private static Shader _hueShift;

    internal static Vector2 Origin(Vector2 size) => new(Math.Max(0, (size.X - Rig.X) / 2), Math.Max(0, (size.Y - Rig.Y) / 2));

    internal static Texture2D Sprite(string name) => ResourceLoader.Load<Texture2D>(Sprites + name + ".png");

    internal static Label Label(string text, int size, Color color)
    {
        Label label = new() { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    internal static StyleBoxFlat GlassBox(int radius, Color? fill = null, Color? rim = null, int rimWidth = 1) => new()
    {
        BgColor = fill ?? Glass,
        BorderColor = rim ?? Rim,
        BorderWidthLeft = rimWidth, BorderWidthTop = rimWidth, BorderWidthRight = rimWidth, BorderWidthBottom = rimWidth,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ShadowColor = new Color(60 / 255f, 80 / 255f, 130 / 255f, .1f),
        ShadowSize = 14,
        ShadowOffset = new Vector2(0, 4),
        AntiAliasing = true
    };

    // The pink ButtonFirst plate from the original game. `sub` shifts it 38 degrees towards violet for secondary actions.
    internal static Button HbrButton(string text, bool sub = false)
    {
        Button button = new()
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddThemeStyleboxOverride("normal", Plate("ButtonFirstNormal"));
        button.AddThemeStyleboxOverride("hover", Plate("ButtonFirstActive"));
        button.AddThemeStyleboxOverride("hover_pressed", Plate("ButtonFirstActive"));
        button.AddThemeStyleboxOverride("pressed", Plate("ButtonFirstActive"));
        button.AddThemeStyleboxOverride("disabled", Plate("ButtonFirstDisable"));
        button.AddThemeStyleboxOverride("focus", Plate("ButtonFirstFocused"));
        button.AddThemeFontSizeOverride("font_size", 30);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(state, Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(1, 1, 1, .7f));
        button.AddThemeColorOverride("font_shadow_color", new Color(80 / 255f, 0, 40 / 255f, .6f));
        button.AddThemeConstantOverride("shadow_offset_y", 2);
        if (sub) button.Material = new ShaderMaterial { Shader = HueShift() };
        return button;
    }

    private static StyleBoxTexture Plate(string sprite) => new() { Texture = Sprite(sprite) };

    // filter: hue-rotate(-38deg) saturate(.8). White text is left as it is.
    private static Shader HueShift()
    {
        if (GodotObject.IsInstanceValid(_hueShift)) return _hueShift;
        _hueShift = new Shader
        {
            Code = "shader_type canvas_item;\n" +
                "void fragment() {\n" +
                "  vec3 c = COLOR.rgb; float a = radians(-38.0); float s = sin(a), k = cos(a);\n" +
                "  mat3 m = mat3(vec3(0.213 + k * 0.787 - s * 0.213, 0.213 - k * 0.213 + s * 0.143, 0.213 - k * 0.213 - s * 0.787),\n" +
                "    vec3(0.715 - k * 0.715 - s * 0.715, 0.715 + k * 0.285 + s * 0.140, 0.715 - k * 0.715 + s * 0.715),\n" +
                "    vec3(0.072 - k * 0.072 + s * 0.928, 0.072 - k * 0.072 - s * 0.283, 0.072 + k * 0.928 + s * 0.072));\n" +
                "  c = clamp(m * c, 0.0, 1.0); float g = dot(c, vec3(0.2126, 0.7152, 0.0722));\n" +
                "  COLOR.rgb = mix(vec3(g), c, 0.8);\n" +
                "}\n"
        };
        return _hueShift;
    }

    // Glass pill with an icon, a caption and a value: the garden's point and memory counters.
    internal static PanelContainer Chip(Control icon, string caption, out Label value)
    {
        PanelContainer chip = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        StyleBoxFlat box = GlassBox(26);
        box.ContentMarginLeft = 12;
        box.ContentMarginRight = 22;
        chip.AddThemeStyleboxOverride("panel", box);
        HBoxContainer row = new() { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 52) };
        row.AddThemeConstantOverride("separation", 10);
        chip.AddChild(row);
        icon.CustomMinimumSize = new Vector2(40, 40);
        icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(icon);
        Label captionLabel = Label(caption, 22, Ink);
        captionLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(captionLabel);
        value = Label(string.Empty, 28, PinkInk);
        value.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(value);
        return chip;
    }
}
