using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Textures, the HBR UI typeface and the text styles of the HBR battle HUD.
internal static class HbrBattleHudArt
{
    internal const string Directory = "res://images/ui/hbr_battle_hud/";
    private const string FontPath = Directory + "font/hbr_hud_ui.otf";
    private const string FallbackFontPath = "res://themes/kreon_bold_glyph_space_one.tres";

    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static readonly Dictionary<int, FontVariation> Spaced = new();
    private static Font _font;

    // The HBR look belongs to the mod's own characters; vanilla characters keep the vanilla HUD.
    internal static bool UsesHud(Player player) => player?.Character is HbrgoSaki or HbrgoHotaru;

    internal static Texture2D Texture(string name)
    {
        // Room cache cleanup can dispose a resource while pooled UI still holds its wrapper.
        if (!Textures.TryGetValue(name, out Texture2D texture) || !GodotObject.IsInstanceValid(texture))
            Textures[name] = texture = ResourceLoader.Load<Texture2D>(Directory + name + ".png");
        return texture;
    }

    internal static Font Font
    {
        get
        {
            if (GodotObject.IsInstanceValid(_font)) return _font;
            Spaced.Clear();
            // The subset only covers the HUD's own strings; anything else uses the locale font.
            Font fallback = FontManager.GetSubstituteFont(LocManager.Instance.Language, FontType.Regular)
                ?? ResourceLoader.Load<Font>(FallbackFontPath);
            FontFile font = ResourceLoader.Load<FontFile>(FontPath);
            if (font != null && fallback != null) font.Fallbacks = new Godot.Collections.Array<Font> { fallback };
            _font = (Font)font ?? fallback;
            return _font;
        }
    }

    // CSS letter-spacing, in pixels at the label's size.
    internal static Font Spacing(int pixels)
    {
        Font font = Font;
        if (pixels == 0) return font;
        if (!Spaced.TryGetValue(pixels, out FontVariation variation) || !GodotObject.IsInstanceValid(variation))
            Spaced[pixels] = variation = new FontVariation { BaseFont = font, SpacingGlyph = pixels };
        return variation;
    }

    internal static TextureRect Sprite(string name, string texture, HbrHudRect rect, Color? tint = null) => new()
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

    // The HUD textures import with mipmaps: most are drawn well below their source size.
    internal const CanvasItem.TextureFilterEnum MipmapFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;

    internal static Label Label(string name, string text, int size, Color color, int spacing = 0)
    {
        Label label = new()
        {
            Name = name,
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride("font", Spacing(spacing));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    // The preview's .ol text shadow: a thin dark outline and a soft drop shadow.
    internal static void Outline(Label label, Color outline, int size = 6)
    {
        label.AddThemeColorOverride("font_outline_color", outline);
        label.AddThemeConstantOverride("outline_size", size);
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.45f));
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        label.AddThemeConstantOverride("shadow_outline_size", size + 4);
    }

    // A coloured halo behind the outline, standing in for the preview's blurred glow.
    internal static void Glow(Label label, Color glow, int size)
    {
        label.AddThemeColorOverride("font_shadow_color", glow);
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 0);
        label.AddThemeConstantOverride("shadow_outline_size", size);
    }

    internal static Color Hex(string hex) => new(hex);

    internal static void FitText(Label label, float width, float height)
    {
        label.Position = Vector2.Zero;
        label.Size = new Vector2(width, height);
        label.PivotOffset = label.Size / 2f;
    }
}
