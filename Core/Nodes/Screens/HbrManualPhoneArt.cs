using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Textures, the HBR UI typeface and shared styles of the phone manual.
internal static class HbrManualPhoneArt
{
    internal const string Directory = "res://images/ui/hbr_manual_phone/";
    private const string FontPath = Directory + "font/hbr_manual_ui.otf";
    private const string FallbackFontPath = "res://themes/kreon_bold_glyph_space_one.tres";

    internal static readonly Color Cyan = new(HbrManualPhoneLayout.Cyan);
    internal static readonly Color Cyan2 = new(HbrManualPhoneLayout.Cyan2);
    internal static readonly Color Muted = new(HbrManualPhoneLayout.Muted);
    internal static readonly Color Pink = new(HbrManualPhoneLayout.Pink);
    internal static readonly Color Ink = new(HbrManualPhoneLayout.Ink);
    internal static readonly Color Body = new("e3f3f5");
    // .glow: text-shadow 0 0 10px rgba(120,232,242,.55), 0 1px 2px rgba(0,0,0,.6).
    internal static readonly Color GlowColor = new(0.47f, 0.91f, 0.95f, 0.3f);

    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static Font _font;

    internal const CanvasItem.TextureFilterEnum MipmapFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;

    internal static Texture2D Texture(string name)
    {
        // Names are files in this folder; a full res:// path reuses the mod's other art.
        if (!Textures.TryGetValue(name, out Texture2D texture) || !GodotObject.IsInstanceValid(texture))
            Textures[name] = texture = ResourceLoader.Load<Texture2D>(
                name.StartsWith("res://", System.StringComparison.Ordinal) ? name : Directory + name + ".png");
        return texture;
    }

    // The subset covers the manual's fixed strings; other characters use the locale font.
    internal static Font Font
    {
        get
        {
            if (GodotObject.IsInstanceValid(_font)) return _font;
            Font fallback = FontManager.GetSubstituteFont(LocManager.Instance.Language, FontType.Regular)
                ?? ResourceLoader.Load<Font>(FallbackFontPath);
            FontFile font = ResourceLoader.Load<FontFile>(FontPath);
            if (font != null && fallback != null) font.Fallbacks = new Godot.Collections.Array<Font> { fallback };
            _font = (Font)font ?? fallback;
            return _font;
        }
    }

    internal static TextureRect Sprite(string name, string texture, HbrPhoneRect rect, Color? tint = null) => new()
    {
        Name = name,
        Position = new Vector2(rect.X, rect.Y),
        Size = new Vector2(rect.Width, rect.Height),
        Texture = texture == null ? null : Texture(texture),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        Modulate = tint ?? Colors.White,
        TextureFilter = MipmapFilter,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    internal static NinePatchRect Patch(string name, string texture, HbrPhoneRect rect, int margin) => new()
    {
        Name = name,
        Position = new Vector2(rect.X, rect.Y),
        Size = new Vector2(rect.Width, rect.Height),
        Texture = Texture(texture),
        PatchMarginLeft = margin,
        PatchMarginTop = margin,
        PatchMarginRight = margin,
        PatchMarginBottom = margin,
        TextureFilter = MipmapFilter,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    // A label in the HBR face. glow: the cyan .glow halo of the preview's headings.
    internal static Label Label(string name, string text, int size, Color color, bool glow = false,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        Label label = new()
        {
            Name = name,
            Text = text,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride("font", Font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", glow ? GlowColor : new Color(0f, 0f, 0f, 0.55f));
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", glow ? 0 : 1);
        label.AddThemeConstantOverride("shadow_outline_size", glow ? 10 : 3);
        return label;
    }

    internal static void Place(Control control, HbrPhoneRect rect)
    {
        control.Position = new Vector2(rect.X, rect.Y);
        control.Size = new Vector2(rect.Width, rect.Height);
    }

    // Flat, invisible button chrome for controls that draw themselves.
    internal static void Bare(BaseButton button)
    {
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        button.FocusMode = Control.FocusModeEnum.All;
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }

    internal static StyleBoxFlat Rounded(Color fill, float radius, Color? border = null, int borderWidth = 1)
    {
        int r = Mathf.RoundToInt(radius);
        StyleBoxFlat style = new()
        {
            BgColor = fill,
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
            AntiAliasing = true
        };
        if (border.HasValue)
        {
            style.BorderColor = border.Value;
            style.BorderWidthLeft = style.BorderWidthTop = style.BorderWidthRight = style.BorderWidthBottom = borderWidth;
        }
        return style;
    }

    // A rounded rect whose fill runs vertically from `top` to `bottom`, as the preview's cyan pills.
    internal static void DrawPill(CanvasItem item, Rect2 rect, Color top, Color bottom, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(rect.Size.X, rect.Size.Y) / 2f);
        var points = new List<Vector2>();
        void Arc(Vector2 centre, float from)
        {
            for (int i = 0; i <= 8; i++)
            {
                float angle = from + i * Mathf.Pi / 16f;
                points.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
        Arc(rect.Position + new Vector2(rect.Size.X - radius, radius), -Mathf.Pi / 2f);
        Arc(rect.End - new Vector2(radius, radius), 0f);
        Arc(rect.Position + new Vector2(radius, rect.Size.Y - radius), Mathf.Pi / 2f);
        Arc(rect.Position + new Vector2(radius, radius), Mathf.Pi);
        var colors = new Color[points.Count];
        for (int i = 0; i < points.Count; i++)
            colors[i] = top.Lerp(bottom, Mathf.Clamp((points[i].Y - rect.Position.Y) / rect.Size.Y, 0f, 1f));
        item.DrawPolygon(points.ToArray(), colors);
    }

    // The frosted circle of the terminal buttons (.frost / .back).
    internal static void DrawFrost(CanvasItem item, Vector2 centre, float radius, bool hovered)
    {
        if (hovered) item.DrawCircle(centre, radius + 10f, new Color(0.55f, 0.94f, 0.98f, 0.12f));
        item.DrawCircle(centre, radius, new Color(0.47f, 0.89f, 0.9f, 0.3f));
        item.DrawCircle(centre - new Vector2(0f, radius * 0.25f), radius * 0.62f, new Color(0.47f, 0.89f, 0.9f, 0.1f));
        if (hovered) item.DrawCircle(centre, radius, new Color(0.59f, 0.96f, 0.98f, 0.14f));
        item.DrawArc(centre, radius - 1f, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 48,
            new Color(0.9f, 1f, 1f, hovered ? 0.8f : 0.55f), 2f, true);
        item.DrawArc(centre, radius - 1f, 0f, Mathf.Tau, 72, new Color(0.75f, 0.99f, 0.98f, hovered ? 0.35f : 0.22f), 1.5f, true);
    }
}
